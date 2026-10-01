using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoyalBuddies
{
    public sealed class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        PlacementController placement; CardView card; int handIndex, pointerId; bool dragging; RectTransform ghost; Canvas canvas;
        public void Bind(PlacementController controller, CardView view, int index) {placement=controller;card=view;handIndex=index;canvas=GetComponentInParent<Canvas>();}
        public void OnBeginDrag(PointerEventData e) {
            if(e.button!=PointerEventData.InputButton.Left || placement==null || !placement.BeginCardDrag(handIndex))return;
            dragging=true;pointerId=e.pointerId;
            var img=UiKit.Img(canvas.transform,"CardDragPreview",CardView.PortraitFor(card.Card,RBGameAssets.Current),new Color(1,1,1,.85f));
            img.raycastTarget=false;img.preserveAspect=true;ghost=img.rectTransform;ghost.anchorMin=ghost.anchorMax=ghost.pivot=new Vector2(.5f,.5f);ghost.sizeDelta=new Vector2(210,190);Move(e);
        }
        void Move(PointerEventData e){if(ghost&&RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,e.pressEventCamera,out var p))ghost.anchoredPosition=p+new Vector2(0,80);}
        public void OnDrag(PointerEventData e){if(dragging&&e.pointerId==pointerId)Move(e);}
        public void OnEndDrag(PointerEventData e){if(!dragging||e.pointerId!=pointerId)return;dragging=false;if(ghost)Destroy(ghost.gameObject);placement.EndCardDrag(e.position);}
        void Cancel(){if(!dragging)return;dragging=false;if(ghost)Destroy(ghost.gameObject);if(placement)placement.CancelCardDrag();}
        void OnDisable(){Cancel();}
        void OnApplicationPause(bool paused){if(paused)Cancel();}
        void OnApplicationFocus(bool focused){if(!focused)Cancel();}
    }
}
