using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RoyalBuddies
{
    /// <summary>
    /// Écran de fin (build_end_screen de main.gd) : bannière VICTOIRE / DÉFAITE, trois couronnes, résumé du combat,
    /// boutons REJOUER (même deck) et RETOUR MENU. Coordonnées identiques à Godot (canvas 720 px de large).
    /// Les icônes Unicode du jeu d'origine (⚔ ♜ ✦ ◷ ♛) n'existent pas dans la police intégrée : elles sont dessinées en polygones.
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        public static EndScreen Create(GameManager gm, RBGameAssets assets, bool win)
        {
            var go = new GameObject("RB_EndScreen");
            var es = go.AddComponent<EndScreen>();
            es.Build(gm, win);
            return es;
        }

        Text ColoredLabel(Transform parent, string text, float x, float y, float w, float h, int size, Color color, TextAnchor a = TextAnchor.MiddleCenter, float outline = 2f)
        {
            var t = UiKit.Label(parent, "Label", text, size, color, a, outline, UiKit.Hex("#241207"));
            UiKit.Place(t.rectTransform, x, y, w, h);
            return t;
        }

        void Build(GameManager gm, bool win)
        {
            var canvas = UiKit.MakeCanvas("EndCanvas", 30, new Vector2(720, 1560), CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0f);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            var veil = UiKit.Img(root, "Veil", null, new Color(0.01f, 0.018f, 0.028f, win ? .47f : .61f), true);
            UiKit.Stretch(veil.rectTransform);

            var glow = UiKit.Panel(root, "CenterGlow", win ? new Color(0.055f, 0.085f, 0.10f, .56f) : new Color(0.045f, 0.045f, 0.055f, .68f),
                                   win ? new Color(0.75f, 0.54f, 0.20f, .28f) : new Color(0.38f, 0.30f, 0.34f, .35f), 2, 34);
            UiKit.Place(glow, 62, 174, 596, 770);

            // Rayons de lumière derrière la bannière
            for (int i = 0; i < 11; i++)
            {
                var ray = UiKit.Img(root, "Ray", null, win ? new Color(1f, .76f, .24f, .075f) : new Color(.45f, .55f, .68f, .025f));
                var rt = ray.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(.5f, 0);
                rt.sizeDelta = new Vector2(8, 235);
                rt.anchoredPosition = new Vector2(356 + 4, -(205 + 235));
                rt.localRotation = Quaternion.Euler(0, 0, -(-50f + i * 10f));
            }
            if (win) for (int i = 0; i < 34; i++)
            {
                Color[] cs = { UiKit.Hex("#f6cf62"), UiKit.Hex("#fff0a3"), UiKit.Hex("#d69a2d"), UiKit.Hex("#79c9e8") };
                var spark = UiKit.Img(root, "Spark", null, cs[i % 4]);
                UiKit.Place(spark.rectTransform, Random.Range(65f, 655f), Random.Range(185f, 905f), Random.Range(2f, 6f), Random.Range(5f, 15f));
                spark.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-.9f, .9f) * Mathf.Rad2Deg);
                spark.gameObject.AddComponent<Flicker>();
            }

            // ---- Bannière : ombre → bois → métal → plaque intérieure
            var bannerShadow = UiKit.Img(root, "BannerShadow", UiKit.Rounded(24), new Color(.02f, .01f, .005f, .72f));
            UiKit.Place(bannerShadow.rectTransform, 71, 214, 578, 142);
            var banner = UiKit.Panel(root, "Banner", win ? UiKit.Hex("#4b2a15") : UiKit.Hex("#30252a"), win ? UiKit.Hex("#f0bd4f") : UiKit.Hex("#9b7880"), 7, 22);
            UiKit.Place(banner, 78, 202, 564, 142);
            var wood = UiKit.Panel(banner, "Wood", win ? UiKit.Hex("#6a3b1d") : UiKit.Hex("#443139"), UiKit.Hex("#7d4d22"), 2, 16);
            UiKit.Place(wood, 9, 9, 546, 124);
            foreach (int yy in new[] { 22, 43, 82, 103 })
            {
                var g = UiKit.Img(wood, "Grain", null, new Color(.16f, .07f, .025f, .34f));
                UiKit.Place(g.rectTransform, 24, yy, 498, 2);
            }
            var inner = UiKit.Panel(banner, "Inner", win ? UiKit.Hex("#15242d") : UiKit.Hex("#211e25"), win ? UiKit.Hex("#b98a35") : UiKit.Hex("#705b63"), 3, 13);
            UiKit.Place(inner, 20, 17, 524, 108);
            var shine = UiKit.Img(banner, "Shine", null, win ? new Color(1, .87f, .48f, .42f) : new Color(.85f, .75f, .78f, .18f));
            UiKit.Place(shine.rectTransform, 34, 22, 496, 3);
            foreach (var p in new[] { new Vector2(8, 8), new Vector2(548, 8), new Vector2(8, 126), new Vector2(548, 126) })
            {
                var rivet = UiKit.Panel(banner, "Rivet", UiKit.Hex("#c09145"), UiKit.Hex("#382416"), 2, 5);
                UiKit.Place(rivet, p.x, p.y, 9, 9);
            }
            ColoredLabel(banner, win ? "VICTOIRE !" : "DÉFAITE", 20, 20, 524, 72, 44, win ? UiKit.Hex("#ffe58a") : UiKit.Hex("#e5c3c8"), TextAnchor.MiddleCenter, 3f);
            ColoredLabel(banner, win ? "TRIOMPHE SUR L'ARÈNE" : "L'ARÈNE A TRANCHÉ", 30, 92, 504, 24, 13, win ? UiKit.Hex("#e2bd67") : UiKit.Hex("#bda5aa"), TextAnchor.MiddleCenter, 3f);

            // ---- Couronnes
            var crownOuter = UiKit.Panel(root, "CrownPlate", win ? UiKit.Hex("#4a2a16") : UiKit.Hex("#32282d"), win ? UiKit.Hex("#d5a23d") : UiKit.Hex("#80676e"), 5, 18);
            UiKit.Place(crownOuter, 103, 370, 514, 154);
            var crownPanel = UiKit.Panel(crownOuter, "CrownPanel", UiKit.Hex("#101b24"), UiKit.Hex("#76572d"), 2, 12);
            UiKit.Place(crownPanel, 10, 10, 494, 134);
            ColoredLabel(crownPanel, "COURONNES", 18, 5, 458, 26, 14, UiKit.Hex("#d9bc76"));
            var crownItems = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                bool earned = i < gm.PlayerCrowns;
                var holder = UiKit.Rect("Crown" + i, crownPanel);
                UiKit.Place(holder, 80 + i * 108, 31, 86, 76);
                UiKit.SetPivotKeep(holder, new Vector2(.5f, .5f));
                MakeCrown(holder, new Vector2(3, 5), new Color(.08f, .04f, .01f, .8f));
                MakeCrown(holder, Vector2.zero, earned ? UiKit.Hex("#ffd75b") : UiKit.Hex("#3e4a53"));
                crownItems[i] = holder;
            }
            var scorePlate = UiKit.Panel(crownPanel, "ScorePlate", UiKit.Hex("#0b1218"), UiKit.Hex("#58472f"), 1, 8);
            UiKit.Place(scorePlate, 116, 105, 262, 24);
            ColoredLabel(scorePlate, "ADVERSAIRE   " + gm.EnemyCrowns + " / 3", 0, 0, 262, 24, 12, UiKit.Hex("#aab4b9"));

            // ---- Résumé du combat
            var stats = UiKit.Panel(root, "StatsPanel", win ? UiKit.Hex("#4b2b16") : UiKit.Hex("#34282d"), win ? UiKit.Hex("#e0ad43") : UiKit.Hex("#866c73"), 6, 20);
            UiKit.Place(stats, 91, 548, 538, 310);
            var metal = UiKit.Panel(stats, "Metal", UiKit.Hex("#26343a"), UiKit.Hex("#80602f"), 2, 14);
            UiKit.Place(metal, 9, 9, 520, 292);
            var statsInner = UiKit.Panel(metal, "Inner", UiKit.Hex("#101a22"), UiKit.Hex("#493a28"), 1, 10);
            UiKit.Place(statsInner, 10, 10, 500, 272);
            ColoredLabel(statsInner, "RÉSUMÉ DU COMBAT", 20, 4, 460, 38, 18, UiKit.Hex("#f2cd70"));
            var sep = UiKit.Img(statsInner, "Sep", null, UiKit.Hex("#a37a35"));
            UiKit.Place(sep.rectTransform, 38, 43, 424, 2);
            int secs = Mathf.FloorToInt(gm.BattleElapsed);
            StatRow(statsInner, 51, "Unités éliminées", gm.KillsPlayer.ToString());
            StatRow(statsInner, 106, "Tours détruites", gm.TowersDestroyedByPlayer.ToString());
            StatRow(statsInner, 161, "Combo maximum", "x" + gm.MaxCombo);
            StatRow(statsInner, 216, "Durée", (secs / 60) + ":" + (secs % 60).ToString("00"));

            // ---- Boutons
            var actionBase = UiKit.Panel(root, "ActionBase", UiKit.Hex("#2f1c10"), UiKit.Hex("#8f682e"), 3, 18);
            UiKit.Place(actionBase, 82, 880, 556, 112);
            var retry = EndButton(root, "REJOUER", 99, 895, UiKit.Hex("#1e6539"), UiKit.Hex("#efbd4c"), 20, SceneFlow.GoBattle);
            var back = EndButton(root, "RETOUR MENU", 369, 895, UiKit.Hex("#214965"), UiKit.Hex("#d9a944"), 17, SceneFlow.GoMenu);

            StartCoroutine(Intro(banner, crownOuter, stats, actionBase, new[] { retry, back }, crownItems));
        }

        Transform EndButton(Transform root, string label, float x, float y, Color fill, Color border, int size, UnityEngine.Events.UnityAction action)
        {
            var b = UiKit.TextButton(root, "Btn_" + label, label, size, fill, border, x, y, 252, 80, action, 14);
            return b.transform;
        }

        void StatRow(Transform parent, float y, string label, string value)
        {
            var row = UiKit.Panel(parent, "Row", UiKit.Hex("#18242b"), UiKit.Hex("#6f5736"), 1, 9);
            UiKit.Place(row, 24, y, 472, 52);
            var bullet = UiKit.Rect("Bullet", row);
            UiKit.Place(bullet, 22, 20, 12, 12);
            var poly = bullet.gameObject.AddComponent<PolygonGraphic>();
            poly.color = UiKit.Hex("#f2c55d");
            poly.SetPoints(new[] { new Vector2(6, 0), new Vector2(12, 6), new Vector2(6, 12), new Vector2(0, 6) });
            var l = UiKit.Label(row, "Label", label, 18, UiKit.Hex("#e9dfc7"), TextAnchor.MiddleLeft);
            UiKit.Place(l.rectTransform, 58, 0, 270, 52);
            var v = UiKit.Label(row, "Value", value, 21, UiKit.Hex("#fff2bd"), TextAnchor.MiddleRight, 2f, UiKit.Hex("#261408"));
            UiKit.Place(v.rectTransform, 330, 0, 126, 52);
        }

        /// <summary>Couronne à 5 pointes dessinée en polygone (♛).</summary>
        static void MakeCrown(RectTransform parent, Vector2 offset, Color color)
        {
            var rt = UiKit.Rect("Crown", parent);
            UiKit.Place(rt, offset.x, offset.y, 86, 76);
            var g = rt.gameObject.AddComponent<PolygonGraphic>();
            g.color = color;
            g.SetPoints(new[]
            {
                new Vector2(8, 60), new Vector2(4, 22), new Vector2(26, 40), new Vector2(43, 8), new Vector2(60, 40),
                new Vector2(82, 22), new Vector2(78, 60)
            });
            // socle
            var b = UiKit.Img(rt, "Base", null, color);
            UiKit.Place(b.rectTransform, 8, 60, 70, 10);
        }

        IEnumerator Intro(RectTransform banner, RectTransform crownOuter, RectTransform stats, RectTransform actionBase, Transform[] buttons, RectTransform[] crowns)
        {
            var all = new[] { (Transform)banner, crownOuter, stats, actionBase, buttons[0], buttons[1] };
            var groups = new CanvasGroup[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                groups[i] = all[i].gameObject.AddComponent<CanvasGroup>();
                groups[i].alpha = 0f;
            }
            foreach (var c in crowns) { var cg = c.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0f; c.localScale = Vector3.one * .28f; }
            UiKit.SetPivotKeep(banner, new Vector2(.5f, .5f)); UiKit.SetPivotKeep(crownOuter, new Vector2(.5f, .5f));
            banner.localScale = Vector3.one * .72f;
            crownOuter.localScale = Vector3.one * .88f;
            float statsY = stats.anchoredPosition.y;
            stats.anchoredPosition += new Vector2(0, -18);

            yield return Tween(.30f, k => { groups[0].alpha = Mathf.Clamp01(k * 2f); banner.localScale = Vector3.one * Mathf.Lerp(.72f, 1f, EaseBack(k)); });
            StartCoroutine(CrownsRoutine(crowns));
            yield return Tween(.24f, k => { groups[1].alpha = Mathf.Clamp01(k * 2f); crownOuter.localScale = Vector3.one * Mathf.Lerp(.88f, 1f, EaseBack(k)); });
            yield return Tween(.22f, k => { groups[2].alpha = Mathf.Clamp01(k * 2f); stats.anchoredPosition = new Vector2(stats.anchoredPosition.x, Mathf.Lerp(statsY - 18, statsY, k)); });
            yield return Tween(.14f, k => { groups[3].alpha = k; groups[4].alpha = k; groups[5].alpha = k; });
        }

        IEnumerator CrownsRoutine(RectTransform[] crowns)
        {
            for (int i = 0; i < crowns.Length; i++)
            {
                int idx = i;
                StartCoroutine(CrownPop(crowns[idx], .30f + idx * .20f));
            }
            yield break;
        }

        IEnumerator CrownPop(RectTransform c, float delay)
        {
            for (float t = 0; t < delay; t += Time.unscaledDeltaTime) yield return null;
            var cg = c.GetComponent<CanvasGroup>();
            for (float t = 0; t < .38f; t += Time.unscaledDeltaTime)
            {
                float k = t / .38f;
                cg.alpha = Mathf.Clamp01(t / .10f);
                c.localScale = Vector3.one * Mathf.Lerp(.28f, 1f, EaseBack(k));
                yield return null;
            }
            cg.alpha = 1f; c.localScale = Vector3.one;
        }

        static float EaseBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }

        static IEnumerator Tween(float dur, System.Action<float> apply)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                apply(t / dur);
                yield return null;
            }
            apply(1f);
        }

        /// <summary>Scintillement d'un éclat doré (alpha oscillant).</summary>
        class Flicker : MonoBehaviour
        {
            Image img; float speed, phase, lo, hi;
            void Start()
            {
                img = GetComponent<Image>();
                speed = Random.Range(.9f, 1.8f); phase = Random.value * 6f; lo = .12f; hi = Random.Range(.55f, .95f);
            }
            void Update()
            {
                if (img == null) return;
                var c = img.color; c.a = Mathf.Lerp(lo, hi, .5f + .5f * Mathf.Sin(Time.unscaledTime * speed * 3f + phase)); img.color = c;
            }
        }
    }
}
