using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoyalBuddies.EditorTools
{
    /// <summary>
    /// Outils de vérification en ligne de commande (lancer SANS -nographics pour pouvoir rendre) :
    ///   RenderScene : ouvre une scène, rend la caméra principale dans un PNG (Logs/shot_*.png).
    /// </summary>
    public static class RBVerify
    {
        public static void RenderBattleStatic() { RenderScene("Assets/RoyalBuddies/Scenes/RB_Battle.unity", "Logs/shot_battle_static.png", 720, 1560); }

        [MenuItem("Royal Buddies/Verify/Render Battle scene to PNG")]
        public static void RenderBattleMenu() { RenderBattleStatic(); }

        public static void RenderScene(string scenePath, string outPng, int w, int h)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("Pas de caméra principale dans " + scenePath); return; }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(outPng));
            File.WriteAllBytes(outPng, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log("[RBVerify] Rendu écrit : " + outPng);
        }
    }
}
