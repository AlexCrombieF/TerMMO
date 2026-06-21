using UnityEngine;

namespace Doodgy.Core
{
    /// <summary>
    /// Seeded noise helpers for procedural generation. Built on
    /// <see cref="Mathf.PerlinNoise"/> with a per-channel domain offset so a
    /// world seed produces a unique, repeatable landscape.
    ///
    /// NOTE: Perlin noise mirrors around the origin and is symmetric for
    /// negative coords, so we push sampling into a large positive-offset region.
    /// Mathf.PerlinNoise is deterministic within a Unity version but not
    /// guaranteed bit-identical across versions/platforms — fine for a
    /// server-authoritative world where only the server generates.
    /// </summary>
    public static class NoiseUtil
    {
        /// <summary>
        /// Deterministically derives a large 2D domain offset from a seed and a
        /// channel id. Different channels (terrain / caves / ore) get different
        /// offsets so their patterns are independent for the same seed.
        /// </summary>
        public static Vector2 SeedOffset(int seed, int channel)
        {
            // Mix seed + channel into a stable hash, then derive two offsets.
            unchecked
            {
                int h = (seed * 73856093) ^ (channel * 19349663);
                var rng = new System.Random(h);
                float ox = (float)(rng.NextDouble() * 20000.0 + 1000.0);
                float oy = (float)(rng.NextDouble() * 20000.0 + 1000.0);
                return new Vector2(ox, oy);
            }
        }

        /// <summary>
        /// Fractal Brownian motion: sums <paramref name="octaves"/> layers of
        /// Perlin noise at increasing frequency / decreasing amplitude, then
        /// normalizes to roughly [0, 1]. Higher octaves add fine detail.
        /// </summary>
        /// <param name="frequency">Base sampling scale; lower = larger features.</param>
        /// <param name="lacunarity">Frequency multiplier per octave (≈2).</param>
        /// <param name="persistence">Amplitude multiplier per octave (≈0.5).</param>
        public static float Fbm(float x, float y, Vector2 offset, float frequency,
                                int octaves, float lacunarity, float persistence)
        {
            float sum = 0f;
            float amp = 1f;
            float freq = frequency;
            float maxAmp = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise(offset.x + x * freq, offset.y + y * freq);
                sum += n * amp;
                maxAmp += amp;
                amp *= persistence;
                freq *= lacunarity;
            }

            return maxAmp > 0f ? sum / maxAmp : 0f;
        }

        /// <summary>1D-style fBm (samples along x at a fixed y) for terrain height.</summary>
        public static float Fbm1D(float x, Vector2 offset, float frequency,
                                  int octaves, float lacunarity, float persistence)
            => Fbm(x, 0f, offset, frequency, octaves, lacunarity, persistence);
    }
}
