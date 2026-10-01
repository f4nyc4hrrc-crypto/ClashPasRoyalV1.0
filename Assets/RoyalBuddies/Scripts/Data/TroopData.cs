using UnityEngine;

namespace RoyalBuddies
{
    public enum AttackKind
    {
        Melee,          // dégâts instantanés sur la cible (r ≤ 2)
        Ranged,         // projectile (r > 2)
        SplashGround,   // dégâts de zone au sol, sans projectile (Bombardeur)
        SplashAll       // dégâts de zone sol + air, sans projectile (Sorcier)
    }

    public enum RigKind { Glb, Procedural, GlbGargoyle }

    /// <summary>
    /// Statistiques et réglages d'une troupe. Valeurs numériques = main.gd, fonction st() (Godot).
    /// </summary>
    [CreateAssetMenu(menuName = "Royal Buddies/Troop Data", fileName = "RB_Troop_")]
    public class TroopData : ScriptableObject
    {
        [Header("Identité")]
        public string id;                 // identifiant Godot, ex. "GEANT"
        public string displayName;

        [Header("Statistiques (Godot st())")]
        public float hp = 720f;
        public float damage = 102f;
        public float speed = 2.42f;       // m/s
        public float range = 1.2f;        // portée d'attaque
        public float attackInterval = 0.98f;   // « a » : cadence (s)
        public float size = 0.5f;         // « sz » : rayon visuel / anneau

        [Header("Règles de combat")]
        public AttackKind attackKind = AttackKind.Melee;
        public float splashRadius = 0f;
        public bool isAir = false;              // vole (gargouille)
        public bool buildingsOnly = false;      // géant, coureur : ignorent les troupes
        public bool canHitAir = false;          // tireur, archer, sorcier, gargouille
        public bool jumpsRiver = false;         // coureur : saute la rivière quand il arrive au centre (|x| < 2.35)

        [Header("Visuel")]
        public GameObject prefab;
        public RigKind rigKind = RigKind.Glb;
        public float spawnHeight = 0f;          // gargouille : 1.75
        public float hpBarHeight = 2.75f;
        public float hpBarWidth = 1.48f;
        public Color armorColor = Color.white;  // silhouettes procédurales

        [Header("Animations")]
        public string[] attackStates;           // ex. Attack_Right, Attack_Left, Attack_Double
        public float[] attackWeights;           // poids de tirage (vide = uniforme)
        public bool noImmediateRepeat = true;   // évite deux fois la même attaque de suite
        public float animBlend = 0.06f;         // durée de fondu play(clip, blend) de Godot (0.06 – 0.10 s)
        public bool hasHitAnimation = false;
        public float sprintDuration = 0f;       // chevalier : Run pendant 5 s cumulées, puis Walk

        [Header("Événements d'animation (Animation Events)")]
        [Tooltip("false = comportement Godot exact : dégâts / projectile au début de l'animation d'attaque. " +
                 "true = dégâts / projectile déclenchés par l'Animation Event OnAttackImpact / OnArrowRelease.")]
        public bool useAnimationEvents = false;
        [Range(0f, 1f)] public float impactNormalizedTime = 0.45f;
    }
}
