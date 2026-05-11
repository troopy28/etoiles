using UnityEngine;
using System.Collections.Generic;

public static class LocalizationManager
{
	public enum Language { EN, FR }
	public static Language CurrentLanguage = Language.EN;

	private static Dictionary<string, Dictionary<Language, string>> m_table = new Dictionary<string, Dictionary<Language, string>>()
	{
		// Global
		{ "BACK", new Dictionary<Language, string> { { Language.EN, "BACK" }, { Language.FR, "RETOUR" } } },
		{ "<< MAIN_MENU", new Dictionary<Language, string> { { Language.EN, "<< MAIN MENU" }, { Language.FR, "<< MENU PRINCIPAL" } } },
		{ "CONFIRM", new Dictionary<Language, string> { { Language.EN, "CONFIRM" }, { Language.FR, "CONFIRMER" } } },
		{ "CANCEL", new Dictionary<Language, string> { { Language.EN, "CANCEL" }, { Language.FR, "ANNULER" } } },
		{ "ON", new Dictionary<Language, string> { { Language.EN, "ON" }, { Language.FR, "OUI" } } },
		{ "OFF", new Dictionary<Language, string> { { Language.EN, "OFF" }, { Language.FR, "NON" } } },
		{ "RETURN", new Dictionary<Language, string> { { Language.EN, "RETURN" }, { Language.FR, "RETOUR" } } },
		{ "MODIFIER", new Dictionary<Language, string> { { Language.EN, "MODIFIER" }, { Language.FR, "MODIFICATEUR" } } },
		{ "AUTOPILOT", new Dictionary<Language, string> { { Language.EN, "AUTOPILOT" }, { Language.FR, "PILOTE AUTO" } } },
		{ "TARGET_ARRIVAL", new Dictionary<Language, string> { { Language.EN, "TARGET: ARRIVAL" }, { Language.FR, "CIBLE: ARRIVÉE" } } },
		{ "TARGET_REFUEL", new Dictionary<Language, string> { { Language.EN, "TARGET: REFUEL" }, { Language.FR, "CIBLE: RAVITAILLEMENT" } } },
		{ "ARRIVAL", new Dictionary<Language, string> { { Language.EN, "ARRIVAL" }, { Language.FR, "ARRIVÉE" } } },
		{ "REFUEL", new Dictionary<Language, string> { { Language.EN, "REFUEL" }, { Language.FR, "RAVITAILLEMENT" } } },
		{ "AUDIO PROTOCOL", new Dictionary<Language, string> { { Language.EN, "AUDIO PROTOCOL" }, { Language.FR, "PROTOCOLE AUDIO" } } },
		{ "NEURAL INTERFACE", new Dictionary<Language, string> { { Language.EN, "NEURAL INTERFACE" }, { Language.FR, "INTERFACE NEURALE" } } },
		{ "CONTROL MAPPING", new Dictionary<Language, string> { { Language.EN, "CONTROL MAPPING" }, { Language.FR, "CONFIGURATION TOUCHES" } } },
		{ "GRAPHICS PROTOCOL", new Dictionary<Language, string> { { Language.EN, "GRAPHICS PROTOCOL" }, { Language.FR, "PROTOCOLE GRAPHIQUE" } } },

		// Main Menu
		{ "LAUNCH MISSION", new Dictionary<Language, string> { { Language.EN, "LAUNCH MISSION" }, { Language.FR, "LANCER MISSION" } } },
		{ "SYSTEM SETTINGS", new Dictionary<Language, string> { { Language.EN, "SYSTEM SETTINGS" }, { Language.FR, "PARAMÈTRES SYSTÈME" } } },
		{ "QUIT", new Dictionary<Language, string> { { Language.EN, "QUIT" }, { Language.FR, "QUITTER" } } },

		// Tabs
		{ "GRAPHICS", new Dictionary<Language, string> { { Language.EN, "GRAPHICS" }, { Language.FR, "GRAPHISMES" } } },
		{ "AUDIO", new Dictionary<Language, string> { { Language.EN, "AUDIO" }, { Language.FR, "AUDIO" } } },
		{ "GAMEPLAY", new Dictionary<Language, string> { { Language.EN, "GAMEPLAY" }, { Language.FR, "JOUABILITÉ" } } },
		{ "CONTROLS", new Dictionary<Language, string> { { Language.EN, "CONTROLS" }, { Language.FR, "TOUCHES" } } },

		// Graphics Settings
		{ "RESOLUTION", new Dictionary<Language, string> { { Language.EN, "RESOLUTION" }, { Language.FR, "RÉSOLUTION" } } },
		{ "FULLSCREEN", new Dictionary<Language, string> { { Language.EN, "FULLSCREEN" }, { Language.FR, "PLEIN ÉCRAN" } } },
		{ "V-SYNC", new Dictionary<Language, string> { { Language.EN, "V-SYNC" }, { Language.FR, "V-SYNC" } } },
		{ "QUALITY", new Dictionary<Language, string> { { Language.EN, "QUALITY" }, { Language.FR, "QUALITÉ" } } },
		{ "LOW", new Dictionary<Language, string> { { Language.EN, "LOW" }, { Language.FR, "BASSE" } } },
		{ "MED", new Dictionary<Language, string> { { Language.EN, "MED" }, { Language.FR, "MOY." } } },
		{ "ULTRA", new Dictionary<Language, string> { { Language.EN, "ULTRA" }, { Language.FR, "ULTRA" } } },
		{ "SHOW FPS", new Dictionary<Language, string> { { Language.EN, "SHOW FPS" }, { Language.FR, "VOIR FPS" } } },

		// Gameplay Settings
		{ "MOUSE SENSITIVITY", new Dictionary<Language, string> { { Language.EN, "MOUSE SENSITIVITY" }, { Language.FR, "SENSIBILITÉ SOURIS" } } },
		{ "FIELD OF VIEW", new Dictionary<Language, string> { { Language.EN, "FIELD OF VIEW" }, { Language.FR, "CHAMP DE VISION" } } },
		{ "LANGUAGE", new Dictionary<Language, string> { { Language.EN, "LANGUAGE" }, { Language.FR, "LANGUE" } } },
		{ "RESPONSE LAG", new Dictionary<Language, string> { { Language.EN, "RESPONSE LAG" }, { Language.FR, "DÉLAI RÉPONSE" } } },
		{ "VIEW ANGLE", new Dictionary<Language, string> { { Language.EN, "VIEW ANGLE" }, { Language.FR, "ANGLE DE VUE" } } },
		{ "ENGINE QUALITY", new Dictionary<Language, string> { { Language.EN, "ENGINE QUALITY" }, { Language.FR, "QUALITÉ MOTEUR" } } },
		{ "MASTER GAIN", new Dictionary<Language, string> { { Language.EN, "MASTER GAIN" }, { Language.FR, "GAIN GÉNÉRAL" } } },
		{ "INVERT Y-AXIS", new Dictionary<Language, string> { { Language.EN, "INVERT Y-AXIS" }, { Language.FR, "INVERSER AXE Y" } } },

		// Pause Menu
		{ "SYSTEM PAUSED", new Dictionary<Language, string> { { Language.EN, "SYSTEM PAUSED" }, { Language.FR, "SYSTÈME PAUSE" } } },
		{ "> MISSION ON HOLD", new Dictionary<Language, string> { { Language.EN, "> MISSION ON HOLD" }, { Language.FR, "> MISSION EN ATTENTE" } } },
		{ "RESUME MISSION", new Dictionary<Language, string> { { Language.EN, "RESUME MISSION" }, { Language.FR, "REPRENDRE MISSION" } } },
		{ "RESTART MISSION", new Dictionary<Language, string> { { Language.EN, "RESTART MISSION" }, { Language.FR, "RECOMMENCER MISSION" } } },
		{ "ABANDON MISSION", new Dictionary<Language, string> { { Language.EN, "QUIT TO MENU" }, { Language.FR, "MENU PRINCIPAL" } } },

		// Briefing
		{ "TRADE GUILD - MISSION ORDER", new Dictionary<Language, string> { { Language.EN, "TRADE GUILD - MISSION ORDER" }, { Language.FR, "GUILDE MARCHANDE - ORDRE DE MISSION" } } },
		{ "INITIALIZE_SYSTEM", new Dictionary<Language, string> { { Language.EN, "INITIALIZE_SYSTEM" }, { Language.FR, "INITIALISER_SYSTÈME" } } },
		
		// Controls
		{ "FORWARD", new Dictionary<Language, string> { { Language.EN, "FORWARD" }, { Language.FR, "AVANCER" } } },
		{ "BACKWARD", new Dictionary<Language, string> { { Language.EN, "BACKWARD" }, { Language.FR, "RECULER" } } },
		{ "LEFT", new Dictionary<Language, string> { { Language.EN, "LEFT" }, { Language.FR, "GAUCHE" } } },
		{ "RIGHT", new Dictionary<Language, string> { { Language.EN, "RIGHT" }, { Language.FR, "DROITE" } } },
		{ "UP", new Dictionary<Language, string> { { Language.EN, "UP" }, { Language.FR, "HAUT" } } },
		{ "DOWN", new Dictionary<Language, string> { { Language.EN, "DOWN" }, { Language.FR, "BAS" } } },
		{ "BOOST", new Dictionary<Language, string> { { Language.EN, "BOOST" }, { Language.FR, "BOOST" } } },
		{ "BRAKE", new Dictionary<Language, string> { { Language.EN, "BRAKE" }, { Language.FR, "FREIN" } } },
		{ "TRAJECTORY", new Dictionary<Language, string> { { Language.EN, "TRAJECTORY" }, { Language.FR, "TRAJECTOIRE" } } },
		{ "YAW_LEFT", new Dictionary<Language, string> { { Language.EN, "YAW LEFT" }, { Language.FR, "LACET GAUCHE" } } },
		{ "YAW_RIGHT", new Dictionary<Language, string> { { Language.EN, "YAW RIGHT" }, { Language.FR, "LACET DROITE" } } },

		// HUD & Radar
		{ "NO TARGET", new Dictionary<Language, string> { { Language.EN, "NO TARGET" }, { Language.FR, "AUCUNE CIBLE" } } },
		{ "TURN AROUND", new Dictionary<Language, string> { { Language.EN, "TURN AROUND" }, { Language.FR, "DEMI-TOUR" } } },
		{ "DIST", new Dictionary<Language, string> { { Language.EN, "DIST" }, { Language.FR, "DIST" } } },
		{ "VEL", new Dictionary<Language, string> { { Language.EN, "VEL" }, { Language.FR, "VEL" } } },
		{ "CLASS", new Dictionary<Language, string> { { Language.EN, "CLASS" }, { Language.FR, "CLASSE" } } },
		{ "ENGAGE ORBIT", new Dictionary<Language, string> { { Language.EN, "[SPACE] ENGAGE ORBIT" }, { Language.FR, "[ESPACE] ORBITE" } } },
		{ "OUT OF RANGE", new Dictionary<Language, string> { { Language.EN, "[SPACE] OUT OF RANGE" }, { Language.FR, "[ESPACE] HORS PORTÉE" } } },
		{ "MANUAL", new Dictionary<Language, string> { { Language.EN, "MANUAL" }, { Language.FR, "MANUEL" } } },
		{ "BOOST_LABEL", new Dictionary<Language, string> { { Language.EN, "BOOST (SHIFT) : " }, { Language.FR, "BOOST (MAJ) : " } } },
		{ "BRAKING_LABEL", new Dictionary<Language, string> { { Language.EN, "BRAKING (B) : " }, { Language.FR, "FREIN (B) : " } } },
		{ "TRAJECTORY_LABEL", new Dictionary<Language, string> { { Language.EN, "TRAJECTORY (T) : " }, { Language.FR, "TRAJECTOIRE (T) : " } } },
		{ "FUEL", new Dictionary<Language, string> { { Language.EN, "FUEL" }, { Language.FR, "CARBURANT" } } },
		{ "REFUELING", new Dictionary<Language, string> { { Language.EN, "REFUELING" }, { Language.FR, "RAVITAILLEMENT" } } },
		{ "TEMP", new Dictionary<Language, string> { { Language.EN, "TEMP" }, { Language.FR, "TEMP" } } },
		{ "OVERHEAT", new Dictionary<Language, string> { { Language.EN, "OVERHEAT" }, { Language.FR, "SURCHAUFFE" } } },

		// Mission Status
		{ "MISSION FAILED", new Dictionary<Language, string> { { Language.EN, "MISSION FAILED" }, { Language.FR, "ÉCHEC DE LA MISSION" } } },
		{ "DELIVERY SUCCESSFUL", new Dictionary<Language, string> { { Language.EN, "DELIVERY SUCCESSFUL" }, { Language.FR, "LIVRAISON RÉUSSIE" } } },
		{ "STABILITY MAINTAINED", new Dictionary<Language, string> { { Language.EN, "Stability maintained. Resources have reached the destination point." }, { Language.FR, "Stabilité maintenue. Les ressources sont arrivées à destination." } } },
		{ "ABANDON TO HANGAR", new Dictionary<Language, string> { { Language.EN, "ABANDON TO HANGAR" }, { Language.FR, "RETOUR AU HANGAR" } } },
		{ "STATUS", new Dictionary<Language, string> { { Language.EN, "STATUS" }, { Language.FR, "STATUT" } } },
		{ "MISSION ON HOLD", new Dictionary<Language, string> { { Language.EN, "> MISSION ON HOLD" }, { Language.FR, "> MISSION EN ATTENTE" } } },
		{ "ABANDON ?", new Dictionary<Language, string> { { Language.EN, "ABANDON ?" }, { Language.FR, "ABANDONNER ?" } } },
		{ "CONFIRM HANGAR RETURN", new Dictionary<Language, string> { { Language.EN, "> CONFIRM HANGAR RETURN" }, { Language.FR, "> CONFIRMER LE RETOUR" } } },
		{ "STARS", new Dictionary<Language, string> { { Language.EN, "STARS" }, { Language.FR, "ÉTOILES" } } },
		{ "NAVIGATION SYSTEM", new Dictionary<Language, string> { { Language.EN, "NAVIGATION SYSTEM" }, { Language.FR, "SYSTÈME DE NAVIGATION" } } },

		// Briefing
		{ "BRIEFING_TITLE", new Dictionary<Language, string> { { Language.EN, "CLASSIFIED // MISSION BRIEFING" }, { Language.FR, "CLASSIFIÉ // BRIEFING DE MISSION" } } },
		{ "BRIEFING_FOOTER", new Dictionary<Language, string> { { Language.EN, "[ STATUS: STANDBY // ENCRYPTION: ACTIVE // AUTHORIZATION: GRANTED ]" }, { Language.FR, "[ STATUT : ATTENTE // CRYPTAGE : ACTIF // AUTORISATION : ACCORDÉE ]" } } },
		{ "BRIEFING_BODY", new Dictionary<Language, string> { 
			{ Language.EN, "The once-safe space routes have vanished. The population has abandoned the idea of crossing solar systems. The stars themselves have become unstable, breaking trajectories.\n\nYOUR MISSION:\nCross these unstable zones to deliver [VITAL CARGO].\n\nNAVIGATION REMINDER:\n• Stellar instability deviates your trajectory: adapt constantly.\n• G-forces and heat will destroy your cargo.\n• Manage your resources. Nothing is stable. Good luck, Pilot." }, 
			{ Language.FR, "Les routes spatiales autrefois sûres ont disparu. La population a abandonné l'idée de traverser les systèmes solaires. Les étoiles elles-mêmes sont devenues instables, brisant les trajectoires.\n\nVOTRE MISSION :\nTraversez ces zones instables pour livrer la [CARGAISON VITALE].\n\nRAPPEL DE NAVIGATION :\n• L'instabilité stellaire dévie votre trajectoire : adaptez-vous constamment.\n• Les forces G et la chaleur détruiront votre cargaison.\n• Gérez vos ressources. Rien n'est stable. Bonne chance, Pilote." } 
		} },

		// Death Reasons
		{ "Critical hull overheating", new Dictionary<Language, string> { { Language.EN, "Critical hull overheating" }, { Language.FR, "Surchauffe critique de la coque" } } },
		{ "Total energy reserves depleted", new Dictionary<Language, string> { { Language.EN, "Total energy reserves depleted" }, { Language.FR, "Réserves d'énergie épuisées" } } },
		{ "Your ship was lost: collision with ", new Dictionary<Language, string> { { Language.EN, "Your ship was lost: collision with " }, { Language.FR, "Vaisseau perdu : collision avec " } } },
		{ "Cargo has been destroyed: ", new Dictionary<Language, string> { { Language.EN, "Cargo has been destroyed: " }, { Language.FR, "Cargaison détruite : " } } },

		// Body Kinds
		{ "PLANET", new Dictionary<Language, string> { { Language.EN, "PLANET" }, { Language.FR, "PLANÈTE" } } },
		{ "STAR", new Dictionary<Language, string> { { Language.EN, "STAR" }, { Language.FR, "ÉTOILE" } } },
		{ "MOON", new Dictionary<Language, string> { { Language.EN, "MOON" }, { Language.FR, "LUNE" } } },
		{ "ASTEROID", new Dictionary<Language, string> { { Language.EN, "ASTEROID" }, { Language.FR, "ASTÉROÏDE" } } },
		{ "COMET", new Dictionary<Language, string> { { Language.EN, "COMET" }, { Language.FR, "COMÈTE" } } },
		{ "WRECK", new Dictionary<Language, string> { { Language.EN, "WRECK" }, { Language.FR, "ÉPAVE" } } },
		{ "OTHER", new Dictionary<Language, string> { { Language.EN, "OTHER" }, { Language.FR, "AUTRE" } } }
	};

	public static string Get(string key)
	{
		if (m_table.ContainsKey(key))
		{
			return m_table[key][CurrentLanguage];
		}
		return key;
	}

	public static void SetLanguage(Language lang)
	{
		CurrentLanguage = lang;
		PlayerPrefs.SetInt("GameLanguage", (int)lang);
		
		var locs = Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include);
		foreach (var l in locs) l.Refresh();
	}

	public static void Load()
	{
		CurrentLanguage = (Language)PlayerPrefs.GetInt("GameLanguage", (int)Language.EN);
	}
}
