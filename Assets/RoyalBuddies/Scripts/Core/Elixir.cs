using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Fluide (élixir) du joueur et de l'IA. Port de main.gd :
    /// départ 7, max 10, +1 toutes les 2.8/mult secondes (mult = 1, 2 sous 60 s, 3 en mort subite).
    /// Joueur et IA gagnent le même tick, comme dans Godot.
    /// </summary>
    public class Elixir
    {
        public float Player = RB.ElixirStart;
        public float Enemy = RB.ElixirStart;
        float tick;

        public static float Multiplier(bool overtime, float matchTimeLeft)
        {
            return overtime ? 3f : (matchTimeLeft <= RB.DoubleElixirAt ? 2f : 1f);
        }

        public void Reset()
        {
            Player = Enemy = RB.ElixirStart;
            tick = 0f;
        }

        public void Update(float dt, float multiplier)
        {
            tick += dt;
            float interval = RB.ElixirBaseInterval / multiplier;
            while (tick >= interval)
            {
                tick -= interval;
                Player = Mathf.Min(RB.ElixirMax, Player + 1f);
                Enemy = Mathf.Min(RB.ElixirMax, Enemy + 1f);
            }
        }

        public bool CanAfford(Team team, int cost) => (team == Team.Player ? Player : Enemy) >= cost;

        public void Spend(Team team, int cost)
        {
            if (team == Team.Player) Player -= cost; else Enemy -= cost;
        }
    }
}
