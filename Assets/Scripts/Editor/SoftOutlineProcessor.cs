using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FRLMapMod.Editor
{
    public class SoftOutlineProcessor : AssetPostprocessor
    {
        void OnPostprocessModel(GameObject g)
        {
            var filters = g.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                if (null != mesh && mesh.isReadable)
                {
                    if (f.name == "S13_SPOILER" || f.name == "S14_BODY_SPOILER_2")
                    {
                        DoSoftOutline2(mesh);
                    }
                    else
                    {
                        DoSoftOutline(mesh);
                    }
                }
            }
        }

        private static void DoSoftOutline(Mesh mesh)
        {
            var n = mesh.vertexCount;
            var averageNormalHash = new Dictionary<Vector3, Vector3>();
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            for (var j = 0; j < n; j++)
            {
                var vert = vertices[j];
                var nor = normals[j];
                if (!averageNormalHash.TryAdd(vert, nor))
                {
                    averageNormalHash[vert] = (averageNormalHash[vert] + nor);
                }
            }

            var tangents = new Vector4[n];
            for (var j = 0; j < n; j++)
            {
                var an = averageNormalHash[vertices[j]];
                an.Normalize();
                tangents[j] = new Vector4(an.x, an.y, an.z, 0);
            }

            mesh.tangents = tangents;
        }

        private static void DoSoftOutline2(Mesh mesh)
        {
            var n = mesh.vertexCount;
            var averageNormalHash = new Dictionary<Vector3, List<Vector3>>();
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            for (var j = 0; j < n; j++)
            {
                var vert = vertices[j];
                var nor = normals[j];
                if (!averageNormalHash.TryGetValue(vert, out var list))
                {
                    averageNormalHash.Add(vert, new List<Vector3> { nor });
                }
                else
                {
                    list.Add(nor);
                }
            }

            var tangents = new Vector4[n];
            for (var j = 0; j < n; j++)
            {
                var wp = vertices[j];
                var list = averageNormalHash[wp];
                var an = SumDistinctNormals(list);
                tangents[j] = new Vector4(an.x, an.y, an.z, 0);
            }

            mesh.tangents = tangents;
        }

        /// <summary>
        /// 从法线列表中过滤掉相近法线，只保留唯一方向，然后返回它们的向量和。
        /// </summary>
        /// <param name="normals">输入的法线列表</param>
        /// <param name="toleranceDegrees">相似容差（度数），比如 1° 或 5°</param>
        /// <returns>过滤后剩余法线的叠加值</returns>
        private static Vector3 SumDistinctNormals(List<Vector3> normals, float toleranceDegrees = 2f)
        {
            if (normals == null || normals.Count == 0)
                return Vector3.zero;

            float cosThreshold = Mathf.Cos(toleranceDegrees * Mathf.Deg2Rad);

            List<Vector3> distinct = new List<Vector3>();

            foreach (var n in normals)
            {
                if (n == Vector3.zero) continue;
                var norm = n.normalized;

                bool tooClose = false;
                foreach (var d in distinct)
                {
                    float dot = Vector3.Dot(norm, d);
                    if (dot > cosThreshold) // 很接近
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    distinct.Add(norm);
            }

            // 求和
            Vector3 sum = Vector3.zero;
            foreach (var d in distinct)
                sum += d;

            return sum.normalized;
        }
    }
}