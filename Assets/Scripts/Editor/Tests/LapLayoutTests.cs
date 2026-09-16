using System.Linq;
using NUnit.Framework;

// LapLayout only validates the gate sequence and sectors; it never touches scene objects.
// Gate numbering convention: see the LapLayout file header.
public class LapLayoutTests
{
    private static bool[] None(int n) => new bool[n];

    private static bool[] Marks(int n, params int[] marked)
    {
        var a = new bool[n];
        foreach (var i in marked) a[i] = true;
        return a;
    }

    [Test]
    public void SameStartFinish_ThreeZones_HasFourGates_LastGateMapsToZone0()
    {
        var l = LapLayout.Build(3, true, false, None(3), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.IsNull(warn);
        Assert.AreEqual(4, l.GateCount);
        Assert.AreEqual(0, l.ZoneOfGate(0));
        Assert.AreEqual(1, l.ZoneOfGate(1));
        Assert.AreEqual(2, l.ZoneOfGate(2));
        Assert.AreEqual(0, l.ZoneOfGate(3));
        Assert.IsFalse(l.HasSectors);
        Assert.AreEqual(0, l.SectorCount);
    }

    [Test]
    public void SeparateFinish_ThreeZones_HasThreeGates_OneToOne()
    {
        var l = LapLayout.Build(3, false, false, None(3), out var err, out _);
        Assert.IsNotNull(l, err);
        Assert.AreEqual(3, l.GateCount);
        Assert.AreEqual(2, l.ZoneOfGate(2));
    }

    [Test]
    public void FewerThanTwoZones_IsHardError()
    {
        Assert.IsNull(LapLayout.Build(1, true, false, None(1), out var err, out _));
        Assert.IsNotEmpty(err);
        Assert.IsNull(LapLayout.Build(0, false, false, None(0), out err, out _));
        Assert.IsNotEmpty(err);
    }

    [Test]
    public void SectorEndLengthMismatch_IsHardError()
    {
        Assert.IsNull(LapLayout.Build(3, true, true, None(2), out var err, out _));
        Assert.IsNotEmpty(err);
        Assert.IsNull(LapLayout.Build(3, true, true, null, out err, out _));
        Assert.IsNotEmpty(err);
    }

    [Test]
    public void SectorsWithoutSameStartFinish_LayoutValid_SectorsDisabled_WithWarning()
    {
        var l = LapLayout.Build(3, false, true, Marks(3, 1), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.IsFalse(l.HasSectors);
        Assert.IsNotEmpty(warn);
    }

    [Test]
    public void SectorsEnabledButNothingMarked_LayoutValid_SectorsDisabled_WithWarning()
    {
        var l = LapLayout.Build(3, true, true, None(3), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.IsFalse(l.HasSectors);
        Assert.IsNotEmpty(warn);
    }

    [Test]
    public void Zone0Marked_IsIgnoredWithWarning_OtherMarksKept()
    {
        var l = LapLayout.Build(3, true, true, Marks(3, 0, 1), out var err, out var warn);
        Assert.IsNotNull(l, err);
        CollectionAssert.AreEqual(new[] { 1 }, l.SectorEndGates);
        Assert.AreEqual(2, l.SectorCount);
        Assert.IsNotEmpty(warn);
    }

    [Test]
    public void TwoMiddleZonesMarked_GivesThreeSectorsInOrder()
    {
        var l = LapLayout.Build(4, true, true, Marks(4, 2, 1), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.IsNull(warn);
        CollectionAssert.AreEqual(new[] { 1, 2 }, l.SectorEndGates);
        Assert.AreEqual(3, l.SectorCount);
        Assert.IsTrue(l.IsSectorEndGate(1));
        Assert.IsTrue(l.IsSectorEndGate(2));
        Assert.IsFalse(l.IsSectorEndGate(3));
        Assert.IsFalse(l.IsSectorEndGate(4));
    }

    [Test]
    public void SectorOfZone_AssignsZonesToSectors()
    {
        // 4 zones, zone1 and zone2 are sector ends: S1 = zone0->zone1, S2 = zone1->zone2, S3 = zone2->zone3->zone0
        var l = LapLayout.Build(4, true, true, Marks(4, 1, 2), out _, out _);
        Assert.AreEqual(1, l.SectorOfZone(0));
        Assert.AreEqual(1, l.SectorOfZone(1));
        Assert.AreEqual(2, l.SectorOfZone(2));
        Assert.AreEqual(3, l.SectorOfZone(3));
    }

    [Test]
    public void SectorOfZone_NoSectors_ReturnsZero()
    {
        var l = LapLayout.Build(3, true, false, Marks(3, 1), out _, out _);
        Assert.AreEqual(0, l.SectorOfZone(1));
    }

    [Test]
    public void SectorsDisabled_MarksIgnored_NoWarning()
    {
        var l = LapLayout.Build(3, true, false, Marks(3, 1, 2), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.IsFalse(l.HasSectors);
        Assert.IsNull(warn);
    }

    [Test]
    public void MoreThanMaxSectors_LayoutValid_SectorsDisabled_WithWarning()
    {
        // 18 zones, zones 1..17 marked as sector ends -> 17 middle ends + 1 = 18 sectors > MaxSectors(16)
        var marked = Enumerable.Range(1, 17).ToArray();
        var l = LapLayout.Build(18, true, true, Marks(18, marked), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.IsFalse(l.HasSectors);
        Assert.IsNotEmpty(warn);
    }

    [Test]
    public void ExactlyMaxSectors_LayoutValid_NoWarning()
    {
        // 16 zones, zones 1..15 marked as sector ends -> 15 middle ends + 1 = 16 sectors == MaxSectors(16)
        var marked = Enumerable.Range(1, 15).ToArray();
        var l = LapLayout.Build(16, true, true, Marks(16, marked), out var err, out var warn);
        Assert.IsNotNull(l, err);
        Assert.AreEqual(16, l.SectorCount);
        Assert.IsNull(warn);
    }

    [Test]
    public void ZoneOfGate_OutOfRange_Throws()
    {
        var l = LapLayout.Build(3, true, false, None(3), out _, out _);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => l.ZoneOfGate(4));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => l.ZoneOfGate(-1));
    }
}
