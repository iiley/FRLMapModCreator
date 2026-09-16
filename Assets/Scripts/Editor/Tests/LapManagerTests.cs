using NUnit.Framework;
using UnityEngine;

// LapManager.TryBuild scans direct children in Hierarchy order; these tests build tiny hierarchies in the
// active scene and destroy them again.
public class LapManagerTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Lap Manager", typeof(LapManager));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    private GameObject AddZone(string name, bool active = true, bool withComponent = true)
    {
        var go = withComponent ? new GameObject(name, typeof(LapCheckZone)) : new GameObject(name);
        go.transform.SetParent(_root.transform, false);
        go.SetActive(active);
        return go;
    }

    [Test]
    public void Defaults_MatchFrl()
    {
        var lm = _root.GetComponent<LapManager>();
        Assert.IsTrue(lm.sameStartFinish);
        Assert.IsFalse(lm.enableSectors);
        var zone = AddZone("Zone 0").GetComponent<LapCheckZone>();
        Assert.IsTrue(zone.snapToGround);
        Assert.IsFalse(zone.sectorEnd);
        Assert.IsTrue(zone.defaultVisual);
    }

    [Test]
    public void TwoZones_SameStartFinish_BuildsThreeGates()
    {
        AddZone("Zone 0");
        AddZone("Zone 1");
        var lm = _root.GetComponent<LapManager>();
        Assert.IsTrue(lm.TryBuild(out var zones, out var layout, out var error, out var warning), error);
        Assert.AreEqual(2, zones.Length);
        Assert.AreEqual(3, layout.GateCount);
        Assert.IsNull(warning);
    }

    [Test]
    public void InactiveChild_IsHardError()
    {
        AddZone("Zone 0");
        AddZone("Zone 1", active: false);
        var lm = _root.GetComponent<LapManager>();
        Assert.IsFalse(lm.TryBuild(out var zones, out var layout, out var error, out _));
        Assert.IsNull(zones);
        Assert.IsNull(layout);
        StringAssert.Contains("inactive", error);
    }

    [Test]
    public void ChildWithoutLapCheckZone_IsHardError()
    {
        AddZone("Zone 0");
        AddZone("Not a zone", withComponent: false);
        var lm = _root.GetComponent<LapManager>();
        Assert.IsFalse(lm.TryBuild(out _, out _, out var error, out _));
        StringAssert.Contains("no LapCheckZone", error);
    }

    [Test]
    public void SingleZone_IsHardError()
    {
        AddZone("Zone 0");
        var lm = _root.GetComponent<LapManager>();
        Assert.IsFalse(lm.TryBuild(out _, out _, out var error, out _));
        StringAssert.Contains("At least 2 zones", error);
    }

    [Test]
    public void SectorEndOnMiddleZone_ProducesTwoSectors()
    {
        AddZone("Zone 0");
        AddZone("Zone 1").GetComponent<LapCheckZone>().sectorEnd = true;
        AddZone("Zone 2");
        var lm = _root.GetComponent<LapManager>();
        lm.enableSectors = true;
        Assert.IsTrue(lm.TryBuild(out _, out var layout, out var error, out var warning), error);
        Assert.IsNull(warning);
        Assert.AreEqual(2, layout.SectorCount);
    }

    [Test]
    public void RaceManager_HasLapManagerField_DefaultNull()
    {
        var go = new GameObject("RaceManager", typeof(RaceManager));
        try
        {
            Assert.IsFalse(go.GetComponent<RaceManager>().lapManager); // UnityEngine.Object bool conversion, not IsNull
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
