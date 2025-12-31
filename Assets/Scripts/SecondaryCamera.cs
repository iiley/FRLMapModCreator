using UnityEngine;

public class SecondaryCamera : MonoBehaviour
{
    [SerializeField] public GameObject TRIGGER;
    [SerializeField] public Vector3 triggerPoint;
    [SerializeField] public bool autoZoom = true;
    [SerializeField] public bool autoTrack = true;
    [SerializeField] public float extraZoomOut = 0f;
}