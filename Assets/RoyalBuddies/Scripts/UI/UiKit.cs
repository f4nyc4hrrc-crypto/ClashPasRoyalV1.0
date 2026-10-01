using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoyalBuddies
{
    /// <summary>
    /// Petits utilitaires pour construire l'interface (uGUI) en code, avec des coordonnées « Godot » :
    /// origine en haut à gauche, y vers le bas, rectangle (x, y, largeur, hauteur).
    /// </summary>
    public static class UiKit
    {
        static Font font;
        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

        public static Canvas MakeCanvas(string name, int sortingOrder, Vector2 referenceResolution, CanvasScaler.ScreenMatchMode mode, float match = 0f)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = mode;
            scaler.matchWidthOrHeight = match;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<MobileCanvasSafeArea>();
            return canvas;
        }

        public static Rect ViewRect(Canvas canvas)
        {var safe=canvas.GetComponent<MobileCanvasSafeArea>();return safe ? safe.Content.rect : ((RectTransform)canvas.transform).rect;}

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Place le rectangle par rapport au coin haut-gauche du parent (coordonnées Godot).</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Change le pivot sans déplacer visuellement le rectangle.</summary>
        public static void SetPivotKeep(RectTransform rt, Vector2 pivot)
        {
            Vector2 delta = pivot - rt.pivot;
            rt.pivot = pivot;
            rt.anchoredPosition += new Vector2(delta.x * rt.sizeDelta.x, delta.y * rt.sizeDelta.y);
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Sprite Atlas(Texture2D tex, float x, float y, float w, float h, Vector4 border = default)
        {
            if (tex == null) return null;
            string key = tex.name + "#" + tex.width + "x" + tex.height + ":" + x + "," + y + "," + w + "," + h + "," + border;
            if (spriteCache.TryGetValue(key, out var s) && s != null) return s;
            s = RBGameAssets.AtlasSprite(tex, new Rect(x, y, w, h), 1f, 1f, border);
            spriteCache[key] = s;
            return s;
        }

        public static Sprite FullSprite(Texture2D tex)
        {
            if (tex == null) return null;
            return Atlas(tex, 0, 0, tex.width, tex.height);
        }

        /// <summary>Rectangle arrondi blanc 9-slice (à teinter avec Image.color).</summary>
        public static Sprite Rounded(int radius)
        {
            if (roundedCache.TryGetValue(radius, out var cached) && cached != null) return cached;
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + .5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + .5f - cx) * (x + .5f - cx) + (y + .5f - cy) * (y + .5f - cy));
                    float a = Mathf.Clamp01(radius - d + .5f);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            roundedCache[radius] = sp;
            return sp;
        }

        static readonly Dictionary<int, Sprite> ringCache = new Dictionary<int, Sprite>();

        /// <summary>Anneau arrondi (centre transparent) 9-slice : liseré de sélection.</summary>
        public static Sprite RoundedRing(int radius, int thickness)
        {
            int key = radius * 100 + thickness;
            if (ringCache.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + .5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + .5f - cx) * (x + .5f - cx) + (y + .5f - cy) * (y + .5f - cy));
                    // distance au bord extérieur (le long des côtés droits, d = 0 → on utilise la distance aux bords du carré)
                    float edge = Mathf.Min(Mathf.Min(x + .5f, size - x - .5f), Mathf.Min(y + .5f, size - y - .5f));
                    float inside = Mathf.Min(radius - d, edge);
                    float outer = Mathf.Clamp01(inside + .5f);
                    float inner = Mathf.Clamp01(inside - thickness + .5f);
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(outer - inner));
                }
            tex.SetPixels32(px);
            tex.Apply();
            int b = radius + 1;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            ringCache[key] = sp;
            return sp;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>Panneau arrondi avec liseré (deux couches : bordure puis fond).</summary>
        public static RectTransform Panel(Transform parent, string name, Color fill, Color border, float borderWidth, int radius, bool raycast = false)
        {
            var outer = Img(parent, name, Rounded(radius), border, raycast);
            var inner = Img(outer.transform, "Fill", Rounded(Mathf.Max(2, radius - 1)), fill);
            var rt = inner.rectTransform;
            Stretch(rt);
            rt.offsetMin = new Vector2(borderWidth, borderWidth);
            rt.offsetMax = new Vector2(-borderWidth, -borderWidth);
            return outer.rectTransform;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, float outline = 0f, Color? outlineColor = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            if (outline > 0f)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = outlineColor ?? new Color(0.1f, 0.06f, 0.03f, 1f);
                o.effectDistance = new Vector2(outline, -outline);
            }
            return t;
        }

        /// <summary>Bouton invisible (zone cliquable) sur un rectangle.</summary>
        public static Button HitButton(Transform parent, string name, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var img = Img(parent, name, null, new Color(1, 1, 1, 0), true);
            Place(img.rectTransform, x, y, w, h);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        /// <summary>Bouton texte stylé (fond arrondi + liseré doré), comme rb_button_style() de Godot.</summary>
        public static Button TextButton(Transform parent, string name, string label, int fontSize, Color fill, Color border,
                                        float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick, int radius = 18)
        {
            var panel = Panel(parent, name, fill, border, 4, radius, true);
            Place(panel, x, y, w, h);
            var b = panel.gameObject.AddComponent<Button>();
            b.targetGraphic = panel.GetComponent<Image>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.1f, 1f);
            colors.pressedColor = new Color(.75f, .75f, .75f, 1f);
            b.colors = colors;
            var t = Label(panel, "Text", label, fontSize, new Color32(0xff, 0xf2, 0xc7, 0xff), TextAnchor.MiddleCenter, 2f, new Color32(0x3a, 0x1b, 0x0c, 0xff));
            Stretch(t.rectTransform);
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static Color Hex(string h) => RB.HexColor(h);
    }

    /// <summary>Polygone convexe plein (éventail depuis le barycentre) : gemme de coût, couronnes.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PolygonGraphic : MaskableGraphic
    {
        public Vector2[] points = new Vector2[0];   // coordonnées locales, origine = coin haut-gauche, y vers le bas

        public override bool Raycast(Vector2 sp, Camera eventCamera) { return false; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points == null || points.Length < 3) return;
            Vector2 c = Vector2.zero;
            foreach (var p in points) c += p;
            c /= points.Length;
            vh.AddVert(ToRect(c), color, Vector2.zero);
            for (int i = 0; i < points.Length; i++) vh.AddVert(ToRect(points[i]), color, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                vh.AddTriangle(0, 1 + i, 1 + (i + 1) % points.Length);
        }

        static Vector3 ToRect(Vector2 p) { return new Vector3(p.x, -p.y, 0); }

        public void SetPoints(Vector2[] pts) { points = pts; SetVerticesDirty(); }
    }
}
