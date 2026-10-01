using UnityEngine;

namespace RoyalBuddies
{
    public enum CardKind { Troop, Spell }
    public enum SpellKind { None, Fireball, ArrowRain, Zap, Freeze }

    /// <summary>Une carte du jeu (15 cartes). Coûts = p_costs (main.gd).</summary>
    [CreateAssetMenu(menuName = "Royal Buddies/Card Data", fileName = "RB_Card_")]
    public class CardData : ScriptableObject
    {
        public string id;                 // identifiant Godot : "GEANT", "BOULE DE FEU"...
        public string displayName;        // nom affiché ("GÉANT")
        public int cost = 3;
        public CardKind kind = CardKind.Troop;
        public TroopData troop;
        public SpellKind spell = SpellKind.None;
        public int catalogIndex;          // ordre dans la collection du menu (= ordre de p_deck)

        [Header("Portrait (région de RB_cards_atlas.png, 1024×1536, origine haut-gauche)")]
        public Rect portraitRect;
        public bool portraitIsSeparateTexture;   // ZAP : RB_zap.png entier
        public Color accent = new Color(0.5f, 0.45f, 0.35f);

        public bool IsSpell => kind == CardKind.Spell;
    }
}
