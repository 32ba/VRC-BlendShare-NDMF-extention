using System;
using UnityEngine;
using UnityEngine.Animations;

namespace Net._32ba.BlendShareNdmfExtension
{
  [Serializable]
  public sealed class BlendShareBlendShapeDefinition : IEquatable<BlendShareBlendShapeDefinition>
  {
    private const float MinWeight = 0f;
    private const float MaxWeight = 100f;

    [SerializeField, NotKeyable]
    private string _shapeName = string.Empty;

    [SerializeField]
    private float _weight = 0f;

    public string ShapeName
    {
      get => _shapeName;
      set => _shapeName = value ?? string.Empty;
    }

    public float Weight
    {
      get => _weight;
      set => _weight = Mathf.Clamp(value, MinWeight, MaxWeight);
    }

    public bool HasValidShape => !string.IsNullOrWhiteSpace(_shapeName);

    public BlendShareBlendShapeDefinition Clone()
    {
      return new BlendShareBlendShapeDefinition
      {
        _shapeName = _shapeName,
        _weight = _weight
      };
    }

    public bool Equals(BlendShareBlendShapeDefinition other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return string.Equals(_shapeName, other._shapeName, StringComparison.Ordinal) &&
             Mathf.Approximately(_weight, other._weight);
    }

    public override bool Equals(object obj)
    {
      return obj is BlendShareBlendShapeDefinition other && Equals(other);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        var hash = StringComparer.Ordinal.GetHashCode(_shapeName ?? string.Empty);
        hash = (hash * 397) ^ _weight.GetHashCode();
        return hash;
      }
    }
  }
}
