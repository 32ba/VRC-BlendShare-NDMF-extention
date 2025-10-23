using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace Net._32ba.BlendShareNdmfExtension
{
  [DisallowMultipleComponent]
  [RequireComponent(typeof(SkinnedMeshRenderer))]
  public sealed class BlendShareRendererMapping : MonoBehaviour
  {
    [SerializeField]
    private ScriptableObject _blendShapeData;

    [SerializeField]
    private string _meshNameOverride;

    [SerializeField]
    private bool _enforceVertexHash = true;

    [SerializeField]
    private DuplicateBlendShapePolicy _duplicateBlendShapePolicy = DuplicateBlendShapePolicy.Overwrite;

    [SerializeReference, NotKeyable]
    private List<BlendShareBlendShapeDefinition> _definitions = new();

    private SkinnedMeshRenderer _cachedRenderer;

    public ScriptableObject BlendShapeDataAsset => _blendShapeData;
    public string MeshNameOverride => _meshNameOverride;
    public bool EnforceVertexHash => _enforceVertexHash;
    public DuplicateBlendShapePolicy DuplicatePolicy => _duplicateBlendShapePolicy;
    public IReadOnlyList<BlendShareBlendShapeDefinition> BlendShapeDefinitions => _definitions;
    public SkinnedMeshRenderer TargetRenderer => _cachedRenderer != null ? _cachedRenderer : (_cachedRenderer = GetComponent<SkinnedMeshRenderer>());

    public bool IsValid => TargetRenderer != null && BlendShapeDataAsset != null;
    public bool HasBlendShapeDefinitions => _definitions != null && _definitions.Count > 0;

    public string EffectiveMeshName
    {
      get
      {
        if (!string.IsNullOrEmpty(_meshNameOverride))
        {
          return _meshNameOverride;
        }

        var renderer = TargetRenderer;
        if (renderer != null && renderer.sharedMesh != null)
        {
          return renderer.sharedMesh.name;
        }

        return string.Empty;
      }
    }

    public void Configure(ScriptableObject blendShapeData, string meshNameOverride, bool enforceHash, DuplicateBlendShapePolicy duplicatePolicy)
    {
      _blendShapeData = blendShapeData;
      _meshNameOverride = meshNameOverride ?? string.Empty;
      _enforceVertexHash = enforceHash;
      _duplicateBlendShapePolicy = duplicatePolicy;
    }

    public bool TryGetDefinition(string shapeName, out BlendShareBlendShapeDefinition definition)
    {
      definition = null;
      if (string.IsNullOrEmpty(shapeName) || _definitions == null) return false;

      for (var i = 0; i < _definitions.Count; i++)
      {
        var candidate = _definitions[i];
        if (candidate != null && string.Equals(candidate.ShapeName, shapeName, StringComparison.Ordinal))
        {
          definition = candidate;
          return true;
        }
      }

      return false;
    }

    public void ApplyDefinitionWeights(SkinnedMeshRenderer renderer)
    {
      if (renderer == null || renderer.sharedMesh == null || _definitions == null) return;

      foreach (var definition in _definitions)
      {
        if (definition == null || !definition.HasValidShape) continue;

        var index = renderer.sharedMesh.GetBlendShapeIndex(definition.ShapeName);
        if (index >= 0)
        {
          renderer.SetBlendShapeWeight(index, definition.Weight);
        }
      }
    }

    private void OnValidate()
    {
      if (_definitions == null)
      {
        _definitions = new List<BlendShareBlendShapeDefinition>();
      }
    }

    public enum DuplicateBlendShapePolicy
    {
      Overwrite,
      Skip
    }
  }
}
