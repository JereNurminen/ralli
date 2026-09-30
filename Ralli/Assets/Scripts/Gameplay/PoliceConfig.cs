using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Gameplay/Police Config", fileName = "PoliceConfig")]
public class PoliceConfig : ScriptableObject
{
    [Header("Start")]
    [Tooltip("Seconds after the scene starts before the police appear.")]
    public float startDelay = 10f;
    [Tooltip("How far behind the player (along the road, m) the police appear.")]
    public float spawnDistanceBehind = 200f;

    [Header("Chase")]
    [Tooltip("Constant chase speed (km/h).")]
    public float chaseSpeedKph = 140f;
    [Tooltip("How quickly the van gets up to chase speed (m/s²).")]
    public float accelerationMps2 = 5f;
    [Tooltip("Within this distance of the player (m), the van steers onto the player's line.")]
    public float homingDistance = 40f;
    [Tooltip("Sideways speed when changing line (m/s).")]
    public float lateralSpeed = 3f;
    [Tooltip("Bumper-to-bumper gap (m) at which the player counts as caught.")]
    public float catchDistance = 1.5f;

    [Header("Traffic")]
    [Tooltip("Traffic cars within this distance of the van (m) are knocked loose so the van shoves them aside.")]
    public float shovePadding = 0.3f;

    [Header("Visuals")]
    [Tooltip("Van body size (x = width, y = height, z = length). Roughly a VW Transporter.")]
    public Vector3 vanSize = new Vector3(1.9f, 1.95f, 4.9f);
    public Color vanColor = Color.white;
}
