using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Main de 4 cartes + file d'attente de 4 cartes (deck de 8).
    /// Mélange UNE fois au départ ; ensuite cycle strict : la carte jouée est remplacée par la tête de file
    /// puis va en fin de file (start_battle / cycle_card / cycle_enemy_card dans main.gd).
    /// </summary>
    public class CardCycle
    {
        public readonly List<string> Hand = new List<string>();
        public readonly List<string> Queue = new List<string>();

        public CardCycle(IList<string> deck)
        {
            var shuffled = new List<string>(deck);
            // Fisher-Yates (équivalent de Array.shuffle() de Godot)
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            int handSize = Mathf.Min(4, shuffled.Count);
            for (int i = 0; i < shuffled.Count; i++)
            {
                if (i < handSize) Hand.Add(shuffled[i]); else Queue.Add(shuffled[i]);
            }
        }

        public string Next => Queue.Count > 0 ? Queue[0] : null;

        /// <summary>Joue la carte : la remplace dans la main par la tête de file, la remet en fin de file.</summary>
        public void Play(string used)
        {
            int idx = Hand.IndexOf(used);
            if (idx < 0 || Queue.Count == 0) return;
            string incoming = Queue[0];
            Queue.RemoveAt(0);
            Hand[idx] = incoming;
            Queue.Add(used);
        }
    }
}
