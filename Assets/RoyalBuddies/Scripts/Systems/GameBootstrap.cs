using UnityEngine;
using Unity.AI.Navigation;

namespace RoyalBuddies
{
    /// <summary>
    /// Point d'entrée des scènes de jeu (Battle et Sandbox) : branche les références d'assets, crée les systèmes
    /// (GameManager, FX, audio, IA, placement, interface), génère le NavMesh à l'exécution et démarre la partie.
    /// Équivalent de Main._ready() + start_battle() de main.gd.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        public RBGameAssets assets;
        public NavMeshSurface navSurface;
        public GameObject deployHint;
        public Camera cam;

        [Header("Options")]
        public bool sandbox = false;             // scène Sandbox : pas de fin de partie, pose libre
        public bool startBattleOnPlay = true;
        public bool enableAI = true;
        public bool enablePlacement = true;
        public bool enableHud = true;

        public GameManager Game { get; private set; }
        public PlacementController Placement { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            RBGameAssets.Current = assets;
            PrimitiveFactory.MaterialProvider = null;   // à l'exécution : matériaux mis en cache de RBGameAssets

            FxManager.Ensure();
            Game = FindAnyObjectByType<GameManager>();
            if (Game == null) Game = new GameObject("RB_GameManager").AddComponent<GameManager>();
            Game.SandboxMode = sandbox;
            if (cam == null) cam = Camera.main;

            if (navSurface != null)
            {
                EnsureNavAgentType();
                navSurface.agentTypeID = Unit.NavAgentTypeId;
                navSurface.BuildNavMesh();
            }

            if (enableAI) Game.AI = Game.gameObject.AddComponent<AIController>();
            if (enablePlacement && !sandbox)
            {
                Placement = Game.gameObject.AddComponent<PlacementController>();
                Placement.cam = cam;
                Placement.deployHint = deployHint;
            }
            AudioManager.Ensure(assets).PlayBattle();
        }

        static bool navTypeCreated;

        /// <summary>
        /// Crée un type d'agent NavMesh au rayon fin (0.3) : les troupes peuvent longer les berges et atteindre
        /// les points d'entrée des ponts (le type Humanoid par défaut, rayon 0.5, érode trop les bords).
        /// </summary>
        static void EnsureNavAgentType()
        {
            if (navTypeCreated) return;
            var settings = UnityEngine.AI.NavMesh.CreateSettings();
            settings.agentRadius = 0.3f;
            settings.agentHeight = 2f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 45f;
            Unit.NavAgentTypeId = settings.agentTypeID;
            navTypeCreated = true;
        }

        void Start()
        {
            var catalog = new System.Collections.Generic.List<string>();
            foreach (var c in assets.cards) catalog.Add(c.id);
            if (startBattleOnPlay)
                Game.StartBattle(sandbox ? new System.Collections.Generic.List<string>(SceneFlow.DefaultDeck) : SceneFlow.LoadDeck(catalog));

            if (sandbox) SandboxController.Create(assets, Game, cam);
            else if (enableHud && Placement != null) BattleHUD.Create(Game, Placement, assets);
        }
    }
}
