using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FRLMapMod.Editor
{
    /// <summary>
    /// Modal-like window to select a new scene path for a UGC item.
    /// Only scenes not already linked to another UGC item (except the current one) can be selected.
    /// </summary>
    public class UGCSceneRetargetWindow : EditorWindow
    {
        private UGCService _service;
        private UGCItemData _item;
        private Action<string> _onSceneSelected;

        private Vector2 _scroll;
        private string _selectedPath;

        private class SceneEntry
        {
            public string Path;
            public bool IsLinked;
            public string LinkedDisplayName;
        }

        private List<SceneEntry> _entries = new List<SceneEntry>();

        internal static void Open(UGCService service, UGCItemData item, Action<string> onSceneSelected)
        {
            if (service == null || item == null)
            {
                Debug.LogWarning("[UGCSceneRetargetWindow] Cannot open: service or item is null.");
                return;
            }

            var window = CreateInstance<UGCSceneRetargetWindow>();
            window._service = service;
            window._item = item;
            window._onSceneSelected = onSceneSelected;
            window.titleContent = new GUIContent("Select Scene");
            window.position = new Rect(Screen.width / 2f, Screen.height / 2f, 600, 400);
            window.InitializeEntries();
            window.ShowUtility(); // utility window behaves closer to modal
            window.Focus();
        }

private void InitializeEntries()
{
    _entries.Clear();
    _selectedPath = _item.ScenePath;

    // Gather all scenes from project assets (not limited to Build Settings)
    var sceneGuids = AssetDatabase.FindAssets("t:Scene");
    var allScenePaths = sceneGuids
        .Select(AssetDatabase.GUIDToAssetPath)
        .Where(path => !string.IsNullOrEmpty(path))
        .Distinct()
        .ToList();

    var allItems = _service.Items;

    foreach (var path in allScenePaths)
    {
        var linkedItem = allItems.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.ScenePath) &&
            string.Equals(i.ScenePath, path, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(i.ItemId, _item.ItemId, StringComparison.OrdinalIgnoreCase));

        var entry = new SceneEntry
        {
            Path = path,
            IsLinked = linkedItem != null,
            LinkedDisplayName = linkedItem?.Title
        };

        _entries.Add(entry);
    }

    // Sort: current path first, then unlinked, then linked
    _entries = _entries
        .OrderByDescending(e => string.Equals(e.Path, _item.ScenePath, StringComparison.OrdinalIgnoreCase))
        .ThenBy(e => e.IsLinked)
        .ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Select a new scene for this UGC item.", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"Current Scene Path: {_item.ScenePath}");

            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var entry in _entries)
            {
                DrawSceneEntry(entry);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !string.IsNullOrEmpty(_selectedPath);
            if (GUILayout.Button("Confirm", GUILayout.Width(100)))
            {
                _onSceneSelected?.Invoke(_selectedPath);
                Close();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSceneEntry(SceneEntry entry)
        {
            EditorGUILayout.BeginHorizontal();

            bool canSelect = !entry.IsLinked;

            // Radio-like selection
            EditorGUI.BeginDisabledGroup(!canSelect);
            bool isSelected = string.Equals(_selectedPath, entry.Path, StringComparison.OrdinalIgnoreCase);
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            EditorGUI.EndDisabledGroup();

            string label;
            if (entry.IsLinked)
            {
                label = $"{entry.Path}   [Linked - {entry.LinkedDisplayName}]";
            }
            else
            {
                label = entry.Path;
            }

            EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));

            // If user toggled this row and it's selectable, update selectedPath
            if (canSelect && newSelected && !isSelected)
            {
                _selectedPath = entry.Path;
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}