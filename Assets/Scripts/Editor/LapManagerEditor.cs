// Stripped SDK copy of FRL/Assets/Scripts/Editor/LapManagerEditor.cs; the menu item uses the Scene view pivot instead of FRL's Bezier helper.
using UnityEngine;
using UnityEditor;

// LapManager panel: two toggles + validation box (showing the lap path) + zone table (Hierarchy order,
// toggle Sector End directly) + a Sectors list (when active) + Add Zone.
// Validation reuses LapManager.TryBuild, recomputed every repaint (there are few zones, so the cost is
// negligible).
[CustomEditor(typeof(LapManager))]
public class LapManagerEditor : Editor
{
    private static readonly Vector3 DefaultZoneScale = new Vector3(8f, 3f, 1f);
    private const float NewZoneDistance = 10f; // How many meters in front of the last zone a new zone is placed
    private const float SceneRayUp = 100f;
    private const float SceneRayLength = 200f;

    private const float ColIndex = 24f;
    private const float ColRole = 80f;
    private const float ColSectorEnd = 72f;

    [MenuItem("GameObject/FR Legend/Lap Manager", false, 0)]
    private static void CreateLapManager()
    {
        var go = new GameObject("Lap Manager", typeof(LapManager));
        go.transform.position = SceneCenterOnGround();
        Undo.RegisterCreatedObjectUndo(go, "Create Lap Manager");
        AddZone(go.GetComponent<LapManager>());
        Selection.activeGameObject = go;
    }

    // The Scene view pivot dropped onto the Ground layer; the pivot itself when there is no Scene view or
    // nothing below it
    private static Vector3 SceneCenterOnGround()
    {
        var view = SceneView.lastActiveSceneView;
        var pivot = null != view ? view.pivot : Vector3.zero;
        var origin = pivot + Vector3.up * SceneRayUp;
        if (Physics.Raycast(origin, Vector3.down, out var hit, SceneRayLength, LayerMask.GetMask("Ground")))
        {
            return hit.point;
        }
        return pivot;
    }

    // Creates a new child zone at the end: NewZoneDistance in front of the last zone along its forward
    // direction, reusing its rotation/scale; with no existing zone, it's placed at the parent's position
    // with default size. Supports Undo.
    public static GameObject AddZone(LapManager lm)
    {
        var parent = lm.transform;
        int n = parent.childCount;
        var last = n > 0 ? parent.GetChild(n - 1) : null;

        var go = new GameObject($"Zone {n}", typeof(LapCheckZone));
        Undo.RegisterCreatedObjectUndo(go, "Add Lap Check Zone");
        Undo.SetTransformParent(go.transform, parent, "Add Lap Check Zone");
        if (null != last)
        {
            go.transform.position = last.position + last.forward * NewZoneDistance;
            go.transform.rotation = last.rotation;
            go.transform.localScale = last.localScale;
        }
        else
        {
            go.transform.position = parent.position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = DefaultZoneScale;
        }
        go.GetComponent<LapCheckZone>().SnapToGround();
        return go;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sameStartFinish"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableSectors"));
        serializedObject.ApplyModifiedProperties();

        var lm = (LapManager)target;
        bool ok = lm.TryBuild(out _, out var layout, out var error, out var warning);

        EditorGUILayout.Space();
        if (!ok)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
        else if (!string.IsNullOrEmpty(warning))
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(Summary(lm.transform, layout), MessageType.Info);
        }
        EditorGUILayout.Space();

        DrawZoneTable(lm, layout);

        if (null != layout && layout.HasSectors)
        {
            EditorGUILayout.Space();
            DrawSectors(lm.transform, layout);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Zone"))
        {
            Selection.activeGameObject = AddZone(lm);
        }
    }

    // "Zone 0 → Zone 1 → … → last", plus " → Zone 0" again when the layout is same-line.
    private static string LapPath(Transform parent, LapLayout layout)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < layout.ZoneCount; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append(parent.GetChild(i).name);
        }
        if (layout.SameStartFinish) sb.Append(" → ").Append(parent.GetChild(0).name);
        return sb.ToString();
    }

    private static string Summary(Transform parent, LapLayout layout)
    {
        return $"Lap: {LapPath(parent, layout)}  ({layout.GateCount} gate crossings per lap)";
    }

    // The zone path a single sector covers, e.g. "Zone 0 → Zone 1". The start zone of S1 is zone 0;
    // for later sectors it's the zone at the previous sector-end gate. The end zone of a non-last sector
    // is its own sector-end gate; the last sector runs to the last zone and then back to zone 0 (sectors
    // only exist in same-line layouts, so that final hop always happens).
    private static string SectorPath(Transform parent, LapLayout layout, int sectorIndex1Based)
    {
        int s = sectorIndex1Based;
        int startZone = s == 1 ? 0 : layout.SectorEndGates[s - 2];
        var sb = new System.Text.StringBuilder();
        sb.Append(parent.GetChild(startZone).name);

        if (s < layout.SectorCount)
        {
            int endZone = layout.SectorEndGates[s - 1];
            for (int z = startZone + 1; z <= endZone; z++)
            {
                sb.Append(" → ").Append(parent.GetChild(z).name);
            }
        }
        else
        {
            int lastZone = layout.ZoneCount - 1;
            for (int z = startZone + 1; z <= lastZone; z++)
            {
                sb.Append(" → ").Append(parent.GetChild(z).name);
            }
            sb.Append(" → ").Append(parent.GetChild(0).name);
        }
        return sb.ToString();
    }

    private static void DrawSectors(Transform parent, LapLayout layout)
    {
        EditorGUILayout.LabelField("Sectors", EditorStyles.boldLabel);
        for (int s = 1; s <= layout.SectorCount; s++)
        {
            EditorGUILayout.LabelField($"S{s}  {SectorPath(parent, layout, s)}");
        }
    }

    private void DrawZoneTable(LapManager lm, LapLayout layout)
    {
        var t = lm.transform;
        int n = t.childCount;
        EditorGUILayout.LabelField("Zones (hierarchy order)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("#", EditorStyles.miniBoldLabel, GUILayout.Width(ColIndex));
            EditorGUILayout.LabelField("Name", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Role", EditorStyles.miniBoldLabel, GUILayout.Width(ColRole));
            EditorGUILayout.LabelField("Sector End", EditorStyles.miniBoldLabel, GUILayout.Width(ColSectorEnd));
        }

        if (n == 0)
        {
            EditorGUILayout.LabelField("(no zones — click Add Zone)");
            return;
        }

        for (int i = 0; i < n; i++)
        {
            var child = t.GetChild(i);
            var zone = child.GetComponent<LapCheckZone>();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(ColIndex));
                if (GUILayout.Button(child.name, EditorStyles.linkLabel))
                {
                    Selection.activeGameObject = child.gameObject;
                }
                if (null == zone)
                {
                    var red = new GUIStyle(EditorStyles.label);
                    red.normal.textColor = Color.red;
                    EditorGUILayout.LabelField("(missing LapCheckZone)", red);
                    continue;
                }

                EditorGUILayout.LabelField(Role(i, n, lm.sameStartFinish), GUILayout.Width(ColRole));

                bool canSector = lm.enableSectors && i != 0 && !(!lm.sameStartFinish && i == n - 1);
                using (new EditorGUI.DisabledScope(!canSector))
                {
                    bool v = EditorGUILayout.Toggle(zone.sectorEnd, GUILayout.Width(ColSectorEnd));
                    if (v != zone.sectorEnd)
                    {
                        Undo.RecordObject(zone, "Toggle Sector End");
                        zone.sectorEnd = v;
                        EditorUtility.SetDirty(zone);
                        Repaint(); // the Sectors list below only updates on the next repaint
                    }
                }
            }
        }
    }

    private static string Role(int index, int count, bool sameStartFinish)
    {
        if (index == 0) return sameStartFinish ? "Start/Finish" : "Start";
        if (!sameStartFinish && index == count - 1) return "Finish";
        return "";
    }
}
