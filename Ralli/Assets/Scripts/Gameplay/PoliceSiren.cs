using UnityEngine;

// Looping siren on the police van. Positional with doppler, so it is heard closing in from far
// behind and bends in pitch as the van passes. Fades in when the van appears. The van root is
// unscaled, so distances are in meters.
public class PoliceSiren : MonoBehaviour
{
    private PoliceConfig config;
    private AudioSource source;
    private float fadeIn;

    public void Initialize(PoliceConfig policeConfig)
    {
        config = policeConfig;
        if (config.siren == null)
        {
            return;
        }

        source = gameObject.AddComponent<AudioSource>();
        source.clip = config.siren;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = config.sirenMinDistance;
        source.maxDistance = config.sirenMaxDistance;
        source.dopplerLevel = config.sirenDoppler;
        source.volume = 0f;
        source.time = Random.Range(0f, config.siren.length);
        source.Play();
    }

    private void Update()
    {
        if (source == null)
        {
            return;
        }

        fadeIn = Mathf.MoveTowards(fadeIn, 1f, Time.deltaTime / Mathf.Max(0.01f, config.sirenFadeInTime));
        source.volume = config.sirenVolume * fadeIn;
    }
}
