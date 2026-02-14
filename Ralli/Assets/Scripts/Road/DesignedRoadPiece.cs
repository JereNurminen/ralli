using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Road/Designed Road Piece", fileName = "DesignedRoadPiece")]
public class DesignedRoadPiece : ScriptableObject
{
    [Header("Metadata")]
    [Tooltip("Human-readable name for this piece.")]
    public string displayName = "Untitled Piece";

    [Header("Geometry (Auto-computed by Authoring Tool)")]
    [Tooltip("Total arc length in meters.")]
    public float arcLength;

    [Tooltip("Net heading change in degrees (positive = left turn).")]
    public float totalYawDeltaDeg;

    [Tooltip("Turn rate at entry (deg/m).")]
    public float entryTurnRate;

    [Tooltip("Turn rate at exit (deg/m).")]
    public float exitTurnRate;

    [Header("Turn Rate")]
    [Tooltip("Turn rate (deg/m) vs normalized distance [0..1].")]
    public AnimationCurve turnRateCurve = AnimationCurve.Constant(0f, 1f, 0f);

    [Header("Elevation (Optional)")]
    [Tooltip("Whether this piece overrides procedural elevation.")]
    public bool hasElevation;

    [Tooltip("Relative elevation (m) vs normalized distance [0..1].")]
    public AnimationCurve elevationCurve = AnimationCurve.Constant(0f, 1f, 0f);
}
