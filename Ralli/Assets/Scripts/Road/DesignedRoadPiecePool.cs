using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DesignedRoadPiecePoolEntry
{
    public DesignedRoadPiece piece;

    [Tooltip("Selection weight (higher = more likely).")]
    [Range(0f, 10f)]
    public float weight = 1f;

    [Tooltip("Can this piece be horizontally mirrored (L/R flipped)?")]
    public bool canMirror = true;
}

[CreateAssetMenu(menuName = "Ralli/Road/Designed Road Piece Pool", fileName = "DesignedRoadPiecePool")]
public class DesignedRoadPiecePool : ScriptableObject
{
    [Tooltip("Collection of designed pieces with selection weights.")]
    public List<DesignedRoadPiecePoolEntry> pieces = new List<DesignedRoadPiecePoolEntry>();
}
