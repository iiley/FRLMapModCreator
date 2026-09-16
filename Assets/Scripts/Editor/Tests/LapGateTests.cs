using NUnit.Framework;
using UnityEngine;

// The SDK LapGate carries only geometry (center, axes, half extents) for Gizmos, handles and snapping.
public class LapGateTests
{
    [Test]
    public void Constructor_HalfExtentsAreAbsoluteHalfSize()
    {
        var gate = new LapGate(Vector3.zero, Quaternion.identity, new Vector2(-4f, 2f));
        Assert.AreEqual(2f, gate.HalfWidth, 1e-6f);
        Assert.AreEqual(1f, gate.HalfHeight, 1e-6f);
        Assert.AreEqual(Vector3.right, gate.Right);
        Assert.AreEqual(Vector3.up, gate.Up);
        Assert.AreEqual(Vector3.forward, gate.Normal);
    }

    [Test]
    public void FromTransform_UsesPositionRotationAndLossyScale()
    {
        var parent = new GameObject("parent");
        var child = new GameObject("zone");
        try
        {
            parent.transform.localScale = new Vector3(2f, 2f, 2f);
            child.transform.SetParent(parent.transform, false);
            child.transform.localScale = new Vector3(2f, 1f, 1f); // lossy = (4,2,2) -> half width 2, half height 1
            child.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // forward = +x, right = -z
            child.transform.position = new Vector3(5f, 0f, 0f);

            var gate = LapGate.FromTransform(child.transform);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), gate.Center);
            Assert.AreEqual(2f, gate.HalfWidth, 1e-5f);
            Assert.AreEqual(1f, gate.HalfHeight, 1e-5f);
            Assert.AreEqual(1f, Vector3.Dot(gate.Normal, Vector3.right), 1e-5f);
            Assert.AreEqual(1f, Vector3.Dot(gate.Right, Vector3.back), 1e-5f);
            Assert.AreEqual(1f, Vector3.Dot(gate.Up, Vector3.up), 1e-5f);
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }
}
