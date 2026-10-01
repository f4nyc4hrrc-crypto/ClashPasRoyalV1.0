using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoyalBuddies.EditorTools
{
    /// <summary>Build WebGL de test (lancé en ligne de commande) pour jouer depuis Safari / iPhone.</summary>
    public static class RBWebBuild
    {
        public static void Build()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;   // pas de compression : servable par un simple serveur statique
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/RoyalBuddies/Scenes/RB_Menu.unity", "Assets/RoyalBuddies/Scenes/RB_Battle.unity", "Assets/RoyalBuddies/Scenes/RB_Sandbox.unity" },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[RBWebBuild] " + r.summary.result + " taille=" + r.summary.totalSize + " erreurs=" + r.summary.totalErrors);
            if (r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
