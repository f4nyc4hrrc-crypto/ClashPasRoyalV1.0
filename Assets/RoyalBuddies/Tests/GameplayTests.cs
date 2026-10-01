using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoyalBuddies.Tests
{
    /// <summary>
    /// Tests de comportement (PlayMode, simulation réelle) : animations, événements, saut du coureur, règles de ciblage,
    /// sorts, fin de partie, pause. Chaque test décrit la règle de main.gd vérifiée.
    /// </summary>
    public class GameplayTests
    {
        static GameManager GM => GameManager.I;

        static IEnumerator RunFor(float simSeconds)
        {
            float t = 0f;
            while (t < simSeconds) { yield return null; t += Time.deltaTime; }
        }

        static Unit FirstUnit(Team team, string id = null)
        {
            foreach (var u in GM.Units)
                if (u.IsAlive && u.Team == team && (id == null || u.Data.id == id)) return u;
            return null;
        }

        static Unit SpawnUnit(string id, Vector3 pos, Team team)
        {
            var card = RBGameAssets.Current.GetCard(id);
            GM.Deploy(card, pos, team);
            return GM.Units[GM.Units.Count - 1];
        }

        // ---------------------------------------------------------------- pures (sans scène)

        [Test]
        public void Elixir_Starts_At_7_And_Ticks_Every_2_8s()
        {
            var e = new Elixir();
            Assert.AreEqual(7f, e.Player); Assert.AreEqual(7f, e.Enemy);
            e.Update(2.79f, 1f); Assert.AreEqual(7f, e.Player);
            e.Update(0.02f, 1f); Assert.AreEqual(8f, e.Player);
            e.Update(0.5f * 2.8f, 2f); Assert.AreEqual(9f, e.Player, "x2 : un point toutes les 1.4 s");
            for (int i = 0; i < 10; i++) e.Update(2.8f, 1f);
            Assert.AreEqual(10f, e.Player, "plafonné à 10");
            Assert.AreEqual(1f, Elixir.Multiplier(false, 120f));
            Assert.AreEqual(2f, Elixir.Multiplier(false, 60f));
            Assert.AreEqual(3f, Elixir.Multiplier(true, 60f));
        }

        [Test]
        public void CardCycle_Is_Strict_4_In_Hand_4_In_Queue()
        {
            var deck = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
            var c = new CardCycle(deck);
            Assert.AreEqual(4, c.Hand.Count); Assert.AreEqual(4, c.Queue.Count);
            string played = c.Hand[1], incoming = c.Queue[0];
            c.Play(played);
            Assert.AreEqual(incoming, c.Hand[1], "la carte piochée prend la place de la carte jouée");
            Assert.AreEqual(played, c.Queue[3], "la carte jouée part en fin de file");
            var all = new System.Collections.Generic.HashSet<string>(c.Hand); foreach (var q in c.Queue) all.Add(q);
            Assert.AreEqual(8, all.Count, "aucune carte perdue");
        }

        [Test]
        public void Deck_Validation_Rejects_Duplicates_And_Unknown()
        {
            var catalog = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I" };
            Assert.IsTrue(SceneFlow.IsValidDeck(new[] { "A", "B", "C", "D", "E", "F", "G", "H" }, catalog));
            Assert.IsFalse(SceneFlow.IsValidDeck(new[] { "A", "A", "C", "D", "E", "F", "G", "H" }, catalog));
            Assert.IsFalse(SceneFlow.IsValidDeck(new[] { "A", "B", "C", "D", "E", "F", "G", "Z" }, catalog));
            Assert.IsFalse(SceneFlow.IsValidDeck(new[] { "A", "B", "C" }, catalog));
        }

        // ---------------------------------------------------------------- données Godot

        [UnityTest]
        public IEnumerator Data_Matches_Godot_Stats()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var a = RBGameAssets.Current;
            (string id, float hp, float d, float s, float r, float cd, int cost)[] expected =
            {
                ("GARDE",720,102,2.42f,1.2f,.98f,3), ("TIREUR",460,86,2.3f,5.1f,1.12f,3), ("GARGOUILLE",700,120,3.15f,1.35f,1.10f,3),
                ("ÉCLAIREUR",410,70,3.7f,1.1f,.67f,2), ("BOMBARDEUR",550,190,2.0f,8.02f,1.45f,4), ("CHEVALIER",1050,132,2.05f,1.25f,1.08f,4),
                ("ARCHER",430,76,2.45f,5.6f,.95f,3), ("COUREUR",850,350,4.15f,1.08f,.58f,2), ("SORCIER",610,128,2.05f,5.3f,1.35f,5),
                ("GOBELIN",280,78,4.0f,1.05f,.62f,2), ("GEANT",2350,225,1.28f,1.35f,1.65f,6),
            };
            foreach (var e in expected)
            {
                var c = a.GetCard(e.id);
                Assert.IsNotNull(c, e.id); Assert.AreEqual(e.cost, c.cost, e.id + " coût");
                var t = c.troop;
                Assert.AreEqual(e.hp, t.hp, e.id); Assert.AreEqual(e.d, t.damage, e.id); Assert.AreEqual(e.s, t.speed, .0001f, e.id);
                Assert.AreEqual(e.r, t.range, .0001f, e.id); Assert.AreEqual(e.cd, t.attackInterval, .0001f, e.id);
                Assert.IsNotNull(t.prefab, e.id + " prefab");
            }
            Assert.AreEqual(15, a.cards.Length);
            Assert.AreEqual(4, a.GetCard("BOULE DE FEU").cost); Assert.AreEqual(3, a.GetCard("PLUIE DE FLÈCHES").cost);
            Assert.AreEqual(2, a.GetCard("ZAP").cost); Assert.AreEqual(4, a.GetCard("GEL").cost);
            Assert.IsTrue(a.GetCard("GEANT").troop.buildingsOnly); Assert.IsTrue(a.GetCard("COUREUR").troop.buildingsOnly);
            Assert.IsTrue(a.GetCard("GARGOUILLE").troop.isAir);
        }

        // ---------------------------------------------------------------- animations

        [UnityTest]
        public IEnumerator Chevalier_Animator_Moves_Attacks_Dies_And_Despawns()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var u = SpawnUnit("CHEVALIER", new Vector3(-5.6f, 0, -6f), Team.Player);
            var anim = u.ModelAnimator;
            Assert.IsNotNull(anim, "Animator du chevalier");
            Assert.IsNotNull(anim.runtimeAnimatorController, "Animator Controller");
            yield return RunFor(0.8f);
            Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Move") || anim.GetNextAnimatorStateInfo(0).IsName("Move"), "Run en déplacement (état Move)");
            // Fait venir un ennemi au contact pour déclencher l'attaque
            var foe = SpawnUnit("GARDE", u.Position + new Vector3(0, 0, 1.0f), Team.Enemy);
            bool attacked = false;
            for (float t = 0; t < 3f && !attacked; t += Time.deltaTime)
            {
                yield return null;
                var st = anim.GetCurrentAnimatorStateInfo(0);
                for (int i = 1; i <= 5; i++) if (st.IsName("Attack_0" + i) || anim.GetNextAnimatorStateInfo(0).IsName("Attack_0" + i)) attacked = true;
            }
            Assert.IsTrue(attacked, "un état Attack_0x doit être joué");
            u.ApplyDamage(99999f);
            yield return null; yield return null;
            Assert.IsFalse(u.IsAlive);
            Assert.IsTrue(anim.GetCurrentAnimatorStateInfo(0).IsName("Death") || anim.GetNextAnimatorStateInfo(0).IsName("Death"), "état Death");
            Assert.IsFalse(GM.Units.Contains(u), "retiré du registre dès la mort");
            yield return RunFor(3.5f);   // Death 1.8 s + .08 + .18 s de rétrécissement
            Assert.IsTrue(u == null, "le chevalier doit être détruit (despawn)");
        }

        [UnityTest]
        public IEnumerator Attack_Variation_Never_Repeats_Immediately()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var card = RBGameAssets.Current.GetCard("CHEVALIER").troop;
            var go = Object.Instantiate(card.prefab);
            var animator = go.GetComponentInChildren<Animator>();
            var ua = new UnitAnimation(animator, card);
            string last = null; var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                string s = ua.PlayAttack();
                Assert.AreNotEqual(last, s, "pas de répétition immédiate");
                seen.Add(s); last = s;
            }
            Assert.AreEqual(5, seen.Count, "les 5 attaques du chevalier sont utilisées");
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Animation_Events_Drive_Damage_When_Enabled()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var data = RBGameAssets.Current.GetCard("CHEVALIER").troop;
            bool old = data.useAnimationEvents;
            data.useAnimationEvents = true;
            try
            {
                var tower = PlayModeTests.FindTower(Team.Enemy, false);
                // pose le chevalier au pied de la tour ennemie
                var u = SpawnUnit("CHEVALIER", tower.Position + new Vector3(0, 0, -2.0f), Team.Player);
                Time.timeScale = 3f;
                float t = 0f;
                while (t < 20f && tower.Hp >= tower.MaxHp) { yield return null; t += Time.deltaTime; }
                Time.timeScale = 1f;
                Assert.Less(tower.Hp, tower.MaxHp, "la tour doit perdre des PV via l'Animation Event OnAttackImpact");
            }
            finally { data.useAnimationEvents = old; Time.timeScale = 1f; }
        }

        [UnityTest]
        public IEnumerator Archer_Fires_A_Projectile_That_Hits()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var foe = SpawnUnit("CHEVALIER", new Vector3(-5.6f, 0, 6f), Team.Enemy);
            var archer = SpawnUnit("ARCHER", new Vector3(-5.6f, 0, 1.0f), Team.Player);
            bool sawProjectile = false;
            float hp0 = foe.Hp;
            for (float t = 0; t < 4f; t += Time.deltaTime)
            {
                yield return null;
                if (Object.FindAnyObjectByType<Projectile>() != null) sawProjectile = true;
            }
            Assert.IsTrue(sawProjectile, "un projectile doit être créé");
            Assert.Less(foe.Hp, hp0, "et toucher sa cible");
        }

        // ---------------------------------------------------------------- navigation & ciblage

        [UnityTest]
        public IEnumerator Coureur_Jumps_The_River_From_The_Center_And_Hits_A_Tower()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var u = SpawnUnit("COUREUR", new Vector3(0f, 0, -7f), Team.Player);
            float maxY = 0f; bool landed = false;
            Time.timeScale = 3f;
            float t = 0f;
            while (t < 25f)
            {
                yield return null; t += Time.deltaTime;
                if (u == null) break;
                maxY = Mathf.Max(maxY, u.transform.position.y);
                if (u.Position.z > 2.6f) landed = true;
                bool damaged = false;
                foreach (var tw in GM.Towers) if (tw.Team == Team.Enemy && tw.Hp < tw.MaxHp) damaged = true;
                if (damaged) break;
            }
            Time.timeScale = 1f;
            Assert.Greater(maxY, 1.0f, "le coureur doit sauter (arc jusqu'à 1.35 m)");
            Assert.IsTrue(landed, "et atterrir de l'autre côté de la rivière");
        }

        [UnityTest]
        public IEnumerator Coureur_Uses_Bridge_When_Not_Central()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var u = SpawnUnit("COUREUR", new Vector3(-5.6f, 0, -7f), Team.Player);
            float maxY = 0f; float t = 0f;
            Time.timeScale = 3f;
            while (t < 8f) { yield return null; t += Time.deltaTime; if (u == null) break; maxY = Mathf.Max(maxY, u.transform.position.y); }
            Time.timeScale = 1f;
            Assert.Less(maxY, 0.3f, "hors du centre : pont obligatoire, aucun saut");
            Assert.Greater(u.Position.z, 2f, "il a traversé");
        }

        [UnityTest]
        public IEnumerator Ground_Melee_Cannot_Target_Air_And_Giant_Ignores_Troops()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            var garde = SpawnUnit("GARDE", new Vector3(-8f, 0, -6f), Team.Player);
            var garg = SpawnUnit("GARGOUILLE", new Vector3(-8f, 0, -5f), Team.Enemy);   // à ~1 m
            var giant = SpawnUnit("GEANT", new Vector3(6f, 0, -6f), Team.Player);
            var bait = SpawnUnit("GARDE", new Vector3(6f, 0, -3.5f), Team.Enemy);
            yield return RunFor(0.4f);
            Assert.IsFalse(garde.CurrentTarget is Unit u1 && u1 == garg, "un GARDE ne peut pas viser une gargouille (air)");
            Assert.IsFalse(giant.CurrentTarget is Unit, "le géant est « bâtiments uniquement »");
            var tireur = SpawnUnit("TIREUR", new Vector3(-2f, 0, -8f), Team.Player);
            var garg2 = SpawnUnit("GARGOUILLE", new Vector3(-2f, 0, -5f), Team.Enemy);
            yield return RunFor(0.3f);
            Assert.IsTrue(tireur.CurrentTarget is Unit tt && tt == garg2, "un TIREUR peut viser l'air");
        }

        [UnityTest]
        public IEnumerator Placement_Rules_Match_Godot_Zones()
        {
            yield return PlayModeTests.LoadScene("RB_Battle");
            string msg;
            Assert.IsTrue(PlacementController.IsLegal(GM, new Vector3(0, 0, -5), false, out msg), "moitié du joueur");
            Assert.IsFalse(PlacementController.IsLegal(GM, new Vector3(0, 0, 5), false, out msg), "moitié adverse interdite");
            Assert.IsFalse(PlacementController.IsLegal(GM, new Vector3(11f, 0, -5), false, out msg), "hors arène");
            Assert.IsTrue(PlacementController.IsLegal(GM, new Vector3(0, 0, 15), true, out msg), "sort : partout");
            Assert.IsFalse(PlacementController.IsLegal(GM, new Vector3(0, 0, 25), true, out msg), "sort hors arène");
            // détruit la tour latérale gauche ennemie : la lane gauche s'ouvre jusqu'à z ≤ 12
            Tower left = null;
            foreach (var t in GM.Towers) if (t.Team == Team.Enemy && !t.IsKing && t.Position.x < 0) left = t;
            left.ApplyDamage(99999f);
            Assert.IsTrue(PlacementController.IsLegal(GM, new Vector3(-6, 0, 8), false, out msg), "lane gauche ouverte");
            Assert.IsFalse(PlacementController.IsLegal(GM, new Vector3(6, 0, 8), false, out msg), "lane droite toujours fermée");
            Assert.IsFalse(PlacementController.IsLegal(GM, new Vector3(-6, 0, 14), false, out msg), "pas au-delà de z = 12");
            Assert.AreEqual(1, GM.PlayerCrowns);
        }

        // ---------------------------------------------------------------- sorts

        [UnityTest]
        public IEnumerator Spells_Deal_Godot_Damage_And_Effects()
        {
            yield return PlayModeTests.LoadScene("RB_Sandbox");
            // Boule de feu : 450 dans un rayon de 2.65, tours à 55 %
            var tank = SpawnUnit("GEANT", new Vector3(-9f, 0, 4f), Team.Enemy);
            float hp0 = tank.Hp;
            var tower = PlayModeTests.FindTower(Team.Enemy, false);
            float th0 = tower.Hp;
            PlayModeTests.Spawn("BOULE DE FEU", new Vector3(-9f, 0, 4f), Team.Player);
            yield return RunFor(2.2f);
            Assert.That(hp0 - tank.Hp, Is.EqualTo(450f).Within(450f * 0.46f + 1f), "boule de feu ≈ 450 (critique possible ×1.45)");
            Assert.Greater(tank.Hp, 0f);
            // Pluie de flèches : 155
            var t2 = SpawnUnit("GEANT", new Vector3(8f, 0, 6f), Team.Enemy);
            float h2 = t2.Hp;
            t2.Stun(0f);
            PlayModeTests.Spawn("PLUIE DE FLÈCHES", new Vector3(8f, 0, 6f), Team.Player);
            yield return RunFor(0.6f);
            Assert.That(h2 - t2.Hp, Is.EqualTo(155f).Within(155f * 0.46f + 1f), "pluie de flèches ≈ 155");
            // Gel : gèle 5 s
            var t3 = SpawnUnit("CHEVALIER", new Vector3(0f, 0, 8f), Team.Enemy);
            PlayModeTests.Spawn("GEL", new Vector3(0f, 0, 8f), Team.Player);
            yield return RunFor(0.4f);
            Assert.IsTrue(t3.IsFrozen, "unité gelée");
            Vector3 p = t3.Position;
            yield return RunFor(2f);
            Assert.AreEqual(0f, Vector3.Distance(p, t3.Position), 0.05f, "une unité gelée ne bouge pas");
            yield return RunFor(3.4f);
            Assert.IsFalse(t3.IsFrozen, "dégel après 5 s");
            // Zap : 105 + étourdissement 1 s
            var t4 = SpawnUnit("GARDE", new Vector3(-4f, 0, 14f), Team.Enemy);
            float h4 = t4.Hp;
            PlayModeTests.Spawn("ZAP", new Vector3(-4f, 0, 14f), Team.Player);
            yield return RunFor(0.2f);
            Assert.That(h4 - t4.Hp, Is.EqualTo(105f).Within(105f * 0.46f + 1f), "zap ≈ 105");
        }

        // ---------------------------------------------------------------- fin de partie

        [UnityTest]
        public IEnumerator King_Tower_Destroyed_Ends_Game_With_EndScreen()
        {
            yield return PlayModeTests.LoadScene("RB_Battle");
            var king = PlayModeTests.FindTower(Team.Enemy, true);
            king.ApplyDamage(999999f);
            yield return RunFor(1.2f);
            Assert.IsTrue(GM.GameOver); Assert.IsTrue(GM.PlayerWon); Assert.AreEqual(3, GM.PlayerCrowns);
            Assert.IsNotNull(Object.FindAnyObjectByType<EndScreen>(), "écran de fin affiché");
            yield return PlayModeTests.Capture("end_victory");
        }

        [UnityTest]
        public IEnumerator Abandon_Gives_Victory_To_Enemy()
        {
            yield return PlayModeTests.LoadScene("RB_Battle");
            GM.Abandon();
            yield return RunFor(1.2f);
            Assert.IsTrue(GM.GameOver); Assert.IsFalse(GM.PlayerWon); Assert.GreaterOrEqual(GM.EnemyCrowns, 1);
            yield return PlayModeTests.Capture("end_defeat");
        }

        [UnityTest]
        public IEnumerator Pause_Freezes_Time_And_Resume_Restores()
        {
            yield return PlayModeTests.LoadScene("RB_Battle");
            var hud = Object.FindAnyObjectByType<BattleHUD>();
            Assert.IsNotNull(hud);
            float m0 = GM.MatchTime;
            hud.OpenPause();
            Assert.AreEqual(0f, Time.timeScale);
            yield return PlayModeTests.Capture("pause");
            hud.ResumePause();
            Assert.AreEqual(1f, Time.timeScale);
            yield return RunFor(0.5f);
            Assert.Less(GM.MatchTime, m0);
        }



        // ---------------------------------------------------------------- sélection + pose

        [UnityTest]
        public IEnumerator Card_Selection_And_Placement_Flow()
        {
            yield return PlayModeTests.LoadScene("RB_Battle");
            var placement = GM.GetComponent<PlacementController>();
            Assert.IsNotNull(placement);
            // carte troupe en main
            CardView view = null;
            foreach (var v in Object.FindObjectsByType<CardView>())
                if (v.CardStyle == CardView.Style.Combat && v.Card != null && !v.Card.IsSpell) { view = v; break; }
            Assert.IsNotNull(view, "une carte troupe en main");
            // Câblage bouton → sélection : on déclenche l'événement de clic uGUI sur le bouton de la carte.
            var es = UnityEngine.EventSystems.EventSystem.current;
            var ped = new UnityEngine.EventSystems.PointerEventData(es) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            UnityEngine.EventSystems.ExecuteEvents.Execute(view.gameObject, ped, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.GreaterOrEqual(placement.SelectedIndex, 0, "le clic sur la carte doit la sélectionner");
            Assert.IsTrue(placement.deployHint.activeSelf, "halo de déploiement affiché pour une troupe");
            yield return PlayModeTests.Capture("card_selected");
            int before = GM.Units.Count; float elixir0 = GM.Elixir.Player;
            // pose : point d'écran qui se projette dans la zone légale (la fenêtre de test 640x480 n'a pas le format portrait)
            Vector2 ground = default; bool found = false;
            for (float y = 10f; y < Screen.height && !found; y += 6f)
            {
                var probe = new Vector2(Screen.width * 0.35f, y);
                if (PlacementController.ScreenToGround(Camera.main, probe, out Vector3 gp) && PlacementController.IsLegal(GM, gp, false, out _)) { ground = probe; found = true; }
            }
            Assert.IsTrue(found);
            placement.PlaceAtScreen(ground);
            yield return null; yield return null;
            Assert.AreEqual(before + 1, GM.Units.Count, "la pose doit créer la troupe");
            Assert.Less(GM.Elixir.Player, elixir0, "le Fluide est dépensé");
            Assert.AreEqual(-1, placement.SelectedIndex, "la sélection est annulée après la pose");
            Assert.IsFalse(placement.deployHint.activeSelf);
            // zone adverse : refus, le Fluide n'est pas dépensé
            UnityEngine.EventSystems.ExecuteEvents.Execute(view.gameObject, ped, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            var viewB = view;
            yield return null;
            if (placement.SelectedIndex < 0) yield break;   // plus assez de Fluide pour cette carte : rien à vérifier de plus
            int n = GM.Units.Count; float e1 = GM.Elixir.Player;
            placement.PlaceAtScreen(Camera.main.WorldToScreenPoint(new Vector3(0f, 0f, 10f)));
            yield return null;
            Assert.AreEqual(n, GM.Units.Count, "zone adverse interdite");
            Assert.AreEqual(e1, GM.Elixir.Player, "pas de dépense en cas de refus");
        }
        [UnityTest]
        public IEnumerator Ai_Plays_Cards_And_Cycles_Its_Hand()
        {
            yield return PlayModeTests.LoadScene("RB_Battle");
            var before = new System.Collections.Generic.List<string>(GM.EnemyCycle.Hand);
            Time.timeScale = 6f;
            float t = 0f; int guard = 0;
            while (t < 40f && guard++ < 100000) { yield return null; t += Time.deltaTime; }
            Time.timeScale = 1f;
            Assert.AreEqual(4, GM.EnemyCycle.Hand.Count); Assert.AreEqual(4, GM.EnemyCycle.Queue.Count);
            Assert.AreNotEqual(string.Join(",", before), string.Join(",", GM.EnemyCycle.Hand), "la main de l'IA a tourné");
            Assert.IsTrue(GM.Elixir.Enemy < 10f || GM.Units.Count > 0, "l'IA dépense son Fluide");
        }
    }
}
