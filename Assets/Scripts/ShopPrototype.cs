using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace XiuXianShop
{
    // A fixed-view prototype screen. All inventory rules live in ShopSession, not in UI objects.
    public sealed partial class ShopPrototype : MonoBehaviour
    {
        [SerializeField] ShopCatalog catalog;
        public ShopSession Session { get; private set; }
        public ShopCatalog Catalog { get=>catalog; set=>catalog=value; }
        public bool IsDragging => dragId!=0;
        public int SelectedId => selectedId;
        public string PreviewMessage { get; private set; }
        Font font;
        public ShopCalendarView CalendarView { get; private set; }
        public ShopNegotiationView NegotiationView { get; private set; }
        public string CalendarMessage { get; private set; }
        public string SavePath { get; set; }
        RectTransform content, ghost, preview;
        Text header, rent, phaseText, selection, customerTitle, customerDetails, notice, furnaceText, previewText, displaySummary, tradeDetails, tradeStatus, tradeHeading;
        int displayedPricingRevision;
        TradeOffer displayedOffer;
        TurnPhase displayedPhase;
        public int CustomerSeed { get; set; } = -1;
        Button beginButton, nextButton, endButton, sleepButton, craftButton, stageButton, acceptButton, rejectButton, rotateButton, flipButton, cancelButton;
        readonly Dictionary<ContainerId,RectTransform> grids=new Dictionary<ContainerId,RectTransform>();
        readonly Dictionary<ContainerId,Text> gridTitles=new Dictionary<ContainerId,Text>();
        readonly Dictionary<int,RectTransform> itemViews=new Dictionary<int,RectTransform>();
        readonly Dictionary<ContainerId,float> cellSizes=new Dictionary<ContainerId,float>();
        int selectedId,dragId,dragRotation;
        bool dragFlipped;
        Vector2Int grabCell;
        Vector2 pointer;
        string localNotice;
        bool previousRunInBackground;
        int previousFrameRate;
        Color textColor=new Color(.89f,.91f,.89f), muted=new Color(.59f,.66f,.68f), gold=new Color(.91f,.72f,.40f);
        Color panel=new Color(.09f,.14f,.17f), line=new Color(.17f,.24f,.27f);

        void Start()
        {
            if(catalog==null) { Debug.LogError("ShopPrototype needs a ShopCatalog asset.",this); enabled=false; return; }
            previousRunInBackground=Application.runInBackground;
            previousFrameRate=Application.targetFrameRate;
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            Session=new ShopSession(catalog, customerSeed: CustomerSeed < 0 ? (int?)null : CustomerSeed);
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Noto Sans CJK SC","Arial"},24);
            BuildScreen(); Refresh();
        }

        void Update()
        {
            if(Session!=null && displayedPricingRevision!=Session.PricingRevision && !IsDragging) Refresh();
            var keyboard=Keyboard.current;
            if(keyboard==null || Session==null) return;
            if(CalendarView!=null && CalendarView.IsOpen)
            {
                if(keyboard.escapeKey.wasPressedThisFrame)CalendarView.Close();
                return;
            }
            if(NegotiationView!=null && NegotiationView.IsOpen)
            {
                if(keyboard.escapeKey.wasPressedThisFrame)NegotiationView.Close();
                return;
            }
            if(keyboard.rKey.wasPressedThisFrame) RotateSelected();
            if(keyboard.fKey.wasPressedThisFrame) FlipSelected();
            if(keyboard.escapeKey.wasPressedThisFrame) CancelDrag();
        }
        void OnApplicationFocus(bool focused) { if(!focused && IsDragging) CancelDrag(); }
        void OnDestroy()
        {
            if(Session!=null) {Application.runInBackground=previousRunInBackground;Application.targetFrameRate=previousFrameRate;}
            if(font!=null) Destroy(font);
        }

        void BuildScreen()
        {
            var canvasGo=new GameObject("ShopCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,1000); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            content=Rect(canvasGo.transform,"FixedShopView",0,0,1600,1000);
            content.anchorMin=content.anchorMax=new Vector2(.5f,.5f); content.pivot=new Vector2(.5f,.5f); content.anchoredPosition=Vector2.zero;
            Image(content,new Color(.045f,.075f,.095f));
            if(EventSystem.current==null)
            {
                var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
            }
            Card("Header",24,20,1552,66,panel);
            Label(content,"ShopName",44,30,190,44,"栖云当铺",30,gold);
            MakeButton("CalendarOpen",240,33,100,38,"日历",()=>{CancelDrag();CalendarView.Open();});
            header=Label(content,"Resources",350,34,380,34,"",24,textColor);
            phaseText=Label(content,"Phase",754,35,310,32,"",22,gold);
            rent=Label(content,"Rent",1110,31,445,44,"",15,muted);

            CreateGrid(ContainerId.Display,24,112,340,312,44,166,44,"展示柜");
            displaySummary=Label(content,"DisplaySummary",44,352,300,66,"",15,textColor);
            Card("DayActions",24,440,340,128,panel);
            beginButton=MakeButton("BeginBusiness",40,451,150,44,"开始营业",()=>Run(()=>Session.BeginBusiness()),true);
            endButton=MakeButton("EndBusiness",198,451,150,44,"结束营业",()=>Run(()=>Session.EndBusiness()));
            nextButton=MakeButton("NextCustomer",40,506,308,46,"下一位顾客",()=>Run(()=>Session.NextCustomer()));

            Card("CustomerCard",388,112,592,176,panel);
            customerTitle=Label(content,"CustomerTitle",410,129,548,38,"",25,gold);
            customerDetails=Label(content,"CustomerDetails",410,173,548,103,"",18,textColor);
            CreateGrid(ContainerId.Counter,388,306,592,262,458,355,49,"谈判柜台");
            stageButton=MakeButton("StageSale",730,358,232,42,"摆入一件同类商品",()=>Run(()=>Session.StageSale()));
            rotateButton=MakeButton("Rotate",730,412,110,42,"旋转 R",RotateSelected);
            flipButton=MakeButton("Flip",852,412,110,42,"翻转 F",FlipSelected);
            cancelButton=MakeButton("CancelDrag",730,466,232,42,"取消拖动 Esc",CancelDrag);
            Label(content,"OwnershipLegend",730,521,232,34,"己：自有   客：未付款",16,muted);

            CreateGrid(ContainerId.CustomerCounter,1004,112,572,246,1026,168,44,"顾客柜台 / 来货");
            Label(content,"CustomerGoodsHint",1270,174,280,150,"蓝框是顾客来货。\n拖入谈判柜台提出买入。\n\n确认购买前不能移入仓库或展示柜。",18,muted);
            Card("TradeCard",1004,370,572,198,panel);
            tradeHeading=Label(content,"TradeHeading",1024,379,532,29,"本次交易",22,gold);
            tradeDetails=ScrollableText("TradeDetails",1024,412,530,56);
            tradeStatus=Label(content,"TradeStatus",1024,475,532,39,"",16,textColor);
            acceptButton=MakeButton("NegotiationOpen",1024,519,260,36,"打开谈判",()=>NegotiationView.Open(),true);
            rejectButton=MakeButton("RejectTrade",1296,519,260,36,"拒绝 / 不成交",()=>Run(()=>Session.RejectTrade()));
            sleepButton=MakeButton("AdvanceTurn",1024,519,532,36,"结算完毕 · 推进下个月",()=>Run(()=>Session.AdvanceTurn()),true);

            notice=Label(content,"Notice",40,577,1516,24,"",16,gold);
            CreateGrid(ContainerId.Storage,24,618,756,358,44,663,43,"库存 / 自由整理");
            previewText=Label(content,"PlacementHint",504,677,250,86,"绿色可放 · 红色不可放\n拖动物品 / R 旋转\nF 翻转 / Esc 取消",17,muted);
            Label(content,"StorageHelp",504,790,250,143,"营业前后均可自由搬运。\n\n展示柜只在开门时吸引顾客；营业后可作额外仓库。",18,textColor);
            Card("ItemDetailCard",802,618,774,272,panel);
            Label(content,"ItemDetailTitle",824,634,730,32,"物品详情",23,gold);
            selection=ScrollableText("Selection",824,677,730,198);
            Card("Furnace",802,906,774,70,panel);
            furnaceText=Label(content,"Recipe",824,918,470,46,"",16,muted);
            craftButton=MakeButton("Craft",1300,919,254,43,"炼制回气丹",()=>Run(()=>Session.Craft()));
            var calendarObject=new GameObject("CalendarOverlay",typeof(RectTransform),typeof(ShopCalendarView));
            calendarObject.transform.SetParent(content,false);CalendarView=calendarObject.GetComponent<ShopCalendarView>();CalendarView.Initialize(this,font);
            var negotiationObject=new GameObject("NegotiationOverlay",typeof(RectTransform),typeof(ShopNegotiationView));
            negotiationObject.transform.SetParent(content,false);NegotiationView=negotiationObject.GetComponent<ShopNegotiationView>();NegotiationView.Initialize(this,font);
        }

        Text ScrollableText(string name,float x,float y,float width,float height)
        {
            var viewport=Rect(content,name+"Viewport",x,y,width,height);
            Image(viewport,new Color(.07f,.105f,.12f),true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            var text=Label(viewport,name,8,5,width-20,height,"",18,textColor);
            var fitter=text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=text.rectTransform;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=25;
            return text;
        }

        void CreateGrid(ContainerId id,float px,float py,float pw,float ph,float gx,float gy,float cell,string title)
        {
            Card(id+"Panel",px,py,pw,ph,panel);
            gridTitles[id]=Label(content,id+"Title",px+22,py+16,pw-36,36,title,22,textColor);
            var size=ShopSession.Size(id);
            var grid=Rect(content,id+"Grid",gx,gy,size.x*cell,size.y*cell);
            grids[id]=grid; cellSizes[id]=cell;
            for(int y=0;y<size.y;y++) for(int x=0;x<size.x;x++)
            {
                var tile=Rect(grid,$"Slot_{x}_{y}",x*cell,y*cell,cell-2,cell-2);
                Image(tile,new Color(.12f,.18f,.21f));
            }
        }
        void Card(string name,float x,float y,float w,float h,Color color) { Image(Rect(content,name,x,y,w,h),color); }
        static RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height); return rect;
        }
        static UnityEngine.UI.Image Image(RectTransform rect,Color color,bool raycast=false)
        { var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=color; image.raycastTarget=raycast; return image; }
        Text Label(Transform parent,string name,float x,float y,float w,float h,string value,int size,Color color)
        {
            var rect=Rect(parent,name,x,y,w,h); var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.fontSize=size;
            text.color=color; text.text=value; text.alignment=TextAnchor.UpperLeft; text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
            return text;
        }
        Button MakeButton(string name,float x,float y,float w,float h,string title,Action action,bool accent=false)
        {
            var rect=Rect(content,name,x,y,w,h); var image=Image(rect,accent?new Color(.29f,.39f,.30f):line,true);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(1.18f,1.18f,1.12f); colors.pressedColor=new Color(.74f,.83f,.76f); colors.disabledColor=new Color(.45f,.45f,.45f,.65f); button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None}; button.onClick.AddListener(()=>action());
            var label=Label(rect,"Label",6,2,w-12,h-4,title,17,textColor); label.alignment=TextAnchor.MiddleCenter;
            return button;
        }

        public void Refresh()
        {
            if(Session==null || content==null) return;
            foreach(var view in itemViews.Values) if(view!=null) {view.gameObject.SetActive(false);Destroy(view.gameObject);}
            itemViews.Clear();
            foreach(var item in Session.Items)
            {
                if(item.Container==ContainerId.Interior && (storageWindow==null || item.StorageItemId!=openStorageId))continue;
                var view=DrawItem(grids[item.Container],item,item.Rotation,item.Flipped,cellSizes[item.Container],true);
                view.anchoredPosition=new Vector2(item.X*cellSizes[item.Container],-item.Y*cellSizes[item.Container]); itemViews[item.Id]=view;
            }
            header.text=$"{Session.DateLabel}    |    灵石 {Session.Money}";
            phaseText.text=Session.Phase==TurnPhase.Preparation?"营业前 · 配置展示":Session.Phase==TurnPhase.Open?"营业中 · 客源已确定":"已闭店 · 整理 / 炼丹";
            rent.text=$"下次房租：第 {Session.NextRentTurn} 回合结束 / {Session.Rent} 灵石\n待付房租 {Session.RentDebt}  ·  每 6 回合结算，下期约涨 5%";
            foreach(var pair in gridTitles)
            {
                string name=pair.Key==ContainerId.Storage?"背包 / 仓库":pair.Key==ContainerId.Display?"本月展示柜":pair.Key==ContainerId.CustomerCounter?"顾客柜台 / 来货":"谈判柜台";
                var size=ShopSession.Size(pair.Key); pair.Value.text=$"{name}  {Session.Occupied(pair.Key)}/{size.x*size.y}";
            }
            var selected=Session.Find(selectedId);
            selection.text=selected==null?"点击物品查看价值、标签与说明。\n仓库中的预估价值等于基础价值；放入谈判柜台后按本次报价显示。":ItemDescription(selected);
            var attraction=Session.Phase==TurnPhase.Preparation?Session.PreviewAttraction():Session.TodayAttraction;
            string preferredCategory=attraction.BuyerCategory==ItemCategory.Unclassified?"随机类别":ShopCatalog.CategoryName(attraction.BuyerCategory);
            displaySummary.text=Session.Phase==TurnPhase.Preparation?$"每回合 5 位 · 仅求购 {1-attraction.SupplierChance:P0} / 携货求购 {attraction.SupplierChance:P0}\n{preferredCategory} · 资金 {attraction.MinimumBuyerBudget}–{attraction.MaximumBuyerBudget}\n基础价值 {attraction.DisplayValue} · 档位 ≥{attraction.BudgetTierMinimum}":$"本月仅求购 {Session.BuyersToday} / 携货求购 {Session.SuppliersToday}\n已离场 {Session.ServedToday} · 待到访 {Session.RemainingCustomers}\n开门时的展示效果已锁定";
            var offer=Session.Offer;
            bool canAccept=Session.CanAcceptTrade(out int total,out string tradeReason);
            bool closed=Session.Phase==TurnPhase.Closed;
            tradeHeading.text=closed?"本月结算":"本次交易 · 清单可滚动";
            if(closed)
            {
                customerTitle.text="本月已闭店";
                customerDetails.text="营业结束，物品留在原位。\n查看右侧结算，结束本回合后进入下个月。";
                tradeDetails.text=$"{Session.DateLabel}\n\n起始余额    {Session.OpeningMoney} 灵石\n本月收入    +{Session.IncomeToday}\n本月支出    −{Session.ExpensesToday}\n余额变化    {Session.BalanceChange:+0;-0;0}\n当前余额    {Session.Money} 灵石\n\n房租在结束回合时按既有规则处理。";
            }
            else if(offer==null)
            {
                customerTitle.text=Session.Phase==TurnPhase.Open?(Session.RemainingCustomers>0?"等待下一位顾客":"本月顾客已全部离场"):"营业前 · 配置店铺";
                customerDetails.text=Session.Phase==TurnPhase.Open?$"{Session.LastCustomerResult}\n剩余 {Session.RemainingCustomers} 位，可呼叫下一位或闭店。":$"空展示柜也有客人 · 每回合 5 位\n偏好：{preferredCategory} · 资金 {attraction.MinimumBuyerBudget}–{attraction.MaximumBuyerBudget}\n供货：{attraction.SupplierDescription}";
                tradeDetails.text=Session.Phase==TurnPhase.Open?"当前没有顾客。\n自有物品可以继续在三区域搬运。": "先把商品或收购牌放到左侧展示柜，再开始营业。\n\n玩家出售：基础价值 + 零售加价及有效标签。\n玩家收购：按卖家货物的当前报价付款。\n\n逐件报价求和，确认时按同一金额结算。";
            }
            else
            {
                bool buying=offer.Direction==TradeDirection.CustomerSells;
                customerTitle.text=$"{offer.CustomerName} · {(buying?"携货求购":"求购商品")}";
                string reason=buying?(attraction.HasAdvertisement?$"收购牌 · {attraction.SupplierDescription}":"自然到访"):(attraction.BuyerCategory==ItemCategory.Unclassified?"自然到访":$"展示柜 · {preferredCategory}");
                customerDetails.text=$"想要：{ShopCatalog.CategoryName(offer.RequestedCategory)} · 剩余资金：{offer.RemainingBudget}\n来货 {offer.SupplierItems.Count(i=>i.ForSale)} 件 · 总价值：{total} 灵石\n吸引原因：{reason}";
                var quote=Session.PreviewTrade();
                tradeDetails.text=string.Join("\n\n",quote.Lines.Select(l=>l.Description));
                if(quote.Lines.Count==0)tradeDetails.text="谈判柜台为空。打开谈判可请求全部来货，或手动选入商品。";
                tradeDetails.text+=$"\n\n合计 {quote.Lines.Count} 件 / {quote.Net} 灵石";
            }
            tradeStatus.text=closed?"收支已核对，可继续整理后进入下个月。":offer==null?tradeReason:$"本次总价 {total} 灵石\n{tradeReason}";
            tradeStatus.color=canAccept?new Color(.54f,.85f,.65f):gold;
            SetButtonTitle(acceptButton,"打开谈判 / 查看报价");
            SetButtonTitle(nextButton,offer==null?"呼叫下一位顾客":"送别并接待下一位");
            acceptButton.gameObject.SetActive(!closed);rejectButton.gameObject.SetActive(!closed);sleepButton.gameObject.SetActive(closed);
            furnaceText.text=$"丹炉（保留功能） · 凝气草 {Count(catalog.herbId)} / 灵露 {Count(catalog.dewId)}\n1 草 + 1 露 → 1 丹";
            displayedPricingRevision=Session.PricingRevision;
            if(displayedOffer!=offer || displayedPhase!=Session.Phase)
            {
                Canvas.ForceUpdateCanvases();
                tradeDetails.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;
                displayedOffer=offer;displayedPhase=Session.Phase;
            }
            notice.text=localNotice??Session.Message;
            beginButton.interactable=Session.Phase==TurnPhase.Preparation;
            nextButton.interactable=Session.Phase==TurnPhase.Open && (offer!=null || Session.RemainingCustomers>0);
            endButton.interactable=Session.Phase==TurnPhase.Open;
            sleepButton.interactable=Session.Phase==TurnPhase.Closed;
            stageButton.interactable=offer!=null;
            acceptButton.interactable=offer!=null;
            rejectButton.interactable=offer!=null;
            rotateButton.interactable=flipButton.interactable=selected!=null;
            cancelButton.interactable=IsDragging;
            if(CalendarView!=null)CalendarView.Refresh();
            if(NegotiationView!=null)NegotiationView.Refresh();
        }
        string ItemDescription(GridItem item)
        {
            var q=Session.Estimate(item);
            string history=item.PurchaseValue.HasValue?$" · 购买价值 {item.PurchaseValue.Value}":"";
            return $"{item.Definition.title} · {ShopCatalog.CategoryName(item.Definition.category)} · {(item.Owner==ItemOwner.Player?"自有":"顾客所有")} · 占 {item.Cells.Length} 格\n基础价值 {q.BaseValue} · 预估价值 {q.Amount}{history}\n{q.Modifiers}\n{item.Definition.description}";
        }
        int Count(string definitionId)=>Session.In(ContainerId.Storage).Count(i=>i.Definition.id==definitionId && i.Owner==ItemOwner.Player);
        static void SetButtonTitle(Button b,string value)=>b.GetComponentInChildren<Text>().text=value;
        public void Run(Func<bool> action) { if(IsDragging) {localNotice="请先放下物品，或按 Esc 取消拖动。";notice.text=localNotice;return;} localNotice=null; action(); Refresh(); }
        public void SelectItem(int id)
        {
            if(IsDragging)return;selectedId=id;localNotice=null;
            if(Session.Find(id).Definition.IsStorage)OpenStorage(id);
            Refresh();
            Canvas.ForceUpdateCanvases();selection.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;
        }

        // Explicit developer/test entry. The Editor menu supplies a temporary catalog clone.
        public void StartVerificationSession(ShopCatalog configuration,int seed,MarketCalendar calendar=null)
        {
            CloseStorage();CancelDrag();catalog=configuration;Session=new ShopSession(catalog,customerSeed:seed,calendar:calendar);
            CalendarMessage=null;CalendarView.Close();NegotiationView.Close();
            selectedId=0;localNotice=null;Refresh();
        }

        string PreparationSavePath => SavePath??System.IO.Path.Combine(Application.dataPath,"../UserSettings/ShopStoragePreparation-v3.json");
        public void SavePreparation()
        {
            try
            {
                string json=Session.CaptureSave(),path=PreparationSavePath;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path+".tmp",json);
                if(System.IO.File.Exists(path))System.IO.File.Replace(path+".tmp",path,null);
                else System.IO.File.Move(path+".tmp",path);
                CalendarMessage=$"已保存{Session.DateLabel}营业准备。";
            }
            catch(Exception e){CalendarMessage="无法保存："+e.Message;}
        }
        public void LoadPreparation()
        {
            if(Session.Phase!=TurnPhase.Preparation){CalendarMessage="仅营业准备阶段可读档。";return;}
            try
            {
                var restored=ShopSession.RestoreSave(catalog,System.IO.File.ReadAllText(PreparationSavePath));
                CloseStorage();CancelDrag();Session=restored;selectedId=0;localNotice=null;
                CalendarMessage=$"已读取{Session.DateLabel}营业准备，行情未重抽。";Refresh();
            }
            catch(Exception e){CalendarMessage="未读取，当前经营保留："+e.Message;}
        }

        RectTransform DrawItem(Transform parent,GridItem item,int rotation,bool flipped,float cell,bool interactive)
        {
            var shape=item.Definition.Shape(rotation,flipped);
            var rect=Rect(parent,$"Item_{item.Id}_{item.Definition.id}",0,0,(shape.Max(p=>p.x)+1)*cell,(shape.Max(p=>p.y)+1)*cell);
            if(interactive) { var handle=rect.gameObject.AddComponent<GridDragHandle>();handle.shop=this;handle.itemId=item.Id; rect.gameObject.AddComponent<CanvasGroup>(); }
            Color border=item.Owner==ItemOwner.Customer?new Color(.33f,.72f,1f):item.Id==selectedId?gold:new Color(.24f,.32f,.32f);
            foreach(var p in shape)
            {
                var tile=Rect(rect,"OccupiedCell",p.x*cell+2,p.y*cell+2,cell-5,cell-5); Image(tile,border,interactive);
                var fill=Rect(tile,"Fill",3,3,cell-11,cell-11); Image(fill,item.Definition.color,interactive);
            }
            var first=shape.OrderBy(p=>p.y).ThenBy(p=>p.x).First();
            var name=Label(rect,"ItemName",first.x*cell+3,first.y*cell+18,cell-7,26,item.Definition.title,12,new Color(.08f,.12f,.14f)); name.alignment=TextAnchor.MiddleCenter;
            Label(rect,"Owner",first.x*cell+5,first.y*cell+3,cell-8,17,item.Owner==ItemOwner.Customer?"客":"己",11,new Color(.12f,.18f,.20f));
            return rect;
        }
        public Vector2 CellScreenPosition(ContainerId container,int x,int y)
        { return RectTransformUtility.WorldToScreenPoint(null,grids[container].TransformPoint(new Vector3((x+.5f)*cellSizes[container],-(y+.5f)*cellSizes[container],0))); }
        public RectTransform ItemView(int id)=>itemViews.TryGetValue(id,out var view)?view:null;
        public Button FindButton(string name)=>content.GetComponentsInChildren<Button>(true).First(b=>b.name==name);

        public void BeginItemDrag(int id,Vector2 screen)
        {
            var item=Session.Find(id); if(item==null || IsDragging)return;
            selectedId=dragId=id; dragRotation=item.Rotation; dragFlipped=item.Flipped;pointer=screen;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(grids[item.Container],screen,null,out var point);
            grabCell=new Vector2Int(Mathf.FloorToInt(point.x/cellSizes[item.Container])-item.X,Mathf.FloorToInt(-point.y/cellSizes[item.Container])-item.Y);
            if(itemViews.TryGetValue(id,out var view)) view.GetComponent<CanvasGroup>().alpha=.25f;
            cancelButton.interactable=true; RebuildGhost(); DragItem(screen);
        }
        void RebuildGhost()
        {
            if(ghost!=null) {ghost.gameObject.SetActive(false);Destroy(ghost.gameObject);}
            var item=Session.Find(dragId); if(item==null)return;
            ghost=DrawItem(content,item,dragRotation,dragFlipped,52,false); var group=ghost.gameObject.AddComponent<CanvasGroup>();group.alpha=.62f;group.blocksRaycasts=false;
        }
        bool DropPosition(Vector2 screen,out ContainerId container,out int x,out int y)
        {
            var shape=Session.Find(dragId).Definition.Shape(dragRotation,dragFlipped);
            int offsetX=Mathf.Clamp(grabCell.x,0,shape.Max(p=>p.x)),offsetY=Mathf.Clamp(grabCell.y,0,shape.Max(p=>p.y));
            foreach(var pair in grids.OrderByDescending(p=>p.Key==ContainerId.Interior))
                if(pair.Value.gameObject.activeInHierarchy && (pair.Key==ContainerId.Interior || storageWindow==null || !RectTransformUtility.RectangleContainsScreenPoint(storageWindow,screen,null)) && RectTransformUtility.RectangleContainsScreenPoint(pair.Value,screen,null))
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(pair.Value,screen,null,out var p);
                    container=pair.Key; x=Mathf.FloorToInt(p.x/cellSizes[container])-offsetX; y=Mathf.FloorToInt(-p.y/cellSizes[container])-offsetY; return true;
                }
            container=ContainerId.Storage;x=y=0;return false;
        }
        public void DragItem(Vector2 screen)
        {
            if(!IsDragging)return;pointer=screen;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content,screen,null,out var point);
            ghost.anchoredPosition=point+new Vector2(800,-500)+new Vector2(18,-18);
            if(preview!=null) {preview.gameObject.SetActive(false);Destroy(preview.gameObject);}
            if(DropPosition(screen,out var target,out int x,out int y))
            {
                bool valid=Session.CanMove(dragId,target,x,y,dragRotation,dragFlipped,out string reason,target==ContainerId.Interior?openStorageId:0);
                PreviewMessage=valid?"可以放置 · 松开鼠标确认":reason;
                var color=valid?new Color(.35f,.92f,.54f,.64f):new Color(1f,.27f,.29f,.65f);
                float cell=cellSizes[target];
                preview=Rect(grids[target],"PlacementPreview",x*cell,y*cell,1,1);
                foreach(var p in Session.Find(dragId).Definition.Shape(dragRotation,dragFlipped)) Image(Rect(preview,"PreviewCell",p.x*cell+2,p.y*cell+2,cell-5,cell-5),color);
                previewText.text=PreviewMessage;previewText.color=color;
            }
            else {PreviewMessage="格子外不能放置，松开将返回原位。";previewText.text=PreviewMessage;previewText.color=gold;}
            notice.text=$"正在拖动 {Session.Find(dragId).Definition.title}  ·  R 旋转 / F 翻转 / Esc 取消\n{PreviewMessage}";
        }
        public void EndItemDrag(Vector2 screen)
        {
            if(!IsDragging)return;
            bool openNegotiation=false;
            if(DropPosition(screen,out var target,out int x,out int y))
            {
                localNotice=null;var item=Session.Find(dragId);
                bool buying=item.ForSale && item.Container==ContainerId.CustomerCounter && target==ContainerId.Counter;
                openNegotiation=Session.Move(dragId,target,x,y,dragRotation,dragFlipped,target==ContainerId.Interior?openStorageId:0) && buying;
            }
            else localNotice="已返回原位：请将物品放在容器格子内。";
            ClearDrag();Refresh();
            if(openNegotiation)NegotiationView.Open(false);
        }
        public void CancelDrag() { if(!IsDragging)return;localNotice="已取消拖动，物品保持原位。";ClearDrag();Refresh(); }
        void ClearDrag()
        {
            dragId=0;
            if(ghost!=null) {ghost.gameObject.SetActive(false);Destroy(ghost.gameObject);}
            if(preview!=null) {preview.gameObject.SetActive(false);Destroy(preview.gameObject);}
            ghost=preview=null;previewText.text="绿色：合法    红色：非法    物品间空洞可以嵌放";previewText.color=muted;
        }
        public void RotateSelected()
        {
            if(IsDragging) {dragRotation=(dragRotation+1)%4;RebuildGhost();DragItem(pointer);return;}
            var item=Session.Find(selectedId); if(item!=null)Run(()=>Session.Move(item.Id,item.Container,item.X,item.Y,(item.Rotation+1)%4,item.Flipped,item.StorageItemId));
        }
        public void FlipSelected()
        {
            // Reflect in screen/grid space even after a quarter-turn: F R = R^-1 F.
            if(IsDragging) {dragRotation=(4-dragRotation)%4;dragFlipped=!dragFlipped;RebuildGhost();DragItem(pointer);return;}
            var item=Session.Find(selectedId);if(item!=null)Run(()=>Session.Move(item.Id,item.Container,item.X,item.Y,(4-item.Rotation)%4,!item.Flipped,item.StorageItemId));
        }
    }
}
