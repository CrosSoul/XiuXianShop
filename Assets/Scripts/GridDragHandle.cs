using UnityEngine;
using UnityEngine.EventSystems;

namespace XiuXianShop
{
    public sealed class GridDragHandle : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ShopPrototype shop;
        public int itemId;
        public void OnPointerClick(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left) shop.SelectItem(itemId); }
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;
            // UGUI begins dragging after crossing its threshold: position is already away
            // from the grabbed cell. Preserve the actual mouse-down position as the anchor.
            shop.BeginItemDrag(itemId,e.pressPosition);
            shop.DragItem(e.position);
        }
        public void OnDrag(PointerEventData e) { shop.DragItem(e.position); }
        public void OnEndDrag(PointerEventData e) { shop.EndItemDrag(e.position); }
    }
}
