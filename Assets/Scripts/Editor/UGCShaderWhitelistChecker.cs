#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FRLMapMod.Editor
{
    public static class UGCShaderWhitelistChecker
    {

        public static bool EnforceShaderWhitelist(string scenePath)
        {
            var violations = new List<string>();

            foreach (var dep in AssetDatabase.GetDependencies(scenePath, true))
            {
                if (!dep.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    continue;

                // 加载 Shader 以验证名字是否在白名单
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(dep);
                var shaderName = shader ? shader.name : "(missing)";

                if (!AllowedShaders.IsAllowed(shaderName))
                {
                    violations.Add(
                        $"Root  : {scenePath}\n" +
                        $"Shader: {shaderName}\n" +
                        $"Path  : {dep}\n");
                }
            }

            if (violations.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("[UGC Shader Whitelist] ❌ Found non-supported Shader，Build aborted. Details:");
                foreach (var v in violations)
                {
                    sb.AppendLine("----");
                    sb.AppendLine(v);
                }

                Debug.LogError(sb.ToString());
                return false;
            }

            Debug.Log("[UGC Shader Whitelist] ✅ Pre-Build Shaders pass.");
            return true;
        }
    }
}
#endif
