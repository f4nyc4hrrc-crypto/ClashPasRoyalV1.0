using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// IA adverse : port fidèle de ai() / cycle_enemy_card() de main.gd.
    /// Tick aléatoire de 1.45 à 2.8 s (premier tick à 1.8 s), choisit la lane selon le danger,
    /// joue une carte abordable au hasard parmi ses 4 cartes en main.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        float aiTick = 1.8f;

        public void Tick(float dt)
        {
            var gm = GameManager.I;
            if (gm == null || gm.EnemyCycle == null) return;
            aiTick -= dt;
            if (aiTick > 0f) return;
            aiTick = Random.Range(1.45f, 2.8f);
            if (gm.EnemyCycle.Hand.Count == 0) return;

            // Défense de base : préfère la lane où se trouvent des troupes du joueur.
            float lane = 0f;
            float dangerLeft = 0f, dangerRight = 0f;
            foreach (var u in gm.Units)
            {
                if (u.IsAlive && u.Team == Team.Player)
                {
                    // Godot : max(0, 17 + z) avec z Godot = −z Unity
                    float threat = Mathf.Max(0f, 17f - u.Position.z);
                    if (u.Position.x < 0f) dangerLeft += threat; else dangerRight += threat;
                }
            }
            if (dangerLeft > 2f || dangerRight > 2f) lane = dangerLeft > dangerRight ? -6f : 6f;
            if (lane == 0f)
            {
                foreach (var u in gm.Units)
                {
                    // Godot : unité adverse déjà avancée (z > −8 en Godot ⇒ z < 8 en Unity)
                    if (u.IsAlive && u.Team == Team.Enemy && u.Position.z < 8f)
                    {
                        lane = u.Position.x < 0f ? -6f : 6f;
                        break;
                    }
                }
            }
            if (lane == 0f) lane = Random.value < .5f ? -6f : 6f;

            // L'IA choisit uniquement parmi ses 4 cartes en main.
            var affordable = new List<string>();
            var assets = RBGameAssets.Current;
            foreach (var id in gm.EnemyCycle.Hand)
            {
                var card = assets.GetCard(id);
                if (card != null && gm.Elixir.CanAfford(Team.Enemy, card.cost)) affordable.Add(id);
            }
            if (affordable.Count == 0) return;
            string chosen = affordable[Random.Range(0, affordable.Count)];
            var chosenCard = assets.GetCard(chosen);

            // Zone avancée si la tour latérale du joueur de cette lane est détruite (55 % du temps).
            int laneSign = lane < 0f ? -1 : 1;
            float spawnZ = Random.Range(9.5f, 21.5f);   // Godot : z ∈ [−21.5 ; −9.5]
            if (gm.SideTowerDestroyed(Team.Player, laneSign) && Random.value < 0.55f)
                spawnZ = -Random.Range(3.2f, 11.8f);    // Godot : z ∈ [3.2 ; 11.8]
            var pos = new Vector3(lane + Random.Range(-1.0f, 1.0f), 0f, spawnZ);
            gm.PlayCard(Team.Enemy, chosenCard, pos);
        }
    }
}
