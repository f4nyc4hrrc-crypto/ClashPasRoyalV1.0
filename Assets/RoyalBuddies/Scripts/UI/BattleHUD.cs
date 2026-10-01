using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RoyalBuddies
{
    /// <summary>
    /// Interface de combat (port de rb_battle_hud.gd) : bandeau du haut (pause, score, chrono, statut),
    /// panneau du bas (jauge Fluide, 4 cartes, carte suivante), annonces, panneau de pause.
    /// Canvas mis à l'échelle sur la largeur (référence 1536 px, comme « factor = largeur / 1536 » de Godot) :
    /// bandeau ancré en haut, main ancrée en bas, valable pour tous les formats de téléphone.
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        const float RefW = 1536f, TopH = 265f, BottomH = 587f;

        GameManager gm;
        PlacementController placement;
        RBGameAssets assets;
        Canvas canvas;
        RectTransform top, bottom;
        Text scoreText, timeText, statusText, elixirText, multText, nextText, specialText, announceText;
        FluidBar fluidBar;
        readonly CardView[] cards = new CardView[4];
        GameObject pauseRoot;
        RectTransform pausePanel;
        EndScreen endScreen;
        Coroutine announceRoutine;

        public static BattleHUD Create(GameManager gm, PlacementController placement, RBGameAssets assets)
        {
            EnsureEventSystem();
            var go = new GameObject("RB_BattleHUD");
            var hud = go.AddComponent<BattleHUD>();
            hud.gm = gm; hud.placement = placement; hud.assets = assets;
            hud.Build();
            return hud;
        }

        public static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ------------------------------------------------------------------ construction

        void Build()
        {
            canvas = UiKit.MakeCanvas("BattleCanvas", 10, new Vector2(RefW, 3328f), CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0f);
            canvas.transform.SetParent(transform, false);
            var sk = assets.hudAtlas;

            // ---- Bandeau du haut (0,0,1536,265) de l'atlas HUD
            top = UiKit.Rect("TopPanel", canvas.transform);
            top.anchorMin = top.anchorMax = new Vector2(.5f, 1); top.pivot = new Vector2(.5f, 1);
            top.sizeDelta = new Vector2(RefW, TopH); top.anchoredPosition = Vector2.zero;
            var topImg = top.gameObject.AddComponent<Image>();
            topImg.sprite = UiKit.Atlas(sk, 0, 0, 1536, 265);
            topImg.raycastTarget = true;   // absorbe les clics (MOUSE_FILTER_STOP)

            UiKit.HitButton(top, "PauseButton", 74, 38, 126, 128, OpenPause);
            scoreText = Lbl(top, "Score", "0 — 0", 831, 47, 169, 97, 47, Color.white);
            timeText = Lbl(top, "Time", "3:00", 1227, 48, 218, 96, 52, Color.white);
            statusText = Lbl(top, "Status", "", 276, 175, 1064, 75, 31, UiKit.Hex("#fff5d9"));
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // ---- Panneau du bas (0,437,1536,587)
            bottom = UiKit.Rect("HandFrame", canvas.transform);
            bottom.anchorMin = bottom.anchorMax = new Vector2(.5f, 0); bottom.pivot = new Vector2(.5f, 0);
            bottom.sizeDelta = new Vector2(RefW, BottomH); bottom.anchoredPosition = Vector2.zero;
            var botImg = bottom.gameObject.AddComponent<Image>();
            botImg.sprite = UiKit.Atlas(sk, 0, 437, 1536, 587);
            botImg.raycastTarget = true;

            // Remplacement de la gemme d'origine : patch de bois + goutte de Fluide
            var wood = UiKit.Img(bottom, "FluidWood", UiKit.Atlas(assets.fluidWood, 90, 501, 161, 135), Color.white);
            UiKit.Place(wood.rectTransform, 90, 64, 161, 135);
            var drop = UiKit.Img(bottom, "FluidDrop", UiKit.FullSprite(assets.fluidDrop), Color.white);
            UiKit.Place(drop.rectTransform, 94, 51, 155, 145);
            drop.preserveAspect = true;

            var barRt = UiKit.Rect("FluidBar", bottom);
            UiKit.Place(barRt, 446, 119, 722, 37);
            fluidBar = barRt.gameObject.AddComponent<FluidBar>();
            fluidBar.raycastTarget = false;

            elixirText = Lbl(bottom, "ElixirText", "7 / 10", 1190, 103, 198, 67, 46, UiKit.Hex("#c8ffb0"));
            multText = Lbl(bottom, "FluidMult", "×2", 326, 103, 104, 67, 42, Color.white);
            multText.gameObject.SetActive(false);

            float[] xs = { 78, 426, 779, 1132 };
            for (int i = 0; i < 4; i++)
            {
                var card = CardView.Create(bottom, CardView.Style.Combat, "Card" + i, xs[i], 198, 328, 306);
                int index = i;
                card.Button.onClick.AddListener(() => placement.Select(index));
                card.gameObject.AddComponent<CardDragHandler>().Bind(placement,card,index);
                cards[i] = card;
            }
            nextText = Lbl(bottom, "Next", "", 100, 540, 1336, 32, 22, UiKit.Hex("#fff5d9"));

            // ---- Textes libres (unités « viewport 720 » de Godot ×2.133)
            var overlay = UiKit.Rect("Overlay", canvas.transform);
            UiKit.Stretch(overlay);
            specialText = UiKit.Label(overlay, "Special", "", 36, UiKit.Hex("#fff2d0"), TextAnchor.MiddleCenter, 3f, UiKit.Hex("#2a1609"));
            specialText.rectTransform.anchorMin = new Vector2(0, 1); specialText.rectTransform.anchorMax = new Vector2(1, 1);
            specialText.rectTransform.pivot = new Vector2(.5f, 1); specialText.rectTransform.sizeDelta = new Vector2(0, 80);
            specialText.rectTransform.anchoredPosition = new Vector2(0, -(TopH + 6));
            announceText = UiKit.Label(overlay, "Announcement", "", 73, UiKit.Hex("#fff2d0"), TextAnchor.MiddleCenter, 5f, UiKit.Hex("#2a1609"));
            announceText.rectTransform.anchorMin = new Vector2(0, .70f); announceText.rectTransform.anchorMax = new Vector2(1, .70f);
            announceText.rectTransform.pivot = new Vector2(.5f, .5f); announceText.rectTransform.sizeDelta = new Vector2(0, 220);
            announceText.rectTransform.anchoredPosition = Vector2.zero;
            announceText.color = new Color(1, 1, 1, 0);

            BuildPause();

            gm.HandChanged += RefreshHand;
            gm.Announced += OnAnnounce;
            gm.GameEnded += OnGameEnded;
            placement.SelectionChanged += RefreshHand;
            RefreshHand();
        }

        Text Lbl(Transform parent, string name, string text, float x, float y, float w, float h, int size, Color color)
        {
            var t = UiKit.Label(parent, name, text, size, color, TextAnchor.MiddleCenter, 3f, UiKit.Hex("#151814"));
            UiKit.Place(t.rectTransform, x, y, w, h);
            return t;
        }

        void OnDestroy()
        {
            if (gm != null)
            {
                gm.HandChanged -= RefreshHand;
                gm.Announced -= OnAnnounce;
                gm.GameEnded -= OnGameEnded;
            }
            if (placement != null) placement.SelectionChanged -= RefreshHand;
        }

        // ------------------------------------------------------------------ pause

        void BuildPause()
        {
            var pc = UiKit.MakeCanvas("PauseCanvas", 40, new Vector2(RefW, 3328f), CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0f);
            pc.transform.SetParent(transform, false);
            pauseRoot = pc.gameObject;
            var shade = UiKit.Img(pc.transform, "Dimmer", null, new Color(0.015f, 0.025f, 0.035f, 0.62f), true);
            UiKit.Stretch(shade.rectTransform);
            var skin = assets.pauseSkin;
            pausePanel = UiKit.Rect("PausePanel", pc.transform);
            var img = pausePanel.gameObject.AddComponent<Image>();
            img.sprite = UiKit.FullSprite(skin);
            img.raycastTarget = true;
            pausePanel.sizeDelta = new Vector2(skin.width, skin.height);
            pausePanel.anchorMin = pausePanel.anchorMax = pausePanel.pivot = new Vector2(.5f, .5f);
            UiKit.HitButton(pausePanel, "ResumeButton", 247, 554, 731, 191, ResumePause);
            UiKit.HitButton(pausePanel, "AbandonButton", 247, 774, 731, 190, AbandonBattle);
            pauseRoot.SetActive(false);
        }

        void LayoutPause()
        {
            // pause_scale = min(largeur·0.75 / skin.w, hauteur_dispo / skin.h), centré entre les deux panneaux
            var cr = UiKit.ViewRect(canvas);
            float viewH = cr.height, viewW = cr.width;
            var skin = assets.pauseSkin;
            float available = viewH - (TopH + BottomH) - 36f;
            float scale = Mathf.Min(viewW * 0.75f / skin.width, available / skin.height);
            pausePanel.localScale = Vector3.one * scale;
            float centerY = (TopH + (viewH - BottomH)) / 2f;   // depuis le haut
            pausePanel.anchoredPosition = new Vector2(0, viewH / 2f - centerY);
        }

        public void OpenPause()
        {
            if (!gm.BattleStarted || gm.GameOver) return;
            pauseRoot.SetActive(true);
            LayoutPause();
            gm.Pause();
        }

        public void ResumePause()
        {
            pauseRoot.SetActive(false);
            gm.Resume();
        }

        void AbandonBattle()
        {
            pauseRoot.SetActive(false);
            gm.Resume();
            gm.Abandon();
        }

        // ------------------------------------------------------------------ événements de jeu

        void RefreshHand()
        {
            if (gm.PlayerCycle == null) return;
            var hand = gm.PlayerCycle.Hand;
            for (int i = 0; i < cards.Length && i < hand.Count; i++)
            {
                var card = assets.GetCard(hand[i]);
                cards[i].Set(card, card.displayName, placement.SelectedIndex == i);
            }
            string nxt = gm.PlayerCycle.Next;
            if (nxt != null) nextText.text = "SUIVANTE  •  " + assets.GetCard(nxt).displayName;
            else nextText.text = "SUIVANTE";
        }

        void OnAnnounce(string text)
        {
            if (announceRoutine != null) StopCoroutine(announceRoutine);
            announceRoutine = StartCoroutine(AnnounceRoutine(text));
        }

        IEnumerator AnnounceRoutine(string text)
        {
            announceText.text = text;
            var rt = announceText.rectTransform;
            announceText.color = Color.white;
            rt.localScale = Vector3.one * .7f;
            for (float t = 0; t < .16f; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * Mathf.Lerp(.7f, 1f, t / .16f);
                yield return null;
            }
            rt.localScale = Vector3.one;
            for (float t = 0; t < .7f; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < .35f; t += Time.unscaledDeltaTime)
            {
                announceText.color = new Color(1, 1, 1, 1f - t / .35f);
                yield return null;
            }
            announceText.color = new Color(1, 1, 1, 0);
            announceRoutine = null;
        }

        void OnGameEnded(bool win)
        {
            if (endScreen == null) endScreen = EndScreen.Create(gm, assets, win);
        }

        // ------------------------------------------------------------------ mise à jour par image

        void Update()
        {
            if (gm == null || !gm.BattleStarted) return;

            if (gm.PlayerCycle != null)
                for (int i = 0; i < cards.Length && i < gm.PlayerCycle.Hand.Count; i++)
                {
                    var card = assets.GetCard(gm.PlayerCycle.Hand[i]);
                    cards[i].SetAffordable(gm.Elixir.Player >= card.cost);
                }

            fluidBar.Value = gm.Elixir.Player;
            elixirText.text = Mathf.FloorToInt(gm.Elixir.Player) + " / 10";
            scoreText.text = gm.PlayerCrowns + " — " + gm.EnemyCrowns;
            int s = Mathf.CeilToInt(gm.MatchTime);
            timeText.text = (s / 60) + ":" + (s % 60).ToString("00");
            statusText.text = gm.StatusText;
            specialText.text = gm.SpecialText;
            float m = gm.ElixirMultiplier;
            if (m >= 3f) { multText.gameObject.SetActive(true); multText.text = "×3"; multText.color = new Color(1f, .84f, .24f); }
            else if (m >= 2f) { multText.gameObject.SetActive(true); multText.text = "×2"; multText.color = new Color(.40f, 1f, .68f); }
            else multText.gameObject.SetActive(false);
        }
    }

    /// <summary>Jauge de Fluide (rb_fluid_bar.gd) : dégradé vert, un séparateur par unité, reflet en haut.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class FluidBar : MaskableGraphic
    {
        float amount = 7f;
        public float Value
        {
            get { return amount; }
            set { if (Mathf.Approximately(amount, value)) return; amount = value; SetVerticesDirty(); }
        }

        public override bool Raycast(Vector2 sp, Camera eventCamera) { return false; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float width = r.width * amount / 10f;
            if (width <= 0f) return;
            float h = r.height;
            float x0 = r.xMin, yTop = r.yMax, yBot = r.yMin;
            Color topHi = UiKit.Hex("#b1ff9b"), mid = UiKit.Hex("#1bf75c"), bot = UiKit.Hex("#049b37");
            float yHi = yTop - h * .18f;
            Quad(vh, x0, yHi, x0 + width, yTop, mid, topHi);      // reflet supérieur (18 %)
            Quad(vh, x0, yBot, x0 + width, yHi, bot, mid);        // dégradé principal
            for (int i = 1; i < 10; i++)
            {
                float x = r.xMin + r.width * i / 10f;
                if (x < x0 + width)
                {
                    Quad(vh, x, yBot + 2, x + 1.5f, yTop - 2, new Color(0, .2f, .09f, .65f), new Color(0, .2f, .09f, .65f));
                    Quad(vh, x + 1.5f, yBot + 3, x + 2.5f, yTop - 3, new Color(.6f, 1f, .65f, .4f), new Color(.6f, 1f, .65f, .4f));
                }
            }
            Quad(vh, x0, yBot, x0 + width, yBot + 1.5f, UiKit.Hex("#8aff93"), UiKit.Hex("#8aff93"));
        }

        static void Quad(VertexHelper vh, float xa, float ya, float xb, float yb, Color bottom, Color topC)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(xa, ya), bottom, Vector2.zero);
            vh.AddVert(new Vector3(xa, yb), topC, Vector2.zero);
            vh.AddVert(new Vector3(xb, yb), topC, Vector2.zero);
            vh.AddVert(new Vector3(xb, ya), bottom, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
