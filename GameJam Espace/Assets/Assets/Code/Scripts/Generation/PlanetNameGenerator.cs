public static class PlanetNameGenerator
{
	private static readonly string[] GreekPrefixes =
	{
		"Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
		"Iota", "Kappa", "Lambda", "Mu", "Nu", "Xi", "Omicron", "Pi", "Rho",
		"Sigma", "Tau", "Upsilon", "Phi", "Chi", "Psi", "Omega",
		"Proxima", "Nova", "Vega", "Rigel"
	};

	private static readonly string[] Constellations =
	{
		"Centauri", "Eridani", "Ceti", "Persei", "Aquilae", "Cygni", "Lyrae",
		"Aurigae", "Draconis", "Pegasi", "Andromedae", "Cassiopeiae", "Bootis",
		"Tauri", "Geminorum", "Leonis", "Virginis", "Scorpii", "Sagittarii",
		"Aquarii", "Piscium", "Hydrae", "Coronae", "Serpentis", "Ursae",
		"Orionis", "Lupi", "Corvi", "Arae", "Lyncis"
	};

	private static readonly string[] Roots =
	{
		"Zeph", "Vex", "Kryp", "Xan", "Naer", "Olor", "Thal", "Drac", "Quor",
		"Aer", "Heli", "Astr", "Cron", "Pyr", "Lyr", "Nox", "Mor", "Tyr",
		"Vald", "Eryn", "Kael", "Sol", "Ner", "Phae", "Sylv", "Umbr", "Vor",
		"Zan", "Cind", "Fero", "Glim", "Hex", "Iron", "Jax", "Kol", "Mar"
	};

	private static readonly string[] Suffixes =
	{
		"aris", "orion", "ion", "ix", "us", "or", "is", "ar", "on", "ya",
		"axis", "ova", "eth", "ax", "ius", "ana", "eus", "ara", "yth", "oth"
	};

	private static readonly string[] Catalogs =
	{
		"Kepler", "HD", "TRAPPIST", "Gliese", "Wolf", "Ross", "Luyten",
		"Tycho", "XO", "K2", "TOI", "HAT-P"
	};

	private static readonly string[] RomanNumerals =
	{
		"I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"
	};

	private static readonly string[] OrbitalLetters =
	{
		"b", "c", "d", "e", "f", "g", "h"
	};

	private static readonly string[] Qualifiers =
	{
		"Prime", "Major", "Minor", "Secundus", "Tertius", "Borealis", "Australis"
	};

	public static string GeneratePlanet(int seed)
	{
		var rng = new System.Random(seed);
		string body = Pick(rng, Roots) + Pick(rng, Suffixes);

		switch (rng.Next(6))
		{
			case 0: return body;
			case 1: return body + " " + Pick(rng, RomanNumerals);
			case 2: return Pick(rng, GreekPrefixes) + " " + body;
			case 3: return Pick(rng, GreekPrefixes) + " " + body + " " + Pick(rng, OrbitalLetters);
			case 4: return Pick(rng, Catalogs) + "-" + rng.Next(10, 9999) + Pick(rng, OrbitalLetters);
			default: return body + " " + Pick(rng, Qualifiers);
		}
	}

	public static string GenerateStar(int seed)
	{
		var rng = new System.Random(seed);

		switch (rng.Next(5))
		{
			case 0: return Pick(rng, GreekPrefixes) + " " + Pick(rng, Constellations);
			case 1: return Pick(rng, Catalogs) + "-" + rng.Next(10, 9999);
			case 2: return Pick(rng, GreekPrefixes) + " " + Pick(rng, Roots) + Pick(rng, Suffixes);
			case 3: return Pick(rng, Roots) + Pick(rng, Suffixes);
			default: return Pick(rng, Roots) + Pick(rng, Suffixes) + " " + Pick(rng, Constellations);
		}
	}

	public static string GenerateMoon(int seed)
	{
		var rng = new System.Random(seed);
		string body = Pick(rng, Roots) + Pick(rng, Suffixes);

		switch (rng.Next(3))
		{
			case 0: return body;
			case 1: return body + " " + Pick(rng, RomanNumerals);
			default: return body + " " + Pick(rng, Qualifiers);
		}
	}

	private static string Pick(System.Random rng, string[] table)
	{
		return table[rng.Next(table.Length)];
	}
}
