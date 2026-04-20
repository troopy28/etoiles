using UnityEngine;

[CreateAssetMenu(fileName = "GenerationSettings", menuName = "SpaceGame/Generation Settings")]
public class GenerationSettings : ScriptableObject
{
	[Header("Seed")]
	[Tooltip("0 = aléatoire. Toute autre valeur fixe la génération de façon déterministe.")]
	public int seed = 0;

	[Header("Galaxy Layout")]
	public int starSystemCount = 150;
	public float corridorLength = 8000f;
	public float corridorRadius = 300f;
	public float minStarDistance = 150f;

	[Header("Scale & Physics")]
	[Tooltip("Multiplie les masses dans le simulateur physique. Doit être identique au multiplicateur utilisé pour les vitesses orbitales. 1e12 = orbites visibles à des distances de 100-500u.")]
	public float gameGravityMassMultiplier = 1e12f;
	public float visualScaleMultiplier = 5.0f;
	public float maxSystemDriftVelocity = 0.05f;

	[Header("Generation Probabilities")]
	[Range(0, 1)] public float binaryStarChance = 0.3f;
	[Range(0, 1)] public float cometChance = 0.3f;
	[Range(0, 1)] public float asteroidBeltChance = 0.4f;
	[Range(0, 1)] public float moonChance = 0.6f;
	[Range(0, 1)] public float ringChance = 0.5f;

	[Header("Planetary System")]
	public Vector2Int planetCountRange = new Vector2Int(2, 7);
	public Vector2 planetSpacingRange = new Vector2(100f, 180f);
	public float binarySystemStartDist = 250f;
	public float singleSystemStartDist = 80f;

	[Header("Celestial Specifics")]
	public float asteroidBeltVariance = 20f;
	public float cometDistanceRangeMin = 300f;
	public float cometDistanceRangeMax = 600f;
	public float cometSpeedMultiplier = 1.3f;
}
