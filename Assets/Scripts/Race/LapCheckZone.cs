// Stripped SDK copy of FRL/Assets/Scripts/Race/LapCheckZone.cs; keep serialized fields in sync with FRL.
using UnityEngine;

// Lap check gate: an oriented rectangular plane defined directly by a Transform, no Collider needed.
//   position   = gate center
//   forward(Z) = driving direction (Gizmo arrow); a crossing only counts going this way, driving backward
//                through it does not count
//   scale.x/y  = gate width/height; scale.z is unused (keep it at 1)
// The crossing test runs in the FR Legends client; this SDK copy only carries the serialized settings
// and the editing tools (ground snap, Gizmo, resize handle).
public class LapCheckZone : MonoBehaviour
{
    [Tooltip("While editing (move/rotate/scale/toggle), raycast down to the Ground layer so the bottom edge of the gate rests on the ground.")]
    public bool snapToGround = true;

    [Tooltip("Crossing this gate ends the current sector and starts the next one. Requires Enable Sectors on the parent Lap Manager. Ignored on zone 0 (it is the start/finish line).")]
    public bool sectorEnd = false;

    [Tooltip("Show the built-in runtime look: a translucent quad over the gate (checkered for the finish line) plus a sign with the gate number. Untick when the map provides its own gate art. Editor-only Gizmos are unaffected.")]
    public bool defaultVisual = true;

#if UNITY_EDITOR
    private const float SnapRayUp = 100f;
    private const float SnapRayLength = 200f;

    void OnValidate()
    {
        if (snapToGround && !Application.isPlaying)
        {
            SnapToGround();
        }
    }

    // Snaps the bottom-edge midpoint of the rectangle onto the Ground layer surface: the ray's x/z is
    // taken from the bottom-edge midpoint (not the center), so repeated triggers won't drift; if nothing
    // is hit, it doesn't move.
    // Returns whether a move actually happened
    public bool SnapToGround()
    {
        var gate = LapGate.FromTransform(transform);
        var bottomMid = gate.Center - gate.Up * gate.HalfHeight;
        var origin = bottomMid + Vector3.up * SnapRayUp;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, SnapRayLength, LayerMask.GetMask("Ground")))
        {
            return false;
        }
        var target = hit.point + gate.Up * gate.HalfHeight;
        if ((target - transform.position).sqrMagnitude < 1e-8f) return false;

        UnityEditor.Undo.RecordObject(transform, "Snap Lap Check Zone To Ground");
        transform.position = target;
        return true;
    }

    private static readonly Color FillColor = new Color(0.2f, 0.9f, 0.4f, 0.15f);
    private static readonly Color FillSelectedColor = new Color(0.2f, 0.9f, 0.4f, 0.35f);
    private static readonly Color EdgeColor = new Color(0.2f, 0.9f, 0.4f, 0.8f);
    private static readonly Color ArrowColor = new Color(1f, 0.85f, 0.2f, 1f);

    void OnDrawGizmos()
    {
        DrawGizmo(false);
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmo(true);
    }

    // The rectangular plane (semi-transparent fill + wireframe) plus an arrow from the center pointing
    // along forward (two pairs of arrowhead wings, one in the right plane and one in the up plane, so
    // it's visible from any angle)
    private void DrawGizmo(bool selected)
    {
        var gate = LapGate.FromTransform(transform);

        Gizmos.matrix = Matrix4x4.TRS(gate.Center, transform.rotation,
            new Vector3(gate.HalfWidth * 2f, gate.HalfHeight * 2f, 1f));
        Gizmos.color = selected ? FillSelectedColor : FillColor;
        Gizmos.DrawCube(Vector3.zero, new Vector3(1f, 1f, 0.001f));
        Gizmos.color = EdgeColor;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 0f));
        Gizmos.matrix = Matrix4x4.identity;

        float len = Mathf.Max(0.5f, Mathf.Min(gate.HalfWidth, gate.HalfHeight));
        float head = len * 0.3f;
        var tip = gate.Center + gate.Normal * len;
        var back = tip - gate.Normal * head;
        Gizmos.color = ArrowColor;
        Gizmos.DrawLine(gate.Center, tip);
        Gizmos.DrawLine(tip, back + gate.Right * head * 0.5f);
        Gizmos.DrawLine(tip, back - gate.Right * head * 0.5f);
        Gizmos.DrawLine(tip, back + gate.Up * head * 0.5f);
        Gizmos.DrawLine(tip, back - gate.Up * head * 0.5f);
    }
#endif
}

#if UNITY_EDITOR
// Scene view handle: an empty GameObject has no render bounds, so the Rect Tool doesn't work here; this
// uses a BoxBoundsHandle (the same handle BoxCollider editing uses) to provide edge/corner dragging —
// dragging an edge only moves that side (hold Alt for symmetric), and the result is written back to
// position and localScale.xy, with Undo support.
[UnityEditor.CustomEditor(typeof(LapCheckZone))]
public class LapCheckZoneEditor : UnityEditor.Editor
{
    private readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle _handle =
        new UnityEditor.IMGUI.Controls.BoxBoundsHandle
        {
            axes = UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.X
                 | UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.Y,
            handleColor = new Color(0.2f, 0.9f, 0.4f, 1f),
            wireframeColor = Color.clear, // the rectangle is already drawn by the Gizmo
        };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UnityEditor.EditorGUILayout.HelpBox(
            "Z (blue axis / arrow) = driving direction; only forward crossings count\nScale X/Y = gate width/height; Scale Z is unused\nDrag the edge/corner handles in the Scene view to resize (Alt = symmetric)",
            UnityEditor.MessageType.Info);

        var zone = (LapCheckZone)target;
        var parent = zone.transform.parent;
        var lm = null != parent ? parent.GetComponent<LapManager>() : null;
        if (null == lm)
        {
            UnityEditor.EditorGUILayout.HelpBox("Not a child of a Lap Manager: this zone is ignored at runtime.", UnityEditor.MessageType.Warning);
        }
        else
        {
            int i = zone.transform.GetSiblingIndex();
            lm.TryBuild(out _, out var layout, out _, out _);
            string suffix = "";
            if (i == 0)
            {
                suffix = " — Start/Finish";
                if (null != layout && layout.HasSectors)
                {
                    suffix += $", ends S{layout.SectorCount}, starts S1";
                }
            }
            else if (null != layout && layout.IsSectorEndGate(i))
            {
                int k = layout.SectorOfZone(i);
                suffix = $" — ends S{k}, starts S{k + 1}";
            }
            else if (null != layout && layout.HasSectors)
            {
                int k = layout.SectorOfZone(i);
                suffix = $" — inside S{k}";
            }

            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                UnityEditor.EditorGUILayout.LabelField($"Zone #{i} of {lm.name}{suffix}");
                if (GUILayout.Button("Select Lap Manager", GUILayout.Width(140f)))
                {
                    UnityEditor.Selection.activeGameObject = lm.gameObject;
                }
            }
        }
    }

    private Vector3 _lastPos;
    private Quaternion _lastRot;
    private Vector3 _lastScale;
    private bool _tracked;

    void OnSceneGUI()
    {
        var zone = (LapCheckZone)target;
        var t = zone.transform;
        SnapIfEdited(zone, t);
        var lossy = t.lossyScale;

        using (new UnityEditor.Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
        {
            _handle.center = Vector3.zero;
            _handle.size = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), 0f);

            UnityEditor.EditorGUI.BeginChangeCheck();
            _handle.DrawHandle();
            if (!UnityEditor.EditorGUI.EndChangeCheck()) return;

            UnityEditor.Undo.RecordObject(t, "Resize Lap Check Zone");
            // The handle operates in the gate's unscaled local space; size is a world size (lossy), so
            // converting back to localScale requires dividing out the parent's accumulated scale
            var local = t.localScale;
            float px = ParentScale(lossy.x, local.x);
            float py = ParentScale(lossy.y, local.y);
            t.position = t.position + t.rotation * _handle.center;
            t.localScale = new Vector3(_handle.size.x / px, _handle.size.y / py, local.z);
        }
    }

    // Snap to ground after move/rotate/scale (including this handle): compares against the last-seen TRS,
    // and snaps when it changed and the toggle is on
    private void SnapIfEdited(LapCheckZone zone, Transform t)
    {
        bool changed = !_tracked
                       || t.position != _lastPos
                       || t.rotation != _lastRot
                       || t.lossyScale != _lastScale;
        if (changed && zone.snapToGround && !Application.isPlaying)
        {
            zone.SnapToGround();
        }
        _lastPos = t.position;
        _lastRot = t.rotation;
        _lastScale = t.lossyScale;
        _tracked = true;
    }

    private static float ParentScale(float lossy, float local)
    {
        if (Mathf.Approximately(local, 0f) || Mathf.Approximately(lossy, 0f)) return 1f;
        return Mathf.Abs(lossy / local);
    }
}
#endif
