using System;
using System.Collections.Generic;
using PlayFab.EconomyModels;

namespace FRLMapMod.Editor
{
    

    internal enum UGCItemStatus
    {
        Draft,
        Submitted,
        Approved,
        Rejected,
    }
    
    internal class UGCItemData
    {
        public CatalogItem catalogItem;
        public string ItemId => catalogItem.Id;

        public string ThumbnailUrl
        {
            get
            {
                if (catalogItem.Images != null)
                {
                    foreach (var img in catalogItem.Images)
                    {
                        if (string.Equals(img.Type, "Thumbnail", StringComparison.OrdinalIgnoreCase))
                        {
                            return img.Url;
                        }
                    }
                }
                return null;
            }
        }
        
        public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);

        public List<string> ScreenshotUrls
        {
            get
            {
                var list = new List<string>();
                if (catalogItem.Images != null)
                {
                    foreach (var img in catalogItem.Images)
                    {
                        if (string.Equals(img.Type, "Screenshot", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(img.Url))
                        {
                            list.Add(img.Url);
                        }
                    }
                }
                return list;
            }
        }
        
        public bool HasScreenshots => Screenshots.Count > 0;
        
        public List<Image> Screenshots
        {
            get
            {
                var list = new List<Image>();
                if (catalogItem.Images != null)
                {
                    foreach (var img in catalogItem.Images)
                    {
                        if (string.Equals(img.Type, "Screenshot", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(img.Url))
                        {
                            list.Add(img);
                        }
                    }
                }
                return list;
            }
        }

        public List<Content> Bundles
        {
            get
            {
                if (catalogItem.Contents != null)
                {
                    var list = new List<Content>();
                    foreach (var content in catalogItem.Contents)
                    {
                        list.Add(content);
                    }
                    return list;
                }

                return null;
            }
        }

        public string Title
        {
            get
            {
                if (catalogItem.Title != null)
                {
                    return catalogItem.Title.TryGetValue(UGCService.NEUTRAL_KEY, out var title) ? title : string.Empty;
                }
                return string.Empty;
            }
        }

        public UGCItemStatus Status
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.STATUS_KEY, out var value) && value != null)
                    {
                        if (int.TryParse(value.ToString(), out var i))
                        {
                            return (UGCItemStatus)i;
                        }
                    }
                }
                return UGCItemStatus.Draft;
            }
            set
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    dp[UGCService.STATUS_KEY] = (int)value;
                }
            }
        }
        
        public string RejectReason
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.REJECT_REASON_KEY, out var value) && value != null)
                    {
                        return value.ToString();
                    }
                }
                return string.Empty;
            }
        }
        
        public string ApprovedVersion
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.APPROVED_VERSION_KEY, out var value) && value != null)
                    {
                        return value.ToString();
                    }
                }
                return string.Empty;
            }
        }

        public string ScenePath
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.SCENE_PATH_KEY, out var value) && value != null)
                    {
                        return value.ToString();
                    }
                }
                return null;
            }
        }

        public DateTime BundleUploadTime
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.BUNDLE_UPLOAD_TIME_KEY, out var value) && value != null)
                    {
                        var v = long.Parse(value.ToString());
                        return DateTimeOffset.FromUnixTimeSeconds(v).LocalDateTime;
                    }
                }
                return DateTime.MinValue;
            }
        }

        public string BundleSizeIos
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.BUNDLE_SIZE_IOS_KEY, out var value) && value != null)
                    {
                        return $"{value:F4}";
                    }
                }
                return "0";
            }
        }
        
        public string BundleSizeAndroid
        {
            get
            {
                if (catalogItem.DisplayProperties is IDictionary<string, object> dp)
                {
                    if (dp.TryGetValue(UGCService.BUNDLE_SIZE_ANDROID_KEY, out var value) && value != null)
                    {
                        return $"{value:F4}";
                    }
                }
                return "0";
            }
        }

        public string BundleDetails
        {
            get
            {
                var bundles = Bundles;
                if (bundles is { Count: > 0 })
                {
                    return $"Last Upload Time: {BundleUploadTime:G}\nios size:{BundleSizeIos}MB\nandroid size:{BundleSizeAndroid}MB";
                }
                return "Bundle: <color=red>Empty!</color>";
            }
        }

        public string Description
        {
            get
            {
                if (catalogItem.Description != null)
                {
                    return catalogItem.Description.TryGetValue(UGCService.NEUTRAL_KEY, out var desc) ? desc : string.Empty;
                }
                return string.Empty;
            }
        }
        
        // Underlying draft/published versions
        public string DraftVersion
        {
            get => catalogItem.DisplayVersion;
            set => catalogItem.DisplayVersion = value;
        }
        
        public string PublishedVersion { get; set; }

        //published item got rejected
        public bool PublishedRejected { get; set; }
        public string PublishedRejectReason { get; set; }

        // Tags from Economy CatalogItem.Tags
        public List<string> Tags => catalogItem.Tags;

        public bool HasDraft => !string.IsNullOrEmpty(DraftVersion);
        public bool HasPublished => !string.IsNullOrEmpty(PublishedVersion);
    }
}