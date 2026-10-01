using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Gestionnaire de partie : minuteur, mort subite, couronnes, élixir, cycles de cartes, combo/rage, fin de partie.
    /// Port de _process(), end_game(), damage_tower() (partie règles), add_combo() et start_battle() de main.gd.
    /// L'ordre de mise à jour de chaque image est celui de Godot : unités → tirs → tours → IA.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public static readonly string[] EnemyDeck = { "GARDE", "TIREUR", "CHEVALIER", "ARCHER", "BOULE DE FEU", "ZAP", "GEANT", "GARGOUILLE" };

        // --- registres ---
        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<Tower> Towers = new List<Tower>();
        readonly List<Unit> removeQueue = new List<Unit>();

        // --- systèmes ---
        public Elixir Elixir { get; } = new Elixir();
        public CardCycle PlayerCycle { get; private set; }
        public CardCycle EnemyCycle { get; private set; }
        public AIController AI { get; set; }

        // --- état de la partie ---
        /// <summary>Mode bac à sable : le chrono ne finit jamais et la destruction d'une tour ne termine pas la partie.</summary>
        public bool SandboxMode { get; set; }
        public bool BattleStarted { get; private set; }
        public bool GameOver { get; private set; }
        public bool PlayerWon { get; private set; }
        public bool Overtime { get; private set; }
        public bool SuddenDeath { get; private set; }
        public float MatchTime { get; private set; } = RB.MatchDuration;
        public float BattleElapsed { get; private set; }
        public int PlayerCrowns { get; private set; }
        public int EnemyCrowns { get; private set; }
        public int KillsPlayer { get; private set; }
        public int KillsEnemy { get; private set; }
        public int TowersDestroyedByPlayer { get; private set; }
        public int TowersDestroyedByEnemy { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public bool RageActive { get; private set; }
        public float ElixirMultiplier { get; private set; } = 1f;
        public string StatusText { get; private set; } = "";
        public string SpecialText { get; private set; } = "";

        bool lastMinuteAnnounced;
        float comboTimer, rageTimer;

        // --- événements pour l'interface ---
        public event Action<string> Announced;
        public event Action<string> StatusChanged;
        public event Action<bool> GameEnded;
        public event Action HandChanged;
        public event Action BattleBegan;

        void Awake()
        {
            I = this;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ registres

        public void Register(Unit u) { if (!Units.Contains(u)) Units.Add(u); }
        public void Unregister(Unit u) { if (u != null && !removeQueue.Contains(u)) removeQueue.Add(u); }
        public void Register(Tower t) { if (!Towers.Contains(t)) Towers.Add(t); }
        public void Unregister(Tower t) { Towers.Remove(t); }

        // ------------------------------------------------------------------ démarrage

        /// <summary>start_battle() : mélange UNE fois les 8 cartes ; 4 en main, 4 en file. Même chose pour l'IA.</summary>
        public void StartBattle(IList<string> playerDeck, IList<string> enemyDeck = null)
        {
            PlayerCycle = new CardCycle(playerDeck);
            EnemyCycle = new CardCycle(enemyDeck ?? EnemyDeck);
            Elixir.Reset();
            MatchTime = RB.MatchDuration;
            BattleElapsed = 0f;
            GameOver = false; Overtime = false; SuddenDeath = false; lastMinuteAnnounced = false;
            PlayerCrowns = EnemyCrowns = 0; KillsPlayer = KillsEnemy = 0;
            TowersDestroyedByPlayer = TowersDestroyedByEnemy = 0;
            Combo = MaxCombo = 0; RageActive = false; comboTimer = rageTimer = 0f; SpecialText = "";
            BattleStarted = true;
            Time.timeScale = 1f;
            SetStatus("Détruis la tour royale ennemie !");
            Announce("BATAILLE !");
            BattleBegan?.Invoke();
            HandChanged?.Invoke();
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            if (!BattleStarted) return;
            float dt = Time.deltaTime;
            UpdateArcade(dt);
            if (!GameOver)
            {
                BattleElapsed += dt;
                MatchTime = Mathf.Max(0f, MatchTime - dt);
                if (MatchTime <= RB.DoubleElixirAt && !lastMinuteAnnounced && !Overtime)
                {
                    lastMinuteAnnounced = true;
                    Announce("DOUBLE FLUIDE !");
                    SetStatus("⚡ DOUBLE FLUIDE !");
                }
                ElixirMultiplier = Elixir.Multiplier(Overtime, MatchTime);
                Elixir.Update(dt, ElixirMultiplier);

                // Ordre de Godot : update_units, update_shots (les projectiles ont leur Update), update_towers, ai
                for (int i = 0; i < Units.Count; i++) Units[i].Tick(dt);
                for (int i = 0; i < Towers.Count; i++) Towers[i].Tick(dt);
                if (AI != null) AI.Tick(dt);

                CheckTimeUp();
            }
        }

        void LateUpdate()
        {
            if (removeQueue.Count == 0) return;
            foreach (var u in removeQueue) Units.Remove(u);
            removeQueue.Clear();
        }

        void CheckTimeUp()
        {
            if (MatchTime > 0f) return;
            if (SandboxMode) { MatchTime = RB.MatchDuration; return; }
            if (!Overtime && PlayerCrowns == EnemyCrowns)
            {
                Overtime = true; SuddenDeath = true; MatchTime = RB.OvertimeDuration;
                Announce("MORT SUBITE !");
                SetStatus("⚡ MORT SUBITE • première tour détruite gagne !");
            }
            else if (Overtime)
            {
                float ph = 0f, eh = 0f;
                foreach (var t in Towers)
                {
                    if (!t.IsAlive) continue;
                    if (t.Team == Team.Player) ph += t.Hp; else eh += t.Hp;
                }
                EndGame(ph >= eh);
            }
            else if (PlayerCrowns > EnemyCrowns) EndGame(true);
            else EndGame(false);
        }

        // ------------------------------------------------------------------ règles

        public void OnTowerDestroyed(Tower t)
        {
            if (t.Team == Team.Player) { TowersDestroyedByEnemy++; EnemyCrowns++; }
            else { TowersDestroyedByPlayer++; PlayerCrowns++; }
            if (t.IsKing)
            {
                if (t.Team == Team.Player) EnemyCrowns = 3; else PlayerCrowns = 3;
                EndGame(t.Team != Team.Player);
            }
            else if (SuddenDeath)
            {
                EndGame(t.Team != Team.Player);
            }
        }

        public void OnUnitKilled(Unit u)
        {
            if (u.Team == Team.Player) KillsEnemy++;
            else { KillsPlayer++; AddCombo(); }
        }

        void AddCombo()
        {
            Combo++; comboTimer = RB.ComboWindow; MaxCombo = Mathf.Max(MaxCombo, Combo);
            if (Combo >= 3)
            {
                SpecialText = "COMBO x" + Combo + " !";
                if (FxManager.I != null) FxManager.I.Burst(new Vector3(0, .4f, -5.5f), RB.HexColor("#ffe06b"), 8);
            }
            if (Combo == RB.RageComboThreshold)
            {
                Announce("RAGE ROYALE !");
                RageActive = true; rageTimer = RB.RageDuration;
            }
        }

        void UpdateArcade(float dt)
        {
            if (comboTimer > 0f)
            {
                comboTimer -= dt;
                if (comboTimer <= 0f) { Combo = 0; SpecialText = ""; }
            }
            if (RageActive)
            {
                rageTimer -= dt;
                SpecialText = "RAGE ROYALE  " + Mathf.Max(0f, rageTimer).ToString("0.0") + "s";
                if (rageTimer <= 0f) { RageActive = false; Combo = 0; SpecialText = ""; }
            }
        }

        public void EndGame(bool playerWins)
        {
            if (GameOver) return;
            if (SandboxMode)
            {
                SetStatus(playerWins ? "Sandbox : la tour royale ennemie est tombée" : "Sandbox : votre tour royale est tombée");
                return;
            }
            GameOver = true;
            PlayerWon = playerWins;
            foreach (var u in Units) u.Halt();
            SetStatus(playerWins ? "🏆 VICTOIRE ROYALE !" : "☠ DÉFAITE");
            Announce(playerWins ? "VICTOIRE !" : "DÉFAITE");
            Time.timeScale = 1f;
            GameEnded?.Invoke(playerWins);
        }

        /// <summary>abandon_battle() : toujours une défaite ; donne la victoire à l'adversaire.</summary>
        public void Abandon()
        {
            if (GameOver) return;
            EnemyCrowns = Mathf.Min(3, Mathf.Max(EnemyCrowns, PlayerCrowns + 1));
            Time.timeScale = 1f;
            SetStatus("⚑ ABANDON • DÉFAITE");
            EndGame(false);
        }

        public void Pause() { if (BattleStarted && !GameOver) Time.timeScale = 0f; }
        public void Resume() { Time.timeScale = 1f; }

        public void Announce(string text) { Announced?.Invoke(text); }
        public void SetStatus(string text) { StatusText = text; StatusChanged?.Invoke(text); }

        // ------------------------------------------------------------------ cartes

        /// <summary>
        /// Joue une carte : dépense le Fluide, fait apparaître la troupe ou lance le sort, fait tourner la main.
        /// Retourne false si le Fluide est insuffisant. Les règles de zone sont vérifiées par PlacementRules.
        /// </summary>
        public bool PlayCard(Team team, CardData card, Vector3 pos)
        {
            if (card == null || GameOver) return false;
            if (!Elixir.CanAfford(team, card.cost)) return false;
            Elixir.Spend(team, card.cost);
            Deploy(card, pos, team);
            var cycle = team == Team.Player ? PlayerCycle : EnemyCycle;
            if (cycle != null) cycle.Play(card.id);
            if (team == Team.Player) HandChanged?.Invoke();
            return true;
        }

        /// <summary>spawn() de main.gd : troupe ou sort.</summary>
        public void Deploy(CardData card, Vector3 pos, Team team)
        {
            if (card.IsSpell) SpellSystem.Cast(card.spell, pos, team);
            else Unit.Spawn(card.troop, pos, team);
        }

        // ------------------------------------------------------------------ dégâts de zone

        /// <summary>splash_damage() : le Sorcier frappe sol + air, le Bombardeur reste strictement terrestre.</summary>
        public void SplashDamage(Vector3 center, Team source, float dmg, float radius, bool hitAir)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var q = Units[i];
                if (q.IsAlive && q.Team != source && (hitAir || !q.IsAir) && Vector3.Distance(q.Position, center) <= radius)
                    q.ApplyDamage(dmg);
            }
            if (FxManager.I != null) FxManager.I.Burst(center, RB.HexColor("#ff9c52"), 14);
        }

        public bool SideTowerDestroyed(Team owner, int laneSign)
        {
            foreach (var t in Towers)
                if (t.Team == owner && !t.IsKing && Mathf.Sign(t.Position.x) == laneSign) return !t.IsAlive;
            return false;
        }
    }
}
