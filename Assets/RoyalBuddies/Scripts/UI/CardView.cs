using UnityEngine;
using UnityEngine.UI;

namespace RoyalBuddies
{
    /// <summary>
    /// Vue d'une carte en uGUI. Trois styles, portés de rb_deck_card.gd / rb_combat_card.gd :
    ///  - Combat : portrait, titre et gemme de coût sur le cadre de la main (le cadre est dans l'atlas du HUD) ;
    ///  - DeckSlot : grande carte du deck (cadre + plaque de nom) ;
    ///  - Collection : petite carte de la collection.
    /// Les portraits sont les régions de RB_cards_atlas.png (mêmes coordonnées que le jeu Godot).
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public enum Style { Combat, DeckSlot, Collection }

        public Button Button { get; private set; }
        public Style CardStyle { get; private set; }
        public CardData Card { get; private set; }

        RectTransform portraitWindow;
        Image portrait, frame, nameplateTex, shade, checkBg, hover;
        Text title, cost;
        PolygonGraphic badgeOuter, badgeInner, badgeGem;
        RectTransform root;
        float w, h;
        bool small;

        public static CardView Create(Transform parent, Style style, string name, float x, float y, float w, float h)
        {
            var bg = UiKit.Panel(parent, name, UiKit.Hex("#0a1b20"), UiKit.Hex("#bd9857"), 3, 12, true);
            if (style == Style.Combat)
            {
                // en combat, le cadre est déjà dessiné par l'atlas du HUD : fond transparent
                foreach (var img in bg.GetComponentsInChildren<Image>()) img.color = new Color(0, 0, 0, 0);
            }
            UiKit.Place(bg, x, y, w, h);
            var v = bg.gameObject.AddComponent<CardView>();
            v.Build(style, bg, w, h);
            return v;
        }

        void Build(Style style, RectTransform rt, float width, float height)
        {
            CardStyle = style; root = rt; w = width; h = height;
            small = style != Style.DeckSlot;
            Button = gameObject.AddComponent<Button>();
            Button.targetGraphic = GetComponent<Image>();
            var colors = Button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(.85f, .85f, .85f, 1f);
            Button.colors = colors;
            if (style == Style.Combat) Button.transition = Selectable.Transition.None;

            var assets = RBGameAssets.Current;
            float band = small ? 31f : 37f;

            portraitWindow=UiKit.Rect("PortraitWindow",transform);
            portraitWindow.gameObject.AddComponent<RectMask2D>();
            if(style==Style.Combat) UiKit.Place(portraitWindow,17,17,w-34,217);
            else UiKit.Place(portraitWindow,5,5,w-10,h-band-8);
            portrait=UiKit.Img(portraitWindow,"Portrait",null,Color.white);
            portrait.raycastTarget=false;
            portrait.preserveAspect=false;
            portrait.rectTransform.anchorMin=portrait.rectTransform.anchorMax=portrait.rectTransform.pivot=new Vector2(.5f,.5f);

            if (style != Style.Combat)
            {
                // cadre 9-slice (marge 7) et plaque de nom
                Rect fr = style == Style.DeckSlot ? new Rect(116, 261, 187, 201) : new Rect(98, 805, 154, 164);
                frame = UiKit.Img(transform, "Frame", UiKit.Atlas(assets.cardsAtlas, fr.x, fr.y, fr.width, fr.height, new Vector4(7, 7, 7, 7)), Color.white);
                frame.fillCenter = false;
                UiKit.Stretch(frame.rectTransform);
                var plate = UiKit.Panel(transform, "Nameplate", UiKit.Hex("#071113"), UiKit.Hex("#b18f50"), 2, 7);
                UiKit.Place(plate, 3, h - band - 3, w - 6, band);
                Rect np = style == Style.DeckSlot ? new Rect(120, 423, 179, 35) : new Rect(101, 936, 149, 31);
                nameplateTex = UiKit.Img(plate, "Tex", UiKit.Atlas(assets.cardsAtlas, np.x, np.y, np.width, np.height), Color.white);
                UiKit.Stretch(nameplateTex.rectTransform);
                title = UiKit.Label(transform, "Title", "", 15, UiKit.Hex("#fff8e4"), TextAnchor.MiddleCenter, 1.5f, UiKit.Hex("#101815"));
                UiKit.Place(title.rectTransform, 7, h - band - 3, w - 14, band);
            }
            else
            {
                title = UiKit.Label(transform, "Title", "", 30, UiKit.Hex("#fff8e4"), TextAnchor.MiddleCenter, 3f, UiKit.Hex("#101815"));
                UiKit.Place(title.rectTransform, 17, 240, w - 34, 48);
            }

            // Gemme de coût (hexagone)
            var badgeRt = UiKit.Rect("Badge", transform);
            UiKit.Place(badgeRt, 0, 0, 1, 1);
            Vector2[] pts;
            if (style == Style.Combat)
                pts = new[] { new Vector2(36, -14), new Vector2(77, 8), new Vector2(77, 65), new Vector2(36, 86), new Vector2(-5, 65), new Vector2(-5, 8) };
            else
            {
                Vector2 bs = small ? new Vector2(48, 52) : new Vector2(54, 58);
                Vector2[] unit = { new Vector2(.5f, 0), new Vector2(1, .22f), new Vector2(1, .78f), new Vector2(.5f, 1), new Vector2(0, .78f), new Vector2(0, .22f) };
                pts = new Vector2[6];
                for (int i = 0; i < 6; i++) pts[i] = new Vector2(unit[i].x * bs.x - 3, unit[i].y * bs.y - 4);
            }
            Vector2 centre = Vector2.zero; foreach (var p in pts) centre += p; centre /= pts.Length;
            badgeOuter = MakePoly(badgeRt, "Outer", Scale(pts, centre, 1f), style == Style.Combat ? UiKit.Hex("#56320d") : UiKit.Hex("#d9ba72"));
            badgeInner = MakePoly(badgeRt, "Inner", Scale(pts, centre, style == Style.Combat ? .92f : .85f), style == Style.Combat ? UiKit.Hex("#ffda70") : UiKit.Hex("#079864"));
            badgeGem = style == Style.Combat ? MakePoly(badgeRt, "Gem", Scale(pts, centre, .78f), UiKit.Hex("#11b779")) : null;

            cost = UiKit.Label(transform, "Cost", "", small ? 28 : 32, Color.white, TextAnchor.MiddleCenter, 2.5f, UiKit.Hex("#003b29"));
            if (style == Style.Combat) { UiKit.Place(cost.rectTransform, -4, -12, 81, 93); cost.fontSize = 53; }
            else UiKit.Place(cost.rectTransform, -3, -4, small ? 48 : 54, small ? 52 : 58);

            // Coche « choisie » : disque vert + 2 traits
            checkBg = UiKit.Img(transform, "Check", UiKit.Rounded(17), UiKit.Hex("#087b4b"));
            UiKit.Place(checkBg.rectTransform, w - 18 - 17, style == Style.Combat ? 0 : h - band - 19 - 17, 34, 34);
            var c1 = UiKit.Img(checkBg.transform, "A", null, Color.white); UiKit.Place(c1.rectTransform, 7, 17, 11, 4); c1.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var c2 = UiKit.Img(checkBg.transform, "B", null, Color.white); UiKit.Place(c2.rectTransform, 12, 19, 18, 4); c2.rectTransform.localRotation = Quaternion.Euler(0, 0, 50);
            checkBg.gameObject.SetActive(false);

            // Bordure de sélection (combat) : anneau vert clair
            hover = UiKit.Img(transform, "Selected", UiKit.RoundedRing(13, 4), UiKit.Hex("#afffd4"));
            UiKit.Place(hover.rectTransform, 7, 7, w - 14, h - 14);
            hover.gameObject.SetActive(false);

            // voile « Fluide insuffisant »
            shade = UiKit.Img(transform, "Shade", UiKit.Rounded(12), new Color(0.05f, 0.05f, 0.08f, 0f));
            UiKit.Stretch(shade.rectTransform);
        }

        static Vector2[] Scale(Vector2[] pts, Vector2 c, float k)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = (pts[i] - c) * k + c;
            return r;
        }

        static PolygonGraphic MakePoly(Transform parent, string name, Vector2[] pts, Color color)
        {
            var rt = UiKit.Rect(name, parent);
            UiKit.Place(rt, 0, 0, 1, 1);
            var g = rt.gameObject.AddComponent<PolygonGraphic>();
            g.color = color;
            g.SetPoints(pts);
            return g;
        }

        /// <summary>set_card() : affiche une carte (ou une case vide si card == null).</summary>
        public void Set(CardData card, string titleOverride, bool chosen)
        {
            Card = card;
            bool empty = card == null;
            var assets = RBGameAssets.Current;
            if (empty)
            {
                portrait.enabled = false;
                cost.text = "+";
            }
            else
            {
                portrait.enabled = true;
                portrait.sprite = PortraitFor(card, assets);
                FitPortrait();
                cost.text = card.cost.ToString();
            }
            string shown = titleOverride ?? (empty ? "CHOISIR" : card.displayName);
            title.text = shown;
            if (CardStyle == Style.Combat)
                title.fontSize = shown.Length > 16 ? 22 : (shown.Length > 12 ? 25 : 30);
            else
                title.fontSize = small ? (shown.Length > 14 ? 13 : 15) : 18;
            if (checkBg != null) checkBg.gameObject.SetActive(chosen && CardStyle != Style.Combat);
            if (hover != null) hover.gameObject.SetActive(chosen && CardStyle == Style.Combat);
        }

        void FitPortrait()
        {
            if(!portrait.sprite)return;
            var region=portrait.sprite.rect;
            var window=portraitWindow.sizeDelta;
            float scale=Mathf.Max(window.x/region.width,window.y/region.height);
            portrait.rectTransform.sizeDelta=new Vector2(region.width*scale,region.height*scale);
            portrait.rectTransform.anchoredPosition=Vector2.zero;
        }

        public static Sprite PortraitFor(CardData card, RBGameAssets assets)
        {
            if (card.portraitIsSeparateTexture) return UiKit.FullSprite(assets.zapTexture);
            var r = card.portraitRect;
            return UiKit.Atlas(assets.cardsAtlas, r.x, r.y, r.width, r.height);
        }

        /// <summary>Griser la carte quand le Fluide est insuffisant (modulate 0.58, 0.58, 0.64).</summary>
        public void SetAffordable(bool affordable)
        {
            if (shade != null) shade.color = new Color(0.05f, 0.05f, 0.08f, affordable ? 0f : 0.42f);
        }
    }
}
