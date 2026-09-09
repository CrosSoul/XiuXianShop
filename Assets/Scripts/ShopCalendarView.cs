using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed class ShopCalendarView : MonoBehaviour
    {
        ShopPrototype shop;
        Font font;
        Text heading,rentInfo,details,message;
        RectTransform rows;
        ScrollRect scroll;
        Button previous,save,load;
        int firstTurn;
        string selectedEventId;
        public int FirstTurn => firstTurn;
        public bool IsOpen => gameObject.activeSelf;
        static readonly Color ink=new Color(.90f,.92f,.90f), gold=new Color(.94f,.74f,.4f);
        public void Initialize(ShopPrototype owner,Font textFont)
        {
            shop=owner;font=textFont;
            var root=(RectTransform)transform;
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            Fill(root,new Color(.02f,.04f,.05f,.96f),true);
            var card=Box(root,"CalendarWindow",64,96,1472,820);Fill(card,new Color(.085f,.13f,.16f));
            heading=Label(root,"CalendarHeading",96,119,840,40,"",28,gold);
            previous=Button(root,"CalendarPrevious",936,119,144,38,"上一年",()=>Show(firstTurn-12));
            Button(root,"CalendarToday",1090,119,130,38,"本年本月",()=>Show(MarketCalendar.YearStart(shop.Session.Turn)));
            Button(root,"CalendarNext",1230,119,144,38,"下一年",()=>Show(firstTurn+12));
            Button(root,"CalendarClose",1384,119,122,38,"关闭 Esc",Close);
            rentInfo=Label(root,"CalendarRent",96,174,1400,53,"",18,ink);
            var viewport=Box(root,"CalendarViewport",96,240,1408,390);Fill(viewport,new Color(.07f,.10f,.12f),true);
            viewport.gameObject.AddComponent<RectMask2D>();scroll=viewport.gameObject.AddComponent<ScrollRect>();
            rows=Box(viewport,"CalendarRows",0,0,1388,390);
            scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            details=Label(root,"CalendarEventDetails",100,650,1396,145,"",20,ink);
            message=Label(root,"CalendarMessage",100,803,1396,28,"点击持续条查看完整名称与价格效果；重叠事件分行显示。",17,gold);
            save=Button(root,"CalendarSave",100,852,188,40,"保存营业准备",()=>{shop.SavePreparation();Refresh();});
            load=Button(root,"CalendarLoad",302,852,188,40,"读取营业准备",()=>{shop.LoadPreparation();Show(MarketCalendar.YearStart(shop.Session.Turn));});
            Label(root,"CalendarSaveHint",518,852,960,44,"仅营业准备阶段存取月度存档；旧日制存档保留，不自动转换。",17,ink);
            gameObject.SetActive(false);
        }
        public void Open() {gameObject.SetActive(true);transform.SetAsLastSibling();Show(MarketCalendar.YearStart(shop.Session.Turn));}
        public void Close() => gameObject.SetActive(false);
        public void Show(int turn)
        {
            firstTurn=MarketCalendar.YearStart(Math.Max(1,turn));selectedEventId=null;Refresh();
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;
        }
        public void Refresh()
        {
            if(!IsOpen)return;
            var session=shop.Session;
            heading.text=$"年度日历 · 第 {(firstTurn-1)/12+1} 年   |   当前：{session.DateLabel}";
            rentInfo.text=$"下次收租：第 {session.NextRentTurn} 回合结束，{session.Rent} 灵石；待付房租 {session.RentDebt}。\n每6回合收租，结束本月时结算；翻阅不扣款。行情采用隔离测试配置，非正式平衡。";
            previous.interactable=firstTurn>1;save.interactable=load.interactable=session.Phase==TurnPhase.Preparation;
            message.text=shop.CalendarMessage??"行情在开始本回合公开完整持续时间。点击持续条查看详情；可滚动查看。绿色生效中，灰色已结束。";
            foreach(Transform child in rows) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var segments=session.Calendar.Segments(firstTurn,session.Turn);
            float top=0;
            for(int week=0;week<2;week++)
            {
                Label(rows,"HalfYearLabel_"+week,6,top,1300,26,week==0?"上半年":"下半年",18,gold);
                top+=30;
                int laneCount=Math.Max(2,segments.Where(s=>s.Row==week).Select(s=>s.Lane+1).DefaultIfEmpty(0).Max());
                float height=70+laneCount*36;
                for(int col=0;col<6;col++)
                {
                    int turn=firstTurn+week*6+col;
                    var cell=Box(rows,"CalendarDay_"+turn,col*231,top,227,height);
                    Fill(cell,turn==session.Turn?new Color(.25f,.29f,.20f):new Color(.12f,.18f,.21f));
                    Label(cell,"Date",10,7,210,27,$"{(turn-1)%12+1} 月"+(turn==session.Turn?" · 当前":""),20,turn==session.Turn?gold:ink);
                    if(turn%session.Catalog.rentPeriod==0)
                    {
                        int? amount=session.ProjectedRent(turn);
                        Label(cell,"RentMarker",10,37,178,25,amount.HasValue?$"收租 {amount} 灵石":"收租日 · 已过",17,gold);
                    }
                }
                foreach(var s in segments.Where(s=>s.Row==week))
                {
                    var e=s.Event;
                    string name=e.title.Length>9*s.Turns?e.title.Substring(0,Math.Max(3,9*s.Turns-1))+"…":e.title;
                    var b=Button(rows,$"MarketBar_{e.id}_{week}",s.Column*231+4,top+68+s.Lane*36,s.Turns*231-12,30,
                        (e.startTurn<firstTurn+week*6?"‹ ":"")+name+(e.endTurn>firstTurn+week*6+5?" ›":""),()=>SelectEvent(e.id));
                    b.targetGraphic.color=e.ActiveOn(session.Turn)?new Color(.23f,.40f,.29f):new Color(.26f,.27f,.28f);
                }
                top+=height+18;
            }
            rows.sizeDelta=new Vector2(1388,Math.Max(390,top));
            var visible=session.Calendar.VisibleBetween(firstTurn,firstTurn+11,session.Turn);
            if(!visible.Any(e=>e.id==selectedEventId))selectedEventId=visible.FirstOrDefault(e=>e.ActiveOn(session.Turn))?.id??visible.FirstOrDefault()?.id;
            SelectEvent(selectedEventId);
        }
        void SelectEvent(string id)
        {
            selectedEventId=id;
            var e=shop.Session.Calendar.VisibleBetween(firstTurn,firstTurn+11,shop.Session.Turn).FirstOrDefault(x=>x.id==id);
            if(e==null){details.text="本年度暂无已公开行情；行情到开始本回合才会公布。日期只随结束回合推进，浏览日历不会改变行情。";return;}
            var t=e.effect;
            string direction=t.playerBuys && t.playerSells?"玩家买入与出售":t.playerBuys?"玩家买入":"玩家出售";
            details.text=$"{e.title}  ·  {e.StatusOn(shop.Session.Turn)}\n第 {e.startTurn}–{e.endTurn} 回合（含首尾，共 {e.Duration} 回合）  |  {ShopCatalog.CategoryName(t.category)}  |  {direction} {t.percent*100:+0.##;-0.##;0}%\n{e.description}\n价格标签与其他有效修正相加；未来不生效，结束后自动失效。";
        }
        static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static void Fill(RectTransform r,Color color,bool raycast=false){var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=raycast;}
        Text Label(Transform p,string name,float x,float y,float w,float h,string value,int size,Color color)
        {
            var t=Box(p,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=value;t.color=color;t.raycastTarget=false;return t;
        }
        Button Button(Transform p,string name,float x,float y,float w,float h,string title,Action action)
        {
            var r=Box(p,name,x,y,w,h);Fill(r,new Color(.22f,.30f,.34f),true);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>action());
            var t=Label(r,"Label",4,1,w-8,h-2,title,17,ink);t.alignment=TextAnchor.MiddleCenter;t.horizontalOverflow=HorizontalWrapMode.Wrap;
            return b;
        }
    }
}
