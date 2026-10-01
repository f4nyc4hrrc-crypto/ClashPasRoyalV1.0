using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Conteneur unique des références d'assets utilisées par le code (cartes, matériaux, textures d'UI, sons, FX).
    /// Généré par le menu « Royal Buddies ▸ Build All » (RBBuilder) et référencé par les scènes via <see cref="GameBootstrap"/>.
    /// Passer par une référence sérialisée garantit que shaders, textures et sons sont inclus dans les builds mobiles.
    /// </summary>
    [CreateAssetMenu(menuName = "Royal Buddies/Game Assets", fileName = "RB_GameAssets")]
    public class RBGameAssets : ScriptableObject
    {
        public static RBGameAssets Current { get; set; }

        [Header("Cartes (ordre du catalogue Godot p_deck)")]
        public CardData[] cards;

        [Header("Matériaux de base (URP)")]
        public Material litMaterial;        // Universal Render Pipeline/Lit
        public Material unlitMaterial;      // Universal Render Pipeline/Unlit
        public Material particleMaterial;   // Universal Render Pipeline/Particles/Unlit (disque doux)
        public Material vertexColorMaterial;// RoyalBuddies/VertexColorLit

        [Header("Prefabs")]
        public GameObject fireballPrefab;   // RB_Fireball_ForgedMeteor (glTF)

        [Header("Tours (Awesome Stylized Mage Tower)")]
        public GameObject mageTowerModel;            // Models/MageTower/RB_MageTower.fbx
        public Material mageTowerPlayerMaterial;     // gemmes et bannières bleues (camp joueur)
        public Material mageTowerEnemyMaterial;      // texture rouge d'origine (camp adverse)

        [Header("Décor viking (modèles Kenney CC0)")]
        public TextAsset vikingLayout;               // Data/RB_VikingLayout.json
        public GameObject[] vikingModels;            // Models/Viking/*.glb
        public string[] vikingModelNames;            // noms de fichier (sans extension), alignés sur vikingModels
        public Material flowWaterMaterial;           // ruisseaux
        public Material waterfallMaterial;           // cascades

        [Header("Textures d'interface (Godot : rb_*.png)")]
        public Texture2D cardsAtlas;        // rb_cards_atlas.png
        public Texture2D zapTexture;        // rb_zap.png
        public Texture2D hudAtlas;          // rb_hud_v86.png
        public Texture2D pauseSkin;         // rb_pause_v86.png
        public Texture2D fluidWood;         // rb_fluid_wood.png
        public Texture2D fluidDrop;         // rb_fluid_drop.png
        public Texture2D menuSkin;          // rb_menu_skin.png
        public Texture2D scenery;           // rb_scenery.png
        public Texture2D settingsGear;      // rb_settings_gear.png
        public Texture2D loadingScreen;     // loading_screen.png

        [Header("Audio")]
        public AudioClip menuMusic;         // Champ de Duel.mp3
        public AudioClip battleMusic;       // Victoire Rapide.mp3

        readonly Dictionary<string, CardData> byId = new Dictionary<string, CardData>();
        readonly Dictionary<uint, Material> colorCache = new Dictionary<uint, Material>();
        readonly Dictionary<uint, Material> unlitCache = new Dictionary<uint, Material>();

        static uint Key(Color c)
        {
            Color32 k = c;
            return ((uint)k.r << 24) | ((uint)k.g << 16) | ((uint)k.b << 8) | k.a;
        }

        public CardData GetCard(string id)
        {
            if (byId.Count == 0 && cards != null)
                foreach (var c in cards) if (c != null) byId[c.id] = c;
            return byId.TryGetValue(id, out var card) ? card : null;
        }

        /// <summary>Matériau Lit uni (équivalent de material(c) dans main.gd). Mis en cache par couleur.</summary>
        public Material ColorMaterial(Color c, float metallic = 0f, float smoothness = 0.3f)
        {
            uint key = Key(c);
            if (colorCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(litMaterial) { name = "RB_Color_" + ColorUtility.ToHtmlStringRGBA(c) };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            colorCache[key] = m;
            return m;
        }

        /// <summary>Matériau Unlit uni (FX lumineux : boules, anneaux, éclairs).</summary>
        public Material UnlitColorMaterial(Color c)
        {
            uint key = Key(c);
            if (unlitCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(unlitMaterial) { name = "RB_Unlit_" + ColorUtility.ToHtmlStringRGBA(c) };
            m.SetColor("_BaseColor", c);
            unlitCache[key] = m;
            return m;
        }

        /// <summary>Crée un Sprite depuis une région d'atlas exprimée comme dans Godot (origine en haut à gauche).</summary>
        public static Sprite AtlasSprite(Texture2D tex, Rect topLeftRect, float ratioX = 1f, float ratioY = 1f, Vector4 border = default)
        {
            if (tex == null) return null;
            float x = topLeftRect.x * ratioX, w = topLeftRect.width * ratioX;
            float h = topLeftRect.height * ratioY;
            float y = tex.height - (topLeftRect.y * ratioY) - h;
            var sp = Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sp.name = tex.name + "_" + topLeftRect;
            return sp;
        }
    }
}
