using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Net._32ba.BlendShareNdmfExtension;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

[assembly: ExportsPlugin(typeof(Net._32ba.BlendShareNdmfExtension.Editor.AddBlendShapeByBlendSharePlugin))]

namespace Net._32ba.BlendShareNdmfExtension.Editor
{
  internal sealed class AddBlendShapeByBlendSharePlugin : Plugin<AddBlendShapeByBlendSharePlugin>
  {
    public override string DisplayName => "BlendShare BlendShape Bridge";
    public override string QualifiedName => "net.32ba.blendshare-ndmf-extension";

    protected override void Configure()
    {
      InPhase(BuildPhase.Transforming)
        .BeforePlugin("nadena.dev.modular-avatar")
        .WithRequiredExtension(typeof(AnimatorServicesContext), phase => phase
          .Run("BlendShare: Append BlendShapes", ApplyBlendShapes)
          .PreviewingWith(new BlendSharePreviewFilter()));
    }

    private sealed class DefinitionAnimationBinding
    {
      public BlendShareRendererMapping Mapping { get; set; }
      public SkinnedMeshRenderer Renderer { get; set; }
      public BlendShareBlendShapeDefinition Definition { get; set; }
    }

    private static void ApplyBlendShapes(BuildContext context)
    {
      var mappings = context.AvatarRootObject.GetComponentsInChildren<BlendShareRendererMapping>(true);
      if (mappings == null || mappings.Length == 0) return;

      var appliedRenderers = new HashSet<SkinnedMeshRenderer>();
      var animationBindings = new List<DefinitionAnimationBinding>();

      foreach (var mapping in mappings)
      {
        if (mapping == null)
        {
          continue;
        }

        if (!mapping.IsValid)
        {
          LogWarning($"Skip invalid mapping on '{mapping.gameObject.name}'");
          continue;
        }

        var renderer = mapping.TargetRenderer;
        if (renderer == null)
        {
          LogWarning($"Renderer reference missing on '{mapping.gameObject.name}'");
          continue;
        }

        if (!appliedRenderers.Add(renderer))
        {
          LogWarning($"Renderer '{renderer.name}' already processed; skipping duplicate mapping");
          continue;
        }

        var bindings = ProcessMapping(context, renderer, mapping);
        if (bindings != null && bindings.Count > 0)
        {
          animationBindings.AddRange(bindings);
        }
      }

      if (animationBindings.Count > 0)
      {
        ConvertDefinitionCurves(context, animationBindings);
      }
    }

    private static List<DefinitionAnimationBinding> ProcessMapping(BuildContext context, SkinnedMeshRenderer renderer, BlendShareRendererMapping mapping)
    {
      var sourceMesh = renderer.sharedMesh;
      if (sourceMesh == null)
      {
        LogWarning($"Renderer '{renderer.name}' has no shared mesh; skipping");
        return null;
      }

      var data = mapping.BlendShapeDataAsset as BlendShapeDataSO;
      if (data == null)
      {
        LogWarning($"BlendShare asset reference on '{renderer.name}' is missing or incompatible; skipping");
        return null;
      }

      var meshName = mapping.EffectiveMeshName;
      var meshData = data.m_MeshDataList?.FirstOrDefault(m => string.Equals(m.m_MeshName, meshName, System.StringComparison.Ordinal));
      if (meshData == null)
      {
        LogWarning($"BlendShare asset '{data.name}' has no mesh entry for '{meshName}'; skipping");
        return null;
      }

      var conflicts = FindConflictingBlendShapes(sourceMesh, meshData);
      if (conflicts.Count > 0 && mapping.DuplicatePolicy == BlendShareRendererMapping.DuplicateBlendShapePolicy.Skip)
      {
        LogWarning($"Conflicting blendshape names on '{renderer.name}' skipped: {string.Join(", ", conflicts)}");
        return null;
      }

      if (mapping.EnforceVertexHash)
      {
        if (meshData.m_VertexCount != sourceMesh.vertexCount || meshData.m_VerticesHash != MeshData.GetVerticesHash(sourceMesh))
        {
          LogError($"Vertex count/hash mismatch between '{renderer.name}' and BlendShare asset '{data.name}'");
          return null;
        }
      }

      var newMesh = BlendShapeAppender.CreateBlendShapesMesh(meshData, sourceMesh);
      if (newMesh == null)
      {
        LogError($"BlendShare failed to create mesh '{meshName}' for renderer '{renderer.name}'");
        return null;
      }

      newMesh.name = string.IsNullOrEmpty(sourceMesh.name) ? "BlendShareMesh" : sourceMesh.name + "_BlendShare";

      var previousWeights = CaptureWeights(renderer);
      renderer.sharedMesh = newMesh;
      RestoreWeights(renderer, previousWeights);
      mapping.ApplyDefinitionWeights(renderer);

      Debug.Log($"[BlendShare] Appended {meshData.m_ShapeNames?.Count ?? 0} blendshapes to '{renderer.name}'");

      if (!mapping.HasBlendShapeDefinitions)
      {
        return null;
      }

      var bindings = new List<DefinitionAnimationBinding>();
      foreach (var definition in mapping.BlendShapeDefinitions)
      {
        if (definition == null || !definition.HasValidShape) continue;
        if (newMesh.GetBlendShapeIndex(definition.ShapeName) < 0)
        {
          LogWarning($"Definition '{definition.ShapeName}' on '{renderer.name}' has no matching blendshape after append; skipping animation binding");
          continue;
        }

        bindings.Add(new DefinitionAnimationBinding
        {
          Mapping = mapping,
          Renderer = renderer,
          Definition = definition
        });
      }

      return bindings;
    }

    private static List<string> FindConflictingBlendShapes(Mesh mesh, MeshData meshData)
    {
      var conflicts = new List<string>();
      if (mesh == null || meshData?.m_ShapeNames == null) return conflicts;

      var existingNames = new HashSet<string>(mesh.blendShapeCount);
      for (var i = 0; i < mesh.blendShapeCount; i++)
      {
        existingNames.Add(mesh.GetBlendShapeName(i));
      }

      foreach (var name in meshData.m_ShapeNames)
      {
        if (existingNames.Contains(name)) conflicts.Add(name);
      }

      return conflicts;
    }

    private static Dictionary<string, float> CaptureWeights(SkinnedMeshRenderer renderer)
    {
      var result = new Dictionary<string, float>(renderer.sharedMesh ? renderer.sharedMesh.blendShapeCount : 0);
      var mesh = renderer.sharedMesh;
      if (mesh == null) return result;
      for (var i = 0; i < mesh.blendShapeCount; i++)
      {
        result[mesh.GetBlendShapeName(i)] = renderer.GetBlendShapeWeight(i);
      }
      return result;
    }

    private static void RestoreWeights(SkinnedMeshRenderer renderer, Dictionary<string, float> weights)
    {
      var mesh = renderer.sharedMesh;
      if (mesh == null) return;
      foreach (var pair in weights)
      {
        var index = mesh.GetBlendShapeIndex(pair.Key);
        if (index >= 0)
        {
          renderer.SetBlendShapeWeight(index, pair.Value);
        }
      }
    }

    private static void ConvertDefinitionCurves(BuildContext context, List<DefinitionAnimationBinding> bindings)
    {
      if (bindings == null || bindings.Count == 0) return;

      AnimatorServicesContext animatorServices;
      try
      {
        animatorServices = context.Extension<AnimatorServicesContext>();
      }
      catch
      {
        LogWarning("Animator services context unavailable; BlendShare definition curves will not be remapped.");
        return;
      }

      var map = new Dictionary<EditorCurveBinding, EditorCurveBinding>();
      foreach (var binding in bindings)
      {
        if (binding?.Mapping == null || binding.Renderer == null || binding.Definition == null)
        {
          continue;
        }

        if (!binding.Definition.HasValidShape)
        {
          continue;
        }

        var source = EditorCurveBinding.SerializeReferenceCurve(
          animatorServices.ObjectPathRemapper.GetVirtualPathForObject(binding.Mapping.transform),
          binding.Mapping.GetType(),
          ManagedReferenceUtility.GetManagedReferenceIdForObject(binding.Mapping, binding.Definition),
          nameof(BlendShareBlendShapeDefinition.Weight),
          false,
          false);

        var target = EditorCurveBinding.FloatCurve(
          animatorServices.ObjectPathRemapper.GetVirtualPathForObject(binding.Renderer.transform),
          binding.Renderer.GetType(),
          $"blendShape.{binding.Definition.ShapeName}");

        map[source] = target;
      }

      if (map.Count == 0)
      {
        return;
      }

      animatorServices.AnimationIndex.EditClipsByBinding(map.Keys, clip =>
      {
        foreach (var pair in map)
        {
          var curve = clip.GetFloatCurve(pair.Key);
          clip.SetFloatCurve(pair.Key, null);
          if (curve != null)
          {
            clip.SetFloatCurve(pair.Value, curve);
          }
        }
      });
    }

    private static void LogWarning(string message) => Debug.LogWarning($"[BlendShare] {message}");
    private static void LogError(string message) => Debug.LogError($"[BlendShare] {message}");
  }
}

