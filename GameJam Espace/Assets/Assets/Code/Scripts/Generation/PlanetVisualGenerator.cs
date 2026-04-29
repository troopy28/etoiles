using UnityEngine;

// Pushes per-instance procedural-planet shader parameters via MaterialPropertyBlock.
// One shared material (Custom/ProceduralPlanet) is reused across all planets so the
// renderer can keep batching them; only the property block varies per body.
//
// Variety comes from a hard-coded list of ~18 archetypes (Earth-like, Mars-like,
// Jupiter-like, …). The seed picks an archetype, then jitters its palette and noise
// frequency a little so two bodies of the same archetype never look identical.
public static class PlanetVisualGenerator
{
    private struct Archetype
    {
        public Color a, b, c, d, e;
        public float t0, t1, t2, t3;
        public float noiseFreq;
        public float planetMode;       // 0 = rocky, 1 = gas giant
        public float bandTurbulence;
        public float poleIceAmount;
        public float smoothness;
        public float metallic;
    }

    // Archetype indices used for moons — small airless or icy bodies, never gas giants.
    // (Mars-like, Moon-like, Mercury-like, Ice, Volcanic.)
    private static readonly int[] s_moonIndices = { 1, 2, 3, 5, 11 };

    private static readonly Archetype[] s_archetypes = BuildArchetypes();

    // Cached property IDs (project convention — see StellarData.cs).
    private static readonly int s_idA          = Shader.PropertyToID("_PaletteA");
    private static readonly int s_idB          = Shader.PropertyToID("_PaletteB");
    private static readonly int s_idC          = Shader.PropertyToID("_PaletteC");
    private static readonly int s_idD          = Shader.PropertyToID("_PaletteD");
    private static readonly int s_idE          = Shader.PropertyToID("_PaletteE");
    private static readonly int s_idT0         = Shader.PropertyToID("_Threshold0");
    private static readonly int s_idT1         = Shader.PropertyToID("_Threshold1");
    private static readonly int s_idT2         = Shader.PropertyToID("_Threshold2");
    private static readonly int s_idT3         = Shader.PropertyToID("_Threshold3");
    private static readonly int s_idFreq       = Shader.PropertyToID("_NoiseFreq");
    private static readonly int s_idSeed       = Shader.PropertyToID("_NoiseSeed");
    private static readonly int s_idMode       = Shader.PropertyToID("_PlanetMode");
    private static readonly int s_idBand       = Shader.PropertyToID("_BandTurbulence");
    private static readonly int s_idIce        = Shader.PropertyToID("_PoleIceAmount");
    private static readonly int s_idSmoothness = Shader.PropertyToID("_Smoothness");
    private static readonly int s_idMetallic   = Shader.PropertyToID("_Metallic");
    private static readonly int s_idBaseColor  = Shader.PropertyToID("_BaseColor"); // for distant LOD batching

    private static MaterialPropertyBlock s_block;

    // Apply procedural planet visuals to obj. Uses System.Random keyed on `seed` so it
    // does not disturb the global UnityEngine.Random stream the rest of generation relies on.
    // Returns the average color of the surface so the caller can store it (used by
    // GalaxyInstancedRenderer for distant LOD batching).
    public static Color Apply(GameObject obj, int seed, Material sharedPlanetMaterial, bool isMoon = false)
    {
        Renderer ren = obj.GetComponentInChildren<Renderer>();
        if (!ren) return Color.gray;

        if (sharedPlanetMaterial != null)
            ren.sharedMaterial = sharedPlanetMaterial;

        var rng = new System.Random(seed);

        int idx;
        if (isMoon)
            idx = s_moonIndices[rng.Next(0, s_moonIndices.Length)];
        else
            idx = rng.Next(0, s_archetypes.Length);

        Archetype arch = s_archetypes[idx];

        // Per-instance variation so two bodies of the same archetype never match exactly.
        Color jA = JitterColor(arch.a, rng);
        Color jB = JitterColor(arch.b, rng);
        Color jC = JitterColor(arch.c, rng);
        Color jD = JitterColor(arch.d, rng);
        Color jE = JitterColor(arch.e, rng);

        float jitterFreq = arch.noiseFreq * (1f + ((float)rng.NextDouble() - 0.5f) * 0.4f); // ±20%

        Vector4 noiseSeed = new Vector4(
            (float)rng.NextDouble() * 100f,
            (float)rng.NextDouble() * 100f,
            (float)rng.NextDouble() * 100f,
            0f
        );

        if (s_block == null) s_block = new MaterialPropertyBlock();
        ren.GetPropertyBlock(s_block);
        s_block.SetColor(s_idA, jA);
        s_block.SetColor(s_idB, jB);
        s_block.SetColor(s_idC, jC);
        s_block.SetColor(s_idD, jD);
        s_block.SetColor(s_idE, jE);
        s_block.SetFloat(s_idT0, arch.t0);
        s_block.SetFloat(s_idT1, arch.t1);
        s_block.SetFloat(s_idT2, arch.t2);
        s_block.SetFloat(s_idT3, arch.t3);
        s_block.SetFloat(s_idFreq, jitterFreq);
        s_block.SetVector(s_idSeed, noiseSeed);
        s_block.SetFloat(s_idMode, arch.planetMode);
        s_block.SetFloat(s_idBand, arch.bandTurbulence);
        s_block.SetFloat(s_idIce, arch.poleIceAmount);
        s_block.SetFloat(s_idSmoothness, arch.smoothness);
        s_block.SetFloat(s_idMetallic, arch.metallic);

        // Average color for distant LOD batching: weighted toward mid-palette colors,
        // since A and E are usually extremes (deep ocean / snow caps).
        Color avg = AverageColor(jA, jB, jC, jD, jE);
        s_block.SetColor(s_idBaseColor, avg);

        ren.SetPropertyBlock(s_block);
        return avg;
    }

    private static Color JitterColor(Color c, System.Random rng)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = Mathf.Repeat(h + ((float)rng.NextDouble() - 0.5f) * 0.04f, 1f);
        s = Mathf.Clamp01(s + ((float)rng.NextDouble() - 0.5f) * 0.10f);
        v = Mathf.Clamp01(v + ((float)rng.NextDouble() - 0.5f) * 0.15f);
        return Color.HSVToRGB(h, s, v);
    }

    private static Color AverageColor(Color a, Color b, Color c, Color d, Color e)
    {
        // Weights bias toward middle palette entries (typical surface tones).
        const float wa = 0.15f, wb = 0.25f, wc = 0.25f, wd = 0.20f, we = 0.15f;
        return new Color(
            a.r * wa + b.r * wb + c.r * wc + d.r * wd + e.r * we,
            a.g * wa + b.g * wb + c.g * wc + d.g * wd + e.g * we,
            a.b * wa + b.b * wb + c.b * wc + d.b * wd + e.b * we,
            1f);
    }

    // ---------- Archetype table ----------
    private static Color RGB(float r, float g, float b) => new Color(r, g, b, 1f);

    private static Archetype Rocky(
        Color a, Color b, Color c, Color d, Color e,
        float t0, float t1, float t2, float t3,
        float noiseFreq, float poleIce, float smoothness = 0.20f, float metallic = 0f)
        => new Archetype {
            a = a, b = b, c = c, d = d, e = e,
            t0 = t0, t1 = t1, t2 = t2, t3 = t3,
            noiseFreq = noiseFreq,
            planetMode = 0f,
            bandTurbulence = 0f,
            poleIceAmount = poleIce,
            smoothness = smoothness,
            metallic = metallic,
        };

    private static Archetype Gas(
        Color a, Color b, Color c, Color d, Color e,
        float t0, float t1, float t2, float t3,
        float noiseFreq, float bandTurbulence, float smoothness = 0.55f)
        => new Archetype {
            a = a, b = b, c = c, d = d, e = e,
            t0 = t0, t1 = t1, t2 = t2, t3 = t3,
            noiseFreq = noiseFreq,
            planetMode = 1f,
            bandTurbulence = bandTurbulence,
            poleIceAmount = 0f,
            smoothness = smoothness,
            metallic = 0f,
        };

    private static Archetype[] BuildArchetypes()
    {
        return new[]
        {
            // 0 — Earth-like: deep ocean → coast → grass → mountain → snow.
            Rocky(
                RGB(0.05f, 0.15f, 0.45f), RGB(0.15f, 0.40f, 0.65f),
                RGB(0.20f, 0.55f, 0.20f), RGB(0.45f, 0.35f, 0.20f),
                RGB(0.95f, 0.95f, 0.98f),
                0.40f, 0.45f, 0.65f, 0.85f,
                noiseFreq: 2.5f, poleIce: 0.18f),

            // 1 — Mars-like: rust palette + tiny polar caps.
            Rocky(
                RGB(0.40f, 0.15f, 0.10f), RGB(0.65f, 0.30f, 0.15f),
                RGB(0.75f, 0.50f, 0.30f), RGB(0.35f, 0.20f, 0.15f),
                RGB(0.95f, 0.92f, 0.88f),
                0.30f, 0.55f, 0.75f, 0.92f,
                noiseFreq: 2.2f, poleIce: 0.10f),

            // 2 — Moon-like: dark to light grays, high frequency.
            Rocky(
                RGB(0.18f, 0.18f, 0.20f), RGB(0.30f, 0.28f, 0.28f),
                RGB(0.50f, 0.48f, 0.46f), RGB(0.65f, 0.62f, 0.60f),
                RGB(0.85f, 0.83f, 0.80f),
                0.25f, 0.50f, 0.70f, 0.90f,
                noiseFreq: 4.0f, poleIce: 0f),

            // 3 — Mercury-like: cratered gray-brown.
            Rocky(
                RGB(0.30f, 0.25f, 0.22f), RGB(0.45f, 0.35f, 0.28f),
                RGB(0.55f, 0.45f, 0.40f), RGB(0.40f, 0.38f, 0.36f),
                RGB(0.70f, 0.65f, 0.60f),
                0.30f, 0.55f, 0.75f, 0.92f,
                noiseFreq: 4.5f, poleIce: 0f),

            // 4 — Desert: sand, ochre, rare green oasis at the top of the ramp.
            Rocky(
                RGB(0.85f, 0.70f, 0.40f), RGB(0.75f, 0.55f, 0.30f),
                RGB(0.65f, 0.40f, 0.20f), RGB(0.50f, 0.30f, 0.18f),
                RGB(0.40f, 0.50f, 0.20f),
                0.25f, 0.55f, 0.80f, 0.95f,
                noiseFreq: 2.0f, poleIce: 0.05f),

            // 5 — Ice world: icy blues to pure white, large polar caps.
            Rocky(
                RGB(0.05f, 0.10f, 0.20f), RGB(0.55f, 0.70f, 0.85f),
                RGB(0.85f, 0.90f, 0.95f), RGB(0.95f, 0.98f, 1.00f),
                RGB(1.00f, 1.00f, 1.00f),
                0.20f, 0.40f, 0.70f, 0.95f,
                noiseFreq: 2.5f, poleIce: 0.40f, smoothness: 0.45f),

            // 6 — Lava world: black basalt with glowing orange/yellow veins.
            Rocky(
                RGB(0.05f, 0.04f, 0.04f), RGB(0.20f, 0.08f, 0.04f),
                RGB(0.50f, 0.10f, 0.05f), RGB(0.95f, 0.40f, 0.10f),
                RGB(1.00f, 0.85f, 0.30f),
                0.30f, 0.55f, 0.75f, 0.92f,
                noiseFreq: 3.5f, poleIce: 0f, smoothness: 0.40f),

            // 7 — Toxic: acid greens and chemical yellows.
            Rocky(
                RGB(0.10f, 0.30f, 0.10f), RGB(0.40f, 0.65f, 0.30f),
                RGB(0.85f, 0.80f, 0.20f), RGB(0.40f, 0.30f, 0.15f),
                RGB(0.90f, 0.85f, 0.40f),
                0.25f, 0.55f, 0.75f, 0.92f,
                noiseFreq: 2.8f, poleIce: 0f),

            // 8 — Forest: dominant deep greens, small lakes.
            Rocky(
                RGB(0.10f, 0.20f, 0.40f), RGB(0.10f, 0.25f, 0.10f),
                RGB(0.15f, 0.45f, 0.20f), RGB(0.30f, 0.40f, 0.15f),
                RGB(0.45f, 0.35f, 0.20f),
                0.20f, 0.30f, 0.65f, 0.90f,
                noiseFreq: 2.3f, poleIce: 0.10f),

            // 9 — Ocean world: water dominates, archipelagos near the top of the ramp.
            Rocky(
                RGB(0.04f, 0.10f, 0.30f), RGB(0.10f, 0.30f, 0.55f),
                RGB(0.30f, 0.65f, 0.75f), RGB(0.40f, 0.55f, 0.20f),
                RGB(0.92f, 0.95f, 0.98f),
                0.45f, 0.55f, 0.75f, 0.92f,
                noiseFreq: 2.6f, poleIce: 0.12f),

            // 10 — Cracked: dark surface with narrow bright cyan veins.
            Rocky(
                RGB(0.08f, 0.08f, 0.10f), RGB(0.20f, 0.20f, 0.22f),
                RGB(0.30f, 0.80f, 0.95f), RGB(0.35f, 0.35f, 0.38f),
                RGB(0.80f, 0.90f, 0.95f),
                0.45f, 0.50f, 0.55f, 0.95f,
                noiseFreq: 5.0f, poleIce: 0.05f, smoothness: 0.50f),

            // 11 — Volcanic: black/brown lows, hot orange/yellow highs.
            Rocky(
                RGB(0.04f, 0.04f, 0.04f), RGB(0.20f, 0.12f, 0.08f),
                RGB(0.55f, 0.30f, 0.18f), RGB(0.90f, 0.45f, 0.15f),
                RGB(1.00f, 0.80f, 0.30f),
                0.40f, 0.65f, 0.80f, 0.95f,
                noiseFreq: 3.0f, poleIce: 0f, smoothness: 0.30f),

            // 12 — Jupiter-like: warm bands, strong turbulence.
            Gas(
                RGB(0.75f, 0.60f, 0.45f), RGB(0.90f, 0.82f, 0.65f),
                RGB(0.70f, 0.40f, 0.25f), RGB(0.45f, 0.30f, 0.20f),
                RGB(0.85f, 0.75f, 0.55f),
                0.25f, 0.45f, 0.60f, 0.80f,
                noiseFreq: 1.5f, bandTurbulence: 0.60f),

            // 13 — Saturn-like: pale ivory, low turbulence.
            Gas(
                RGB(0.95f, 0.90f, 0.75f), RGB(0.92f, 0.85f, 0.55f),
                RGB(0.80f, 0.70f, 0.45f), RGB(0.75f, 0.65f, 0.40f),
                RGB(0.95f, 0.92f, 0.80f),
                0.20f, 0.40f, 0.60f, 0.85f,
                noiseFreq: 1.2f, bandTurbulence: 0.25f),

            // 14 — Neptune-like: deep blue with subtle bands.
            Gas(
                RGB(0.10f, 0.20f, 0.55f), RGB(0.20f, 0.40f, 0.75f),
                RGB(0.30f, 0.55f, 0.85f), RGB(0.15f, 0.30f, 0.65f),
                RGB(0.60f, 0.75f, 0.95f),
                0.25f, 0.50f, 0.65f, 0.85f,
                noiseFreq: 1.0f, bandTurbulence: 0.30f, smoothness: 0.65f),

            // 15 — Hot Jupiter: scorched red-orange.
            Gas(
                RGB(0.60f, 0.10f, 0.05f), RGB(0.90f, 0.40f, 0.10f),
                RGB(0.95f, 0.65f, 0.20f), RGB(0.85f, 0.25f, 0.10f),
                RGB(1.00f, 0.85f, 0.30f),
                0.20f, 0.45f, 0.65f, 0.85f,
                noiseFreq: 1.5f, bandTurbulence: 0.70f),

            // 16 — Cyan storm: stormy turquoise/white.
            Gas(
                RGB(0.10f, 0.40f, 0.50f), RGB(0.30f, 0.70f, 0.80f),
                RGB(0.70f, 0.90f, 0.95f), RGB(0.20f, 0.55f, 0.65f),
                RGB(0.95f, 0.98f, 1.00f),
                0.25f, 0.45f, 0.60f, 0.85f,
                noiseFreq: 1.3f, bandTurbulence: 0.50f),

            // 17 — Purple gas: violet/magenta exotic.
            Gas(
                RGB(0.30f, 0.10f, 0.40f), RGB(0.60f, 0.20f, 0.55f),
                RGB(0.85f, 0.65f, 0.85f), RGB(0.45f, 0.25f, 0.60f),
                RGB(0.95f, 0.85f, 0.95f),
                0.25f, 0.45f, 0.65f, 0.88f,
                noiseFreq: 1.2f, bandTurbulence: 0.45f),
        };
    }
}
