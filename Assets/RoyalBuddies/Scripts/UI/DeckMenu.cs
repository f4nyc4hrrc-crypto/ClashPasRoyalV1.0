using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoyalBuddies
{
    /// <summary>
    /// Menu « Deck de combat » (port de rb_deck_screen.gd) : 8 emplacements de deck + collection de 15 cartes,
    /// Fluide moyen, bouton COMBATTRE, sauvegarde du deck, réglages audio.
    /// Toucher un emplacement puis une carte de la collection la remplace ; toucher deux fois retire la carte.
    /// (Le glisser-déposer de Godot n'est pas porté : le mode toucher/remplacer couvre les mêmes actions.)
    /// Composition 1024 × 1536 mise à l'échelle par « min(largeur/1024, hauteur/1536) », comme dans Godot.
    /// </summary>
    public class DeckMenu : MonoBehaviour
    {
        RBGameAssets assets;
        Canvas canvas;
        RectTransform content;
        Image scenery, headerSkin, deckSkin, collectionSkin, footerSkin, averageSkin;
        Text titleLabel, countLabel, averageLabel, collectionTitle, collectionHint, helpLabel;
        Button playButton;
        Image playImage;
        RectTransform playRt, countRt, averageRt, collectionTitleRt, collectionHintRt, helpRt, titleRt, dropRt;
        readonly List<CardView> slots = new List<CardView>();
        readonly List<CardView> collection = new List<CardView>();
        readonly List<string> deck = new List<string>();
        List<string> catalogIds = new List<string>();
        int activeSlot = -1;
        float lastHeight = -1f;
        GameObject settingsRoot;

        public static DeckMenu Create(RBGameAssets assets)
        {
            BattleHUD.EnsureEventSystem();
            var go = new GameObject("RB_DeckMenu");
            var m = go.AddComponent<DeckMenu>();
            m.assets = assets;
            m.Build();
            return m;
        }

        void Build()
        {
            foreach (var c in assets.cards) catalogIds.Add(c.id);
            deck.AddRange(SceneFlow.LoadDeck(catalogIds));

            canvas = UiKit.MakeCanvas("MenuCanvas", 20, new Vector2(1024, 1536), CanvasScaler.ScreenMatchMode.Expand, 0f);
            canvas.transform.SetParent(transform, false);

            // Décor plein écran (recouvre l'écran en gardant les proportions)
            scenery = UiKit.Img(canvas.transform, "Scenery", UiKit.FullSprite(assets.scenery), Color.white);
            UiKit.Stretch(scenery.rectTransform);
            var fitter = scenery.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)assets.scenery.width / assets.scenery.height;
            scenery.rectTransform.anchorMin = scenery.rectTransform.anchorMax = new Vector2(.5f, .5f);
            scenery.rectTransform.pivot = new Vector2(.5f, .5f);

            content = UiKit.Rect("Content", canvas.transform);
            content.anchorMin = content.anchorMax = new Vector2(.5f, .5f);
            content.pivot = new Vector2(.5f, .5f);
            content.sizeDelta = new Vector2(1024, 1536);

            var skin = assets.menuSkin;
            headerSkin = Skin(skin, "Header", 0, 0, 1024, 205);
            deckSkin = Skin(skin, "DeckSkin", 0, 205, 1024, 542);
            collectionSkin = Skin(skin, "CollectionSkin", 0, 747, 1024, 610);
            footerSkin = Skin(skin, "Footer", 0, 1357, 1024, 179);

            titleLabel = Lbl("Title", "DECK DE COMBAT", 35, UiKit.Hex("#fff4da"), out titleRt);
            UiKit.Place(titleRt, 275, 208, 475, 45);

            for (int i = 0; i < 8; i++)
            {
                var card = CardView.Create(content, CardView.Style.DeckSlot, "DeckSlot" + i, 0, 0, 186, 200);
                int idx = i;
                card.Button.onClick.AddListener(() => SlotPressed(idx));
                slots.Add(card);
            }

            countLabel = Lbl("Count", "", 25, UiKit.Hex("#ffe598"), out countRt);
            UiKit.Place(countRt, 180, 680, 250, 41);
            averageSkin = Skin(assets.cardsAtlas, "AverageSkin", 509, 677, 369, 48);
            averageLabel = Lbl("Average", "", 23, UiKit.Hex("#53ffbd"), out averageRt);
            UiKit.Place(averageRt, 563, 680, 297, 39);
            // petite goutte verte
            var drop = UiKit.Rect("Drop", content);
            UiKit.Place(drop, 540, 690, 16, 22);
            dropRt = drop;
            var dg = drop.gameObject.AddComponent<PolygonGraphic>();
            dg.color = UiKit.Hex("#4bffb1");
            dg.SetPoints(new[] { new Vector2(8, 0), new Vector2(13, 8), new Vector2(16, 14), new Vector2(15, 19), new Vector2(11, 22), new Vector2(5, 22), new Vector2(1, 19), new Vector2(0, 14), new Vector2(3, 8) });

            collectionTitle = Lbl("CollectionTitle", "COLLECTION", 35, UiKit.Hex("#fff4da"), out collectionTitleRt);
            collectionTitle.alignment = TextAnchor.MiddleLeft;
            UiKit.Place(collectionTitleRt, 112, 752, 315, 47);
            collectionHint = Lbl("CollectionHint", assets.cards.Length + " CARTES  •  CHOISIS TON DECK", 15, UiKit.Hex("#d9c69c"), out collectionHintRt);
            UiKit.Place(collectionHintRt, 600, 760, 300, 33);

            for (int i = 0; i < assets.cards.Length; i++)
            {
                var card = CardView.Create(content, CardView.Style.Collection, "Collection" + i, 0, 0, 154, 164);
                string id = assets.cards[i].id;
                card.Button.onClick.AddListener(() => CollectionPressed(id));
                collection.Add(card);
            }

            // Bouton COMBATTRE
            playImage = UiKit.Img(content, "Combattre", UiKit.Atlas(assets.cardsAtlas, 245, 1364, 535, 119), Color.white, true);
            playRt = playImage.rectTransform;
            UiKit.Place(playRt, 245, 1364, 535, 119);
            playButton = playImage.gameObject.AddComponent<Button>();
            playButton.targetGraphic = playImage;
            var pc = playButton.colors; pc.highlightedColor = new Color(1.12f, 1.12f, 1.05f, 1f); pc.disabledColor = new Color(.5f, .6f, .53f, 1f); playButton.colors = pc;
            playButton.onClick.AddListener(StartBattle);
            var playText = UiKit.Label(playRt, "Text", "COMBATTRE", 43, UiKit.Hex("#fff4da"), TextAnchor.MiddleCenter, 2.5f, UiKit.Hex("#10100b"));
            UiKit.Stretch(playText.rectTransform);

            helpLabel = Lbl("Help", "", 15, UiKit.Hex("#fff4da"), out helpRt);
            UiKit.Place(helpRt, 210, 1485, 604, 33);

            BuildTopButtons();
            BuildSettings();
            Layout();
            Refresh();
            AudioManager.Ensure(assets).PlayMenu();
        }

        Image Skin(Texture2D tex, string name, float x, float y, float w, float h)
        {
            var img = UiKit.Img(content, name, UiKit.Atlas(tex, x, y, w, h), Color.white);
            UiKit.Place(img.rectTransform, x, y, w, h);
            return img;
        }

        Text Lbl(string name, string text, int size, Color color, out RectTransform rt)
        {
            var t = UiKit.Label(content, name, text, size, color, TextAnchor.MiddleCenter, 2.5f, UiKit.Hex("#10100b"));
            rt = t.rectTransform;
            return t;
        }

        // ------------------------------------------------------------------ mise en page (layout() de Godot)

        void Update()
        {
            float h = UiKit.ViewRect(canvas).height;
            if (!Mathf.Approximately(h, lastHeight)) Layout();
        }

        void Layout()
        {
            float viewH = UiKit.ViewRect(canvas).height;
            lastHeight = viewH;
            float designH = Mathf.Clamp(viewH, 1536f, 1680f);
            content.sizeDelta = new Vector2(1024, designH);
            float extra = designH - 1536f;
            float deckExtra = extra * .44f;
            float collectionExtra = extra - deckExtra;

            deckSkin.rectTransform.sizeDelta = new Vector2(1024, 542 + deckExtra);
            UiKit.Place(titleRt, 275, 208 + deckExtra * .045f, 475, 45);
            UiKit.Place(collectionSkin.rectTransform, 0, 747 + deckExtra, 1024, 610 + collectionExtra);
            UiKit.Place(footerSkin.rectTransform, 0, 1357 + extra, 1024, 179);
            for (int i = 0; i < slots.Count; i++)
            {
                int row = i / 4;
                UiKit.Place((RectTransform)slots[i].transform, 116 + (i % 4) * 204, 262 + row * 211 + deckExtra * (.24f + row * .48f), 186, 200);
            }
            UiKit.Place(countRt, 180, 680 + deckExtra, 250, 41);
            UiKit.Place(averageSkin.rectTransform, 509, 677 + deckExtra, 369, 48);
            UiKit.Place(averageRt, 563, 680 + deckExtra, 297, 39);
            UiKit.Place(dropRt, 540, 689 + deckExtra, 16, 22);
            UiKit.Place(collectionTitleRt, 112, 752 + deckExtra, 315, 47);
            UiKit.Place(collectionHintRt, 600, 760 + deckExtra, 300, 33);
            for (int i = 0; i < collection.Count; i++)
            {
                int row = i / 5;
                UiKit.Place((RectTransform)collection[i].transform, 98 + (i % 5) * 169.5f, 805 + row * 177 + deckExtra + collectionExtra * (.12f + row * .36f), 154, 164);
            }
            UiKit.Place(playRt, 245, 1364 + extra, 535, 119);
            UiKit.Place(helpRt, 210, 1485 + extra, 604, 33);
        }

        // ------------------------------------------------------------------ logique du deck

        bool ValidDeck() { return SceneFlow.IsValidDeck(deck, catalogIds); }

        void Refresh()
        {
            for (int i = 0; i < 8; i++)
            {
                CardData c = i < deck.Count ? assets.GetCard(deck[i]) : null;
                slots[i].Set(c, c == null ? "CHOISIR" : c.displayName, i == activeSlot);
            }
            for (int i = 0; i < collection.Count; i++)
            {
                var c = assets.cards[i];
                collection[i].Set(c, c.displayName, deck.Contains(c.id));
            }
            countLabel.text = "DECK  " + deck.Count + " / 8";
            float total = 0f;
            foreach (var id in deck) total += assets.GetCard(id).cost;
            averageLabel.text = "FLUIDE MOYEN : " + (total / Mathf.Max(1, deck.Count)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            helpLabel.text = activeSlot >= 0 ? "Choisis la carte de remplacement" : "Touche une carte du deck pour la remplacer";
            playButton.interactable = ValidDeck();
            if (ValidDeck()) SceneFlow.SaveDeck(deck);
        }

        void SlotPressed(int index)
        {
            if (activeSlot == index && index < deck.Count)
            {
                deck.RemoveAt(index);
                activeSlot = -1;
            }
            else activeSlot = index;
            Refresh();
        }

        void CollectionPressed(string id)
        {
            if (activeSlot >= 0) ReplaceSlot(activeSlot, id);
            else if (deck.Contains(id)) { activeSlot = deck.IndexOf(id); Refresh(); }
            else if (deck.Count < 8) { deck.Add(id); Refresh(); }
            else helpLabel.text = "Touche d’abord la carte du deck à remplacer";
        }

        void ReplaceSlot(int index, string id)
        {
            int old = deck.IndexOf(id);
            if (old >= 0)
            {
                if (index < deck.Count)
                {
                    string previous = deck[index];
                    deck[index] = id;
                    deck[old] = previous;
                }
            }
            else if (index < deck.Count) deck[index] = id;
            else deck.Add(id);
            activeSlot = -1;
            Refresh();
        }

        void StartBattle()
        {
            if (!ValidDeck()) return;
            SceneFlow.SaveDeck(deck);
            SceneFlow.GoBattle();
        }

        // ------------------------------------------------------------------ boutons du haut + réglages

        void BuildTopButtons()
        {
            // Engrenage : médaillon bleu / or
            var gear = UiKit.Panel(content, "GearButton", new Color32(0x17, 0x66, 0xa8, 0xee), UiKit.Hex("#f1c55b"), 4, 24, true);
            UiKit.Place(gear, 916, 42, 72, 72);
            var gi = UiKit.Img(gear, "Icon", UiKit.FullSprite(assets.settingsGear), Color.white);
            UiKit.Stretch(gi.rectTransform);
            gi.rectTransform.offsetMin = new Vector2(8, 8); gi.rectTransform.offsetMax = new Vector2(-8, -8);
            var gb = gear.gameObject.AddComponent<Button>();
            gb.targetGraphic = gear.GetComponent<Image>();
            gb.onClick.AddListener(() => settingsRoot.SetActive(!settingsRoot.activeSelf));

            // Accès au bac à sable de test (ajout du port Unity)
            var sb = UiKit.TextButton(content, "SandboxButton", "SANDBOX", 18, UiKit.Hex("#5c351c"), UiKit.Hex("#d69a42"), 60, 52, 170, 52, SceneFlow.GoSandbox, 14);
        }

        void BuildSettings()
        {
            settingsRoot = UiKit.Rect("Settings", content).gameObject;
            var rt = (RectTransform)settingsRoot.transform;
            UiKit.Place(rt, -100, -100, 1224, 1900);
            var shade = UiKit.Img(rt, "Shade", null, new Color32(0x10, 0x25, 0x36, 0x70), true);
            UiKit.Stretch(shade.rectTransform);
            shade.gameObject.AddComponent<Button>().onClick.AddListener(() => settingsRoot.SetActive(false));

            var panel = UiKit.Panel(rt, "Panel", new Color32(0x8b, 0x55, 0x29, 0xf8), UiKit.Hex("#f2c35c"), 7, 34, true);
            UiKit.Place(panel, 100 + 172, 100 + 330, 680, 565);
            var banner = UiKit.Panel(panel, "Banner", UiKit.Hex("#176ca8"), UiKit.Hex("#f4cf72"), 4, 22);
            UiKit.Place(banner, 34, 24, 612, 76);
            var title = UiKit.Label(banner, "Title", "PARAMÈTRES", 34, UiKit.Hex("#fff2c7"), TextAnchor.MiddleCenter, 2f, UiKit.Hex("#402410"));
            UiKit.Stretch(title.rectTransform);

            var am = AudioManager.Ensure(assets);
            var musicBox = UiKit.Panel(panel, "MusicBox", new Color32(0x5e, 0x39, 0x1f, 0xe8), UiKit.Hex("#c98b3d"), 3, 22);
            UiKit.Place(musicBox, 40, 122, 600, 174);
            var ml = UiKit.Label(musicBox, "Label", "MUSIQUE", 24, UiKit.Hex("#fff2c7"), TextAnchor.MiddleLeft, 2f, UiKit.Hex("#402410")); UiKit.Place(ml.rectTransform, 28, 14, 300, 40);
            var musicVal = UiKit.Label(musicBox, "Value", "", 22, UiKit.Hex("#fff2c7"), TextAnchor.MiddleCenter, 2f, UiKit.Hex("#402410")); UiKit.Place(musicVal.rectTransform, 472, 58, 96, 44);
            var muteText = MakeMuteButton(musicBox, am, 82, 111);
            var musicSlider = MakeSlider(musicBox, 28, 61, 430, 42, am.MusicVolume, v => { am.SetMusicVolume(v); musicVal.text = Mathf.RoundToInt(v * 100) + " %"; });
            musicVal.text = Mathf.RoundToInt(am.MusicVolume * 100) + " %";

            var sfxBox = UiKit.Panel(panel, "SfxBox", new Color32(0x5e, 0x39, 0x1f, 0xe8), UiKit.Hex("#c98b3d"), 3, 22);
            UiKit.Place(sfxBox, 40, 315, 600, 142);
            var sl = UiKit.Label(sfxBox, "Label", "EFFETS SONORES", 23, UiKit.Hex("#fff2c7"), TextAnchor.MiddleLeft, 2f, UiKit.Hex("#402410")); UiKit.Place(sl.rectTransform, 28, 13, 340, 40);
            var sfxVal = UiKit.Label(sfxBox, "Value", "", 22, UiKit.Hex("#fff2c7"), TextAnchor.MiddleCenter, 2f, UiKit.Hex("#402410")); UiKit.Place(sfxVal.rectTransform, 472, 56, 96, 44);
            MakeSlider(sfxBox, 28, 59, 430, 42, am.SfxVolume, v => { am.SetSfxVolume(v); sfxVal.text = Mathf.RoundToInt(v * 100) + " %"; });
            sfxVal.text = Mathf.RoundToInt(am.SfxVolume * 100) + " %";
            var hint = UiKit.Label(sfxBox, "Hint", "Prêt pour les futurs bruitages", 14, UiKit.Hex("#f2d59c"), TextAnchor.MiddleCenter); UiKit.Place(hint.rectTransform, 28, 103, 544, 28);

            UiKit.TextButton(panel, "Close", "FERMER", 22, UiKit.Hex("#238b62"), UiKit.Hex("#f4c760"), 215, 480, 250, 58, () => settingsRoot.SetActive(false), 18);
            settingsRoot.SetActive(false);
        }

        Text MakeMuteButton(Transform parent, AudioManager am, float x, float y)
        {
            Text label = null;
            var b = UiKit.TextButton(parent, "Mute", "", 19, UiKit.Hex("#176ca8"), UiKit.Hex("#f4c760"), x, y, 436, 48, null, 18);
            label = b.GetComponentInChildren<Text>();
            System.Action refresh = () => label.text = am.MusicMuted ? "MUSIQUE COUPÉE" : "MUSIQUE ACTIVE";
            b.onClick.AddListener(() => { am.SetMuted(!am.MusicMuted); refresh(); });
            refresh();
            return label;
        }

        Slider MakeSlider(Transform parent, float x, float y, float w, float h, float value, UnityEngine.Events.UnityAction<float> onChange)
        {
            var root = UiKit.Rect("Slider", parent);
            UiKit.Place(root, x, y, w, h);
            var bg = UiKit.Img(root, "Background", UiKit.Rounded(9), UiKit.Hex("#5c351c"));
            bg.rectTransform.anchorMin = new Vector2(0, .5f); bg.rectTransform.anchorMax = new Vector2(1, .5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 18);
            var fillArea = UiKit.Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f); fillArea.sizeDelta = new Vector2(-22, 18);
            var fill = UiKit.Img(fillArea, "Fill", UiKit.Rounded(9), UiKit.Hex("#28b989"));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1); fill.rectTransform.sizeDelta = new Vector2(10, 0);
            var handleArea = UiKit.Rect("HandleArea", root);
            UiKit.Stretch(handleArea); handleArea.offsetMin = new Vector2(11, 0); handleArea.offsetMax = new Vector2(-11, 0);
            var handle = UiKit.Img(handleArea, "Handle", UiKit.Rounded(11), UiKit.Hex("#8ff1c7"), true);
            handle.rectTransform.sizeDelta = new Vector2(30, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0); handle.rectTransform.anchorMax = new Vector2(0, 1);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight; s.minValue = 0; s.maxValue = 1; s.value = value;
            s.onValueChanged.AddListener(onChange);
            return s;
        }
    }
}
