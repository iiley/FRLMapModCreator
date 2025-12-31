// filepath: Assets/Scripts/Editor/UGCService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;
using PlayFab.EconomyModels;
using UnityEngine.Networking;
using CatalogItem = PlayFab.EconomyModels.CatalogItem;
using EntityKey = PlayFab.EconomyModels.EntityKey;

namespace FRLMapMod.Editor
{
    /// <summary>
    /// Service responsible for login state and UGC item data.
    /// Uses real PlayFab Client login + PlayFab Economy Catalog for UGC map items.
    /// </summary>
    internal class UGCService
    {
        // -----------------------------
        // EditorPrefs Keys
        // -----------------------------

        private const string PrefKeyUsername = "UGCManager_Username";
        private const string PrefKeyPassword = "UGCManager_Password";

        // -----------------------------
        // Economy constants
        // -----------------------------

        public const string TRACK_CONTENT_TYPE = "ugc_track";
        public const string NEUTRAL_KEY = "NEUTRAL";
        public const string SCENE_PATH_KEY = "scenePath";
        public const string BUNDLE_UPLOAD_TIME_KEY = "uploadTime";
        public const string BUNDLE_SIZE_IOS_KEY = "sizeIos";
        public const string BUNDLE_SIZE_ANDROID_KEY = "sizeAnd";
        public const string STATUS_KEY = "state";
        public const string REJECT_REASON_KEY = "rejectedReason";
        public const string APPROVED_VERSION_KEY = "approvedVersion";

        // -----------------------------
        // Static shared session (per Unity editor process)
        // -----------------------------

        private static PlayFabAuthenticationContext s_sharedAuthContext;
        private static EntityKey s_sharedEntityKey;
        private static bool s_sharedIsLoggedIn;

        // -----------------------------
        // Public login state
        // -----------------------------

        public string Username { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;

        public bool IsLoggedIn { get; private set; }
        public bool IsLoggingIn { get; private set; }
        public string LastLoginError { get; private set; }

        public PlayFabAuthenticationContext AuthContext { get; private set; }
        public EntityKey entityKey { get; private set; }

        public bool HasValidSession => IsLoggedIn && AuthContext != null && entityKey != null;

        public int trackModMaxCount { get; private set; } = 5;
        public float trackModMaxFileMB { get; private set; } = 10f;

        // -----------------------------
        // UGC items
        // -----------------------------

        private readonly List<UGCItemData> _items = new List<UGCItemData>();
        public IReadOnlyList<UGCItemData> Items => _items;

        // -----------------------------
        // Ctor: restore shared session
        // -----------------------------

        public UGCService()
        {
            if (s_sharedIsLoggedIn && s_sharedAuthContext != null && s_sharedEntityKey != null)
            {
                IsLoggedIn = true;
                AuthContext = s_sharedAuthContext;
                entityKey = s_sharedEntityKey;
            }
        }

        // -----------------------------
        // Credentials persistence
        // -----------------------------

        public void LoadCredentialsFromEditorPrefs()
        {
            Username = EditorPrefs.GetString(PrefKeyUsername, string.Empty);
            Password = EditorPrefs.GetString(PrefKeyPassword, string.Empty);
        }

        public void SaveCredentialsToEditorPrefs()
        {
            EditorPrefs.SetString(PrefKeyUsername, Username ?? string.Empty);
            EditorPrefs.SetString(PrefKeyPassword, Password ?? string.Empty);
        }

        public void SetCredentials(string username, string password)
        {
            Username = username ?? string.Empty;
            Password = password ?? string.Empty;
        }

        // -----------------------------
        // PlayFab login
        // -----------------------------

        /// <summary>
        /// Starts a real PlayFab login using username or email + password.
        /// Calls onCompleted(success) when the request finishes.
        /// </summary>
        public void StartLogin(string user, string pass, bool auto, Action<bool> onCompleted = null)
        {
            LastLoginError = null;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                IsLoggedIn = false;
                if (!auto)
                {
                    LastLoginError = "Username and password cannot be empty.";
                    Debug.LogWarning("[UGCService] Login failed: empty username or password.");
                }

                onCompleted?.Invoke(false);
                return;
            }

            if (IsLoggingIn)
            {
                // Already logging in, ignore repeated calls
                onCompleted?.Invoke(false);
                return;
            }

            IsLoggingIn = true;

            // Cache credentials locally; will be persisted on success
            Username = user;
            Password = pass;

            bool useEmail = IsEmailFormat(user);

            if (useEmail)
            {
                var request = new LoginWithEmailAddressRequest
                {
                    Email = user,
                    Password = pass
                };

                PlayFabClientAPI.LoginWithEmailAddress(
                    request,
                    result => OnLoginSuccess(result, onCompleted),
                    error => OnLoginFailure(error, auto, onCompleted)
                );
            }
            else
            {
                var request = new LoginWithPlayFabRequest
                {
                    Username = user,
                    Password = pass
                };

                PlayFabClientAPI.LoginWithPlayFab(
                    request,
                    result => OnLoginSuccess(result, onCompleted),
                    error => OnLoginFailure(error, auto, onCompleted)
                );
            }
        }

        private bool IsEmailFormat(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            int atIndex = value.IndexOf('@');
            if (atIndex <= 0 || atIndex == value.Length - 1)
                return false;

            int dotIndex = value.IndexOf('.', atIndex);
            if (dotIndex <= atIndex + 1 || dotIndex == value.Length - 1)
                return false;

            return true;
        }

        private void OnLoginSuccess(LoginResult result, Action<bool> onCompleted)
        {
            IsLoggingIn = false;
            IsLoggedIn = true;
            LastLoginError = null;

            AuthContext = result.AuthenticationContext;
            var authEntity = result.EntityToken.Entity;

            entityKey = new EntityKey
            {
                Id = authEntity.Id,
                Type = authEntity.Type
            };

            // Persist credentials for future auto-login
            SaveCredentialsToEditorPrefs();

            // Update shared static session
            s_sharedIsLoggedIn = true;
            s_sharedAuthContext = AuthContext;
            s_sharedEntityKey = entityKey;

            Debug.Log(
                $"[UGCService] PlayFab login succeeded. PlayFabId = {result.PlayFabId}, EntityId = {entityKey.Id}");

            onCompleted?.Invoke(true);
            
            PlayFabClientAPI.GetUserReadOnlyData(
                new GetUserDataRequest
                {
                    Keys = new List<string>{"trackModMaxCount", "trackModMaxFileMB"}
                },
                r =>
                {
                    var data = r.Data;
                    foreach (var kvp in data)
                    {
                        Debug.Log($"Player RO {kvp.Key}: {kvp.Value.Value}");
                        if (kvp.Key == "trackModMaxFileMB")
                        {
                            trackModMaxFileMB = float.TryParse(kvp.Value.Value, out var mb) ? mb : 10f;
                        }else if (kvp.Key == "trackModMaxCount")
                        {
                            trackModMaxCount = int.TryParse(kvp.Value.Value, out var ct) ? ct : 1;
                        }
                    }
                },
                error => Debug.LogError(error.GenerateErrorReport())
            );
        }

        private void OnLoginFailure(PlayFabError error, bool auto, Action<bool> onCompleted)
        {
            IsLoggingIn = false;
            IsLoggedIn = false;

            string message = $"PlayFab login failed: {error.Error} - {error.ErrorMessage}";
            Debug.LogError($"[UGCService] {message}");

            if (!auto)
            {
                LastLoginError = message;
            }

            onCompleted?.Invoke(false);
        }


        public void Logout()
        {
            IsLoggedIn = false;
            IsLoggingIn = false;
            LastLoginError = null;
            AuthContext = null;
            entityKey = null;

            // Clear shared static session
            s_sharedIsLoggedIn = false;
            s_sharedAuthContext = null;
            s_sharedEntityKey = null;

            // Keep username, clear password (better for switching accounts)
            Password = string.Empty;
            EditorPrefs.SetString(PrefKeyPassword, string.Empty);

            _items.Clear();

            Debug.Log("[UGCService] Logged out.");
        }

        // -----------------------------
        // UGC abstraction (Economy-backed)
        // -----------------------------

        /// <summary>
        /// Refreshes the UGC item list from PlayFab Economy for the current entity.
        /// </summary>
        public void RefreshItemsFromServer(Action<bool> onCompleted = null)
        {
            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogWarning("[UGCService] Cannot refresh UGC items: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            var entity = GetEntityKeyOrLogError();
            if (entity == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            _items.Clear();

            var draftById = new Dictionary<string, CatalogItem>();

            var draftRequest = new GetEntityDraftItemsRequest
            {
                Entity = entity,
                Count = 50,
                ContinuationToken = null
            };

            PlayFabEconomyAPI.GetEntityDraftItems(
                draftRequest,
                draftResult =>
                {
                    if (draftResult.Items != null)
                    {
                        foreach (var item in draftResult.Items)
                        {
                            if (!string.Equals(item.ContentType, TRACK_CONTENT_TYPE, StringComparison.OrdinalIgnoreCase))
                                continue;

                            draftById[item.Id] = item;
                        }
                    }

                    if (draftById.Count > 0)
                    {
                        var ids = draftById.Keys.ToList();
                        GetItems(ids, publishedById =>
                        {
                            MergeDraftAndPublished(draftById, publishedById);
                            onCompleted?.Invoke(true);
                        });
                    }
                    else
                    {
                        onCompleted?.Invoke(true);
                    }
                },
                draftError =>
                {
                    Debug.LogError(
                        $"[UGCService] Failed to get draft UGC items: {draftError.Error} - {draftError.ErrorMessage}");
                    onCompleted?.Invoke(false);
                });
        }

        public void GetItems(List<string> ids, Action<Dictionary<string, CatalogItem>> onCompleted)
        {
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest()
            {
                FunctionName = "GetItems", //This should be the name of your Azure Function that you created.
                FunctionParameter = new Dictionary<string, object>()
                {
                    { "Ids", ids}, 
                }, //This is the data that you would want to pass into your function.
                GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
            }, 
            result =>
            {
                if (result.FunctionResultTooLarge ?? false)
                {
                    Debug.LogError("[UGCService] This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                    return;
                }
                try
                {
                    var json = result.FunctionResult.ToString();
                    Debug.Log($"[UGCService] GetItems Result: \n{json}");
                    var ser = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
                    var res = ser.DeserializeObject<GetItemsResponse>(json);
                    var data = new Dictionary<string, CatalogItem>();
                    foreach (var it in res.Items)
                    {
                        data[it.Id] = it;
                    }
                    onCompleted?.Invoke(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UGCService] Parsing error: {ex.Message}");
                    onCompleted?.Invoke(null);
                }
            }, 
            error =>
            {
                Debug.LogError($"[UGCService] Opps Something went wrong: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(null);
            });
        }

        /// <summary>
        /// Creates a new UGC map item (Draft) on PlayFab Economy for the given scene.
        /// </summary>
        public void CreateItemOnServerForScene(string scenePath, string displayName, Action<UGCItemData> onCompleted = null)
        {
            if(_items.Count >= trackModMaxCount)
            {
                EditorUtility.DisplayDialog(
                    "Create Failed",
                    $"Max UGC Items Reached {trackModMaxCount}",
                    "OK");
                onCompleted?.Invoke(null);
                return;
            }
            
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogWarning("[UGCService] Cannot create UGC item: scene path is empty.");
                onCompleted?.Invoke(null);
                return;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            }

            var existing = FindItemByScenePath(scenePath);
            if (existing != null)
            {
                Debug.LogWarning(
                    $"[UGCService] UGC map for this scene already exists (ItemId={existing.ItemId}).");
                onCompleted?.Invoke(null);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogWarning("[UGCService] Cannot create UGC item: not logged in.");
                onCompleted?.Invoke(null);
                return;
            }
            
            
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest()
            {
                FunctionName = "CreateDraftItem", //This should be the name of your Azure Function that you created.
                FunctionParameter = new Dictionary<string, object>()
                {
                    { "Title", displayName}, 
                    { "ScenePath", scenePath}, 
                }, //This is the data that you would want to pass into your function.
                GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
            }, 
            result =>
            {
                if (result.FunctionResultTooLarge ?? false)
                {
                    Debug.LogError("[UGCService] This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                    return;
                }
                try
                {
                    var json = result.FunctionResult.ToString();
                    Debug.Log($"[UGCService] CreateDraftItem Result: \n{json}");
                    var it = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer).DeserializeObject<CatalogItem>(json);
                    if (!string.Equals(it.ContentType, TRACK_CONTENT_TYPE,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning(
                            $"[UGCService] Created item has unexpected ContentType: {it.ContentType}");
                    }
                    
                    var data = MapCatalogItemToUGC(it, null);
                    _items.Add(data);
                    onCompleted?.Invoke(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UGCService] Parsing error: {ex.Message}");
                    onCompleted?.Invoke(null);
                }
            }, 
            error =>
            {
                Debug.LogError($"[UGCService] Opps Something went wrong: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(null);
            });
        }
        
        public void CreateVersionOnServer(UGCItemData item, string newVersion, Action<bool> onCompleted = null)
        {
            if (item == null)
            {
                onCompleted?.Invoke(false);
                return;
            }
            UpdateItemState(item, "NewVersion", newVersion, onCompleted);
        }
        
        public void SubmitItemOnServer(UGCItemData item, Action<bool> onCompleted = null)
        {
            if (!CanSubmit(item))
            {
                Debug.LogError(
                    $"[UGCService] Nothing to publish for ItemId={item.ItemId}: draft is not newer than published.");
                onCompleted?.Invoke(false);
                return;
            }
            UpdateItemState(item, "Submit", null, onCompleted);
        }
        
        public void CancelSubmitItemOnServer(UGCItemData item, Action<bool> onCompleted = null)
        {
            if (item == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (item.Status != UGCItemStatus.Submitted)
            {
                Debug.LogError($"[UGCService] item is not in Submitted status (ItemId={item.ItemId}).");
                onCompleted?.Invoke(false);
            }
            
            UpdateItemState(item, "CancelSubmit", null, onCompleted);
        }

        private void UpdateItemState(UGCItemData item, string op, string newVersion, Action<bool> onCompleted)
        {
            if (item == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (!item.HasDraft)
            {
                Debug.LogError(
                    $"[UGCService] Cannot UpdateItemState: item has no draft version (ItemId={item.ItemId}).");
                onCompleted?.Invoke(false);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogError("[UGCService] Cannot UpdateItemState UGC item: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            var entity = GetEntityKeyOrLogError();
            if (entity == null)
            {
                onCompleted?.Invoke(false);
                return;
            }
            
            var dict = new Dictionary<string, object>
            {
                ["Id"] = item.ItemId,
                ["Op"] = op, 
            };
            if (op == "NewVersion")
            {
                dict["DraftVersion"] = newVersion;
            }
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest()
                {
                    FunctionName = "UpdateItemState", 
                    FunctionParameter = dict, 
                    GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
                }, 
                result =>
                {
                    if (result.FunctionResultTooLarge ?? false)
                    {
                        Debug.LogError("[UGCService] This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                        return;
                    }
                    
                    if(op == "Submit")
                    {
                        item.Status = UGCItemStatus.Submitted;
                    }
                    else if(op == "NewVersion")
                    {
                        item.DraftVersion = newVersion;
                        item.Status = UGCItemStatus.Draft;
                    }else if (op == "CancelSubmit")
                    {
                        item.Status = UGCItemStatus.Draft;
                    }

                    Debug.Log(
                        $"[UGCService] UpdateItemState UGC map item '{item.Title}' (ItemId={item.ItemId}, Status={item.Status}).");

                    onCompleted?.Invoke(true);
                }, 
                error =>
                {
                    Debug.LogError(
                        $"[UGCService] Failed to UpdateItemState UGC map item '{item.ItemId}': {error.Error} - {error.ErrorMessage}");
                    onCompleted?.Invoke(false);
                });
        }

        public void DeleteItemOnServer(UGCItemData item, Action<bool> onCompleted = null)
        {
            if (item == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogError("[UGCService] Cannot delete UGC item: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            var entity = GetEntityKeyOrLogError();
            if (entity == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(item.ItemId))
            {
                Debug.LogError("[UGCService] Cannot delete UGC item: ItemId is empty.");
                onCompleted?.Invoke(false);
                return;
            }

            if (item.HasPublished)
            {
                EditorUtility.DisplayDialog("Delete Failed",
                    $"Cannot delete item that has been published. Please contact info.frlegends@gmail with Creator ID:{entity.Id} and Item ID:{item.ItemId} if you really need deleting.",
                    "OK");
                onCompleted?.Invoke(false);
                return;
            }

            if (item.Status == UGCItemStatus.Submitted)
            {
                EditorUtility.DisplayDialog("Delete Failed",
                    $"Cannot delete item that has been submitted. Please cancel it then delete.",
                    "OK");
                onCompleted?.Invoke(false);
                return;
            }

            var request = new DeleteItemRequest
            {
                AuthenticationContext = AuthContext,
                Entity = entity,
                Id = item.ItemId
            };

            EditorUtility.DisplayProgressBar(
                "Delete Item",
                $"Deleting '{item.Title}'...",
                0.5f);
            PlayFabEconomyAPI.DeleteItem(
                request,
                result =>
                {
                    EditorUtility.ClearProgressBar();
                    _items.Remove(item);
                    Debug.Log($"[UGCService] Deleted UGC map item '{item.Title}' (ItemId={item.ItemId}).");
                    onCompleted?.Invoke(true);
                },
                error =>
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogError(
                        $"[UGCService] Failed to delete UGC map item '{item.ItemId}': {error.Error} - {error.ErrorMessage}");
                    onCompleted?.Invoke(false);
                });
        }

        /// <summary>
        /// Updates basic draft metadata (Name, ScenePath, Description) on server using UpdateDraftItem.
        /// </summary>
        public void UpdateDraftItemMetadataOnServer(
            UGCItemData item,
            UpdateDraftItemArgument args,
            Action<bool> onCompleted = null)
        {
            if (item == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogWarning("[UGCService] Cannot update UGC item: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(item.ItemId))
            {
                Debug.LogWarning("[UGCService] Cannot update UGC item: ItemId is empty.");
                onCompleted?.Invoke(false);
                return;
            }
            
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest()
            {
                FunctionName = "UpdateDraftItem", 
                FunctionParameter = args.ToDictionary(item.ItemId), 
                GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
            }, 
            result =>
            {
                if (result.FunctionResultTooLarge ?? false)
                {
                    Debug.LogError("[UGCService] This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                    return;
                }
                try
                {
                    var json = result.FunctionResult.ToString();
                    Debug.Log($"[UGCService] UpdateDraftItem Result: \n{json}");
                    var it = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer)
                        .DeserializeObject<CatalogItem>(json);

                    item.catalogItem = it;
                    onCompleted?.Invoke(true);
                    
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UGCService] Parsing error: {ex.Message}");
                    onCompleted?.Invoke(false);
                }
            }, 
            error =>
            {
                Debug.LogError($"[UGCService] Opps Something went wrong: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(false);
            });
            
        }

        public void UpdateDraftFiles(UGCItemData item, UpdateDraftFilesArgument args, Action<bool> onCompleted = null)
        {
            if (item == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogWarning("[UGCService] Cannot update UGC item: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(item.ItemId))
            {
                Debug.LogWarning("[UGCService] Cannot update UGC item: ItemId is empty.");
                onCompleted?.Invoke(false);
                return;
            }
            
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest()
            {
                FunctionName = "UpdateDraftFiles", 
                FunctionParameter = args.ToDictionary(item.ItemId), 
                GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
            }, 
            result =>
            {
                if (result.FunctionResultTooLarge ?? false)
                {
                    Debug.LogError("[UGCService] This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                    return;
                }
                try
                {
                    var json = result.FunctionResult.ToString();
                    Debug.Log($"[UGCService] UpdateDraftFiles Result: \n{json}");
                    var it = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer)
                        .DeserializeObject<CatalogItem>(json);

                    item.catalogItem = it;
                    onCompleted?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UGCService] Parsing error: {ex.Message}");
                    onCompleted?.Invoke(false);
                }
            }, 
            error =>
            {
                Debug.LogError($"[UGCService] Opps Something went wrong: {error.GenerateErrorReport()}");
                onCompleted?.Invoke(false);
            });
        }

        public void UploadThumbnailForItem(UGCItemData item, string localFilePath, Action<bool> onCompleted)
        {
            if (item == null)
            {
                Debug.LogWarning("[UGCService] UploadThumbnailForItem failed: item is null.");
                onCompleted?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(localFilePath) || !File.Exists(localFilePath))
            {
                Debug.LogWarning("[UGCService] UploadThumbnailForItem failed: local file does not exist.");
                onCompleted?.Invoke(false);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogWarning("[UGCService] Cannot upload thumbnail: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            var entity = GetEntityKeyOrLogError();
            if (entity == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(localFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UGCService] Failed to read thumbnail file: {ex.Message}");
                onCompleted?.Invoke(false);
                return;
            }

            var createRequest = new CreateUploadUrlsRequest
            {
                AuthenticationContext = AuthContext, 
                Files = new List<UploadInfo>
                {
                    new UploadInfo
                    {
                        FileName = "Thumbnail.jpg",
                    }
                }
            };

            PlayFabEconomyAPI.CreateUploadUrls(
                createRequest,
                response =>
                {
                    if (response.UploadUrls == null || response.UploadUrls.Count == 0)
                    {
                        Debug.LogError("[UGCService] CreateUploadUrls returned no URLs.");
                        onCompleted?.Invoke(false);
                        return;
                    }

                    var uploadInfo = response.UploadUrls[0];
                    
                    // Debug.Log($"file bytes {fileBytes.Length}");
                    bool uploaded = UploadFile(uploadInfo.Url, fileBytes, "image/jpeg", progress =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Uploading Thumbnail",
                            "Uploading selected thumbnail to server...",
                            progress);
                    });
                    if (!uploaded)
                    {
                        onCompleted?.Invoke(false);
                        return;
                    }

                    var filesArg = new UpdateDraftFilesArgument
                    {
                        Thumbnail = new Image
                        {
                            Id = uploadInfo.Id,
                            Url = uploadInfo.Url,
                        }
                    };

                    UpdateDraftFiles(item, filesArg, onCompleted);
                },
                error =>
                {
                    Debug.LogError($"[UGCService] CreateUploadUrls failed: {error.Error} - {error.ErrorMessage}");
                    onCompleted?.Invoke(false);
                });
        }

        public void DeleteScreenShotForItem(UGCItemData item, int index, Action<bool> onCompleted)
        {
            if (item == null)
            {
                Debug.LogWarning("[UGCService] DeleteScreenShotForItem failed: item is null.");
                onCompleted?.Invoke(false);
                return;
            }

            var screenshotImages = item.Screenshots;
            if (index < 0 || index >= screenshotImages.Count)
            {
                Debug.LogWarning("[UGCService] DeleteScreenShotForItem failed: index out of range.");
                onCompleted?.Invoke(false);
                return;
            }

            screenshotImages.RemoveAt(index);

            var filesArg = new UpdateDraftFilesArgument
            {
                Screenshots = screenshotImages
            };

            UpdateDraftFiles(item, filesArg, onCompleted);
        }
        
        public void UploadScreenshotForItem(UGCItemData item, string localFilePath, int index, Action<bool> onCompleted)
        {
            if (item == null)
            {
                Debug.LogWarning("[UGCService] UploadScreenshotsForItem failed: item is null.");
                onCompleted?.Invoke(false);
                return;
            }

            if (localFilePath == null)
            {
                Debug.LogWarning("[UGCService] UploadScreenshotsForItem failed: no files provided.");
                onCompleted?.Invoke(false);
                return;
            }

            if (!IsLoggedIn || AuthContext == null)
            {
                Debug.LogWarning("[UGCService] Cannot upload screenshots: not logged in.");
                onCompleted?.Invoke(false);
                return;
            }

            var entity = GetEntityKeyOrLogError();
            if (entity == null)
            {
                onCompleted?.Invoke(false);
                return;
            }

            // Read all files into memory
            byte[] fileBytes;
            var uploadInfos = new List<UploadInfo>();
            try
            {
                fileBytes = File.ReadAllBytes(localFilePath);

                uploadInfos.Add(new UploadInfo
                {
                    FileName = $"Screenshot_{index + 1}.jpg",
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UGCService] Failed to read screenshot files: {ex.Message}");
                onCompleted?.Invoke(false);
                return;
            }

            var createRequest = new CreateUploadUrlsRequest
            {
                AuthenticationContext = AuthContext,
                Files = uploadInfos
            };

            PlayFabEconomyAPI.CreateUploadUrls(
                createRequest,
                response =>
                {
                    if (response.UploadUrls == null || response.UploadUrls.Count == 0)
                    {
                        Debug.LogError("[UGCService] CreateUploadUrls for screenshots returned no URLs.");
                        onCompleted?.Invoke(false);
                        return;
                    }

                    var screenshotImages = item.Screenshots;

                    var info = response.UploadUrls[0];
                    var bytes = fileBytes;

                    bool uploaded = UploadFile(info.Url, bytes, "image/jpeg", progress =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Uploading Screenshot",
                            "Uploading progress ...",
                            progress);
                    });
                    if (!uploaded)
                    {
                        onCompleted?.Invoke(false);
                        return;
                    }

                    if (index >= screenshotImages.Count)
                    {
                        screenshotImages.Add(new Image
                        {
                            Id = info.Id,
                            Url = info.Url
                        });
                    }
                    else
                    {
                        screenshotImages[index] = new Image
                        {
                            Id = info.Id,
                            Url = info.Url
                        };
                    }


                    var filesArg = new UpdateDraftFilesArgument
                    {
                        Screenshots = screenshotImages
                    };

                    UpdateDraftFiles(item, filesArg, onCompleted);
                },
                error =>
                {
                    Debug.LogError($"[UGCService] CreateUploadUrls for screenshots failed: {error.Error} - {error.ErrorMessage}");
                    onCompleted?.Invoke(false);
                });
        }

        // -----------------------------
        // Economy helpers
        // -----------------------------

        private EntityKey GetEntityKeyOrLogError()
        {
            if (AuthContext == null)
            {
                Debug.LogError(
                    "[UGCService] Cannot call Economy APIs: AuthContext is null. Please login first.");
                return null;
            }
            return entityKey;
        }

        private UGCItemData MapCatalogItemToUGC(CatalogItem draftItem, CatalogItem publishedItem)
        {
            var publishedRejected = false;
            if (null != publishedItem)
            {
                var status = publishedItem.Moderation;
                publishedRejected = status?.Status is ModerationStatus.Rejected;
            }
            var data = new UGCItemData
            {
                catalogItem = draftItem,
                PublishedVersion = publishedItem?.DisplayVersion,
                PublishedRejected = publishedRejected,
                PublishedRejectReason = publishedRejected ? publishedItem.Moderation?.Reason : null,
            };
            return data;
        }

        /// <summary>
        /// Merges Draft and Published items into UGCItemData list.
        /// </summary>
        private void MergeDraftAndPublished(Dictionary<string, CatalogItem> draftById, Dictionary<string, CatalogItem> publishedById)
        {
            _items.Clear();

            if (publishedById == null)
                publishedById = new Dictionary<string, CatalogItem>();

            var allIds = new HashSet<string>(draftById.Keys);
            foreach (var id in publishedById.Keys)
                allIds.Add(id);

            foreach (var id in allIds)
            {
                draftById.TryGetValue(id, out var draft);
                publishedById.TryGetValue(id, out var pub);
                var data = MapCatalogItemToUGC(draft, pub);
                _items.Add(data);
            }

            Debug.Log(
                $"[UGCService] Refreshed {_items.Count} UGC map items from PlayFab Economy (draft+published).");
        }

        // -----------------------------
        // Draft/Published helpers
        // -----------------------------

        private int CompareVersionStrings(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
            if (string.IsNullOrEmpty(a)) return -1;
            if (string.IsNullOrEmpty(b)) return 1;

            var aParts = a.Split('.');
            var bParts = b.Split('.');

            int len = Mathf.Max(aParts.Length, bParts.Length);
            for (int i = 0; i < len; i++)
            {
                int ai = i < aParts.Length && int.TryParse(aParts[i], out var av) ? av : 0;
                int bi = i < bParts.Length && int.TryParse(bParts[i], out var bv) ? bv : 0;

                if (ai < bi) return -1;
                if (ai > bi) return 1;
            }

            return 0;
        }

        public string GetStatusDisplayText(UGCItemData item)
        {
            if (item == null)
                return "Non-Existing";

            bool hasDraft = item.HasDraft;
            bool hasPublished = item.HasPublished;

            if (!hasDraft)
                return "Non-Existing";

            var df = item.DraftVersion;
            if (string.IsNullOrEmpty(df)) df = "?";
            
            var draftStr = $"Draft({df})";
            if (item.Status == UGCItemStatus.Submitted)
            {
                draftStr = UGCVersionHelper.ColorString($"Waiting Review({df})", "blue");
            }else if (item.Status == UGCItemStatus.Rejected)
            {
                draftStr = UGCVersionHelper.RedString($"Rejected({df})", true);
            }else if (item.Status == UGCItemStatus.Approved)
            {
                draftStr = UGCVersionHelper.ColorString($"Approved{df}", "green");
            }

            if (!hasPublished)
            {
                return $"Non-Publish, {draftStr}";
            }

            string pv2 = string.IsNullOrEmpty(item.PublishedVersion) ? "?" : item.PublishedVersion;

            int cmp = CompareVersionStrings(df, item.PublishedVersion);
            var pubStr = UGCVersionHelper.RedString($"Published({pv2})", item.PublishedRejected);
            if (cmp == 0)
            {
                return pubStr;
            }

            if (cmp > 0)
            {
                return $"{pubStr} - {draftStr}";
            }

            return pubStr;
        }

        public bool CanEdit(UGCItemData item)
        {
            if (item == null) return false;
            return item.Status != UGCItemStatus.Submitted;
        }

        public bool CanSubmit(UGCItemData item)
        {
            if (item == null) return false;
            if (!item.HasDraft)
                return false;
            if(item.Status != UGCItemStatus.Draft)
                return false;
            
            int cmp = CompareVersionStrings(item.DraftVersion, item.ApprovedVersion);
            if(cmp <= 0)
                return false; // draft not newer than approved

            return true;
        }
        
        public bool CanCancelSubmit(UGCItemData item)
        {
            if (item == null) return false;
            return item.Status == UGCItemStatus.Submitted;
        }

        public UGCItemData FindItemByScenePath(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return null;

            return _items.Find(i =>
                string.Equals(i.ScenePath, scenePath, StringComparison.OrdinalIgnoreCase));
        }

        public void BuildAndUploadBundle(UGCItemData item, Action<object> onCompleted)
        {
            if(BundleBuilder.Build(item.ScenePath, out var dataIos, out var dataAndroid))
            {
                var iosMb = (dataIos.Length / (1024f * 1024f));
                var andMb = (dataAndroid.Length / (1024f * 1024f));
                if(iosMb > trackModMaxFileMB || andMb > trackModMaxFileMB)
                {
                    EditorUtility.DisplayDialog(
                        "Upload Failed",
                        $"Bundle size exceeds the maximum allowed size of {trackModMaxFileMB:F2} MB.\n\n" +
                        $"iOS Size: {iosMb:F2} MB\n" +
                        $"Android Size: {andMb:F2} MB",
                        "OK");
                    onCompleted?.Invoke(false);
                    return;
                }
                
                var createRequest = new CreateUploadUrlsRequest
                {
                    AuthenticationContext = AuthContext, 
                    Files = new List<UploadInfo>
                    {
                        new()
                        {
                            FileName = "ios.track"
                        }, 
                        new()
                        {
                            FileName = "and.track"
                        }
                    }
                };
                
                PlayFabEconomyAPI.CreateUploadUrls(
                    createRequest,
                    response =>
                    {
                        if (response.UploadUrls == null || response.UploadUrls.Count == 0)
                        {
                            Debug.LogError("[UGCService] CreateUploadUrls for screenshots returned no URLs.");
                            onCompleted?.Invoke(false);
                            return;
                        }
                        
                        var uploadUrls = response.UploadUrls;

                        var iosMeta     = uploadUrls.FirstOrDefault(u => u.FileName == "ios.track");
                        var androidMeta = uploadUrls.FirstOrDefault(u => u.FileName == "and.track");

                        if (iosMeta == null || androidMeta == null)
                        {
                            Debug.LogError("[UGCService] Missing UploadUrlMetadata for ios.track or and.track.");
                            onCompleted?.Invoke(false);
                            return;
                        }

                        // iOS
                        if (!UploadFile(iosMeta.Url, dataIos, "application/octet-stream",p =>
                                EditorUtility.DisplayProgressBar("Build & Upload Bundle", "Uploading iOS bundle…", p)))
                        {
                            onCompleted?.Invoke(false);
                            return;
                        }

                        // Android
                        if (!UploadFile(androidMeta.Url, dataAndroid, "application/octet-stream",  p =>
                                EditorUtility.DisplayProgressBar("Build & Upload Bundle", "Uploading Android bundle…", p)))
                        {
                            onCompleted?.Invoke(false);
                            return;
                        }
                            
                        var filesArg = new UpdateDraftFilesArgument
                        {
                            Tracks = new List<Content>
                            {
                                new()
                                {
                                    Id = iosMeta.Id,
                                    Url = iosMeta.Url,
                                    Type = "track_ios"
                                },
                                new()
                                {
                                    Id = androidMeta.Id,
                                    Url = androidMeta.Url,
                                    Type = "track_and"
                                }
                            },
                            SizeIos = iosMb,
                            SizeAnd = andMb,
                        };
                        EditorUtility.DisplayProgressBar("Build & Upload Bundle", "Updating item...", 1f);
                        UpdateDraftFiles(item, filesArg, ok =>
                        {
                            onCompleted?.Invoke(ok);
                        });
                    },
                    error =>
                    {
                        Debug.LogError($"[UGCService] CreateUploadUrls for bundles failed: {error.Error} - {error.ErrorMessage}");
                        onCompleted?.Invoke(false);
                    });

            }
            else
            {
                onCompleted.Invoke(false);
            }
        }
        
        
        private bool UploadFile(string url, byte[] data, string httpContentType, Action<float> onProgress)
        {
            using var req = UnityWebRequest.Put(url, data);
            req.SetRequestHeader("Content-Type", httpContentType);
            req.SetRequestHeader("x-ms-blob-type", "BlockBlob");
            var op = req.SendWebRequest();
            
            while (!op.isDone)
            {
                onProgress?.Invoke(req.uploadProgress);
                System.Threading.Thread.Sleep(10);
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[UGCService] Upload failed: {req.responseCode} - {req.error}");
                return false;
            }
            Debug.Log($"Upload result: {req.responseCode}, error: {req.error}");
            onProgress?.Invoke(1f);
            return true;
        }
        
    }
    
    internal class UpdateDraftItemArgument
    {
        public string Title;
        public string Description;
        public string ScenePath;
        public List<string> Tags;
        public string DraftVersion;
        
        public Dictionary<string, object> ToDictionary(string id)
        {
            var dict = new Dictionary<string, object>();
            dict["Id"] = id;
            if (Title != null) dict["Title"] = Title;
            if (Description != null) dict["Description"] = Description;
            if (ScenePath != null) dict["ScenePath"] = ScenePath;
            if (Tags != null) dict["Tags"] = Tags;
            if (DraftVersion != null) dict["DraftVersion"] = DraftVersion;
            return dict;
        }
    }
    
    internal class UpdateDraftFilesArgument
    {
        public Image Thumbnail;
        public List<Image> Screenshots;
        public List<Content> Tracks;
        public double? SizeAnd;
        public double? SizeIos;
        public double? SizeWin;

        public Dictionary<string, object> ToDictionary(string id)
        {
            var dict = new Dictionary<string, object>();
            dict["Id"] = id;
            if (Thumbnail != null) dict["Thumbnail"] = Thumbnail;
            if (Screenshots != null) dict["Screenshots"] = Screenshots;
            if (Tracks != null) dict["Tracks"] = Tracks;
            if (SizeAnd != null) dict["SizeAnd"] = SizeAnd;
            if (SizeIos != null) dict["SizeIos"] = SizeIos;
            if (SizeWin != null) dict["SizeWin"] = SizeWin;
            return dict;
        }
    }
}
