#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RB_WebGLBuild
{
    [MenuItem("Royal Buddies/Build/WebGL Production")]
    public static void BuildWebGL()
    {
        string[] scenes = {
            "Assets/RoyalBuddies/Scenes/RB_Menu.unity",
            "Assets/RoyalBuddies/Scenes/RB_Battle.unity",
            "Assets/RoyalBuddies/Scenes/RB_Sandbox.unity"
        };

        foreach (string scene in scenes)
            if (!File.Exists(scene)) throw new FileNotFoundException("Missing scene", scene);

        Directory.CreateDirectory("Builds/WebGL");
        var options = new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("WebGL build failed: " + report.summary.result);
        Debug.Log($"Royal Buddies WebGL build OK: {report.summary.totalSize / (1024f*1024f):F1} MB");
    }
}
#endif
