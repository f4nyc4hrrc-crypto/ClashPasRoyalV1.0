using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoyalBuddies.Tests
{
    /// <summary>
    /// Tests de jeu exécutés en ligne de commande (PlayMode). Ils font tourner la vraie simulation (NavMesh, animations,
    /// combat, IA) et vérifient des invariants, puis enregistrent des captures dans Logs/.
    /// Ce sont des vérifications automatiques, pas un test manuel dans l'éditeur.
    /// </summary>
    public class PlayModeTests
    {
        static string Shot(string name) { Directory.CreateDirectory("Logs"); return "Logs/shot_" + name + ".png"; }

        /// <summary>
        /// Capture 720×1560 : ScreenCapture ne rend rien en batchmode sans fenêtre, on rend donc la caméra dans une
        /// RenderTexture, après avoir basculé les canvas « overlay » en mode caméra pour inclure l'interface.
        /// </summary>
        internal static IEnumerator Capture(string name, int w = 720, int h = 1560)
        {
            yield return null;
            var cam = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>();
            var previous = new System.Collections.Generic.List<(Canvas c, RenderMode m, Camera cam, float plane)>();
            foreach (var c in canvases)
            {
                if (c.rootCanvas != c) continue;
                previous.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                }
            }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            float oldAspect = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            yield return null;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            cam.ResetAspect();
            foreach (var p in previous) { p.c.renderMode = p.m; p.c.worldCamera = p.cam; p.c.planeDistance = p.plane; }
            File.WriteAllBytes(Shot(name), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Debug.Log("[RB-TEST] capture written " + name);
        }

        internal static IEnumerator LoadScene(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null; yield return null;
        }

        internal static void Spawn(string cardId, Vector3 pos, Team team)
        {
            var card = RBGameAssets.Current.GetCard(cardId);
            Assert.IsNotNull(card, "carte " + cardId);
            GameManager.I.Deploy(card, pos, team);
        }

        internal static Tower FindTower(Team team, bool king)
        {
            foreach (var t in GameManager.I.Towers) if (t.Team == team && t.IsKing == king) return t;
            return null;
        }

        [UnityTest]
        public IEnumerator Sandbox_Boots_With_Six_Towers_And_NavMesh()
        {
            yield return LoadScene("RB_Sandbox");
            var gm = GameManager.I;
            Assert.IsNotNull(gm, "GameManager");
            Assert.AreEqual(6, gm.Towers.Count, "six tours");
            Assert.IsTrue(gm.BattleStarted);
            var hit = new UnityEngine.AI.NavMeshHit();
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, 0, -10), out hit, 1f, UnityEngine.AI.NavMesh.AllAreas), "NavMesh côté joueur");
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(-5.6f, 0, 0), out hit, 1f, UnityEngine.AI.NavMesh.AllAreas), "NavMesh sur le pont");
            Assert.IsFalse(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, 0, 0), out hit, 0.5f, UnityEngine.AI.NavMesh.AllAreas), "la rivière n'est pas navigable au centre");
            yield return Capture("sandbox_boot");
        }

        [UnityTest]
        public IEnumerator Melee_Troop_Crosses_Bridge_And_Damages_Tower()
        {
            yield return LoadScene("RB_Sandbox");
            var gm = GameManager.I;
            var target = FindTower(Team.Enemy, false);
            float before = 0f;
            Spawn("CHEVALIER", new Vector3(-5.6f, 0, -8f), Team.Player);
            yield return null;
            Assert.AreEqual(1, gm.Units.Count);
            var u = gm.Units[0];
            Time.timeScale = 6f;
            float t = 0f; bool damaged = false; bool crossed = false;
            foreach (var tw in gm.Towers) if (tw.Team == Team.Enemy && !tw.IsKing) before = Mathf.Max(before, tw.MaxHp);
            while (t < 40f && !damaged)
            {
                yield return null; t += Time.deltaTime;
                if (u != null && u.Position.z > 2.5f) crossed = true;
                foreach (var tw in gm.Towers) if (tw.Team == Team.Enemy && tw.Hp < tw.MaxHp) damaged = true;
                if (t > 3f && !gm.Units.Contains(u) && !damaged) break;
            }
            Time.timeScale = 1f;
            Assert.IsTrue(crossed, "le chevalier doit traverser le pont (z > 2.5)");
            Assert.IsTrue(damaged, "une tour ennemie doit avoir perdu des PV");
            yield return Capture("chevalier_vs_tower");
        }

        [UnityTest]
        public IEnumerator All_Troops_And_Spells_Run_Without_Errors()
        {
            yield return LoadScene("RB_Sandbox");
            var gm = GameManager.I;
            string[] troops = { "GARDE", "TIREUR", "ÉCLAIREUR", "BOMBARDEUR", "CHEVALIER", "ARCHER", "COUREUR", "SORCIER", "GOBELIN", "GEANT", "GARGOUILLE" };
            for (int i = 0; i < troops.Length; i++)
            {
                Spawn(troops[i], new Vector3(-9f + i * 1.8f, 0, -12f), Team.Player);
                Spawn(troops[i], new Vector3(9f - i * 1.8f, 0, 12f), Team.Enemy);
            }
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(troops.Length * 2, gm.Units.Count);
            Time.timeScale = 4f;
            yield return new WaitForSeconds(2f);
            yield return Capture("all_troops_early");
            Spawn("BOULE DE FEU", new Vector3(0, 0, -12), Team.Enemy);
            Spawn("PLUIE DE FLÈCHES", new Vector3(3, 0, -12), Team.Enemy);
            Spawn("ZAP", new Vector3(-3, 0, 12), Team.Player);
            Spawn("GEL", new Vector3(6, 0, 10), Team.Player);
            yield return new WaitForSeconds(3f);
            yield return Capture("all_troops_spells");
            yield return new WaitForSeconds(12f);
            Time.timeScale = 1f;
            yield return Capture("all_troops_late");
            Assert.Pass("11 troupes x2 + 4 sorts joués sans erreur ; unités vivantes = " + gm.Units.Count);
        }

        [UnityTest]
        public IEnumerator Full_Battle_With_AI_Runs()
        {
            PlayerPrefs.DeleteKey("rb_deck");
            yield return LoadScene("RB_Battle");
            var gm = GameManager.I;
            Assert.IsTrue(gm.BattleStarted);
            Assert.AreEqual(4, gm.PlayerCycle.Hand.Count);
            Assert.AreEqual(4, gm.PlayerCycle.Queue.Count);
            Assert.AreEqual(7f, gm.Elixir.Player);
            yield return Capture("battle_start");
            // le joueur pose deux cartes
            var card0 = RBGameAssets.Current.GetCard(gm.PlayerCycle.Hand[0]);
            Assert.IsTrue(gm.PlayCard(Team.Player, card0, new Vector3(-5, 0, -8)));
            Assert.AreEqual(7f - card0.cost, gm.Elixir.Player, 0.01f);
            Assert.AreEqual(4, gm.PlayerCycle.Hand.Count);
            Time.timeScale = 8f;
            float t = 0f;
            while (t < 70f && !gm.GameOver) { yield return null; t += Time.deltaTime; }
            Time.timeScale = 1f;
            yield return Capture("battle_mid");
            Assert.Greater(gm.Units.Count + gm.KillsPlayer + gm.KillsEnemy, 0, "l'IA doit avoir posé des troupes");
            Assert.Less(gm.MatchTime, RB.MatchDuration - 30f, "le chrono avance");
            Debug.Log("[TEST] battle t=" + t + " units=" + gm.Units.Count + " kills " + gm.KillsPlayer + "/" + gm.KillsEnemy + " crowns " + gm.PlayerCrowns + "-" + gm.EnemyCrowns + " elixir " + gm.Elixir.Player + "/" + gm.Elixir.Enemy);
        }


        [UnityTest]
        public IEnumerator UI_Custom_Graphics_Generate_Meshes()
        {
            var canvas = UiKit.MakeCanvas("DbgCanvas", 5, new Vector2(720, 1560), UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0f);
            var rt = UiKit.Rect("Poly", canvas.transform);
            UiKit.Place(rt, 100, 100, 1, 1);
            var g = rt.gameObject.AddComponent<PolygonGraphic>();
            g.color = Color.red;
            g.SetPoints(new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100) });
            var brt = UiKit.Rect("Bar", canvas.transform);
            UiKit.Place(brt, 100, 300, 400, 40);
            var bar = brt.gameObject.AddComponent<FluidBar>();
            yield return null; yield return null; yield return null;
            var cr = g.GetComponent<CanvasRenderer>();
            var mesh = cr.GetMesh();
            Debug.Log("[RB-DBG] poly mesh verts=" + (mesh != null ? mesh.vertexCount : -1) + " cull=" + cr.cull + " hasMoved=" + cr.hasMoved + " mat=" + (cr.GetMaterial() != null) + " rect=" + rt.rect + " enabled=" + g.enabled + " active=" + g.gameObject.activeInHierarchy);
            var cr2 = bar.GetComponent<CanvasRenderer>();
            var m2 = cr2.GetMesh();
            Debug.Log("[RB-DBG] bar mesh verts=" + (m2 != null ? m2.vertexCount : -1) + " rect=" + brt.rect + " cull=" + cr2.cull);
            Assert.Greater(mesh.vertexCount, 0, "PolygonGraphic doit produire un maillage");
            Assert.Greater(m2.vertexCount, 0, "FluidBar doit produire un maillage");
            Object.Destroy(canvas.gameObject);
        }
        [UnityTest]
        public IEnumerator Menu_Scene_Builds()
        {
            yield return LoadScene("RB_Menu");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("menu");
            Assert.IsNotNull(Object.FindAnyObjectByType<DeckMenu>());
        }
    }
}
