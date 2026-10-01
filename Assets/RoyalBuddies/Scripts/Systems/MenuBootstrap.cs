using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>Point d'entrée de la scène Menu : construit l'écran « Deck de combat ».</summary>
    [DefaultExecutionOrder(-100)]
    public class MenuBootstrap : MonoBehaviour
    {
        public RBGameAssets assets;

        void Awake()
        {
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            RBGameAssets.Current = assets;
            DeckMenu.Create(assets);
        }
    }
}
