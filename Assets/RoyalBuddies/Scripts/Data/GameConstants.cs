using UnityEngine;

namespace RoyalBuddies
{
    public enum Team { Player = 0, Enemy = 1 }

    /// <summary>
    /// Constantes de jeu reprises telles quelles de main.gd (Godot).
    ///
    /// CONVENTION DE COORDONNÉES : Unity Z = −(Godot Z). Ainsi, avec une caméra qui regarde vers +Z,
    /// l'écran est identique à celui de Godot (le joueur en bas, l'adversaire en haut, +X à droite).
    /// Le joueur avance vers +Z (Unity), l'adversaire vers −Z.
    /// </summary>
    public static class RB
    {
        // --- Arène (limites de déplacement, main.gd move_to / place) ---
        public const float ArenaHalfX = 10.35f;
        public const float ArenaHalfZ = 22.5f;
        public const float RiverHalfWidth = 2.35f;      // vide de la rivière : z ∈ [−2.35 ; +2.35]
        public const float BridgeX = 5.6f;               // ponts centrés en x = ±5.6

        // --- Zones de déploiement (valeurs Godot exprimées en Z Unity) ---
        public const float DeployNearZ = 2.55f;          // moitié joueur : Unity z ≤ −2.55 (Godot z ≥ 2.55)
        public const float DeployAdvancedZ = 12.0f;      // après tour latérale ennemie détruite : Unity z ≤ +12 (Godot z ≥ −12)
        public const float DeployAdvancedMinX = 1.3f;    // |x| ≥ 1.3 pour la lane ouverte

        // --- Partie ---
        public const float MatchDuration = 180f;
        public const float OvertimeDuration = 60f;
        public const float DoubleElixirAt = 60f;

        // --- Élixir (« Fluide ») ---
        public const float ElixirStart = 7f;
        public const float ElixirMax = 10f;
        public const float ElixirBaseInterval = 2.8f;

        // --- Tours ---
        public const float SideTowerHp = 2500f;
        public const float KingTowerHp = 3600f;
        public const float SideTowerDamage = 115f;       // SIDE_TOWER_DAMAGE
        public const float KingTowerDamage = 130f;       // KING_TOWER_DAMAGE
        public const float SideTowerRange = 13.0f;
        public const float KingTowerRange = 6.35f;
        public const float TowerAttackInterval = 1.03f;
        public const float TowerFireHeight = 3.72f;
        public const float SideTowerRadius = 1.62f;      // tower_radius()
        public const float KingTowerRadius = 1.95f;
        public const float SideTowerCarveRadius = 1.30f; // obstacle NavMesh (< rayon visuel pour laisser l'accès au corps à corps)
        public const float KingTowerCarveRadius = 1.50f;

        // --- Ciblage / combat ---
        public const float AggroRadius = 7.25f;          // nearest_unit : bd = 7.25
        public const float AggroBehindTolerance = -0.65f;
        public const float AggroBehindMaxDistance = 2.20f;
        public const float CritChance = 0.08f;
        public const float CritMultiplier = 1.45f;
        public const float RageDamageTakenMultiplier = 1.18f;
        public const float ComboWindow = 4.0f;
        public const int RageComboThreshold = 5;
        public const float RageDuration = 8.0f;

        // --- Projectiles ---
        public const float ProjectileSpeed = 12f;
        public const float ProjectileHitDistance = 0.32f;
        public const float UnitFireHeight = 1.5f;
        public const float ProjectileRadius = 0.15f;

        // --- Sorts (main.gd cast_spell) ---
        public const float FireballDamage = 450f, FireballRadius = 2.65f, FireballTowerFactor = 0.55f;
        public const float ArrowRainDamage = 155f, ArrowRainRadius = 3.2f, ArrowRainDelay = 0.28f;
        public const float ZapDamage = 105f, ZapRadius = 2.25f, ZapStun = 1.0f;
        public const float FreezeDamage = 45f, FreezeRadius = 3.0f, FreezeDuration = 5.0f;   // freeze_area(pos, src, 3.0, 5.0)

        // --- Caméra (Godot : position (0,47,36), rotation −54°, fov 57) ---
        public static readonly Vector3 CameraPosition = new Vector3(0f, 47f, -36f);
        public static readonly Vector3 CameraEuler = new Vector3(54f, 0f, 0f);
        public const float CameraFov = 57f;

        // --- Couleurs d'équipe ---
        public static readonly Color PlayerColor = new Color32(0x4c, 0xae, 0xff, 0xff);
        public static readonly Color EnemyColor = new Color32(0xf2, 0x5b, 0x62, 0xff);
        public static readonly Color TowerPlayerColor = new Color32(0x3e, 0x8f, 0xd7, 0xff);
        public static readonly Color TowerEnemyColor = new Color32(0xd8, 0x4f, 0x4b, 0xff);

        /// <summary>Direction d'avance d'une équipe sur l'axe Z Unity.</summary>
        public static float Forward(Team t) => t == Team.Player ? 1f : -1f;

        public static Team Opponent(Team t) => t == Team.Player ? Team.Enemy : Team.Player;

        public static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
