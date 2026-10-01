using UnityEngine;

namespace RoyalBuddies.EditorTools
{
    /// <summary>
    /// Tables de données recopiées de main.gd (fonction st(), p_costs, scales de spawn()) et de rb_deck_screen.gd (REGIONS).
    /// Le builder les transforme en ScriptableObjects (TroopData / CardData).
    /// </summary>
    public static class RBDataTables
    {
        public class TroopRow
        {
            public string id, display, ascii;
            public float hp, dmg, speed, range, interval, size;
            public AttackKind kind = AttackKind.Melee;
            public float splash;
            public bool air, buildingsOnly, canHitAir, jumps;
            public RigKind rig = RigKind.Glb;
            public float spawnHeight;
            public string model;          // fichier .glb dans Models/ (null = procédural)
            public float modelScale = 1f;
            public string idleClip, moveClip, walkClip, hitClip, deathClip, jumpClip;
            public string[] attackClips;
            public float blend = 0.06f;
            public float sprint;
            public Color armor = Color.white;
            public bool vertexColors;     // le modèle utilise COLOR_0 (Blender)
            public float impactTime = 0.45f;   // position (0-1) de l'Animation Event d'impact dans l'animation d'attaque
        }

        static Color H(string h) { return RB.HexColor(h); }

        public static readonly TroopRow[] Troops =
        {
            new TroopRow { id="GARDE", display="GARDE", ascii="GARDE", hp=720, dmg=102, speed=2.42f, range=1.2f, interval=.98f, size=.5f,
                rig=RigKind.Procedural, armor=H("#e2b84d") },
            new TroopRow { id="TIREUR", display="TIREUR", ascii="TIREUR", hp=460, dmg=86, speed=2.3f, range=5.1f, interval=1.12f, size=.45f,
                kind=AttackKind.Ranged, canHitAir=true, rig=RigKind.Procedural, armor=H("#61c7dd") },
            new TroopRow { id="GARGOUILLE", display="GARGOUILLE", ascii="GARGOUILLE", hp=700, dmg=120, speed=3.15f, range=1.35f, interval=1.10f, size=.48f,
                air=true, canHitAir=true, rig=RigKind.GlbGargoyle, spawnHeight=1.75f, model="RB_Gargouille.glb", modelScale=0.58f,
                idleClip="Gargouille_Attente_5s", blend=0f },
            new TroopRow { id="ÉCLAIREUR", display="ÉCLAIREUR", ascii="ECLAIREUR", hp=410, dmg=70, speed=3.7f, range=1.1f, interval=.67f, size=.42f,
                rig=RigKind.Procedural, armor=H("#64c476") },
            new TroopRow { id="BOMBARDEUR", display="BOMBARDEUR", ascii="BOMBARDEUR", hp=550, dmg=190, speed=2.0f, range=8.02f, interval=1.45f, size=.53f,
                kind=AttackKind.SplashGround, splash=1.8f, model="RB_Bombardeur_V2.glb", modelScale=0.62f,
                idleClip="Idle", moveClip="Move", attackClips=new[]{"Attack"}, deathClip="Death", blend=.06f, impactTime=.55f },
            new TroopRow { id="CHEVALIER", display="CHEVALIER", ascii="CHEVALIER", hp=1050, dmg=132, speed=2.05f, range=1.25f, interval=1.08f, size=.60f,
                model="RB_Chevalier_V1.glb", modelScale=1.35f, vertexColors=true,
                idleClip="Idle", moveClip="Run", walkClip="Walk", hitClip="Hit", deathClip="Death",
                attackClips=new[]{"Attack_01","Attack_02","Attack_03","Attack_04","Attack_05"}, blend=.06f, sprint=5f },
            new TroopRow { id="ARCHER", display="ARCHER", ascii="ARCHER", hp=430, dmg=76, speed=2.45f, range=5.6f, interval=.95f, size=.43f,
                kind=AttackKind.Ranged, canHitAir=true, model="RB_Archere_V1.glb", modelScale=1.65f, vertexColors=true,
                idleClip="Idle", moveClip="Run", hitClip="Hit", deathClip="Death",
                attackClips=new[]{"Attack_01","Attack_02","Attack_03"}, blend=.06f, impactTime=.5f },
            new TroopRow { id="COUREUR", display="COUREUR", ascii="COUREUR", hp=850, dmg=350, speed=4.15f, range=1.08f, interval=.58f, size=.40f,
                buildingsOnly=true, jumps=true, model="RB_Coureur_V1.glb", modelScale=1.15f, vertexColors=true,
                idleClip="Run", moveClip="Run", deathClip="Death", jumpClip="Jump", attackClips=new[]{"Attack"}, blend=.06f, impactTime=.4f },
            new TroopRow { id="SORCIER", display="SORCIER", ascii="SORCIER", hp=610, dmg=128, speed=2.05f, range=5.3f, interval=1.35f, size=.52f,
                kind=AttackKind.SplashAll, splash=1.35f, canHitAir=true, model="RB_Sorcier_V1.glb", modelScale=1.04f, vertexColors=true,
                idleClip="Idle", moveClip="Walk", deathClip="Death", attackClips=new[]{"Attack"}, blend=.08f, impactTime=.55f },
            new TroopRow { id="GOBELIN", display="GOBELIN", ascii="GOBELIN", hp=280, dmg=78, speed=4.0f, range=1.05f, interval=.62f, size=.36f,
                rig=RigKind.Procedural, armor=H("#5b8f3d") },
            new TroopRow { id="GEANT", display="GÉANT", ascii="GEANT", hp=2350, dmg=225, speed=1.28f, range=1.35f, interval=1.65f, size=.86f,
                buildingsOnly=true, model="RB_Geant_V1.glb", modelScale=1.30f, vertexColors=true,
                idleClip="Idle", moveClip="Walk", hitClip="Hit", deathClip="Death",
                attackClips=new[]{"Attack_Right","Attack_Left","Attack_Double"}, blend=.10f, impactTime=.5f },
        };

        public class CardRow
        {
            public string id, display, ascii;
            public int cost;
            public bool spell;
            public SpellKind spellKind;
            public Rect portrait;
            public bool separate;
            public Color accent;
        }

        static Color A(string h) { return RB.HexColor(h); }

        /// <summary>Ordre du catalogue = p_deck de main.gd. Régions = REGIONS de rb_deck_screen.gd (origine en haut à gauche).</summary>
        public static readonly CardRow[] Cards =
        {
            new CardRow { id="GARDE", display="GARDE", ascii="GARDE", cost=3, portrait=new Rect(104,812,142,121), accent=A("#a99365") },
            new CardRow { id="TIREUR", display="TIREUR", ascii="TIREUR", cost=3, portrait=new Rect(533,481,171,151), accent=A("#7e7158") },
            new CardRow { id="ÉCLAIREUR", display="ÉCLAIREUR", ascii="ECLAIREUR", cost=2, portrait=new Rect(445,812,139,121), accent=A("#698c63") },
            new CardRow { id="BOMBARDEUR", display="BOMBARDEUR", ascii="BOMBARDEUR", cost=4, portrait=new Rect(614,812,138,121), accent=A("#a65c45") },
            new CardRow { id="CHEVALIER", display="CHEVALIER", ascii="CHEVALIER", cost=4, portrait=new Rect(783,812,137,121), accent=A("#a99365") },
            new CardRow { id="ARCHER", display="ARCHER", ascii="ARCHER", cost=3, portrait=new Rect(124,481,171,151), accent=A("#698c63") },
            new CardRow { id="COUREUR", display="COUREUR", ascii="COUREUR", cost=2, portrait=new Rect(276,989,139,119), accent=A("#7e7158") },
            new CardRow { id="BOULE DE FEU", display="BOULE DE FEU", ascii="BOULE_DE_FEU", cost=4, spell=true, spellKind=SpellKind.Fireball, portrait=new Rect(740,267,168,154), accent=A("#a65c45") },
            new CardRow { id="PLUIE DE FLÈCHES", display="PLUIE DE FLÈCHES", ascii="PLUIE_DE_FLECHES", cost=3, spell=true, spellKind=SpellKind.ArrowRain, portrait=new Rect(614,989,138,119), accent=A("#7e7158") },
            new CardRow { id="ZAP", display="ZAP", ascii="ZAP", cost=2, spell=true, spellKind=SpellKind.Zap, separate=true, portrait=new Rect(0,0,1254,1254), accent=A("#657fa8") },
            new CardRow { id="GEL", display="GEL", ascii="GEL", cost=4, spell=true, spellKind=SpellKind.Freeze, portrait=new Rect(123,267,172,154), accent=A("#657fa8") },
            new CardRow { id="SORCIER", display="SORCIER", ascii="SORCIER", cost=5, portrait=new Rect(740,481,168,151), accent=A("#657fa8") },
            new CardRow { id="GOBELIN", display="GOBELIN", ascii="GOBELIN", cost=2, portrait=new Rect(329,267,169,154), accent=A("#71904d") },
            new CardRow { id="GEANT", display="GÉANT", ascii="GEANT", cost=6, portrait=new Rect(329,481,170,151), accent=A("#a76c43") },
            new CardRow { id="GARGOUILLE", display="GARGOUILLE", ascii="GARGOUILLE", cost=3, portrait=new Rect(533,267,171,154), accent=A("#6f82a8") },
        };
    }
}
