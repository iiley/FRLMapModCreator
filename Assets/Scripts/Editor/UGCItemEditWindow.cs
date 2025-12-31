// filepath: Assets/Scripts/Editor/UGCItemEditWindow.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace FRLMapMod.Editor
{
    /// <summary>
    /// Editor window for editing a single UGC map item.
    /// Edits basic metadata (Name, Scene Path, Description) and draft version.
    /// Persists changes via PlayFab Economy APIs through UGCService.
    /// </summary>
    public class UGCItemEditWindow : EditorWindow
    {
        private UGCService _service;
        private UGCItemData _item;

        private static string s_startDirectory;
        // Local editable copies
        private string _displayName;
        private string _scenePath;
        private string _description;
        private string _draftVersion;
        private string _publishedVersion;
        private string _statusText;

        // New version input field
        private string _newDraftVersionInput = string.Empty;
        
        // Tags
        private static readonly string[] s_availableTags = new[]
        {
            "Mini","Medium","Large","City","Touge","Park","Circuit","Harbor","Industrial","Fantasy"
        };

        private readonly List<string> _selectedTags = new List<string>();

        // Thumbnail preview
        private Texture2D _thumbnailTexture;
        private string _thumbnailPath;
        private string _thumbnailErrorMessage;
        private bool _isThumbnailLoading;
        private WebClient _thumbnailWebClient;
        private string _thumbnailLoadingUrl;

        private const int THUMBNAIL_WIDTH = 480;
        private const int THUMBNAIL_HEIGHT = 270;
        private const long THUMBNAIL_MAX_BYTES = 100 * 1024; // 200 KB

        // Screenshots (up to 5)
        private const int SCREENSHOT_WIDTH = 1920;
        private const int SCREENSHOT_HEIGHT = 1080;
        private const long SCREENSHOT_MAX_BYTES = 500 * 1024; // 500 KB
        private const int SCREENSHOT_MAX_COUNT = 5;
        
        private const int MAX_TITLE_LENGTH = 20;
        private const int MAX_DESC_LENGTH = 100;
        

        private readonly Texture2D[] _screenshotTextures = new Texture2D[SCREENSHOT_MAX_COUNT];
        private readonly string[] _screenshotUrls = new string[SCREENSHOT_MAX_COUNT];
        private readonly bool[] _screenshotLoading = new bool[SCREENSHOT_MAX_COUNT];
        private bool _isUploading = false;
        private bool _isDestroyed = false;
        private GUIStyle _richLabelStyle;

        /// <summary>
        /// Opens the edit window for a given UGC item.
        /// </summary>
        internal static void Open(UGCService service, UGCItemData item)
        {
            if (service == null || item == null)
            {
                Debug.LogWarning("[UGCItemEditWindow] Cannot open: service or item is null.");
                return;
            }

            var window = GetWindow<UGCItemEditWindow>("Edit UGC Map Item");
            window._service = service;
            window._item = item;
            window.InitializeFromItem();
            window.Show();
            window.Focus();
            window.InitLoad();
        }
        
        internal static void CloseWindow()
        {
            var window = GetWindow<UGCItemEditWindow>("Edit UGC Map Item");
            if (window != null)
            {
                window.Close();
            }
        }
        
        private void InitializeFromItem()
        {
            if (_item == null)
                return;
            _isUploading = false;
            if(null == s_startDirectory) 
            {
                s_startDirectory = Application.dataPath;
            }
            _displayName = _item.Title;
            _scenePath = _item.ScenePath;
            _draftVersion = _item.DraftVersion ?? "N/A";
            _publishedVersion = _item.PublishedVersion ?? "N/A";
            _statusText = _service != null ? _service.GetStatusDisplayText(_item) : string.Empty;

            _description = _item.Description ?? string.Empty;

            // Suggest next version as default input if current draft version is valid
            if (!string.IsNullOrEmpty(_item.DraftVersion) &&
                _item.DraftVersion != "N/A" &&
                UGCVersionHelper.TryParseVersion(_item.DraftVersion, out var major, out var minor))
            {
                minor += 1;
                _newDraftVersionInput = $"{major}.{minor}";
            }
            else
            {
                _newDraftVersionInput = "0.1";
            }

            _selectedTags.Clear();
            if (_item.Tags != null && _item.Tags.Count > 0)
            {
                foreach (var t in _item.Tags)
                {
                    if (Array.IndexOf(s_availableTags, t) >= 0 && !_selectedTags.Contains(t))
                    {
                        _selectedTags.Add(t);
                    }
                }
            }

            // Cancel previous thumbnail download if any
            if (_thumbnailWebClient != null)
            {
                try
                {
                    _thumbnailWebClient.CancelAsync();
                    _thumbnailWebClient.Dispose();
                }
                catch { }
                _thumbnailWebClient = null;
                _thumbnailLoadingUrl = null;
            }

            // Initialize thumbnail from server if available
            if (_thumbnailTexture != null)
            {
                DestroyImmediate(_thumbnailTexture);
                _thumbnailTexture = null;
            }
            _thumbnailPath = null;
            _thumbnailErrorMessage = null;
            _isThumbnailLoading = false;

        }

        private void InitLoad()
        {
            string remoteUrl = _item.ThumbnailUrl;
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                StartLoadThumbnailFromUrl(remoteUrl);
            }

            // Initialize screenshots from server if available
            ReloadScreenshots();
        }

        private void OnGUI()
        {
            _richLabelStyle ??= new GUIStyle(EditorStyles.label)
            {
                richText = true
            };
            
            if (_service == null || _item == null)
            {
                EditorGUILayout.HelpBox(
                    "No UGC item is selected. Please open this window from UGC Map Manager.",
                    MessageType.Info);
                if (GUILayout.Button("Close"))
                {
                    Close();
                }

                return;
            }

            EditorGUILayout.LabelField("UGC Map Item", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawReadonlyInfo();
            EditorGUILayout.Space();
            DrawEditableFields();
            EditorGUILayout.Space();
        }

        private void DrawReadonlyInfo()
        {
            EditorGUILayout.LabelField("Item Id", _item.ItemId);
            EditorGUILayout.LabelField("Status", _statusText, _richLabelStyle);
        }

        private void DrawEditableFields()
        {
            EditorGUILayout.LabelField("Editable Fields", EditorStyles.boldLabel);

            bool canEdit = CanEditMetadata();

            EditorGUI.BeginDisabledGroup(!canEdit);
            _displayName = EditorGUILayout.TextField("Name", _displayName);
            _displayName = _displayName.Trim();
            if (_displayName.Length > MAX_TITLE_LENGTH)
            {
                _displayName = _displayName.Substring(0, MAX_TITLE_LENGTH);
                GUIUtility.keyboardControl = 0;
            }

            // Scene Path: read-only + Retarget
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scene Path", GUILayout.Width(100));
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(_scenePath) ? "<None>" : _scenePath,
                EditorStyles.textField,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button("Retarget", GUILayout.Width(80)))
            {
                OpenSceneRetargetWindow();
            }
            EditorGUILayout.EndHorizontal();

            // Thumbnail preview and picker
            DrawThumbnailSection(canEdit);

            // Screenshot gallery below thumbnail
            DrawScreenshotSection(canEdit);

            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.Height(60));
            if (_description.Length > MAX_DESC_LENGTH)
            {
                _description = _description.Substring(0, MAX_DESC_LENGTH);
                GUIUtility.keyboardControl = 0;
            }
            EditorGUILayout.Space();

            // Tags section (also controlled by canEdit)
            DrawTagsSection(canEdit);

            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            
            var disableSave = !canEdit;
            if (canEdit)
            {
                disableSave = !IsItemEdited();
            }
            EditorGUI.BeginDisabledGroup(disableSave);
            if (GUILayout.Button("Save", GUILayout.Width(150), GUILayout.Height(30)))
            {
                SaveItemChanges();
            }
            if(!disableSave)
            {
                EditorGUILayout.LabelField("For Title/Description/ScenePath/Tags changes, click Save to apply.", EditorStyles.miniLabel);
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            DrawBundleSection(canEdit);
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Version Control", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Current Draft Version: {_draftVersion}");
            EditorGUILayout.LabelField($"Current Published Version: {_publishedVersion}");

            _newDraftVersionInput = EditorGUILayout.TextField("New Draft Version", _newDraftVersionInput);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Create Version {_newDraftVersionInput}", GUILayout.Width(150), GUILayout.Height(30)))
            {
                CreateNewVersion();
            }
            
            // 如果底层版本检查不允许编辑元数据，则在按钮右侧显示绿色提示
            if (_item != null && !UGCVersionHelper.CanEditMetadata(_item))
            {
                GUILayout.Space(10);
                if (_item.Status == UGCItemStatus.Rejected)
                {
                    var readStyle = new GUIStyle(EditorStyles.label);
                    readStyle.normal.textColor = Color.red;
                    // 垂直居中对齐文本
                    readStyle.alignment = TextAnchor.MiddleLeft;
                    EditorGUILayout.LabelField(
                        $"Need to create new version because the old version is rejected of reason \"{_item.RejectReason}\".",
                        readStyle,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(30));
                }
                else
                {
                    var greenStyle = new GUIStyle(EditorStyles.label);
                    greenStyle.normal.textColor = Color.green;
                    // 垂直居中对齐文本
                    greenStyle.alignment = TextAnchor.MiddleLeft;
                    // 指定与按钮相同的高度以实现垂直居中显示
                    EditorGUILayout.LabelField(
                        $"Need to create new version because v{_publishedVersion} is already published if you want to edit it.",
                        greenStyle,
                        GUILayout.ExpandWidth(true),
                        GUILayout.Height(30));
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OpenSceneRetargetWindow()
        {
            if (_service == null || _item == null)
                return;

            UGCSceneRetargetWindow.Open(_service, _item, newPath =>
            {
                // Only change local editable value; Save will persist via UpdateDraftItemMetadataOnServer
                _scenePath = newPath;
                Repaint();
            });
        }

        private void DrawBundleSection(bool canEdit)
        {
            EditorGUILayout.LabelField("Track Bundle Files", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(!canEdit);
            var rich = new GUIStyle(EditorStyles.label);
            rich.richText = true;
            EditorGUILayout.LabelField(_item.BundleDetails, rich,GUILayout.Width(300), GUILayout.Height(60));
            
            if (GUILayout.Button("Build & Upload", GUILayout.Width(150), GUILayout.Height(30)))
            {
                BuildAndUploadBundle();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawTagsSection(bool canEdit)
        {
            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Select up to 3 tags:", GUILayout.Width(160));
            EditorGUILayout.LabelField(
                $"{_selectedTags.Count}/3 selected",
                EditorStyles.miniLabel,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            // Wrap-style horizontal layout inside a help box
            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            float viewWidth = rect.width > 0f ? rect.width : EditorGUIUtility.currentViewWidth;
            float x = 0f;
            float y = 0f;
            float lineHeight = EditorGUIUtility.singleLineHeight + 4f;
            float spacing = 4f;

            // We'll manually place toggles using GUILayoutUtility.GetRect for better wrapping
            for (int i = 0; i < s_availableTags.Length; i++)
            {
                string tag = s_availableTags[i];
                bool isSelected = _selectedTags.Contains(tag);

                GUIContent content = new GUIContent(tag);
                Vector2 size = EditorStyles.miniButton.CalcSize(content);
                float width = size.x + 12f;

                if (x + width > viewWidth - 20f) // wrap to next line
                {
                    x = 0f;
                    y += lineHeight;
                }

                Rect toggleRect = new Rect(rect.x + x + 4f, rect.y + y + 4f, width, lineHeight - 4f);

                EditorGUI.BeginDisabledGroup(!canEdit);

                bool clicked = GUI.Toggle(
                    toggleRect,
                    isSelected,
                    content,
                    EditorStyles.miniButton);

                EditorGUI.EndDisabledGroup();

                if (canEdit && clicked != isSelected)
                {
                    if (clicked)
                    {
                        if (_selectedTags.Count >= 3)
                        {
                            EditorUtility.DisplayDialog(
                                "Tags",
                                "You can select at most 3 tags.",
                                "OK");
                        }
                        else
                        {
                            _selectedTags.Add(tag);
                        }
                    }
                    else
                    {
                        _selectedTags.Remove(tag);
                    }
                }

                x += width + spacing;
            }

            // Add some space to accommodate the last line
            GUILayout.Space(y + lineHeight + 4f);

            EditorGUILayout.EndVertical();
        }
        
        private void CreateNewVersion()
        {
            if (_item == null || _service == null)
                return;

            if (!_item.HasDraft)
            {
                EditorUtility.DisplayDialog(
                    "Create New Version",
                    "This item has no draft version and cannot create a new version.",
                    "OK");
                return;
            }

            var currentVersion = _item.DraftVersion;
            var newVersion = _newDraftVersionInput;

            // Basic empty check
            if (string.IsNullOrEmpty(newVersion))
            {
                EditorUtility.DisplayDialog(
                    "Create New Version",
                    "New version cannot be empty.",
                    "OK");
                return;
            }

            // Validate new version format
            if (!UGCVersionHelper.TryParseVersion(newVersion, out var newMajor, out var newMinor))
            {
                EditorUtility.DisplayDialog(
                    "Create New Version",
                    "Invalid version format.\n\nIt must be in 'major.minor' format, " +
                    "with each part a non-negative integer up to 3 digits (e.g. 0.1, 1.0, 10.25).",
                    "OK");
                return;
            }

            // If old version is parseable, enforce newVersion > oldVersion
            // If old version is not parseable, we only check new format (no size comparison).
            var canCompareOld = UGCVersionHelper.TryParseVersion(currentVersion, out var oldMajor, out var oldMinor);
            if (canCompareOld && !UGCVersionHelper.IsNewVersionGreater(oldMajor, oldMinor, newMajor, newMinor))
            {
                EditorUtility.DisplayDialog(
                    "Create New Version",
                    $"New version '{newVersion}' must be greater than current version '{currentVersion}'.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Create New Version",
                    $"Create new draft version?\n\nCurrent: {currentVersion ?? "N/A"}\nNew: {newVersion}",
                    "Create", "Cancel"))
            {
                return;
            }

            EditorUtility.DisplayProgressBar(
                "Creating New Version",
                $"Updating draft version for '{_displayName}' to {newVersion}...",
                0.5f);

            _service.CreateVersionOnServer(
                _item,
                newVersion,
                success =>
                {
                    EditorUtility.ClearProgressBar();

                    if (success)
                    {
                        _draftVersion = _item.DraftVersion ?? "N/A";
                        _statusText = _service.GetStatusDisplayText(_item);

                        var managerWindow = EditorWindow.GetWindow<UGCManagerWindow>(false, null, false);
                        if (managerWindow != null)
                        {
                            managerWindow.Repaint();
                        }

                        EditorUtility.DisplayDialog(
                            "Create New Version",
                            $"New draft version created: {newVersion}",
                            "OK");

                        // Update suggested input for next time
                        _newDraftVersionInput = newVersion;
                        Repaint();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "Create New Version",
                            "Error: failed to create new version.\nPlease check Console for details.",
                            "OK");
                    }
                });
        }

        private void DrawThumbnailSection(bool canEdit)
        {
            EditorGUILayout.LabelField("Thumbnail", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Thumbnail preview area
            float previewWidth = 160f;
            float previewHeight = previewWidth * THUMBNAIL_HEIGHT / (float)THUMBNAIL_WIDTH;

            GUILayout.BeginVertical(GUILayout.Width(previewWidth));
            Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));

            if (_isThumbnailLoading)
            {
                EditorGUI.LabelField(previewRect, "Loading...", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                });
            }
            else if (_thumbnailTexture != null)
            {
                GUI.DrawTexture(previewRect, _thumbnailTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                EditorGUI.LabelField(previewRect, "No Thumbnail", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                });
            }

            GUILayout.EndVertical();

            GUILayout.Space(10f);

            // Right side info and button
            GUILayout.BeginVertical();

            EditorGUILayout.LabelField($"Required Size: {THUMBNAIL_WIDTH} x {THUMBNAIL_HEIGHT}");
            EditorGUILayout.LabelField($"Max File Size: {THUMBNAIL_MAX_BYTES / 1024} KB");

            if (!string.IsNullOrEmpty(_thumbnailPath))
            {
                EditorGUILayout.LabelField("Current File:");
                EditorGUILayout.LabelField(_thumbnailPath, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrEmpty(_thumbnailErrorMessage))
            {
                EditorGUILayout.HelpBox(_thumbnailErrorMessage, MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(!canEdit || _isThumbnailLoading);
            if (GUILayout.Button("Change Thumbnail (JPG)...", GUILayout.Width(220)))
            {
                BrowseAndUploadThumbnail();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawScreenshotSection(bool canEdit)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Screenshots", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Up to {SCREENSHOT_MAX_COUNT} JPG images, {SCREENSHOT_WIDTH}x{SCREENSHOT_HEIGHT}, <= {SCREENSHOT_MAX_BYTES / 1024} KB each.",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();

            float slotWidth = 100f;
            float slotHeight = slotWidth * SCREENSHOT_HEIGHT / (float)SCREENSHOT_WIDTH;

            for (int i = 0; i < SCREENSHOT_MAX_COUNT; i++)
            {
                GUILayout.BeginVertical(GUILayout.Width(slotWidth + 10f));

                Rect rect = GUILayoutUtility.GetRect(slotWidth, slotHeight);
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

                if (_screenshotLoading[i])
                {
                    EditorGUI.LabelField(rect, "Loading...", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    });
                }
                else
                {
                    var tex = _screenshotTextures[i];
                    if (tex != null)
                    {
                        GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
                    }
                    else
                    {
                        EditorGUI.LabelField(rect, "Empty", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter
                        });
                    }
                }

                EditorGUILayout.Space(2f);

                EditorGUI.BeginDisabledGroup(!canEdit || _isThumbnailLoading || _screenshotLoading[i]);
                if (GUILayout.Button("Change", GUILayout.Width(slotWidth)))
                {
                    BrowseAndUploadScreenshot(i);
                }      
                if (GUILayout.Button("Delete", GUILayout.Width(slotWidth)))
                {
                    DeleteScreenshotAtIndex(i);
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void StartLoadScreenshotTexture(int index, string url)
        {
            if (index < 0 || index >= SCREENSHOT_MAX_COUNT)
                return;
            if (string.IsNullOrEmpty(url))
                return;

            _screenshotLoading[index] = true;

            try
            {
                var client = new WebClient();
                client.DownloadDataCompleted += (sender, args) =>
                {
                    _screenshotLoading[index] = false;

                    if (_isDestroyed)
                    {
                        try { client.Dispose(); } catch { }
                        return;
                    }

                    if (args.Cancelled || args.Error != null)
                    {
                        try { client.Dispose(); } catch { }
                        Repaint();
                        return;
                    }
                    
                    // Ignore callbacks for stale URLs (e.g. previous item) 
                    if (!string.Equals(_screenshotUrls[index], url, StringComparison.OrdinalIgnoreCase))
                    {
                        try { client.Dispose(); } catch { }
                        Repaint();
                        return;
                    }

                    var data = args.Result;
                    if (data == null || data.Length == 0)
                    {
                        try { client.Dispose(); } catch { }
                        Repaint();
                        return;
                    }

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(data))
                    {
                        if (_screenshotTextures[index] != null)
                        {
                            DestroyImmediate(_screenshotTextures[index]);
                        }
                        _screenshotTextures[index] = tex;
                    }
                    else
                    {
                        DestroyImmediate(tex);
                    }

                    Repaint();
                    try { client.Dispose(); } catch { }
                };

                client.DownloadDataAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                _screenshotLoading[index] = false;
                Debug.LogWarning($"[UGCItemEditWindow] Failed to start screenshot download: {ex.Message}");
            }
        }

        private void BrowseAndUploadThumbnail()
        {
            _thumbnailErrorMessage = null;

            string startDir = !string.IsNullOrEmpty(_thumbnailPath)
                ? System.IO.Path.GetDirectoryName(_thumbnailPath)
                : s_startDirectory;

            string filePath = EditorUtility.OpenFilePanel("Select Thumbnail JPG", startDir, "jpg");
            if (string.IsNullOrEmpty(filePath))
                return;

            if (!System.IO.File.Exists(filePath))
            {
                _thumbnailErrorMessage = "Selected file does not exist.";
                return;
            }

            System.IO.FileInfo fi = new System.IO.FileInfo(filePath);
            if (fi.Length > THUMBNAIL_MAX_BYTES)
            {
                _thumbnailErrorMessage =
                    $"File is too large. Max allowed size is {THUMBNAIL_MAX_BYTES / 1024} KB.";
                return;
            }

            s_startDirectory = Path.GetDirectoryName(filePath);

            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                _thumbnailErrorMessage = $"Failed to read file: {ex.Message}";
                return;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                _thumbnailErrorMessage = "Failed to load image data from file.";
                DestroyImmediate(tex);
                return;
            }

            if (tex.width != THUMBNAIL_WIDTH || tex.height != THUMBNAIL_HEIGHT)
            {
                _thumbnailErrorMessage =
                    $"Invalid dimensions. Required {THUMBNAIL_WIDTH}x{THUMBNAIL_HEIGHT}, got {tex.width}x{tex.height}.";
                DestroyImmediate(tex);
                return;
            }

            // At this point the new texture is valid and passes client-side checks.
            // We now upload it immediately. Keep a reference to old texture in case upload fails.
            var oldTexture = _thumbnailTexture;
            var oldPath = _thumbnailPath;

            EditorUtility.DisplayProgressBar(
                "Uploading Thumbnail",
                "Uploading selected thumbnail to server...",
                0.5f);

            _isUploading = true;
            _service.UploadThumbnailForItem(_item, filePath, success =>
            {
                _isUploading = false;
                EditorUtility.ClearProgressBar();

                if (success)
                {
                    // Replace previous thumbnail with the newly loaded texture
                    if (oldTexture != null)
                    {
                        DestroyImmediate(oldTexture);
                    }

                    _thumbnailTexture = tex;
                    _thumbnailPath = filePath;
                    _thumbnailErrorMessage = null;
                }
                else
                {
                    // Upload failed: keep previous thumbnail and discard new texture
                    DestroyImmediate(tex);
                    _thumbnailTexture = oldTexture;
                    _thumbnailPath = oldPath;
                    _thumbnailErrorMessage = "Failed to upload thumbnail. Please check Console for details.";
                }

                Repaint();
            });
        }

        private void DeleteScreenshotAtIndex(int index)
        {
            
            if (index < 0 || index >= SCREENSHOT_MAX_COUNT)
                return;
            EditorUtility.DisplayProgressBar(
                "Deleting Screenshot",
                "Deleting selected screenshot to server...",
                0.5f);
            _service.DeleteScreenShotForItem(_item, index, success =>
            {
                EditorUtility.ClearProgressBar();
                if(success) ReloadScreenshots();
            });
        }

        private void BuildAndUploadBundle()
        {
            EditorUtility.DisplayProgressBar(
                "Build & Upload Bundle",
                "Preparing Build...",
                0f);
            _service.BuildAndUploadBundle(_item, success =>
            {
                EditorUtility.ClearProgressBar();
                Close();
            });
        }

        private void BrowseAndUploadScreenshot(int index)
        {
            if (index < 0 || index >= SCREENSHOT_MAX_COUNT)
                return;

            // Compute the target slot: first empty slot before or at the clicked index
            int targetIndex = index;
            for (int i = 0; i < index; i++)
            {
                if (string.IsNullOrEmpty(_screenshotUrls[i]))
                {
                    targetIndex = i;
                    break;
                }
            }

            string startDir = s_startDirectory;
            string filePath = EditorUtility.OpenFilePanel("Select Screenshot JPG", startDir, "jpg");
            if (string.IsNullOrEmpty(filePath))
                return;

            if (!System.IO.File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("Screenshot", "Selected file does not exist.", "OK");
                return;
            }
            
            s_startDirectory = Path.GetDirectoryName(filePath);
            System.IO.FileInfo fi = new System.IO.FileInfo(filePath);
            if (fi.Length > SCREENSHOT_MAX_BYTES)
            {
                EditorUtility.DisplayDialog(
                    "Screenshot",
                    $"File is too large. Max allowed size is {SCREENSHOT_MAX_BYTES / 1024} KB.",
                    "OK");
                return;
            }

            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Screenshot", $"Failed to read file: {ex.Message}", "OK");
                return;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                EditorUtility.DisplayDialog("Screenshot", "Failed to load image data from file.", "OK");
                DestroyImmediate(tex);
                return;
            }

            if (tex.width != SCREENSHOT_WIDTH || tex.height != SCREENSHOT_HEIGHT)
            {
                EditorUtility.DisplayDialog(
                    "Screenshot",
                    $"Invalid dimensions. Required {SCREENSHOT_WIDTH}x{SCREENSHOT_HEIGHT}, got {tex.width}x{tex.height}.",
                    "OK");
                DestroyImmediate(tex);
                return;
            }

            _isUploading = true;

            EditorUtility.DisplayProgressBar(
                "Uploading Screenshot",
                "Uploading selected screenshot to server...",
                0.5f);

            _service.UploadScreenshotForItem(_item, filePath, targetIndex, success =>
            {
                EditorUtility.ClearProgressBar();
                _isUploading = false;

                if (success)
                {
                    // Refresh screenshot URLs from updated catalog item and reload textures
                    ReloadScreenshots();
                }
                else
                {
                    DestroyImmediate(tex);
                    EditorUtility.DisplayDialog(
                        "Screenshot",
                        "Failed to upload screenshot. Please check Console for details.",
                        "OK");
                }
            });
        }

        private void ReloadScreenshots()
        {
            var urls = _item.ScreenshotUrls;

            // Clear existing textures and loading flags
            for (int i = 0; i < SCREENSHOT_MAX_COUNT; i++)
            {
                if (_screenshotTextures[i] != null)
                {
                    DestroyImmediate(_screenshotTextures[i]);
                    _screenshotTextures[i] = null;
                }
                _screenshotUrls[i] = null;
                _screenshotLoading[i] = false;
            }

            // Fill URLs compactly from server data
            for (int i = 0; i < SCREENSHOT_MAX_COUNT && i < urls.Count; i++)
            {
                _screenshotUrls[i] = urls[i];
                StartLoadScreenshotTexture(i, urls[i]);
            }

            Repaint();
        }

        void OnDestroy()
        {
            _isDestroyed = true;
            if (_thumbnailTexture)
            {
                DestroyImmediate(_thumbnailTexture);
                _thumbnailTexture = null;
                _thumbnailLoadingUrl = null;
            }

            if (_screenshotTextures != null)
            {
                for (var i=0; i<_screenshotTextures.Length; i++)
                {
                   DestroyImmediate(_screenshotTextures[i]);
                   _screenshotUrls[i] = null;
                   _screenshotTextures[i] = null;
                }
            }
        }

        // Async server thumbnail loading
        private void StartLoadThumbnailFromUrl(string url)
        {
            // Debug.LogError($"Thumbnail URL: {url}");
            if (string.IsNullOrEmpty(url))
                return;

            // Cancel any previous download
            if (_thumbnailWebClient != null)
            {
                try
                {
                    _thumbnailWebClient.CancelAsync();
                    _thumbnailWebClient.Dispose();
                }
                catch
                {
                    // ignore
                }
                _thumbnailWebClient = null;
                _thumbnailLoadingUrl = null;
            }

            _isThumbnailLoading = true;
            _thumbnailErrorMessage = null;
            _thumbnailLoadingUrl = url;

            try
            {
                var client = new WebClient();
                _thumbnailWebClient = client;

                client.DownloadDataCompleted += (sender, args) =>
                {
                    // Window might have been closed; if so, just dispose client and exit
                    if (_isDestroyed)
                    {
                        try { client.Dispose(); } catch { }
                        return;
                    }

                    _isThumbnailLoading = false;

                    // Ignore callbacks for stale URLs (e.g. previous item) 
                    if (!string.Equals(_thumbnailLoadingUrl, url, StringComparison.OrdinalIgnoreCase))
                    {
                        try { client.Dispose(); } catch { }
                        Repaint();
                        return;
                    }

                    _thumbnailWebClient = null;
                    _thumbnailLoadingUrl = null;

                    if (args.Cancelled || args.Error != null)
                    {
                        if (args.Error != null)
                        {
                            Debug.LogWarning($"[UGCItemEditWindow] Failed to load thumbnail from URL: {args.Error.Message}");
                        }
                        Repaint();
                        try { client.Dispose(); } catch { }
                        return;
                    }

                    byte[] data = args.Result;
                    if (data == null || data.Length == 0)
                    {
                        Repaint();
                        try { client.Dispose(); } catch { }
                        return;
                    }

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(data))
                    {
                        if (_thumbnailTexture != null)
                        {
                            DestroyImmediate(_thumbnailTexture);
                        }

                        _thumbnailTexture = tex;
                        _thumbnailPath = null; // remote-only
                        _thumbnailErrorMessage = null;
                    }
                    else
                    {
                        DestroyImmediate(tex);
                    }

                    Repaint();
                    try { client.Dispose(); } catch { }
                };

                client.DownloadDataAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                _isThumbnailLoading = false;
                _thumbnailWebClient = null;
                _thumbnailLoadingUrl = null;
                Debug.LogWarning($"[UGCItemEditWindow] Failed to start thumbnail download: {ex.Message}");
            }
        }

        private void SaveItemChanges()
        {
            if (_item == null || _service == null)
                return;

            bool nameChanged = _item.Title != _displayName;
            bool pathChanged = _item.ScenePath != _scenePath;
            bool descChanged = (_item.Description ?? string.Empty) != (_description ?? string.Empty);

            // Compare tags as sets (ignoring order)
            bool tagsChanged = false;
            {
                var original = _item.Tags ?? new List<string>();
                if (original.Count != _selectedTags.Count ||
                    original.Except(_selectedTags).Any() ||
                    _selectedTags.Except(original).Any())
                {
                    tagsChanged = true;
                }
            }

            bool metadataChanged = nameChanged || pathChanged || descChanged || tagsChanged;

            if (!metadataChanged)
            {
                EditorUtility.DisplayDialog(
                    "Save",
                    "No changes to save.",
                    "OK");
                return;
            }

            EditorUtility.DisplayProgressBar(
                "Saving UGC Item",
                $"Saving changes for '{_displayName}'...",
                0.5f);

            var args = new UpdateDraftItemArgument();
            if (nameChanged)
                args.Title = _displayName;
            if (pathChanged)
                args.ScenePath = _scenePath;
            if (descChanged)
                args.Description = _description;
            if (tagsChanged)
                args.Tags = new List<string>(_selectedTags);

            _service.UpdateDraftItemMetadataOnServer(
                _item,
                args,
                success =>
                {
                    EditorUtility.ClearProgressBar();

                    if (success)
                    {
                        _statusText = _service.GetStatusDisplayText(_item);
                        _draftVersion = _item.DraftVersion ?? "N/A";
                        _publishedVersion = _item.PublishedVersion ?? "N/A";
                        _description = _item.Description ?? string.Empty;

                        var managerWindow = EditorWindow.GetWindow<UGCManagerWindow>(false, null, false);
                        if (managerWindow != null)
                        {
                            managerWindow.Repaint();
                        }

                        EditorUtility.DisplayDialog(
                            "Save",
                            "Save Success!",
                            "OK");

                        Repaint();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "Save",
                            "Error: failed to save changes to server.\nPlease check Console for details.",
                            "OK");
                        Close();
                    }
                });
        }


        private bool CanEditMetadata()
        {
            return UGCVersionHelper.CanEditMetadata(_item) && !_isUploading;
        }

        private bool IsItemEdited()
        {
            if (_item.Title != _displayName) return true;
            if (_item.ScenePath != _scenePath) return true;
            if ((_item.Description ?? string.Empty) != (_description ?? string.Empty)) return true;
            var originalTags = _item.Tags ?? new List<string>();
            if (originalTags.Count != _selectedTags.Count ||
                originalTags.Except(_selectedTags).Any() ||
                _selectedTags.Except(originalTags).Any())
            {
                return true;
            }
            return false;
        }
    }
}
