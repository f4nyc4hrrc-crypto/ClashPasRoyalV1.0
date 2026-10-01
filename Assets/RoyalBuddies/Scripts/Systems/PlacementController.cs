using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RoyalBuddies
{
    /// <summary>
    /// Sélection d'une carte de la main puis pose sur l'arène. Port de choose_hand(), _unhandled_input() et place() de main.gd.
    /// Entrée : souris (test PC) et écran tactile via l'Input System. Les clics sur l'interface sont ignorés.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        public Camera cam;
        public GameObject deployHint;

        public int SelectedIndex { get; private set; } = -1;
        public CardData SelectedCard { get; private set; }
        bool cardDragging;
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        public bool SpellTargeting => SelectedCard != null && SelectedCard.IsSpell;

        public event Action SelectionChanged;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (GameManager.I != null) GameManager.I.HandChanged += OnHandChanged;
        }

        void OnDestroy()
        {
            if (GameManager.I != null) GameManager.I.HandChanged -= OnHandChanged;
        }

        void OnHandChanged()
        {
            // Après une pose la main change : la sélection est déjà annulée ; ce hook garde la cohérence si la main tourne autrement.
            if (SelectedIndex >= 0 && GameManager.I.PlayerCycle != null &&
                (SelectedIndex >= GameManager.I.PlayerCycle.Hand.Count || SelectedCard == null ||
                 GameManager.I.PlayerCycle.Hand[SelectedIndex] != SelectedCard.id))
                Deselect();
        }

        /// <summary>choose_hand(index) : vérifie le Fluide, mémorise la carte, affiche le halo de déploiement.</summary>
        public bool Select(int handIndex)
        {
            var gm = GameManager.I;
            if (gm == null || gm.GameOver || gm.PlayerCycle == null) return false;
            if (handIndex < 0 || handIndex >= gm.PlayerCycle.Hand.Count) return false;
            var card = RBGameAssets.Current.GetCard(gm.PlayerCycle.Hand[handIndex]);
            if (card == null) return false;
            if (!gm.Elixir.CanAfford(Team.Player, card.cost))
            {
                gm.SetStatus("Pas assez de Fluide");
                return false;
            }
            SelectedIndex = handIndex;
            SelectedCard = card;
            gm.SetStatus(card.IsSpell ? card.id + " • cible n'importe où dans l'arène"
                                      : card.id + " sélectionné • zone bleue = placement");
            if (deployHint != null) deployHint.SetActive(!card.IsSpell);
            SelectionChanged?.Invoke();
            return true;
        }

        public void Deselect()
        {
            SelectedIndex = -1;
            SelectedCard = null;
            if (deployHint != null) deployHint.SetActive(false);
            SelectionChanged?.Invoke();
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.GameOver || SelectedCard == null || Time.timeScale == 0f || cardDragging) return;
            if (!TryGetPress(out Vector2 screenPos, out bool overUi) || overUi) return;
            PlaceAtScreen(screenPos);
        }

        public bool BeginCardDrag(int handIndex)
        {
            if(cardDragging || Time.timeScale==0f || !Select(handIndex))return false;
            cardDragging=true;return true;
        }
        public void CancelCardDrag(){cardDragging=false;}
        public void EndCardDrag(Vector2 screenPos)
        {
            if(!cardDragging)return;
            cardDragging=false;
            if(!IsScreenOverUi(screenPos))PlaceAtScreen(screenPos);
        }
        public static bool IsScreenOverUi(Vector2 position)
        {
            var es=EventSystem.current;if(es==null)return false;
            uiHits.Clear();es.RaycastAll(new PointerEventData(es){position=position},uiHits);
            // Only canvas UI blocks placement; physics raycasters must not treat the ground as UI.
            foreach(var hit in uiHits)if(hit.module is UnityEngine.UI.GraphicRaycaster)return true;
            return false;
        }
        static bool TryGetPress(out Vector2 pos,out bool overUi)
        {
            pos=default;overUi=false;
            var ts=Touchscreen.current;
            if(ts!=null && ts.primaryTouch.press.wasPressedThisFrame)
            {pos=ts.primaryTouch.position.ReadValue();overUi=IsScreenOverUi(pos);return true;}
            // Prefer the active finger over a mouse event on hybrid or touch-simulated devices.
            if(ts!=null && ts.primaryTouch.press.isPressed)return false;
            var mouse=Mouse.current;
            if(mouse!=null && mouse.leftButton.wasPressedThisFrame)
            {pos=mouse.position.ReadValue();overUi=IsScreenOverUi(pos);return true;}
            return false;
        }

        /// <summary>Projette le point d'écran sur le plan y = 0 (place() de main.gd).</summary>
        public static bool ScreenToGround(Camera cam, Vector2 screenPos, out Vector3 p)
        {
            p = default;
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Mathf.Abs(ray.direction.y) < .001f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return false;
            p = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>place() de main.gd : projette le point d'écran sur l'arène, vérifie la zone et le Fluide, puis pose la carte sélectionnée.</summary>
        public void PlaceAtScreen(Vector2 screenPos)
        {
            var gm = GameManager.I;
            if (gm == null || gm.GameOver || SelectedCard == null || Time.timeScale == 0f) return;
            if (cam == null || !ScreenToGround(cam, screenPos, out Vector3 p)) return;
            if (!IsLegal(gm, p, SelectedCard.IsSpell, out string message))
            {
                gm.SetStatus(message);
                return;
            }
            if (!gm.Elixir.CanAfford(Team.Player, SelectedCard.cost)) return;
            p.y = 0f;
            gm.PlayCard(Team.Player, SelectedCard, p);
            Deselect();
        }

        /// <summary>
        /// Zones autorisées. Sorts : partout dans l'arène. Troupes : moitié du joueur (Unity z ≤ −2.55) ;
        /// après destruction d'une tour latérale ennemie, la lane correspondante s'ouvre jusqu'à z ≤ 12.
        /// </summary>
        public static bool IsLegal(GameManager gm, Vector3 p, bool spell, out string message)
        {
            message = null;
            if (spell)
            {
                if (Mathf.Abs(p.x) > RB.ArenaHalfX || Mathf.Abs(p.z) > RB.ArenaHalfZ) { message = "Hors de l'arene"; return false; }
                return true;
            }
            if (Mathf.Abs(p.x) > RB.ArenaHalfX || p.z < -RB.ArenaHalfZ) { message = "Zone interdite"; return false; }
            bool legal = p.z <= -RB.DeployNearZ;
            if (!legal && p.z <= RB.DeployAdvancedZ)
            {
                if (p.x <= -RB.DeployAdvancedMinX && gm.SideTowerDestroyed(Team.Enemy, -1)) legal = true;
                else if (p.x >= RB.DeployAdvancedMinX && gm.SideTowerDestroyed(Team.Enemy, 1)) legal = true;
            }
            if (!legal) message = "Zone interdite";
            return legal;
        }
    }
}
