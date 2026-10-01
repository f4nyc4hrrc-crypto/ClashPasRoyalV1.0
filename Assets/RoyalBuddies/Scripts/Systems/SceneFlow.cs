using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoyalBuddies
{
    /// <summary>
    /// Navigation entre les scènes et sauvegarde du deck / des réglages audio
    /// (équivalent de user://royal_buddies_deck.cfg et royal_buddies_settings.cfg de Godot, via PlayerPrefs).
    /// </summary>
    public static class SceneFlow
    {
        public const string MenuScene = "RB_Menu";
        public const string BattleScene = "RB_Battle";
        public const string SandboxScene = "RB_Sandbox";

        public static readonly string[] DefaultDeck = { "GEL", "GOBELIN", "GARGOUILLE", "BOULE DE FEU", "ARCHER", "GEANT", "TIREUR", "SORCIER" };

        /// <summary>true après « REJOUER » : le combat démarre directement (royal_buddies_autostart.flag de Godot).</summary>
        public static bool AutoStartBattle;

        public static List<string> LoadDeck(ICollection<string> catalog)
        {
            string saved = PlayerPrefs.GetString("rb_deck", "");
            if (!string.IsNullOrEmpty(saved))
            {
                var cards = new List<string>(saved.Split('|'));
                if (IsValidDeck(cards, catalog)) return cards;
            }
            return new List<string>(DefaultDeck);
        }

        public static void SaveDeck(IList<string> deck)
        {
            if (deck.Count != 8) return;
            PlayerPrefs.SetString("rb_deck", string.Join("|", deck));
            PlayerPrefs.Save();
        }

        public static bool IsValidDeck(IList<string> cards, ICollection<string> catalog)
        {
            if (cards.Count != 8) return false;
            var seen = new HashSet<string>();
            foreach (var id in cards)
                if (!catalog.Contains(id) || !seen.Add(id)) return false;
            return true;
        }

        public static void GoMenu() { Time.timeScale = 1f; SceneManager.LoadScene(MenuScene); }
        public static void GoBattle() { Time.timeScale = 1f; SceneManager.LoadScene(BattleScene); }
        public static void GoSandbox() { Time.timeScale = 1f; SceneManager.LoadScene(SandboxScene); }
    }
}
