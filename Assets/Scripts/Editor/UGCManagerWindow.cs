// filepath: d:\workspace\FRLMapMod\Assets\Scripts\Editor\UGCManagerWindow.cs
// filepath: d:\workspace\FRLMapMod\Assets\Scripts\Editor\UGCManagerWindow.cs
// filepath: d:\workspace\FRLMapMod\Assets\Scripts\Editor\UGCManagerWindow.cs

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FRLMapMod.Editor
{
    public class UGCManagerWindow : EditorWindow
    {
        // -----------------------------
        // Service
        // -----------------------------

        private UGCService _service;

        // -----------------------------
        // UI state
        // -----------------------------

        private Vector2 _listScroll;

        // Current scene create-MOD UI state
        private bool _showCreateModForCurrentScene;

        private string _newModNameForCurrentScene = string.Empty;

        // New: whether we are currently refreshing items from server
        private bool _isRefreshingItems;

        // -----------------------------
        // Menu entry
        // -----------------------------

        [MenuItem("Tools/UGC Map Manager")]
        private static void ShowWindow()
        {
            GetWindow<UGCManagerWindow>("UGC Map Manager");
        }

        // -----------------------------
        // Lifecycle
        // -----------------------------

        private void OnEnable()
        {
            EnsureService();

            // Load credentials from EditorPrefs so UI fields and potential auto-login have data
            _service.LoadCredentialsFromEditorPrefs();

            // If already logged in and we have an AuthContext + entity key, do NOT auto-login again.
            // Optionally refresh items once.
            if (_service.HasValidSession)
            {
                _isRefreshingItems = true;
                _service.RefreshItemsFromServer(refreshSuccess =>
                {
                    _isRefreshingItems = false;
                    Repaint();
                });
                return;
            }

            // Otherwise, if we have stored credentials, try automatic login
            if (!string.IsNullOrEmpty(_service.Username) && !string.IsNullOrEmpty(_service.Password))
            {
                _service.StartLogin(_service.Username, _service.Password, auto: true, onCompleted: success =>
                {
                    if (success)
                    {
                        _isRefreshingItems = true;
                        _service.RefreshItemsFromServer(refreshSuccess =>
                        {
                            _isRefreshingItems = false;
                            Repaint();
                        });
                    }
                    else
                    {
                        Repaint();
                    }
                });
            }
        }


        private void OnDisable()
        {
            // Always save current username and password
            _service?.SaveCredentialsToEditorPrefs();
        }

        // -----------------------------
        // GUI
        // -----------------------------

        private GUIStyle _richLabelStyle;
        
        private void OnGUI()
        {
            _richLabelStyle ??= new GUIStyle(EditorStyles.label)
            {
                richText = true
            };
            
            DrawLoginRegion();

            GUILayout.Space(8f);

            if (_service == null)
            {
                EditorGUILayout.HelpBox(
                    "Service is not initialized.",
                    MessageType.Warning);
                return;
            }

            // If login or auto-login is in progress, show a loading message and hide UGC sections
            if (_service.IsLoggingIn)
            {
                EditorGUILayout.HelpBox(
                    "Logging in to PlayFab...",
                    MessageType.Info);
                return;
            }

            // If items are being refreshed, also hide UGC sections
            if (_isRefreshingItems)
            {
                EditorGUILayout.HelpBox(
                    "Loading UGC items from PlayFab...",
                    MessageType.Info);
                return;
            }

            if (_service.IsLoggedIn)
            {
                DrawCurrentSceneRegion();

                GUILayout.Space(8f);

                DrawItemListRegion();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Please log into your PlayFab account first.",
                    MessageType.Info);
            }
        }


        private void DrawLoginRegion()
        {
            EnsureService();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Account Login", EditorStyles.boldLabel);

            if (!_service.IsLoggedIn)
            {
                // Bind UI to service credentials
                string username = _service.Username;
                string password = _service.Password;

                username = EditorGUILayout.TextField("Username", username);
                password = EditorGUILayout.PasswordField("Password", password);

                // Keep latest typed values in service (so auto-login next time uses them)
                _service.SetCredentials(username, password);

                GUILayout.Space(4f);

                EditorGUI.BeginDisabledGroup(_service.IsLoggingIn);
                string buttonLabel = _service.IsLoggingIn ? "Logging in..." : "Login";
                if (GUILayout.Button(buttonLabel))
                {
                    _service.StartLogin(username, password, auto: false, onCompleted: success =>
                    {
                        if (success)
                        {
                            _isRefreshingItems = true;
                            _service.RefreshItemsFromServer(refreshSuccess =>
                            {
                                _isRefreshingItems = false;
                                Repaint();
                            });
                        }
                        else
                        {
                            Repaint();
                        }
                    });
                }

                EditorGUI.EndDisabledGroup();

                if (!string.IsNullOrEmpty(_service.LastLoginError))
                {
                    EditorGUILayout.HelpBox(_service.LastLoginError, MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.LabelField($"Logged in as: {_service.Username}");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh list", GUILayout.Width(160)))
                {
                    _isRefreshingItems = true;
                    _service.RefreshItemsFromServer(success =>
                    {
                        _isRefreshingItems = false;
                        Repaint();
                    });
                }

                if (GUILayout.Button("Logout", GUILayout.Width(80)))
                {
                    _service.Logout();
                    _showCreateModForCurrentScene = false;
                    _newModNameForCurrentScene = string.Empty;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCurrentSceneRegion()
        {
            EnsureService();

            // Use helpBox style to make this panel stand out
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Current Scene", EditorStyles.boldLabel);
            // Thin separator under the title
            EditorGUILayout.LabelField(GUIContent.none, GUI.skin.horizontalSlider);

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                EditorGUILayout.HelpBox(
                    "No scene is currently open. Please open a scene in the Editor to manage its UGC item.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            string scenePath = activeScene.path;
            EditorGUILayout.LabelField("Scene Path", scenePath);

            // Try to find existing UGC item for this scene
            UGCItemData currentItem = _service.FindItemByScenePath(scenePath);

            if (currentItem == null)
            {
                // Scene not yet created as UGC item
                EditorGUILayout.HelpBox(
                    "This scene has not been created as a UGC item.",
                    MessageType.None);

                if (!_showCreateModForCurrentScene)
                {
                    if (GUILayout.Button("Create MOD", GUILayout.Width(120)))
                    {
                        _showCreateModForCurrentScene = true;
                        _newModNameForCurrentScene = activeScene.name;
                    }
                }
                else
                {
                    DrawCreateModInlineUI(scenePath);
                }
            }
            else
            {
                // Scene already associated with a UGC item
                EditorGUILayout.LabelField("Name", currentItem.Title);

                // Show raw versions
                EditorGUILayout.LabelField("Draft Version", currentItem.DraftVersion ?? "N/A");
                EditorGUILayout.LabelField("Published Version", currentItem.PublishedVersion ?? "N/A");

                // Aggregated status string, e.g. "Draft(0.1), Non-Publish" / "Published(0.1)" / "Published(0.1) - Draft(0.2)"
                string statusText = _service.GetStatusDisplayText(currentItem);
                EditorGUILayout.LabelField("Status", statusText, _richLabelStyle);
                if (currentItem.PublishedRejected)
                {
                    EditorGUILayout.HelpBox($"Your map has been hidden because of[{currentItem.PublishedRejectReason}]. Please fix the policy issues before submitting again.", MessageType.Error);
                }
                if (currentItem.Status == UGCItemStatus.Submitted)
                {
                    EditorGUILayout.HelpBox("Your map is awaiting review. Once approved, it will be automatically published.", MessageType.Info);
                }else if (currentItem.Status == UGCItemStatus.Rejected)
                {
                    EditorGUILayout.HelpBox($"Your map is rejected because of [{currentItem.RejectReason}]\nPlease fix the issue then create new version to submit.", MessageType.Error);
                }
                
                EditorGUILayout.BeginHorizontal();
                
                if (currentItem.Status != UGCItemStatus.Submitted)
                {
                    if (GUILayout.Button("Edit", GUILayout.Width(100)))
                    {
                        UGCItemEditWindow.Open(_service, currentItem);
                    }    
                }

                if (_service.CanSubmit(currentItem))
                {
                    if (GUILayout.Button("Publish", GUILayout.Width(100)))
                    {
                        UGCItemEditWindow.CloseWindow();
                        TryPublish(currentItem);
                    }
                }else if (_service.CanCancelSubmit(currentItem))
                {
                    if (GUILayout.Button("Abort Publishing", GUILayout.Width(150)))
                    {
                        TryCancelPublish(currentItem);
                    }
                }

                if (GUILayout.Button("Delete", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Delete UGC Item",
                            $"Are you sure you want to delete '{currentItem.Title}'?\nThis will delete both draft and published versions on PlayFab.",
                            "Delete", "Cancel"))
                    {
                        _service.DeleteItemOnServer(currentItem, success => { Repaint(); });
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void TryCancelPublish(UGCItemData item)
        {
            if (EditorUtility.DisplayDialog("Abort Publish Confirmation",
                    $"Do you want to cancel this submission?", "Yes, cancel it",
                    "No, keep it"))
            {
                EditorUtility.DisplayProgressBar("Abort Publishing", $"Cancelling this submission '{item.Title}'...", 0.5f);
                _service.CancelSubmitItemOnServer(item, success =>
                {
                    EditorUtility.ClearProgressBar();
                    Repaint();
                });
            }
        }

        private void TryPublish(UGCItemData item)
        {
            if (!item.HasThumbnail)
            {
                var open = EditorUtility.DisplayDialog("Publish Failed",
                    "Item does not have a thumbnail image. Please set a thumbnail in Item Edit window.", "OK",
                    "Cancel");
                if(open) UGCItemEditWindow.Open(_service, item);
                return;
            }

            if (!item.HasScreenshots)
            {
                var open = EditorUtility.DisplayDialog("Publish Failed",
                    "Item does not have any screenshot images. Please add screenshots in Item Edit window.", "OK",
                    "Cancel");
                if(open) UGCItemEditWindow.Open(_service, item);
                return;
            }

            if (string.IsNullOrEmpty(item.Title))
            {
                var open = EditorUtility.DisplayDialog("Publish Failed",
                    "Item does not have a title. Please edit the title in Item Edit Window.", "OK",
                    "Cancel");
                if(open) UGCItemEditWindow.Open(_service, item);
                return;
            }
            
            if (item.Bundles is { Count: >= 2 })
            {
                if (EditorUtility.DisplayDialog("Publish Confirmation",
                        $"Please check is the bundles are newest ready?\n\n{item.BundleDetails}", "Ready, Go Publish",
                        "Cancel"))
                {
                    EditorUtility.DisplayProgressBar("Publishing Item", $"Publishing '{item.Title}'...", 0.5f);
                    _service.SubmitItemOnServer(item, success =>
                    {
                        EditorUtility.ClearProgressBar();
                        Repaint();
                    });
                }
            }
            else
            {
                if (EditorUtility.DisplayDialog("Publish Failed",
                        "Item bundles are not ready for publish. Please upload bundles in Item Edit window.", "OK",
                        "Cancel"))
                {
                    UGCItemEditWindow.Open(_service, item);
                }
            }
        }

        private void DrawCreateModInlineUI(string scenePath)
        {
            EnsureService();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Create MOD for Current Scene", EditorStyles.miniBoldLabel);

            _newModNameForCurrentScene = EditorGUILayout.TextField("Name", _newModNameForCurrentScene);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirm Create", GUILayout.Width(120)))
            {
                
                EditorUtility.DisplayProgressBar(
                    "Create MOD",
                    $"Creating '{_newModNameForCurrentScene}'...",
                    0.5f);
                _service.CreateItemOnServerForScene(scenePath, _newModNameForCurrentScene, createdItem =>
                {
                    // After creating on server, you might want to refresh list again.
                    // For now, just repaint because mock creation updates in-memory list.
                    Repaint();
                    EditorUtility.ClearProgressBar();
                    if(null != createdItem) UGCItemEditWindow.Open(_service, createdItem);
                });

                _showCreateModForCurrentScene = false;
                _newModNameForCurrentScene = string.Empty;
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _showCreateModForCurrentScene = false;
                _newModNameForCurrentScene = string.Empty;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawItemListRegion()
        {
            EnsureService();
            var items = _service.Items;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"All UGC Map Items (MAX {_service.trackModMaxCount})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4f);


            if (items.Count == 0)
            {
                EditorGUILayout.HelpBox("No UGC map items found.", MessageType.Info);
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(250));

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(160));
            GUILayout.Label("Scene Path", GUILayout.ExpandWidth(true));
            GUILayout.Label("Status", GUILayout.Width(200));
            GUILayout.Label("Actions", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2f);

            foreach (var item in items)
            {
                DrawItemRow(item);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawItemRow(UGCItemData item)
        {
            if (item == null) return;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(item.Title, GUILayout.Width(160));
            GUILayout.Label(item.ScenePath, GUILayout.ExpandWidth(true));

            // GUILayout.Label(statusText, GUILayout.Width(200));
            
            var statusText = _service.GetStatusDisplayText(item);
            EditorGUILayout.LabelField(statusText, _richLabelStyle,GUILayout.Width(220));

// Open scene button
            if (GUILayout.Button("Open", GUILayout.Width(70)))
            {
                var scenePath = item.ScenePath;

                if (string.IsNullOrEmpty(scenePath))
                {
                    EditorUtility.DisplayDialog(
                        "Open Scene",
                        $"This UGC item '{item.Title}' does not have a valid scene path.",
                        "OK");
                }
                else if (!System.IO.File.Exists(scenePath))
                {
                    // Scene path is set but file is missing: offer retarget flow
                    bool retarget = EditorUtility.DisplayDialog(
                        "Open Scene",
                        $"Scene file not found:\n{scenePath}\n\n" +
                        "This scene might have been moved or deleted.\n" +
                        "Do you want to retarget this MOD to another scene?",
                        "Retarget", "Cancel");

                    if (retarget)
                    {
                        RetargetSceneForItem(item);
                    }
                }
                else
                {
                    // If there are unsaved changes, Unity will prompt the user
                    var openedScene = EditorSceneManager.OpenScene(scenePath);
                    if (!openedScene.IsValid())
                    {
                        EditorUtility.DisplayDialog(
                            "Open Scene",
                            $"Failed to open scene:\n{scenePath}",
                            "OK");
                    }
                }
            }

            if (item.Status == UGCItemStatus.Submitted)
            {
                EditorGUI.BeginDisabledGroup(true);
            }
            
            if (GUILayout.Button("Delete", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog(
                        "Delete UGC Item",
                        $"Are you sure you want to delete '{item.Title}'?\nThis will delete both draft and published versions on PlayFab.",
                        "Delete", "Cancel"))
                {
                    _service.DeleteItemOnServer(item, success => { Repaint(); });
                }
            }
            
            if (item.Status == UGCItemStatus.Submitted)
            {
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RetargetSceneForItem(UGCItemData item)
        {
            if (item == null || _service == null)
                return;

            UGCSceneRetargetWindow.Open(_service, item, newPath =>
            {
                if (string.IsNullOrEmpty(newPath))
                    return;

                // Decide if we can edit metadata directly or need a new version
                bool canEdit = UGCVersionHelper.CanEditMetadata(item);

                void OpenNewScene()
                {
                    // Auto-open target scene if it is not currently active
                    var activeScene = EditorSceneManager.GetActiveScene();
                    bool needOpen = !activeScene.IsValid()
                                    || string.IsNullOrEmpty(activeScene.path)
                                    || !string.Equals(activeScene.path, newPath,
                                        System.StringComparison.OrdinalIgnoreCase);

                    if (needOpen)
                    {
                        if (!System.IO.File.Exists(newPath))
                        {
                            EditorUtility.DisplayDialog(
                                "Open Scene",
                                $"Scene file not found after retarget:\n{newPath}",
                                "OK");
                        }
                        else
                        {
                            var openedScene = EditorSceneManager.OpenScene(newPath);
                            if (!openedScene.IsValid())
                            {
                                EditorUtility.DisplayDialog(
                                    "Open Scene",
                                    $"Failed to open scene after retarget:\n{newPath}",
                                    "OK");
                            }
                        }
                    }
                }

                void DoUpdateScene()
                {
                    var args = new UpdateDraftItemArgument();
                    args.ScenePath = newPath;
                    _service.UpdateDraftItemMetadataOnServer(
                        item,
                        args, 
                        success =>
                        {
                            if (success)
                            {
                                Repaint();
                                OpenNewScene();
                            }
                            else
                            {
                                EditorUtility.DisplayDialog(
                                    "Retarget Scene",
                                    "Error: failed to update scene path on server.\nPlease check Console for details.",
                                    "OK");
                            }
                        });
                }

                if (canEdit)
                {
                    // Directly update scene path on current draft
                    DoUpdateScene();
                }
                else
                {
                    // Need to create a new version first, then update
                    string baseVersion = item.DraftVersion ?? item.PublishedVersion;
                    string newVersion = UGCVersionHelper.GetNextVersionString(baseVersion);

                    if (!EditorUtility.DisplayDialog(
                            "Create New Version",
                            $"Current draft version is already published.\n\n" +
                            $"A new draft version will be created:\n" +
                            $"Old: {item.DraftVersion ?? item.PublishedVersion ?? "N/A"}\n" +
                            $"New: {newVersion}\n\nContinue?",
                            "Create & Retarget", "Cancel"))
                    {
                        return;
                    }
                    var args = new UpdateDraftItemArgument();
                    args.DraftVersion = newVersion;
                    args.ScenePath = newPath;
                    _service.UpdateDraftItemMetadataOnServer(
                        item,
                        args,
                        versionSuccess =>
                        {
                            if (!versionSuccess)
                            {
                                EditorUtility.DisplayDialog(
                                    "Create New Version",
                                    "Error: failed to create new draft version.\nPlease check Console for details.",
                                    "OK");
                                return;
                            }
                            
                            Repaint();
                            OpenNewScene();
                        });
                }
            });
        }

        private void EnsureService()
        {
            if (_service == null)
            {
                _service = new UGCService();
            }
        }
    }
}