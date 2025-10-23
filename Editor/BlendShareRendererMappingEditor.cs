using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.preview;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEditor;
using UnityEngine;

namespace Net._32ba.BlendShareNdmfExtension.Editor
{
  [CustomEditor(typeof(BlendShareRendererMapping))]
  internal sealed class BlendShareRendererMappingEditor : UnityEditor.Editor
  {
    private static readonly GUIContent DefinitionsHeader = new GUIContent("BlendShape Definitions");
    private static readonly GUIContent AddDefinitionLabel = new GUIContent("Add Definition");
    private static readonly GUIContent AddAllLabel = new GUIContent("Add All From Asset");
    private static readonly GUIContent RemoveLabel = new GUIContent("Remove");
    private static readonly GUIContent PickLabel = new GUIContent("Pick");
    private static readonly GUIContent PreviewLabel = new GUIContent("Preview");
    private static readonly GUIContent[] PreviewToolbarContents =
    {
      new GUIContent("Disable"),
      new GUIContent("Enable")
    };
    private const float WeightMin = 0f;
    private const float WeightMax = 100f;

    private SerializedProperty _blendShapeDataProp;
    private SerializedProperty _meshNameOverrideProp;
    private SerializedProperty _enforceHashProp;
    private SerializedProperty _duplicatePolicyProp;
    private SerializedProperty _definitionsProp;

    private void OnEnable()
    {
      _blendShapeDataProp = serializedObject.FindProperty("_blendShapeData");
      _meshNameOverrideProp = serializedObject.FindProperty("_meshNameOverride");
      _enforceHashProp = serializedObject.FindProperty("_enforceVertexHash");
      _duplicatePolicyProp = serializedObject.FindProperty("_duplicateBlendShapePolicy");
      _definitionsProp = serializedObject.FindProperty("_definitions");
    }

    public override void OnInspectorGUI()
    {
      if (serializedObject.isEditingMultipleObjects)
      {
        DrawDefaultInspector();
        return;
      }

      serializedObject.Update();

      EditorGUILayout.PropertyField(_blendShapeDataProp);
      EditorGUILayout.PropertyField(_meshNameOverrideProp);
      EditorGUILayout.PropertyField(_enforceHashProp);
      EditorGUILayout.PropertyField(_duplicatePolicyProp);

      DrawPreviewControls();

      DrawDefinitionsSection();

      serializedObject.ApplyModifiedProperties();
    }

    private void DrawPreviewControls()
    {
      EditorGUILayout.Space();
      using (new EditorGUILayout.HorizontalScope())
      {
        EditorGUILayout.PrefixLabel(PreviewLabel);
        var current = BlendSharePreviewFilter.PreviewNode.IsEnabled.Value ? 1 : 0;
        var selection = GUILayout.Toolbar(current, PreviewToolbarContents);
        BlendSharePreviewFilter.PreviewNode.IsEnabled.Value = selection == 1;
      }
    }

    private void DrawDefinitionsSection()
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField(DefinitionsHeader, EditorStyles.boldLabel);

      var shapeNames = GetAvailableShapeNames();

      if (_definitionsProp != null && _definitionsProp.isArray)
      {
        for (var i = 0; i < _definitionsProp.arraySize; i++)
        {
          if (DrawDefinitionElement(i, shapeNames))
          {
            return;
          }
        }
      }

      using (new EditorGUILayout.HorizontalScope())
      {
        if (GUILayout.Button(AddDefinitionLabel))
        {
          AppendDefinition();
        }

        using (new EditorGUI.DisabledScope(shapeNames.Count == 0))
        {
          if (GUILayout.Button(AddAllLabel))
          {
            AddAllDefinitions(shapeNames);
          }
        }
      }
    }

    private bool DrawDefinitionElement(int index, IReadOnlyList<string> shapeNames)
    {
      var element = _definitionsProp.GetArrayElementAtIndex(index);
      if (element == null) return false;

      using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
      {
        var nameProp = element.FindPropertyRelative("_shapeName");
        var weightProp = element.FindPropertyRelative("_weight");

        using (new EditorGUILayout.HorizontalScope())
        {
          EditorGUILayout.PrefixLabel("BlendShape");
          var newName = EditorGUILayout.DelayedTextField(string.Empty, nameProp.stringValue);
          if (!string.Equals(newName, nameProp.stringValue, StringComparison.Ordinal))
          {
            nameProp.stringValue = newName;
          }

          using (new EditorGUI.DisabledScope(shapeNames.Count == 0))
          {
            if (GUILayout.Button(PickLabel, EditorStyles.miniButton, GUILayout.Width(48f)))
            {
              ShowShapePicker(nameProp, shapeNames);
            }
          }

          if (GUILayout.Button(RemoveLabel, EditorStyles.miniButtonRight, GUILayout.Width(64f)))
          {
            RemoveDefinitionAt(index);
            serializedObject.ApplyModifiedProperties();
            return true;
          }
        }

        if (weightProp != null)
        {
          EditorGUILayout.Slider(weightProp, WeightMin, WeightMax, new GUIContent("Weight"));
        }
      }

      return false;
    }

    private void ShowShapePicker(SerializedProperty nameProp, IReadOnlyList<string> shapeNames)
    {
      if (nameProp == null || shapeNames == null || shapeNames.Count == 0)
      {
        return;
      }

      var menu = new GenericMenu();
      var serialized = serializedObject;
      var propertyPath = nameProp.propertyPath;
      var currentValue = nameProp.stringValue;

      foreach (var shapeName in shapeNames)
      {
        var capturedName = shapeName;
        menu.AddItem(new GUIContent(shapeName), string.Equals(currentValue, capturedName, StringComparison.Ordinal), () =>
        {
          serialized.Update();
          var targetProp = serialized.FindProperty(propertyPath);
          if (targetProp != null)
          {
            targetProp.stringValue = capturedName;
            serialized.ApplyModifiedProperties();
          }
        });
      }

      menu.ShowAsContext();
    }

    private void AppendDefinition()
    {
      var index = _definitionsProp.arraySize;
      _definitionsProp.InsertArrayElementAtIndex(index);
      var element = _definitionsProp.GetArrayElementAtIndex(index);
      if (element != null)
      {
        element.managedReferenceValue = new BlendShareBlendShapeDefinition();
        element.FindPropertyRelative("_shapeName").stringValue = string.Empty;
        element.FindPropertyRelative("_weight").floatValue = 0f;
      }
    }

    private void AddAllDefinitions(IReadOnlyList<string> shapeNames)
    {
      if (shapeNames.Count == 0) return;

      var existing = new HashSet<string>(StringComparer.Ordinal);
      for (var i = 0; i < _definitionsProp.arraySize; i++)
      {
        var element = _definitionsProp.GetArrayElementAtIndex(i);
        var nameProp = element?.FindPropertyRelative("_shapeName");
        if (nameProp == null) continue;
        var value = nameProp.stringValue;
        if (!string.IsNullOrEmpty(value))
        {
          existing.Add(value);
        }
      }

      foreach (var shapeName in shapeNames)
      {
        if (!existing.Add(shapeName)) continue;
        AppendDefinition();
        var element = _definitionsProp.GetArrayElementAtIndex(_definitionsProp.arraySize - 1);
        element.FindPropertyRelative("_shapeName").stringValue = shapeName;
      }
    }

    private void RemoveDefinitionAt(int index)
    {
      if (index < 0 || index >= _definitionsProp.arraySize) return;
      _definitionsProp.DeleteArrayElementAtIndex(index);
      if (index < _definitionsProp.arraySize)
      {
        var element = _definitionsProp.GetArrayElementAtIndex(index);
        if (element != null && element.managedReferenceValue == null)
        {
          _definitionsProp.DeleteArrayElementAtIndex(index);
        }
      }
    }

    private List<string> GetAvailableShapeNames()
    {
      var mapping = (BlendShareRendererMapping)target;
      var result = new List<string>();
      if (mapping == null) return result;

      var data = _blendShapeDataProp.objectReferenceValue as BlendShapeDataSO;
      if (data?.m_MeshDataList == null || data.m_MeshDataList.Count == 0)
      {
        return result;
      }

      var meshName = mapping.EffectiveMeshName;
      if (!string.IsNullOrEmpty(meshName))
      {
        var meshData = data.m_MeshDataList.FirstOrDefault(m => string.Equals(m.m_MeshName, meshName, StringComparison.Ordinal));
        if (meshData?.m_ShapeNames != null)
        {
          result.AddRange(meshData.m_ShapeNames);
          return result.Distinct(StringComparer.Ordinal).ToList();
        }
      }

      foreach (var meshData in data.m_MeshDataList)
      {
        if (meshData?.m_ShapeNames == null) continue;
        foreach (var shapeName in meshData.m_ShapeNames)
        {
          if (!string.IsNullOrEmpty(shapeName) && !result.Contains(shapeName))
          {
            result.Add(shapeName);
          }
        }
      }

      return result;
    }
  }
}
