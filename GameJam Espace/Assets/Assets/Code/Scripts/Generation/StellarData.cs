using Assets.Code.Scripts.Generation;
using UnityEngine;

public struct StarProperties {
	public StellarClass sc;
	public float mass;
	public float radius;
	public float temperature;
	public float luminosity;
	public Color color;
}

public static class StellarMath {
    public struct BodyRef
    {
        public SimGravityBody body;
        public int bodyID;
        public Renderer renderer;
        public Mesh mesh;
        public Material material;
        public Color color;
        public float emission;
    }

    static StellarClass SampleStellarClass()
    {
	    float probability = Random.value;

	    if (probability < 0.0003f)
	    {
		    if (Random.value < 0.3f)
		    {
			    return StellarClass.Remnant_BlackHole;
		    }
		    if (Random.value < 0.1f)
		    {
			    return StellarClass.Remnant_WhiteHole;
		    }
		    return StellarClass.O;
	    }
	    if (probability < 0.013f)
	    {
		    if (Random.value < 0.2f)
		    {
			    return StellarClass.Remnant_NeutronStar;
		    }
		    return StellarClass.B;
	    }
	    if (probability < 0.073f)
	    {
		    if (Random.value < 0.1f)
		    {
			    return StellarClass.Remnant_WhiteDwarf;
		    }
		    return StellarClass.A;
	    }
	    if (probability < 0.15f)
	    {
		    return StellarClass.F;
	    }
	    if (probability < 0.30f)
	    {
		    return StellarClass.G;
	    }
	    if (probability < 0.50f)
	    {
		    return StellarClass.K;
	    }
	    return StellarClass.M;
    }
    static float SampleMass(StellarClass sc)
    {
	    switch (sc)
	    {
		    case StellarClass.O: return Random.Range(16f, 60f);
		    case StellarClass.B: return Random.Range(2.1f, 16f); 
		    case StellarClass.A: return Random.Range(1.4f, 2.1f);
		    case StellarClass.F: return Random.Range(1.04f, 1.4f); 
		    case StellarClass.G: return Random.Range(0.8f, 1.04f);
		    case StellarClass.K: return Random.Range(0.45f, 0.8f);
		    case StellarClass.M: return Random.Range(0.08f, 0.45f);
		    case StellarClass.Remnant_BlackHole: return Random.Range(10f, 40f);
		    case StellarClass.Remnant_WhiteHole: return -Random.Range(20f, 60f);
		    case StellarClass.Remnant_NeutronStar: return Random.Range(1.4f, 3f);
		    case StellarClass.Remnant_WhiteDwarf: return Random.Range(0.6f, 1.4f);
		    default:
			    // Erreur.
			    return 0.0f;
	    }
    }
    
	public static StarProperties GetRandomStarProperties(float? forcedMass = null)
	{
		StarProperties props = new StarProperties();
		
		// 1. Sample the class.
		props.sc = SampleStellarClass();

		// 2. Then its mass.
		if (forcedMass.HasValue)
		{
			props.mass = Mathf.Abs(forcedMass.Value);
		}
		else
		{
			props.mass = SampleMass(props.sc);
		}

		// 3. Then derive the other properties from the mass.
		props.radius = (props.mass < 1.0f) ? Mathf.Pow(props.mass, 0.5f) : Mathf.Pow(props.mass, 0.8f);
		props.luminosity = Mathf.Pow(props.mass, 3.5f);
		props.temperature = 5778f * Mathf.Pow(props.luminosity / (props.radius * props.radius), 0.25f);

		// Hack: make these smaller.
		if (props.sc == StellarClass.Remnant_BlackHole 
		    || props.sc == StellarClass.Remnant_NeutronStar
		    || props.sc == StellarClass.Remnant_WhiteHole)
		{
			props.radius *= 0.1f;
		}
		
		// Same here.
		if (props.sc == StellarClass.Remnant_WhiteDwarf)
		{
			props.radius *= 0.3f;
		}

		props.color = GetStarColor(props.temperature, props.sc);
		return props;
	}

	public static Color GetStarColor(float temp, StellarClass sc)
	{
		if (sc == StellarClass.Remnant_BlackHole) return Color.black; 
		if (sc == StellarClass.Remnant_WhiteHole) return Color.white;
		if (sc == StellarClass.Remnant_NeutronStar) return new Color(0.7f, 0.4f, 1.0f);
		if (sc == StellarClass.Remnant_WhiteDwarf) return new Color(0.9f, 0.95f, 1.0f);

		temp /= 100f;
		float r, g, b;

		if (temp <= 66) r = 255;
		else {
			r = temp - 60;
			r = 329.698727446f * Mathf.Pow(r, -0.1332047592f);
		}

		if (temp <= 66) {
			g = temp;
			g = 99.4708025861f * Mathf.Log(g) - 161.1195681661f;
		}
		else {
			g = temp - 60;
			g = 288.1221695283f * Mathf.Pow(g, -0.0755148492f);
		}

		if (temp >= 66) b = 255;
		else if (temp <= 19) b = 0;
		else {
			b = temp - 10;
			b = 138.5177312231f * Mathf.Log(b) - 305.0447927307f;
		}

		return new Color(
			Mathf.Clamp01(r / 255f),
			Mathf.Clamp01(g / 255f),
			Mathf.Clamp01(b / 255f)
		);
	}

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static MaterialPropertyBlock sharedPropBlock;

    public static void MatchMaterialColor(GameObject obj, Color color, float emissionIntensity)
    {
        Renderer ren = obj.GetComponentInChildren<Renderer>();
        if (ren)
        {
            if (sharedPropBlock == null) sharedPropBlock = new MaterialPropertyBlock();
            ren.GetPropertyBlock(sharedPropBlock);
            sharedPropBlock.SetColor(BaseColorId, color);
            sharedPropBlock.SetColor(ColorId, color);
            if (emissionIntensity > 0f)
            {
                if (ren.sharedMaterial && !ren.sharedMaterial.IsKeywordEnabled("_EMISSION"))
                    ren.sharedMaterial.EnableKeyword("_EMISSION");
                Color hdrColor = new Color(color.r * emissionIntensity, color.g * emissionIntensity, color.b * emissionIntensity, 1f);
                sharedPropBlock.SetColor(EmissionColorId, hdrColor);
            }
            ren.SetPropertyBlock(sharedPropBlock);
        }
    }
}
