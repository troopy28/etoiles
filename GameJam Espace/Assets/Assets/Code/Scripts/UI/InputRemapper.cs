using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public static class InputRemapper
{
	public static Dictionary<string, Key> Binds = new Dictionary<string, Key>();
	public static Dictionary<string, Key> Defaults = new Dictionary<string, Key>()
	{
		{ "FORWARD", Key.W },
		{ "BACKWARD", Key.S },
		{ "LEFT", Key.A },
		{ "RIGHT", Key.D },
		{ "UP", Key.R },
		{ "DOWN", Key.F },
		{ "BOOST", Key.LeftShift },
		{ "BRAKE", Key.B },
		{ "TRAJECTORY", Key.T },
		{ "YAW_LEFT", Key.Q },
		{ "YAW_RIGHT", Key.E },
		{ "MODIFIER", Key.LeftCtrl },
		{ "AUTOPILOT", Key.Space },
		{ "TARGET_ARRIVAL", Key.Digit1 },
		{ "TARGET_REFUEL", Key.Digit2 }
	};

	public static void Load()
	{
		if (Binds.Count > 0) return;
		foreach (var kvp in Defaults)
		{
			Binds[kvp.Key] = (Key)PlayerPrefs.GetInt("Bind_" + kvp.Key, (int)kvp.Value);
		}
	}

	public static void ResetBind(string action)
	{
		if (Defaults.ContainsKey(action))
		{
			SetBind(action, Defaults[action]);
		}
	}

	public static void SetBind(string action, Key key)
	{
		Binds[action] = key;
		PlayerPrefs.SetInt("Bind_" + action, (int)key);
	}

	public static bool GetKey(string action)
	{
		if (!Binds.ContainsKey(action)) return false;
		var kb = Keyboard.current;
		if (kb == null) return false;
		return kb[Binds[action]].isPressed;
	}
	
	public static bool GetKeyDown(string action)
	{
		if (!Binds.ContainsKey(action)) return false;
		var kb = Keyboard.current;
		if (kb == null) return false;
		return kb[Binds[action]].wasPressedThisFrame;
	}
}
