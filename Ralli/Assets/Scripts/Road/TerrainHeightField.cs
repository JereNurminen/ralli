using UnityEngine;

// Seeded world-space terrain height H(x, z). Layered Perlin noise, shifted so
// H at the road origin equals the origin height (the road starts on the ground).
public class TerrainHeightField
{
    private readonly RoadGenerationConfig config;
    private readonly Vector2 noiseOffset;
    private readonly float baseHeight;

    public TerrainHeightField(RoadGenerationConfig config, int seed, Vector3 origin)
    {
        this.config = config;
        noiseOffset = new Vector2(
            Hash01(seed, 0, 0, 11) * 10000f,
            Hash01(seed, 0, 0, 12) * 10000f
        );
        baseHeight = origin.y - RawHeight(origin.x, origin.z);
    }

    public float GetHeight(float x, float z)
    {
        return baseHeight + RawHeight(x, z);
    }

    private float RawHeight(float x, float z)
    {
        float scale = Mathf.Max(1f, config.terrainNoiseScale);
        float amplitude = config.terrainNoiseAmplitude;
        float frequency = 1f / scale;
        float height = 0f;
        int octaves = Mathf.Clamp(config.terrainNoiseOctaves, 1, 5);

        for (int i = 0; i < octaves; i++)
        {
            float n = Mathf.PerlinNoise(x * frequency + noiseOffset.x, z * frequency + noiseOffset.y);
            height += (n * 2f - 1f) * amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return height;
    }

    public static float Hash01(int seed, int x, int z, int salt)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 374761393u;
            h ^= (uint)z * 668265263u;
            h ^= (uint)(salt + 17) * 2891336453u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }
}
