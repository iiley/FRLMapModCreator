using System.Text;
using FRLMapMod.Editor;
using NUnit.Framework;
using UnityEngine;

// Lap rules of the scene validator, exercised on small throwaway hierarchies (no full scene needed).
public class CheckMapSceneValidLapTests
{
    private GameObject _rmGo;
    private RaceManager _rm;
    private readonly System.Collections.Generic.List<GameObject> _created = new System.Collections.Generic.List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        _rmGo = new GameObject("RaceManager", typeof(RaceManager));
        _rm = _rmGo.GetComponent<RaceManager>();
        _created.Add(_rmGo);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _created) Object.DestroyImmediate(go);
        _created.Clear();
    }

    private LapManager NewLapManager(int zones)
    {
        var go = new GameObject("Lap Manager", typeof(LapManager));
        _created.Add(go);
        for (int i = 0; i < zones; i++)
        {
            var z = new GameObject($"Zone {i}", typeof(LapCheckZone));
            z.transform.SetParent(go.transform, false);
        }
        return go.GetComponent<LapManager>();
    }

    [Test]
    public void NoLapManager_IsValid()
    {
        var sb = new StringBuilder();
        Assert.IsTrue(CheckMapSceneValid.CheckLapSetup(_rm, new LapManager[0], sb));
        Assert.AreEqual(0, sb.Length);
    }

    [Test]
    public void TwoLapManagers_IsInvalid()
    {
        var a = NewLapManager(2);
        var b = NewLapManager(2);
        _rm.lapManager = a;
        var sb = new StringBuilder();
        Assert.IsFalse(CheckMapSceneValid.CheckLapSetup(_rm, new[] { a, b }, sb));
        StringAssert.Contains("at most one LapManager, found 2", sb.ToString());
    }

    [Test]
    public void LapManagerPresentButUnassigned_IsInvalid()
    {
        var a = NewLapManager(2);
        var sb = new StringBuilder();
        Assert.IsFalse(CheckMapSceneValid.CheckLapSetup(_rm, new[] { a }, sb));
        StringAssert.Contains("RaceManager.lapManager is not assigned", sb.ToString());
    }

    [Test]
    public void AssignedButInvalidLayout_IsInvalid_WithTryBuildError()
    {
        var a = NewLapManager(1);
        _rm.lapManager = a;
        var sb = new StringBuilder();
        Assert.IsFalse(CheckMapSceneValid.CheckLapSetup(_rm, new[] { a }, sb));
        StringAssert.Contains("RaceManager.lapManager is invalid: At least 2 zones", sb.ToString());
    }

    [Test]
    public void AssignedAndValid_IsValid()
    {
        var a = NewLapManager(2);
        _rm.lapManager = a;
        var sb = new StringBuilder();
        Assert.IsTrue(CheckMapSceneValid.CheckLapSetup(_rm, new[] { a }, sb));
        Assert.AreEqual(0, sb.Length);
    }

    [Test]
    public void SectorWarning_DoesNotBlock()
    {
        var a = NewLapManager(2);
        a.enableSectors = true; // nothing marked -> warning only
        _rm.lapManager = a;
        var sb = new StringBuilder();
        Assert.IsTrue(CheckMapSceneValid.CheckLapSetup(_rm, new[] { a }, sb));
    }

    [Test]
    public void HasLapTiming_TrueOnlyForExactlyOneRaceManagerWithLapManager()
    {
        Assert.IsFalse(CheckMapSceneValid.HasLapTiming(null));
        Assert.IsFalse(CheckMapSceneValid.HasLapTiming(new RaceManager[0]));
        Assert.IsFalse(CheckMapSceneValid.HasLapTiming(new[] { _rm }));
        _rm.lapManager = NewLapManager(2);
        Assert.IsTrue(CheckMapSceneValid.HasLapTiming(new[] { _rm }));
        Assert.IsFalse(CheckMapSceneValid.HasLapTiming(new[] { _rm, _rm }));
    }

    [Test]
    public void SamePath_NormalizesSeparatorsAndCase()
    {
        Assert.IsTrue(CheckMapSceneValid.SamePath(@"Assets\Scenes\MapExample048.unity", "assets/scenes/mapexample048.unity"));
        Assert.IsFalse(CheckMapSceneValid.SamePath("Assets/Scenes/A.unity", "Assets/Scenes/B.unity"));
        Assert.IsFalse(CheckMapSceneValid.SamePath(null, "Assets/Scenes/A.unity"));
        Assert.IsFalse(CheckMapSceneValid.SamePath("", ""));
    }
}
