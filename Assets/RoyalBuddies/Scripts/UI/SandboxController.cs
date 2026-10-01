using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RoyalBuddies
{
    /// <summary>
    /// Scène de test « Sandbox » : choisissez une carte et une équipe dans le panneau, puis cliquez (ou touchez) l'arène pour
    /// poser la troupe / lancer le sort n'importe où, sans coût de Fluide ni limite de zone. On y voit le combat, la mort
    /// (animation puis rétrécissement) et le despawn. La partie ne se termine jamais (mode bac à sable de GameManager).
    /// </summary>
    public class SandboxController : MonoBehaviour
    {
        RBGameAssets assets;
        GameManager gm;
        Camera cam;
        Team team = Team.Player;
        CardData selected;
        Text info, teamLabel, aiLabel, speedLabel;
        readonly Dictionary<CardData, Image> cardButtons = new Dictionary<CardData, Image>();
        readonly float[] speeds = { 1f, 0.25f, 2f, 0f };
        int speedIndex;

        public static SandboxController Create(RBGameAssets assets, GameManager gm, Camera cam)
        {
            BattleHUD.EnsureEventSystem();
            var go = new GameObject("RB_SandboxUI");
            var s = go.AddComponent<SandboxController>();
            s.assets = assets; s.gm = gm; s.cam = cam;
            s.Build();
            return s;
        }

        void Build()
        {
            var canvas = UiKit.MakeCanvas("SandboxCanvas", 10, new Vector2(1080, 1920), UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, 0.5f);
            canvas.transform.SetParent(transform, false);

            var panel = UiKit.Panel(canvas.transform, "Panel", new Color(0.05f, 0.08f, 0.1f, .82f), UiKit.Hex("#d69a42"), 3, 14, true);
            UiKit.Place(panel, 12, 12, 330, 1010);
            var title = UiKit.Label(panel, "Title", "SANDBOX", 30, UiKit.Hex("#fff2c7"), TextAnchor.MiddleCenter, 2f);
            UiKit.Place(title.rectTransform, 0, 8, 330, 44);
            info = UiKit.Label(panel, "Info", "", 16, UiKit.Hex("#c8ffb0"), TextAnchor.UpperLeft);
            UiKit.Place(info.rectTransform, 16, 54, 300, 46);

            var tb = UiKit.TextButton(panel, "Team", "", 20, UiKit.Hex("#176ca8"), UiKit.Hex("#f4c760"), 15, 104, 300, 48, ToggleTeam, 12);
            teamLabel = tb.GetComponentInChildren<Text>();

            int col = 0, row = 0;
            foreach (var c in assets.cards)
            {
                var card = c;
                var b = UiKit.TextButton(panel, "Card_" + c.id, c.displayName, 15, UiKit.Hex("#3a2a1c"), UiKit.Hex("#8f682e"),
                                         15 + col * 152, 162 + row * 56, 148, 50, () => Select(card), 10);
                cardButtons[c] = b.GetComponent<Image>();
                if (++col == 2) { col = 0; row++; }
            }
            float y = 162 + (row + (col > 0 ? 1 : 0)) * 56 + 8;
            UiKit.TextButton(panel, "Clear", "EFFACER LES TROUPES", 16, UiKit.Hex("#7a2a2a"), UiKit.Hex("#f4c760"), 15, y, 300, 44, ClearUnits, 10);
            var ai = UiKit.TextButton(panel, "AI", "", 16, UiKit.Hex("#1e6539"), UiKit.Hex("#efbd4c"), 15, y + 50, 300, 44, ToggleAI, 10);
            aiLabel = ai.GetComponentInChildren<Text>();
            var sp = UiKit.TextButton(panel, "Speed", "", 16, UiKit.Hex("#214965"), UiKit.Hex("#d9a944"), 15, y + 100, 300, 44, CycleSpeed, 10);
            speedLabel = sp.GetComponentInChildren<Text>();
            UiKit.TextButton(panel, "Heal", "RECHARGER LA SCÈNE", 16, UiKit.Hex("#5c351c"), UiKit.Hex("#d69a42"), 15, y + 150, 300, 44, ReloadScene, 10);
            UiKit.TextButton(panel, "Menu", "RETOUR MENU", 16, UiKit.Hex("#5c351c"), UiKit.Hex("#d69a42"), 15, y + 200, 300, 44, SceneFlow.GoMenu, 10);
            RefreshLabels();
            Select(assets.GetCard("GARDE"));
        }

        void ToggleTeam() { team = RB.Opponent(team); RefreshLabels(); }
        void ToggleAI()
        {
            if (gm.AI != null) { Destroy(gm.AI); gm.AI = null; }
            else gm.AI = gm.gameObject.AddComponent<AIController>();
            RefreshLabels();
        }
        void CycleSpeed() { speedIndex = (speedIndex + 1) % speeds.Length; Time.timeScale = speeds[speedIndex]; RefreshLabels(); }
        void ReloadScene() { UnityEngine.SceneManagement.SceneManager.LoadScene(SceneFlow.SandboxScene); Time.timeScale = 1f; }

        void ClearUnits()
        {
            foreach (var u in gm.Units.ToArray())
                if (u != null && u.IsAlive) u.ApplyDamage(999999f);
        }

        void Select(CardData c)
        {
            selected = c;
            foreach (var kv in cardButtons)
                kv.Value.color = kv.Key == c ? UiKit.Hex("#28b989") : UiKit.Hex("#8f682e");
        }

        void RefreshLabels()
        {
            teamLabel.text = "ÉQUIPE : " + (team == Team.Player ? "JOUEUR (bleu)" : "ADVERSAIRE (rouge)");
            aiLabel.text = "IA ADVERSE : " + (gm.AI != null ? "ACTIVE" : "COUPÉE");
            speedLabel.text = "VITESSE : " + (speeds[speedIndex] == 0f ? "PAUSE" : "×" + speeds[speedIndex]);
        }

        void Update()
        {
            info.text = "Troupes : " + gm.Units.Count + "   FPS : " + Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)) +
                        "\nTours J/E : " + CountTowers(Team.Player) + "/" + CountTowers(Team.Enemy);
            var mouse = Mouse.current;
            Vector2 pos; bool pressed = false, overUi = false;
            var es = EventSystem.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pos = mouse.position.ReadValue(); pressed = true; overUi = es != null && es.IsPointerOverGameObject();
            }
            else
            {
                var ts = Touchscreen.current;
                pos = default;
                if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
                {
                    pos = ts.primaryTouch.position.ReadValue(); pressed = true;
                    overUi = es != null && es.IsPointerOverGameObject(ts.primaryTouch.touchId.ReadValue());
                }
            }
            if (!pressed || overUi || selected == null) return;
            if (!PlacementController.ScreenToGround(cam, pos, out Vector3 p)) return;
            if (Mathf.Abs(p.x) > RB.ArenaHalfX + 1f || Mathf.Abs(p.z) > RB.ArenaHalfZ + 1f) return;
            p.y = 0f;
            gm.Deploy(selected, p, team);
        }

        int CountTowers(Team t)
        {
            int n = 0;
            foreach (var tw in gm.Towers) if (tw.Team == t && tw.IsAlive) n++;
            return n;
        }
    }
}
