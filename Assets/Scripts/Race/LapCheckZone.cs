using System;
using UnityEngine;

public class LapCheckZone : MonoBehaviour
{
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(LapCheckZone))]
public class LapCheckZoneEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var resetTrigger = (LapCheckZone)target;
        var colliders = resetTrigger.GetComponentsInChildren<Collider>(false);

        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField(
            $"Contained Colliders ({colliders.Length})",
            UnityEditor.EditorStyles.boldLabel);

        var hasNonConvexMeshCollider = Array.Exists(
            colliders,
            collider => collider is MeshCollider { convex: false });

        if (hasNonConvexMeshCollider)
        {
            UnityEditor.EditorGUILayout.HelpBox(
                "Contains non-convex MeshCollider(s). They will be changed to convex at runtime.",
                UnityEditor.MessageType.Warning);
        }

        using (new UnityEditor.EditorGUI.DisabledScope(true))
        {
            for (var i = 0; i < colliders.Length; i++)
            {
                UnityEditor.EditorGUILayout.ObjectField(
                    $"Collider {i}",
                    colliders[i],
                    typeof(Collider),
                    true);
            }
        }
    }
}
#endif
