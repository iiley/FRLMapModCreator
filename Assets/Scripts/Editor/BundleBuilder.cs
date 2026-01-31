using System;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace FRLMapMod.Editor
{
    public static class BundleBuilder
    {
        private const string TEMP_BUNDLE_PATH = "Temp/TrackBundles";

        public static bool Build(string scenePath, out byte[] dataIos, out byte[] dataAndroid)
        {
            dataIos = null;
            dataAndroid = null;
            
            AssetBundle.UnloadAllAssetBundles(true);


            EditorUtility.DisplayProgressBar(
                "Build & Upload Bundle",
                "Checking shaders...",
                0.1f);

            try
            {
                Directory.CreateDirectory(TEMP_BUNDLE_PATH);
            }
            catch (Exception er)
            {
                EditorUtility.DisplayDialog("Build Error", er.Message, "OK");
                return false;
            }


            EditorUtility.DisplayProgressBar(
                "Build & Upload Bundle",
                "Build for iOS...",
                0.2f);
            dataIos = BuildSingle(BuildTarget.iOS, scenePath);

            EditorUtility.DisplayProgressBar(
                "Build & Upload Bundle",
                "Build for Android...",
                0.3f);
            dataAndroid = BuildSingle(BuildTarget.Android, scenePath);
            return true;
        }

        private static byte[] BuildSingle(BuildTarget platform, string scenePath)
        {
            var build = new AssetBundleBuild
            {
                assetBundleName = $"{platform}.track",
                assetNames = new[] { scenePath }
            };
            
            BuildPipeline.BuildAssetBundles(
                TEMP_BUNDLE_PATH,
                new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression,
                platform // 可根据需要切换 iOS / Android
            );
            var fullPath = Path.Combine(TEMP_BUNDLE_PATH, build.assetBundleName);
            
            if (!File.Exists(fullPath))
            {
                var linuxPath = Path.Combine(TEMP_BUNDLE_PATH, build.assetBundleName.ToLower());
                if (File.Exists(linuxPath))
                {
                    File.Move(linuxPath, linuxPath + ".tmp");
                    File.Move(linuxPath + ".tmp", fullPath);
                }
            }
            // 3️⃣ 读取字节数据
            var data = File.ReadAllBytes(fullPath);
            Debug.Log($"✅ {build.assetBundleName} built, size = {data.Length / 1024f / 1024f:F2} MB");

            return data;
        }
    }
}
