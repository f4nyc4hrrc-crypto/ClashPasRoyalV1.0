using UnityEngine;
using UnityEngine.UI;

namespace RoyalBuddies
{
    [DisallowMultipleComponent]
    public sealed class MobileCanvasSafeArea : MonoBehaviour
    {
        CanvasScaler scaler; RectTransform container; float designWidth; Rect previous; Vector2Int resolution;
        public RectTransform Content {get {Ensure();return container;}}
        void Ensure(){if(container)return;scaler=GetComponent<CanvasScaler>();designWidth=scaler.referenceResolution.x;container=UiKit.Rect("SafeArea",transform);container.pivot=new Vector2(.5f,.5f);Apply();}
        void Apply(){Rect safe=Screen.safeArea;if(Screen.width<=0||Screen.height<=0||safe.width<=0)return;
            scaler.referenceResolution=new Vector2(designWidth*Screen.width/safe.width,scaler.referenceResolution.y);
            container.anchorMin=new Vector2(safe.xMin/Screen.width,safe.yMin/Screen.height);container.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);container.offsetMin=container.offsetMax=Vector2.zero;
            previous=safe;resolution=new Vector2Int(Screen.width,Screen.height);}
        void LateUpdate(){Ensure();if(previous!=Screen.safeArea||resolution.x!=Screen.width||resolution.y!=Screen.height)Apply();
            // Canvas siblings define drawing and raycast priority. Keep their original order:
            // backdrop first, panels and buttons afterwards. Removing a child shifts the next into its index.
            for(int i=0;i<transform.childCount;){
                var child=transform.GetChild(i);
                if(child==container||child.name=="CardDragPreview"){i++;continue;}
                child.SetParent(container,false);
                child.SetAsLastSibling();
            }}
    }
}
