using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    // The modal only presents the session quote. Closing never mutates the transaction.
    public sealed class ShopNegotiationView : MonoBehaviour
    {
        ShopPrototype shop;
        Font font;
        Text customer, lines, total, status;
        Button confirm;
        ScrollRect scroll;
        TradeOffer openedOffer;
        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(ShopPrototype owner,Font textFont)
        {
            shop=owner;font=textFont;
            var root=(RectTransform)transform;
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            Fill(root,new Color(.015f,.025f,.035f,.93f),true);
            Fill(Box(root,"NegotiationWindow",250,125,1100,760),new Color(.085f,.13f,.16f));
            Label(root,"NegotiationTitle",285,150,720,42,"谈判 · 确认本次交易",30);
            Button(root,"NegotiationClose",1110,150,205,42,"关闭 / 继续整理",Close);
            customer=Label(root,"NegotiationCustomer",285,212,1025,65,"",21);
            var viewport=Box(root,"NegotiationViewport",285,290,1030,350);
            Fill(viewport,new Color(.055f,.085f,.11f),true);viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();
            lines=Label(viewport,"NegotiationLines",15,10,995,330,"",21);
            lines.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=lines.rectTransform;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            total=Label(root,"NegotiationTotal",285,665,1025,42,"",27);
            status=Label(root,"NegotiationStatus",285,717,1025,65,"",20);
            confirm=Button(root,"NegotiationConfirm",995,806,320,48,"确认交易",()=>
            {
                if(shop.Session.Offer!=openedOffer){Close();return;}
                if(shop.Session.AcceptTrade())Close();
                shop.Refresh();
            });
            Label(root,"NegotiationHint",285,808,670,50,"卖出为正，买入为负；确认前不扣款、不转移所有权。",18);
            gameObject.SetActive(false);
        }
        public void Open()
        {
            shop.CancelDrag();shop.CalendarView.Close();openedOffer=shop.Session.Offer;
            gameObject.SetActive(true);transform.SetAsLastSibling();Refresh();
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
        }
        public void Close()=>gameObject.SetActive(false);
        public void Refresh()
        {
            if(!IsOpen)return;
            var session=shop.Session;var offer=session.Offer;
            if(offer!=openedOffer){Close();return;}
            customer.text=offer==null?"当前没有顾客。":$"{offer.CustomerName}  |  求购：{ShopCatalog.CategoryName(offer.RequestedCategory)}  |  剩余预算：{offer.RemainingBudget}\n你的可用资金：{session.Money} 灵石";
            bool buying=offer?.Direction==TradeDirection.CustomerSells;
            var basket=buying?offer.SupplierItems.Where(i=>i.ForSale).ToArray():session.In(ContainerId.Counter).Where(i=>i.Owner==ItemOwner.Player).ToArray();
            lines.text=string.Join("\n\n",basket.Select(i=>{var q=session.Quote(i);return $"{(i.Owner==ItemOwner.Player?"卖出 +":"买入 −")}{q.Amount}    {i.Definition.title} × 1\n基础 {q.BaseValue} · {q.Modifiers}";}));
            if(basket.Length==0)lines.text="当前没有选入的商品。关闭弹窗后可继续整理柜台。";
            confirm.interactable=session.CanAcceptTrade(out int amount,out string reason);
            int net=buying?-amount:amount;
            total.text=$"合计净额 {net:+0;-0;0} 灵石  ·  {(net<0?$"你支付 {-net}":net>0?$"你收取 {net}":"无需收付")}  ·  {basket.Length} 件";
            status.text=reason;
        }
        static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static void Fill(RectTransform r,Color color,bool raycast=false){var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=raycast;}
        Text Label(Transform p,string name,float x,float y,float w,float h,string value,int size)
        {
            var t=Box(p,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;
            t.color=new Color(.9f,.92f,.9f);t.raycastTarget=false;return t;
        }
        Button Button(Transform p,string name,float x,float y,float w,float h,string title,Action action)
        {
            var r=Box(p,name,x,y,w,h);Fill(r,new Color(.25f,.35f,.30f),true);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>action());
            Label(r,"Label",5,3,w-10,h-6,title,20).alignment=TextAnchor.MiddleCenter;return b;
        }
    }
}
