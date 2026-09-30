using UnityEngine;

// One song, split the way the stage music plays it: intro once, then A, then B looping.
[CreateAssetMenu(menuName = "Ralli/Audio/Music Track", fileName = "MusicTrack")]
public class MusicTrack : ScriptableObject
{
    public AudioClip intro;
    public AudioClip partA;
    public AudioClip partB;
}
