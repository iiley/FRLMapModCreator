// Stripped SDK copy of FRL/Assets/Scripts/Race/LapGate/LapLayout.cs; keep serialized fields in sync with FRL.
using System.Collections.Generic;

// Lap layout (pure data, no scene object dependency): zone count, whether start/finish are the same line,
// the gate sequence, and the sector-end gates.
// Gate numbering convention (matches LapCheckBase.Progress): gate 0 = zone 0 (the start); after crossing
// gate k, Progress = k + 1; crossing gate GateCount-1 completes the lap. When same-line, GateCount =
// ZoneCount + 1 and the last gate maps back to zone 0; when not same-line, gates map one-to-one to zones.
// SectorEndGates only contains middle gates (1..GateCount-2), ascending; the last segment implicitly ends
// when the lap completes.
// Validation rules:
//   Hard error (returns null): zoneCount < 2; sectorEnd length doesn't match zone count.
//   Sector invalid (layout valid but no sectors, sectorWarning non-null): sectors enabled but not
//   same-line; sectors enabled but no middle zone is checked.
//   Ignored with a note: zone 0 is checked (it is the start/finish line).
//   When enableSectors=false, sectorEnd is entirely ignored with no warning.
public sealed class LapLayout
{
    public const int MaxSectors = 16; // server-side LapCompleteV2 limit; more than this and the server would reject every lap

    public int ZoneCount { get; }
    public bool SameStartFinish { get; }
    public int GateCount { get; }
    public int[] SectorEndGates { get; }
    public bool HasSectors => SectorEndGates.Length > 0;
    public int SectorCount => HasSectors ? SectorEndGates.Length + 1 : 0;

    private LapLayout(int zoneCount, bool sameStartFinish, int[] sectorEndGates)
    {
        ZoneCount = zoneCount;
        SameStartFinish = sameStartFinish;
        GateCount = sameStartFinish ? zoneCount + 1 : zoneCount;
        SectorEndGates = sectorEndGates;
    }

    // Which zone gate number `gate` maps to; when same-line, the last gate (gate == ZoneCount) maps back to zone 0
    public int ZoneOfGate(int gate)
    {
        if (gate < 0 || gate >= GateCount) throw new System.ArgumentOutOfRangeException(nameof(gate));
        return gate == ZoneCount ? 0 : gate;
    }

    public bool IsSectorEndGate(int gate)
    {
        return System.Array.IndexOf(SectorEndGates, gate) >= 0;
    }

    // The 1-based sector index a zone belongs to: zone 0 belongs to S1; a zone checked as a sector-end
    // belongs to the sector it ends. Returns 0 when there are no sectors.
    public int SectorOfZone(int zone)
    {
        if (!HasSectors) return 0;
        int s = 1;
        foreach (var g in SectorEndGates)
        {
            if (g < zone) s++;
        }
        return s;
    }

    public static LapLayout Build(int zoneCount, bool sameStartFinish, bool enableSectors, bool[] sectorEnd,
        out string error, out string sectorWarning)
    {
        error = null;
        sectorWarning = null;
        if (zoneCount < 2)
        {
            error = $"At least 2 zones are required (found {zoneCount})";
            return null;
        }
        if (null == sectorEnd || sectorEnd.Length != zoneCount)
        {
            error = "sectorEnd length must equal zone count";
            return null;
        }

        var ends = new List<int>();
        if (enableSectors)
        {
            if (!sameStartFinish)
            {
                sectorWarning = "Sectors require Same Start/Finish; sectors disabled";
            }
            else
            {
                bool zone0Marked = sectorEnd[0];
                for (int i = 1; i < zoneCount; i++)
                {
                    if (sectorEnd[i]) ends.Add(i);
                }
                if (ends.Count == 0)
                {
                    sectorWarning = "Mark at least one middle zone as Sector End; sectors disabled";
                }
                else if (ends.Count + 1 > MaxSectors)
                {
                    sectorWarning = $"More than {MaxSectors} sectors; sectors disabled";
                    ends.Clear();
                }
                else if (zone0Marked)
                {
                    sectorWarning = "Sector End on zone 0 is ignored (it is the start/finish line)";
                }
            }
        }
        return new LapLayout(zoneCount, sameStartFinish, ends.ToArray());
    }
}
