using System;
using System.Collections.Generic;
using System.Linq;
using Net._32ba.BlendShareNdmfExtension;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEditor;
using UnityEngine;

namespace Net._32ba.BlendShareNdmfExtension.Editor
{
  public sealed class AddBlendShapeByBlendShareWizard : EditorWindow
  {
    private GameObject _targetRoot;
    private SkinnedMeshRenderer _renderer;
    private BlendShapeDataSO _blendShapeData;
    private string _meshNameOverride;
    private bool _enforceHash = true;
    private BlendShareRendererMapping.DuplicateBlendShapePolicy _policy = BlendShareRendererMapping.DuplicateBlendShapePolicy.Overwrite;
    private MatchAnalysis _matchAnalysis;
    private bool _matchDirty = true;

    private struct MatchAnalysis
    {
      public bool HasMatch;
      public bool IsAmbiguous;
      public SkinnedMeshRenderer Renderer;
      public MeshData MeshData;
      public int Score;
      public string Message;
      public MessageType MessageType;
    }

    [MenuItem("BlendShare/Mapping Wizard", priority = 10)]
    private static void OpenFromMenu()
    {
      GetWindow<AddBlendShapeByBlendShareWizard>(true, "BlendShare Mapping Wizard");
    }

    private void OnEnable()
    {
      if (_targetRoot == null && Selection.activeGameObject != null)
      {
        _targetRoot = Selection.activeGameObject;
      }

      if (_renderer == null && _targetRoot != null)
      {
        _renderer = _targetRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (_renderer != null && string.IsNullOrEmpty(_meshNameOverride))
        {
          var mesh = _renderer.sharedMesh;
          _meshNameOverride = mesh ? mesh.name : string.Empty;
        }
      }

      MarkMatchDirty();
      UpdateMatch(false);
    }

    private void OnGUI()
    {
      DrawTargetSelection();
      EditorGUILayout.Space();
      DrawBlendShapeSelection();
      EditorGUILayout.Space();
      DrawMatchSection();
      EditorGUILayout.Space();
      DrawApplySection();
    }

    private void DrawTargetSelection()
    {
      EditorGUILayout.LabelField("Step 1: Target GameObject", EditorStyles.boldLabel);
      using (new EditorGUILayout.HorizontalScope())
      {
        var previousRoot = _targetRoot;
        _targetRoot = (GameObject)EditorGUILayout.ObjectField(_targetRoot, typeof(GameObject), true);
        if (GUILayout.Button("Use Selection", GUILayout.Width(120f)))
        {
          _targetRoot = Selection.activeGameObject;
        }

        if (_targetRoot != previousRoot)
        {
          _renderer = null;
          _meshNameOverride = string.Empty;
          MarkMatchDirty();
          UpdateMatch(true);
        }
      }

      if (_targetRoot == null)
      {
        EditorGUILayout.HelpBox("Select the GameObject whose hierarchy contains the target SkinnedMeshRenderer.", MessageType.Info);
        return;
      }

      if (!TargetHasRenderer())
      {
        EditorGUILayout.HelpBox($"'{_targetRoot.name}' does not contain a SkinnedMeshRenderer in its hierarchy.", MessageType.Warning);
      }
    }

    private void DrawBlendShapeSelection()
    {
      EditorGUILayout.LabelField("Step 2: BlendShare Asset", EditorStyles.boldLabel);
      var previousData = _blendShapeData;
      _blendShapeData = (BlendShapeDataSO)EditorGUILayout.ObjectField("BlendShapeDataSO", _blendShapeData, typeof(BlendShapeDataSO), false);
      if (_blendShapeData != previousData)
      {
        MarkMatchDirty();
        UpdateMatch(true);
      }
    }

    private void DrawMatchSection()
    {
      EditorGUILayout.LabelField("Step 3: Match Review", EditorStyles.boldLabel);
      EnsureMatchEvaluated();

      if (!string.IsNullOrEmpty(_matchAnalysis.Message))
      {
        EditorGUILayout.HelpBox(_matchAnalysis.Message, _matchAnalysis.MessageType);
      }

      using (new EditorGUI.DisabledScope(true))
      {
        EditorGUILayout.ObjectField("Matched Renderer", _renderer, typeof(SkinnedMeshRenderer), true);
      }

      using (new EditorGUI.DisabledScope(_renderer == null))
      {
        _meshNameOverride = EditorGUILayout.TextField("Mesh Name Override", _meshNameOverride);
        _enforceHash = EditorGUILayout.Toggle("Enforce Vertex Hash", _enforceHash);
        _policy = (BlendShareRendererMapping.DuplicateBlendShapePolicy)EditorGUILayout.EnumPopup("On Conflict", _policy);
      }
    }

    private void DrawApplySection()
    {
      using (new EditorGUI.DisabledScope(!CanCreateMapping()))
      {
        if (GUILayout.Button("Apply Mapping"))
        {
          ApplyMapping();
        }
      }
    }

    private void ApplyMapping()
    {
      EnsureMatchEvaluated();
      if (!CanCreateMapping())
      {
        EditorUtility.DisplayDialog("Cannot Apply", "Resolve the issues highlighted above before applying the mapping.", "OK");
        return;
      }

      var rendererName = _renderer != null ? _renderer.name : "None";
      var assetName = _blendShapeData != null ? _blendShapeData.name : "None";
      var summary = string.Join(Environment.NewLine,
        "Apply BlendShare mapping?",
        string.Empty,
        $"Target GameObject: {_targetRoot.name}",
        $"Renderer: {rendererName}",
        $"BlendShape Asset: {assetName}",
        $"Mesh Override: {_meshNameOverride}");
      if (!EditorUtility.DisplayDialog("Confirm BlendShare Mapping", summary, "Apply", "Cancel"))
      {
        return;
      }

      var mapping = EnsureMappingOnRenderer();
      if (mapping == null)
      {
        Debug.LogError("[BlendShare] Failed to locate or create BlendShareRendererMapping component.");
        return;
      }

      Undo.RecordObject(mapping, "Configure BlendShare Mapping");
      mapping.Configure(_blendShapeData, _meshNameOverride, _enforceHash, _policy);
      EditorUtility.SetDirty(mapping);
      PrefabUtility.RecordPrefabInstancePropertyModifications(mapping);

      Debug.Log($"[BlendShare] Mapping applied on '{mapping.gameObject.name}' for renderer '{mapping.TargetRenderer?.name ?? "(missing)"}'.", mapping);

      MarkMatchDirty();
      UpdateMatch(false);
    }

    private void EnsureMatchEvaluated()
    {
      if (_matchDirty)
      {
        UpdateMatch(false);
      }
    }

    private void MarkMatchDirty()
    {
      _matchDirty = true;
    }

    private void UpdateMatch(bool allowClearRenderer)
    {
      _matchAnalysis = EvaluateMatch();

      if (_matchAnalysis.HasMatch && !_matchAnalysis.IsAmbiguous)
      {
        _renderer = _matchAnalysis.Renderer;
        var meshData = _matchAnalysis.MeshData;
        if (meshData != null && !string.IsNullOrEmpty(meshData.m_MeshName))
        {
          _meshNameOverride = meshData.m_MeshName;
        }
        else if (_renderer != null && _renderer.sharedMesh != null && string.IsNullOrEmpty(_meshNameOverride))
        {
          _meshNameOverride = _renderer.sharedMesh.name;
        }
      }
      else if (allowClearRenderer)
      {
        _renderer = null;
        _meshNameOverride = string.Empty;
      }

      _matchDirty = false;
    }

    private MatchAnalysis EvaluateMatch()
    {
      if (_targetRoot == null)
      {
        return new MatchAnalysis
        {
          Message = "Select a GameObject before evaluating matches.",
          MessageType = MessageType.Info
        };
      }

      var renderers = _targetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
      if (renderers == null || renderers.Length == 0)
      {
        return new MatchAnalysis
        {
          Message = $"'{_targetRoot.name}' does not contain a SkinnedMeshRenderer in its hierarchy.",
          MessageType = MessageType.Warning
        };
      }

      if (_blendShapeData == null)
      {
        return new MatchAnalysis
        {
          Message = "Assign a BlendShapeDataSO asset (step 2).",
          MessageType = MessageType.Info
        };
      }

      var meshDataList = _blendShapeData.m_MeshDataList;
      if (meshDataList == null || meshDataList.Count == 0)
      {
        return new MatchAnalysis
        {
          Message = $"BlendShare asset '{_blendShapeData.name}' does not contain mesh data entries.",
          MessageType = MessageType.Warning
        };
      }

      var candidates = new List<(SkinnedMeshRenderer renderer, MeshData data, int score)>();
      foreach (var meshData in meshDataList)
      {
        if (meshData == null)
        {
          continue;
        }

        foreach (var candidate in renderers)
        {
          if (candidate == null)
          {
            continue;
          }

          var sharedMesh = candidate.sharedMesh;
          if (!sharedMesh)
          {
            continue;
          }

          var score = CalculateMatchScore(sharedMesh, meshData);
          if (score > 0)
          {
            candidates.Add((candidate, meshData, score));
          }
        }
      }

      if (candidates.Count == 0)
      {
        return new MatchAnalysis
        {
          Message = "No SkinnedMeshRenderer under the selected GameObject matches the BlendShape asset.",
          MessageType = MessageType.Warning
        };
      }

      var bestScore = candidates.Max(c => c.score);
      var bestCandidates = candidates.Where(c => c.score == bestScore).ToList();

      if (bestCandidates.Count > 1)
      {
        var names = string.Join(", ", bestCandidates.Select(c => c.renderer.name));
        return new MatchAnalysis
        {
          IsAmbiguous = true,
          Message = $"Multiple renderers match equally (score {bestScore}): {names}. Refine the selection to continue.",
          MessageType = MessageType.Warning
        };
      }

      var best = bestCandidates[0];
      var message = $"Matched renderer '{best.renderer.name}' (score {best.score}).";
      if (!string.IsNullOrEmpty(best.data?.m_MeshName))
      {
        message += $" Mesh entry: {best.data.m_MeshName}.";
      }

      return new MatchAnalysis
      {
        HasMatch = true,
        Renderer = best.renderer,
        MeshData = best.data,
        Score = best.score,
        Message = message,
        MessageType = MessageType.Info
      };
    }

    private static int CalculateMatchScore(Mesh candidateMesh, MeshData meshData)
    {
      if (candidateMesh == null || meshData == null)
      {
        return 0;
      }

      if (meshData.m_OriginMesh && candidateMesh == meshData.m_OriginMesh)
      {
        return 5;
      }

      var score = 0;

      if (!string.IsNullOrEmpty(meshData.m_MeshName) && string.Equals(candidateMesh.name, meshData.m_MeshName, StringComparison.Ordinal))
      {
        score = Math.Max(score, 3);
      }

      if (meshData.m_VertexCount == candidateMesh.vertexCount)
      {
        score = Math.Max(score, 2);

        if (meshData.m_VerticesHash == MeshData.GetVerticesHash(candidateMesh))
        {
          score = Math.Max(score, 4);
        }
      }

      return score;
    }

    private bool CanCreateMapping()
    {
      if (_targetRoot == null || _blendShapeData == null || _renderer == null)
      {
        return false;
      }

      if (!_renderer.transform.IsChildOf(_targetRoot.transform))
      {
        return false;
      }

      if (!_matchAnalysis.HasMatch || _matchAnalysis.IsAmbiguous)
      {
        return false;
      }

      return true;
    }

    private bool TargetHasRenderer()
    {
      return _targetRoot != null && _targetRoot.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    }

    private BlendShareRendererMapping EnsureMappingOnRenderer()
    {
      if (_renderer == null)
      {
        return null;
      }

      var mapping = _renderer.GetComponent<BlendShareRendererMapping>();
      if (mapping != null)
      {
        return mapping;
      }

      return Undo.AddComponent<BlendShareRendererMapping>(_renderer.gameObject);
    }
  }
}
