using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class BuildScript
{
    [MenuItem("Build/Build All (Win, Linux, Mac)")]
    public static void BuildAll()
    {
        BuildWindows();
        BuildLinux();
        BuildMacOS();
    }

    [MenuItem("Build/Build Windows")]
    public static void BuildWindows()
    {
        string version = PlayerSettings.bundleVersion;
        string buildPath = $"Builds/v{version}/Windows/Oort.exe";
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenes();
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        Debug.Log("Building Windows...");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Windows Build Complete: " + buildPath);
    }

    [MenuItem("Build/Build Linux")]
    public static void BuildLinux()
    {
        string version = PlayerSettings.bundleVersion;
        string buildPath = $"Builds/v{version}/Linux/Oort.x86_64";
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenes();
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.StandaloneLinux64;
        buildPlayerOptions.options = BuildOptions.None;

        Debug.Log("Building Linux...");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Linux Build Complete: " + buildPath);
    }

    [MenuItem("Build/Build MacOS")]
    public static void BuildMacOS()
    {
        string version = PlayerSettings.bundleVersion;
        string buildPath = $"Builds/v{version}/Mac/Oort.app";
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenes();
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.StandaloneOSX;
        buildPlayerOptions.options = BuildOptions.None;

        Debug.Log("Building MacOS...");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("MacOS Build Complete: " + buildPath);
    }

    private static string[] GetScenes()
    {
        return new string[]
        {
            "Assets/Assets/Level/Scenes/MainMenu.unity",
            "Assets/Assets/Level/Scenes/Galaxie.unity"
        };
    }
}
