// Stripped SDK copy of FRL/Assets/Scripts/Race/LapManager.cs; keep serialized fields in sync with FRL.
using UnityEngine;

// Parent-node component for lap zones: the direct children (in Hierarchy order) are the check-zone order;
// every child must have a LapCheckZone component.
// RaceManager.lapManager pointing at this component enables lap timing.
// Validation logic lives in LapLayout.Build; the Editor (LapManagerEditor) and the FR Legends client go
// through TryBuild, so what the Inspector shows is exactly what the game sees.
// An inactive child is a hard error (in the game its gate can never be crossed); to temporarily disable a
// zone, move it out of this node instead.
// This node should keep position 0, rotation 0, scale 1, to avoid distorting a zone's lossyScale
// (see the LapGate file header).
public class LapManager : MonoBehaviour
{
    [Tooltip("Same start/finish line: zone 0 is both the lap start and the lap finish. Off: the last zone is the finish.")]
    public bool sameStartFinish = true;

    [Tooltip("Enable sector split timing. Requires Same Start Finish and at least one middle zone marked Sector End.")]
    public bool enableSectors = false;

    // Scans direct children to build the zone array and layout. Returns false and sets error on failure
    // (zones/layout are null); on success, sectorWarning may still be non-null (sectors invalid, or some
    // checkbox was ignored).
    public bool TryBuild(out LapCheckZone[] zones, out LapLayout layout, out string error, out string sectorWarning)
    {
        layout = null;
        sectorWarning = null;
        int n = transform.childCount;
        zones = new LapCheckZone[n];
        var sectorEnd = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf)
            {
                error = $"Child '{child.name}' (#{i}) is inactive; remove it from the LapManager instead of deactivating it";
                zones = null;
                return false;
            }
            var zone = child.GetComponent<LapCheckZone>();
            if (null == zone)
            {
                error = $"Child '{child.name}' (#{i}) has no LapCheckZone component";
                zones = null;
                return false;
            }
            zones[i] = zone;
            sectorEnd[i] = zone.sectorEnd;
        }
        layout = LapLayout.Build(n, sameStartFinish, enableSectors, sectorEnd, out error, out sectorWarning);
        if (null == layout)
        {
            zones = null;
            return false;
        }
        return true;
    }

#if UNITY_EDITOR
    // Scene view: a two-line label above each zone (name, then role/sector annotation), connected in
    // order (closed back to zone 0 when same-line); when sectors are active, segments alternate between
    // two colors and each segment midpoint gets an "Sn" label. Lines highlight when this node or any
    // child zone is selected.
    private static readonly Color LineColorA = new Color(0.2f, 0.9f, 0.4f, 1f);
    private static readonly Color LineColorB = new Color(0.3f, 0.6f, 1f, 1f);
    private static GUIStyle s_labelStyle;
    private static GUIStyle s_sectorLabelStyle;

    void OnDrawGizmos()
    {
        int n = transform.childCount;
        if (n == 0) return;

        var sel = UnityEditor.Selection.activeTransform;
        bool highlight = null != sel && (sel == transform || sel.parent == transform);
        float alpha = highlight ? 1f : 0.45f;

        TryBuild(out _, out var layout, out _, out _); // On failure layout is null; only labels and single-color lines are drawn

        if (null == s_labelStyle)
        {
            s_labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel);
            s_labelStyle.normal.textColor = new Color(1f, 0.95f, 0.6f, 1f);
        }
        if (null == s_sectorLabelStyle)
        {
            s_sectorLabelStyle = new GUIStyle(UnityEditor.EditorStyles.miniBoldLabel);
            s_sectorLabelStyle.normal.textColor = new Color(0.6f, 0.85f, 1f, 1f);
        }

        var centers = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var child = transform.GetChild(i);
            centers[i] = child.position;
            var gate = LapGate.FromTransform(child);
            var labelPos = gate.Center + gate.Up * (gate.HalfHeight + 0.5f);
            UnityEditor.Handles.Label(labelPos, ZoneLabel(child.name, i, n, layout), s_labelStyle);
        }

        int segments = sameStartFinish ? n : n - 1;
        int sector = 1;
        for (int s = 0; s < segments; s++)
        {
            bool useB = null != layout && layout.HasSectors && (sector % 2 == 0);
            var c = useB ? LineColorB : LineColorA;
            c.a = alpha;
            Gizmos.color = c;
            var a = centers[s];
            var b = centers[(s + 1) % n];
            Gizmos.DrawLine(a, b);
            if (null != layout && layout.HasSectors)
            {
                UnityEditor.Handles.Label((a + b) * 0.5f, $"S{sector}", s_sectorLabelStyle);
            }
            if (null != layout && layout.IsSectorEndGate(s + 1)) sector++;
        }
    }

    // Two-line zone label: line 1 is the zone's name; line 2 (when present) is the role and/or sector
    // annotation — "Start/Finish"/"Start" for zone 0 (plus "· S{last} end · S1 start" when sectors are
    // active), "S{k} end · S{k+1} start" for a sector-end zone, "Finish" for the last zone when not
    // same-line, or "(inside S{k})" for any other zone while sectors are active.
    private string ZoneLabel(string name, int i, int n, LapLayout layout)
    {
        string line2 = null;
        if (i == 0)
        {
            line2 = sameStartFinish ? "Start/Finish" : "Start";
            if (null != layout && layout.HasSectors)
            {
                line2 += $" · S{layout.SectorCount} end · S1 start";
            }
        }
        else if (null != layout && layout.IsSectorEndGate(i))
        {
            int k = layout.SectorOfZone(i);
            line2 = $"S{k} end · S{k + 1} start";
        }
        else if (!sameStartFinish && i == n - 1)
        {
            line2 = "Finish";
        }
        else if (null != layout && layout.HasSectors)
        {
            int k = layout.SectorOfZone(i);
            line2 = $"(inside S{k})";
        }
        return null == line2 ? name : $"{name}\n{line2}";
    }
#endif
}
