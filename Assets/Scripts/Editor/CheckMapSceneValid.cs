using System.Linq;
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

            if (!isValid)
            {
                errorMessage = sb.ToString().TrimEnd();
            }

            return isValid;
        }
    }
}