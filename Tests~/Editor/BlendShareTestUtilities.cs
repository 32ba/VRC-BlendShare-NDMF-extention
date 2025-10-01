using Net._32ba.BlendShareNdmfExtension;
using UnityEditor;
using UnityEngine;

namespace Net._32ba.BlendShareNdmfExtension.Tests
{
  internal static class BlendShareTestUtilities
  {
    [MenuItem("BlendShare/Test/Attach Mapping To Selected Renderer", true)]
    private static bool ValidateAttach()
    {
      return GetRendererFromSelection() != null;
    }

    [MenuItem("BlendShare/Test/Attach Mapping To Selected Renderer", false, 0)]
    private static void AttachToSelected()
    {
      var renderer = GetRendererFromSelection();
      if (renderer == null)
      {
        EditorUtility.DisplayDialog("BlendShare Test", "Select a GameObject with a SkinnedMeshRenderer to create a test setup.", "OK");
        return;
      }

      var mapping = renderer.GetComponent<BlendShareRendererMapping>();
      if (mapping == null)
      {
        mapping = Undo.AddComponent<BlendShareRendererMapping>(renderer.gameObject);
      }

      Undo.RecordObject(mapping, "Configure BlendShare Mapping Test");
      var mesh = renderer.sharedMesh;
      mapping.Configure(null, mesh ? mesh.name : string.Empty, true, BlendShareRendererMapping.DuplicateBlendShapePolicy.Overwrite);
      EditorUtility.SetDirty(mapping);
      Selection.activeObject = mapping;
    }

    private static SkinnedMeshRenderer GetRendererFromSelection()
    {
      var active = Selection.activeGameObject;
      if (active == null) return null;
      return active.GetComponent<SkinnedMeshRenderer>() ?? active.GetComponentInChildren<SkinnedMeshRenderer>();
    }
  }
}
