using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        [SerializeField, Min(0)] float itemTooltipDelay=1;
        public float TooltipHoverDelaySeconds
        {
            get=>itemTooltipDelay;
            set=>itemTooltipDelay=value;
        }
        RectTransform itemTooltip;
        Text itemTooltipText;
        GridItem hoveredItem;
        float hoverStarted;
        readonly List<RaycastResult> tooltipHits=new List<RaycastResult>();

        void LateUpdate()
        {
            if(Session==null || Mouse.current==null || EventSystem.current==null)return;
            var screen=Mouse.current.position.ReadValue();
            tooltipHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},tooltipHits);
            var handle=tooltipHits.Count==0?null:tooltipHits[0].gameObject.GetComponentInParent<GridDragHandle>();
            var item=!IsDragging && handle!=null && handle.shop==this?Session.Find(handle.itemId):null;
            // Views are rebuilt on pricing/resource refresh; hover belongs to the item instance.
            if(item!=hoveredItem){hoveredItem=item;hoverStarted=Time.unscaledTime;}
            if(item==null || Time.unscaledTime-hoverStarted<TooltipHoverDelaySeconds)
            {
                if(itemTooltip!=null)itemTooltip.gameObject.SetActive(false);
                return;
            }
            if(itemTooltip==null)
            {
                itemTooltip=Rect(content.parent,"ItemTooltip",0,0,300,180);
                Image(itemTooltip,new Color(.06f,.10f,.12f,.98f));
                itemTooltipText=Label(itemTooltip,"ItemTooltipText",12,10,276,160,"",18,textColor);
            }
            itemTooltipText.text=ItemTooltipDescription(item);
            float height=itemTooltipText.preferredHeight+20;
            itemTooltip.sizeDelta=new Vector2(300,height);
            itemTooltipText.rectTransform.sizeDelta=new Vector2(276,height-20);
            itemTooltip.gameObject.SetActive(true);
            itemTooltip.SetAsLastSibling();
            var parent=(RectTransform)itemTooltip.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,null,out var local);
            float x=local.x-parent.rect.xMin+16, y=local.y-parent.rect.yMax-16;
            itemTooltip.anchoredPosition=new Vector2(Mathf.Clamp(x,8,parent.rect.width-308),
                Mathf.Clamp(y,-parent.rect.height+height+8,-8));
        }

        string ItemTooltipDescription(GridItem item)
        {
            var resource=item.Definition.spiritResource;
            var cells=item.Cells;
            string category=resource!=null?"灵石":ShopCatalog.CategoryName(item.Definition.category);
            string result=$"{item.Definition.title}\n类别：{category}\n基础价值：{item.BaseValue:0.##}\n占格：{cells.Length} 格（{cells.Max(c=>c.x)+1} × {cells.Max(c=>c.y)+1}）";
            if(resource!=null)
            {
                result+=$"\n品级：{ShopCatalog.CategoryName(item.Definition.category).Replace("灵石","")}\n灵气：{(decimal)item.SpiritUnits/resource.UnitsPerEquivalent:0.##} / {(decimal)resource.CapacityUnits/resource.UnitsPerEquivalent:0.##}";
                result+=resource.Reusable?"\n可重复充能"+(item.SpiritUnits==0?" · 空壳":""):"\n耗尽后破碎";
            }
            if(item.Definition.category==ItemCategory.Medicine)result+="\n品相："+AlchemySettings.QualityName(item.Quality);
            if(item.Definition.category==ItemCategory.Material)result+="\n状态："+(Session.IsAlchemyGround(item.Id)?"已研磨":"完整");
            return result;
        }
    }
}
