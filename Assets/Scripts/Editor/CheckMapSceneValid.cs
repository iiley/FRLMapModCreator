using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace FRLMapMod.Editor
{
    /// <summary>
    /// Helper to validate that the currently open scene contains
    /// exactly one instance of required scripts/components.
    /// </summary>
    public static class CheckMapSceneValid
    {
        /// <summary>
        /// Checks the currently active scene for required scripts.
        /// The scene is valid only when it contains exactly ONE instance
        /// of each of the following components:
        /// - RaceManager
        /// - EventSystem
        /// - DriftCamera
        /// - Skidmarks
        ///
        /// Returns true if valid; otherwise false. When invalid, an
        /// error message describing the problem is returned via
        /// <paramref name="errorMessage"/>.
        /// </summary>
        public static bool CheckCurrentScene(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Ensure there is an open scene
            if (SceneManager.GetActiveScene().isLoaded == false)
            {
                errorMessage = "No active scene is loaded.";
                return false;
            }

            bool isValid = true;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // Helper local function to validate a component type
            void ValidateSingle<T>(string name) where T : Component
            {
                var list = Object.FindObjectsOfType<T>(true); // include inactive
                if (list.Length != 1)
                {
                    isValid = false;
                    sb.AppendLine($"Scene must contain exactly one {name}, found {list.Length}.");
                }
            }

            // RaceManager
            ValidateSingle<RaceManager>(nameof(RaceManager));

            // EventSystem
            ValidateSingle<EventSystem>(nameof(EventSystem));

            // DriftCamera
            ValidateSingle<DriftCamera>(nameof(DriftCamera));

            // Skidmarks
            ValidateSingle<Skidmarks>(nameof(Skidmarks));

            // Additional check: RaceManager references must be non-null
            var raceManagers = Object.FindObjectsOfType<RaceManager>(true);
            if (raceManagers.Length == 1)
            {
                var rm = raceManagers[0];

                bool refOk = true;

                // Directly check driftCamera and skidmarks references.
                if (!rm.driftCamera)
                {
                    refOk = false;
                    sb.AppendLine("RaceManager.driftCamera reference must not be null.");
                }

                if (!rm.skidmarks)
                {
                    refOk = false;
                    sb.AppendLine("RaceManager.skidmarks reference must not be null.");
                }

                if (!rm.cameraStartPosition)
                {
                    refOk = false;
                    sb.AppendLine("RaceManager.cameraStartPosition reference must not be null.");
                }

                if (!rm.cameraMoveDirection)
                {
                    refOk = false;
                    sb.AppendLine("RaceManager.cameraMoveDirection reference must not be null.");
                }

                if (!rm.qualityHidden)
                {
                    refOk = false;
                    sb.AppendLine("RaceManager.qualityHidden reference must not be null.");
                }

                if (!refOk)
                {
                    isValid = false;
                }
            }

            // 新增：辅助函数 - 获取 GameObject 在场景内的完整路径
            string GetGameObjectPath(GameObject go)
            {
                if (go == null) return "(null)";
                var names = new List<string>();
                Transform t = go.transform;
                while (t != null)
                {
                    names.Add(t.name);
                    t = t.parent;
                }
                names.Reverse();
                return string.Join("->", names);
            }

            // Check all Material, accept only these shaders
            {
                var allowedShaders = new HashSet<string>
                {
                    "FR Legend/Car Outline",
                    "FR Legend/Car Outline Simple", 
                    "FR Legend/Toon Outline",
                    "FR Legend/Toon",
                    "FR Legend/SkidMarks",
                    "FR Legend/Hard Edge Unlit",
                    "FR Legend/Soft Edge Unlit",
                    "Projector/Multiply",
                    "Sprites/Default",
                    "FR Legend/Mountain Fog",
                    "Mobile/Particles/Alpha Blended", 
                    "GUI/Text Shader", 
                };

                // 遍历 Renderer，实时检查 sharedMaterials，发现第一个不允许的立即返回失败并包含场景路径
                var renderers = Object.FindObjectsOfType<Renderer>(true);
                foreach (var r in renderers)
                {
                    var shared = r.sharedMaterials;
                    if (shared == null) continue;
                    foreach (var m in shared)
                    {
                        if (m == null) continue;
                        var shaderName = m.shader ? m.shader.name : "(null shader)";
                        if (!allowedShaders.Contains(shaderName))
                        {
                            var assetPath = AssetDatabase.GetAssetPath(m);
                            var path = GetGameObjectPath(r.gameObject);
                            errorMessage = $"Material '{m.name}' (path: {assetPath}) uses disallowed shader '{shaderName}'. Used on: {path}";
                            return false; // 立即返回
                        }
                    }
                }

                // 再检查 Projector，用法类似：若发现不允许的 shader 立即返回
                var projectors = Object.FindObjectsOfType<Projector>(true);
                foreach (var p in projectors)
                {
                    if (p == null) continue;
                    var m = p.material;
                    if (m == null) continue;
                    var shaderName = m.shader ? m.shader.name : "(null shader)";
                    if (!allowedShaders.Contains(shaderName))
                    {
                        var assetPath = AssetDatabase.GetAssetPath(m);
                        var path = GetGameObjectPath(p.gameObject);
                        errorMessage = $"Material '{m.name}' (path: {assetPath}) uses disallowed shader '{shaderName}'. Used on: {path}";
                        return false; // 立即返回
                    }
                }
            }

            if (!isValid)
            {
                errorMessage = sb.ToString().TrimEnd();
            }

            return isValid;
        }
    }
}