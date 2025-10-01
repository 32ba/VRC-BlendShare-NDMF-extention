using UnityEngine;

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

    private SkinnedMeshRenderer _cachedRenderer;

    public ScriptableObject BlendShapeDataAsset => _blendShapeData;
    public string MeshNameOverride => _meshNameOverride;
    public bool EnforceVertexHash => _enforceVertexHash;
    public DuplicateBlendShapePolicy DuplicatePolicy => _duplicateBlendShapePolicy;
    public SkinnedMeshRenderer TargetRenderer => _cachedRenderer != null ? _cachedRenderer : (_cachedRenderer = GetComponent<SkinnedMeshRenderer>());

    public bool IsValid
    {
      get
      {
        if (TargetRenderer == null)
        {
          return false;
        }

        return _blendShapeData != null;
      }
    }

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

    public enum DuplicateBlendShapePolicy
    {
      Overwrite,
      Skip
    }
  }
}
