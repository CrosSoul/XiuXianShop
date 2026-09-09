using UnityEngine;
using UnityEngine.EventSystems;

namespace XiuXianShop
{
    public sealed class StorageWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform window;
        Vector2 pointerOffset;
        public void OnBeginDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)window.parent,e.pressPosition,e.pressEventCamera,out var p);
            pointerOffset=window.anchoredPosition-p;
        }
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)window.parent,e.position,e.pressEventCamera,out var p);
            var parent=(RectTransform)window.parent;
            var position=p+pointerOffset;
            position.x=Mathf.Clamp(position.x,0,parent.rect.width-window.rect.width);
            position.y=Mathf.Clamp(position.y,-parent.rect.height+window.rect.height,0);
            window.anchoredPosition=position;
        }
    }
}
