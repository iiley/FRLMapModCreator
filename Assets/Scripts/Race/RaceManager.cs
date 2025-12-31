
using UnityEngine;

[DisallowMultipleComponent]
public class RaceManager : MonoBehaviour {

	[SerializeField] public Transform startSign;
	[SerializeField] public Transform finishSign;
	[SerializeField] public QualityHiddenManager qualityHidden;
	[SerializeField] public DriftCamera driftCamera;
	[SerializeField] public Skidmarks skidmarks; 
	[SerializeField] public string CircuitName;

	//Dynamic objects requiring positional tracking will automatically reset to their original positions when playback begins.
	[SerializeField] public Transform[] dynamicObjects;

	//The initial positions and headings of online players in multiplayer
	[SerializeField] public Transform[] onlineLocators;
	
	//Game intro camera position and motion direction
	public Transform cameraStartPosition;
	public Transform cameraMoveDirection;

}