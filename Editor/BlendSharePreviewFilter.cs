using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using Triturbo.BlendShapeShare;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEngine;

namespace Net._32ba.BlendShareNdmfExtension.Editor
{
  internal sealed class BlendSharePreviewFilter : IRenderFilter
  {
    private const string LogPrefix = "[BlendShare Preview]";

    public static readonly TogglablePreviewNode PreviewNode = TogglablePreviewNode.Create(
      () => "BlendShare",
      "net.32ba.blendshare-ndmf-extension",
      false);

    public bool IsEnabled(ComputeContext context)
    {
      return context.Observe(PreviewNode.IsEnabled);
    }

    public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
    {
      yield return PreviewNode;
    }

    public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
    {
      return context.GetComponentsByType<BlendShareRendererMapping>()
        .Where(mapping =>
          context.Observe(mapping, m => m.TargetRenderer) &&
          context.Observe(mapping, m => m.TargetRenderer != null ? m.TargetRenderer.sharedMesh : null) &&
          context.Observe(mapping, m => m.BlendShapeDataAsset))
        .Select(mapping => RenderGroup.For(mapping.TargetRenderer).WithData(mapping))
        .ToImmutableList();
    }

    public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
    {
      if (group == null) return Task.FromResult<IRenderFilterNode>(null);

      var mapping = group.GetData<BlendShareRendererMapping>();
      if (mapping == null) return Task.FromResult<IRenderFilterNode>(null);

      var node = new Node(mapping);
      return node.Refresh(proxyPairs, context, 0);
    }

    private sealed class Node : IRenderFilterNode
    {
      private readonly BlendShareRendererMapping _mapping;
      private readonly Mesh _previewMesh;
      private ComputeContext _shapesContext;
      private ComputeContext _weightsContext;

      public RenderAspects WhatChanged { get; private set; }

      public Node(BlendShareRendererMapping mapping)
      {
        _mapping = mapping;
        _shapesContext = new ComputeContext("BlendSharePreview.Shapes");
        _weightsContext = new ComputeContext("BlendSharePreview.Weights");
        _previewMesh = GeneratePreviewMesh(mapping);
        WhatChanged = RenderAspects.Mesh | RenderAspects.Shapes;
      }

      public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
      {
        if (_previewMesh == null)
        {
          return Task.FromResult<IRenderFilterNode>(null);
        }

        if ((updatedAspects & RenderAspects.Mesh) != 0)
        {
          WhatChanged = RenderAspects.Mesh;
          return Task.FromResult<IRenderFilterNode>(null);
        }

        _shapesContext.Invalidates(context);
        _shapesContext.Observe(_mapping, CaptureShapeSnapshot, SequenceEqual);
        if (_shapesContext.IsInvalidated)
        {
          WhatChanged = RenderAspects.Mesh;
          _shapesContext = new ComputeContext("BlendSharePreview.Shapes");
          return Task.FromResult<IRenderFilterNode>(null);
        }

        _weightsContext.Invalidates(context);
        _weightsContext.Observe(_mapping, CaptureWeightSnapshot, SequenceEqual);
        if (_weightsContext.IsInvalidated)
        {
          WhatChanged = RenderAspects.Shapes;
          _weightsContext = new ComputeContext("BlendSharePreview.Weights");
          return Task.FromResult<IRenderFilterNode>(this);
        }

        WhatChanged = 0;
        return Task.FromResult<IRenderFilterNode>(this);
      }

      public void OnFrame(Renderer original, Renderer proxy)
      {
        if (_previewMesh == null)
        {
          return;
        }

        if (proxy is not SkinnedMeshRenderer proxyRenderer)
        {
          return;
        }

        proxyRenderer.sharedMesh = _previewMesh;
        _mapping.ApplyDefinitionWeights(proxyRenderer);
      }

      public void OnFrameGroup()
      {
      }

      public void Dispose()
      {
        if (_previewMesh != null)
        {
          if (Application.isPlaying)
          {
            UnityEngine.Object.Destroy(_previewMesh);
          }
          else
          {
            UnityEngine.Object.DestroyImmediate(_previewMesh);
          }
        }
      }

      private static ImmutableList<string> CaptureShapeSnapshot(BlendShareRendererMapping mapping)
      {
        if (mapping?.BlendShapeDefinitions == null)
        {
          return ImmutableList<string>.Empty;
        }

        return mapping.BlendShapeDefinitions
          .Select(definition => definition?.ShapeName ?? string.Empty)
          .ToImmutableList();
      }

      private static ImmutableList<float> CaptureWeightSnapshot(BlendShareRendererMapping mapping)
      {
        if (mapping?.BlendShapeDefinitions == null)
        {
          return ImmutableList<float>.Empty;
        }

        return mapping.BlendShapeDefinitions
          .Select(definition => definition?.Weight ?? 0f)
          .ToImmutableList();
      }

      private static bool SequenceEqual<T>(ImmutableList<T> left, ImmutableList<T> right)
      {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;
        if (left.Count != right.Count) return false;

        for (var i = 0; i < left.Count; i++)
        {
          if (!Equals(left[i], right[i]))
          {
            return false;
          }
        }

        return true;
      }

      private static Mesh GeneratePreviewMesh(BlendShareRendererMapping mapping)
      {
        if (mapping == null)
        {
          return null;
        }

        var renderer = mapping.TargetRenderer;
        if (renderer == null)
        {
          Debug.LogWarning($"{LogPrefix} Mapping '{mapping.name}' has no renderer; preview skipped.");
          return null;
        }

        var sourceMesh = renderer.sharedMesh;
        if (sourceMesh == null)
        {
          Debug.LogWarning($"{LogPrefix} Renderer '{renderer.name}' has no shared mesh; preview skipped.");
          return null;
        }

        var data = mapping.BlendShapeDataAsset as BlendShapeDataSO;
        if (data == null)
        {
          Debug.LogWarning($"{LogPrefix} Mapping '{mapping.name}' has no BlendShare asset; preview skipped.");
          return null;
        }

        var meshName = mapping.EffectiveMeshName;
        var meshData = data.m_MeshDataList?.FirstOrDefault(m => string.Equals(m.m_MeshName, meshName, StringComparison.Ordinal));
        if (meshData == null)
        {
          Debug.LogWarning($"{LogPrefix} Asset '{data.name}' does not contain mesh '{meshName}'; preview skipped.");
          return null;
        }

        if (mapping.DuplicatePolicy == BlendShareRendererMapping.DuplicateBlendShapePolicy.Skip)
        {
          var conflicts = FindConflictingBlendShapes(sourceMesh, meshData);
          if (conflicts.Count > 0)
          {
            Debug.LogWarning($"{LogPrefix} Conflicting blendshape names [{string.Join(", ", conflicts)}]; preview skipped.");
            return null;
          }
        }

        if (mapping.EnforceVertexHash)
        {
          if (meshData.m_VertexCount != sourceMesh.vertexCount)
          {
            Debug.LogWarning($"{LogPrefix} Vertex count mismatch (renderer {sourceMesh.vertexCount}, asset {meshData.m_VertexCount}); preview skipped.");
            return null;
          }

          if (meshData.m_VerticesHash != MeshData.GetVerticesHash(sourceMesh))
          {
            Debug.LogWarning($"{LogPrefix} Vertex hash mismatch between renderer '{renderer.name}' and asset '{data.name}'; preview skipped.");
            return null;
          }
        }

        var previewMesh = BlendShapeAppender.CreateBlendShapesMesh(meshData, sourceMesh);
        if (previewMesh == null)
        {
          Debug.LogWarning($"{LogPrefix} BlendShare failed to generate preview mesh for '{meshName}'.");
          return null;
        }

        previewMesh.name = string.IsNullOrEmpty(sourceMesh.name)
          ? "BlendSharePreview"
          : sourceMesh.name + "_BlendSharePreview";
        previewMesh.hideFlags = HideFlags.HideAndDontSave;

        return previewMesh;
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
          if (!string.IsNullOrEmpty(name) && existingNames.Contains(name))
          {
            conflicts.Add(name);
          }
        }

        return conflicts;
      }
    }
  }
}
