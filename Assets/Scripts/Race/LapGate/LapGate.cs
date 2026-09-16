using UnityEngine;

// Stripped SDK copy of FRL/Assets/Scripts/Race/LapGate/LapGate.cs; keep serialized fields in sync with FRL.
// Oriented rectangular gate: Center, Normal (= Transform.forward, the driving direction), Right/Up are the
// rectangle's width/height directions (unit vectors); half width/height come from size (= lossyScale.xy) / 2.
// The SDK only uses this geometry for Gizmos, the resize handle and ground snapping; the crossing test
// runs in the FR Legends client.
// A parent with non-uniform scale plus rotation shears the gate and makes lossyScale inaccurate; do not
// place zones under such parents.
public readonly struct LapGate
{
    public readonly Vector3 Center;
    public readonly Vector3 Right;
    public readonly Vector3 Up;
    public readonly Vector3 Normal;
    public readonly float HalfWidth;
    public readonly float HalfHeight;

    public LapGate(Vector3 center, Quaternion rotation, Vector2 size)
    {
        Center = center;
        Right = rotation * Vector3.right;
        Up = rotation * Vector3.up;
        Normal = rotation * Vector3.forward;
        HalfWidth = Mathf.Abs(size.x) * 0.5f;
        HalfHeight = Mathf.Abs(size.y) * 0.5f;
    }

    public static LapGate FromTransform(Transform t)
    {
        return new LapGate(t.position, t.rotation, t.lossyScale);
    }
}
