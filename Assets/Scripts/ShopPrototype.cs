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
    public sealed class ShopPrototype : MonoBehaviour
    {
        [SerializeField] ShopCatalog catalog;
        public ShopSession Session { get; private set; }
        public ShopCatalog Catalog { get=>catalog; set=>catalog=value; }
        public bool IsDragging => dragId!=0;
        public int SelectedId => selectedId;
        public string PreviewMessage { get; private set; }
        Font font;
        RectTransform content, ghost, preview;
        Text header, rent, phaseText, selection, customerTitle, customerDetails, notice, furnaceText, previewText, displaySummary;
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
            Session=new ShopSession(catalog);
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Noto Sans CJK SC","Arial"},24);
            BuildScreen(); Refresh();
        }

        void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || Session==null) return;
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
            Card("Header",24,22,1552,80,panel);
            Label(content,"ShopName",44,33,335,42,"栖云小铺",32,gold);
            Label(content,"Subtitle",45,76,330,20,"空间经营 / 炼丹原型",13,muted);
            header=Label(content,"Resources",385,38,330,38,"",25,textColor);
            phaseText=Label(content,"Phase",760,40,265,32,"",22,gold);
            rent=Label(content,"Rent",1045,35,505,52,"",17,muted);
            Label(content,"Instructions",36,118,970,50,"拖动物品到格子 · 点击选中后也可用按钮变形\n拖动时：R 旋转 / F 水平翻转 / Esc 取消。绿色可放，红色不可放。",17,muted);
            rotateButton=MakeButton("Rotate",1015,115,168,46,"旋转  R ↻",RotateSelected);
            flipButton=MakeButton("Flip",1195,115,176,46,"水平翻转  F",FlipSelected);
            cancelButton=MakeButton("CancelDrag",1383,115,181,46,"取消拖动 Esc",CancelDrag);

            CreateGrid(ContainerId.Storage,24,181,714,638,54,238,62,"背包 / 仓库");
            CreateGrid(ContainerId.Display,758,181,394,328,781,238,58,"今日展示柜");
            CreateGrid(ContainerId.Counter,1172,181,404,328,1224,238,58,"谈判柜台");
            selection=Label(content,"Selection",52,700,658,96,"",17,textColor);
            displaySummary=Label(content,"DisplaySummary",775,474,360,32,"",13,muted);
            Label(content,"OwnershipLegend",1191,478,365,24,"己 = 你的物品    客 = 顾客物品",14,muted);

            Card("CustomerCard",758,528,818,224,panel);
            Card("CustomerPortrait",781,550,82,116,new Color(.15f,.23f,.25f));
            Label(content,"Portrait",796,567,54,60,"客",38,gold);
            Label(content,"PortraitFoot",790,625,68,28,"来客",13,muted);
            customerTitle=Label(content,"CustomerTitle",885,545,660,35,"",23,gold);
            customerDetails=Label(content,"CustomerDetails",885,589,660,92,"",16,textColor);
            customerDetails.resizeTextForBestFit=true;customerDetails.resizeTextMinSize=12;customerDetails.resizeTextMaxSize=16;
            stageButton=MakeButton("StageSale",781,689,234,43,"摆入一件同类商品",()=>Run(()=>Session.StageSale()));
            acceptButton=MakeButton("AcceptTrade",1027,689,246,43,"确认交易",()=>Run(()=>Session.AcceptTrade()),true);
            rejectButton=MakeButton("RejectTrade",1285,689,264,43,"拒绝 / 不成交",()=>Run(()=>Session.RejectTrade()));

            Label(content,"DailyActions",776,770,770,27,"每日流程：配置展示 → 营业 → 接待 → 闭店 → 睡觉",17,muted);
            beginButton=MakeButton("BeginBusiness",775,811,180,50,"开始营业",()=>Run(()=>Session.BeginBusiness()),true);
            nextButton=MakeButton("NextCustomer",972,811,180,50,"下一位顾客",()=>Run(()=>Session.NextCustomer()));
            endButton=MakeButton("EndBusiness",1169,811,180,50,"结束营业",()=>Run(()=>Session.EndBusiness()));
            sleepButton=MakeButton("Sleep",1366,811,180,50,"睡觉 / 下一天",()=>Run(()=>Session.Sleep()),true);

            Card("Furnace",24,837,714,139,panel);
            Label(content,"FurnaceTitle",48,853,422,31,"丹炉 · 已拥有",23,gold);
            furnaceText=Label(content,"Recipe",48,893,420,64,"",16,textColor);
            craftButton=MakeButton("Craft",493,875,214,58,"炼制回气丹",()=>Run(()=>Session.Craft()),true);
            Card("NoticeCard",758,879,818,97,new Color(.12f,.19f,.21f));
            notice=Label(content,"Notice",778,893,774,67,"",17,textColor);
            previewText=Label(content,"PlacementHint",51,675,659,25,"绿色：合法    红色：非法    物品间空洞可以嵌放",15,muted);
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
                var view=DrawItem(grids[item.Container],item,item.Rotation,item.Flipped,cellSizes[item.Container],true);
                view.anchoredPosition=new Vector2(item.X*cellSizes[item.Container],-item.Y*cellSizes[item.Container]); itemViews[item.Id]=view;
            }
            header.text=$"第 {Session.Day} 天    |    灵石 {Session.Money}";
            phaseText.text=Session.Phase==DayPhase.Preparation?"营业前 · 配置展示":Session.Phase==DayPhase.Open?"营业中 · 客源已确定":"已闭店 · 整理 / 炼丹";
            rent.text=$"下次房租：第 {Session.NextRentDay} 天夜间 / {Session.Rent} 灵石\n待付房租 {Session.RentDebt}  ·  每 7 天结算，下期约涨 5%";
            foreach(var pair in gridTitles)
            {
                string name=pair.Key==ContainerId.Storage?"背包 / 仓库":pair.Key==ContainerId.Display?"今日展示柜":"谈判柜台";
                var size=ShopSession.Size(pair.Key); pair.Value.text=$"{name}  {Session.Occupied(pair.Key)}/{size.x*size.y}";
            }
            var selected=Session.Find(selectedId);
            selection.text=selected==null?"点击物品查看名称、价格与形状。\n先把紫色「收购牌」与金色「回气丹」拖进展示柜。\n同一格子规则适用于背包、展示柜和柜台。":$"选中：{selected.Definition.title} · {(selected.Owner==ItemOwner.Player?"你的物品":"顾客所有")} · 占 {selected.Cells.Length} 格\n{selected.Definition.description}\n收购 {selected.Definition.purchasePrice} / 出售 {selected.Definition.salePrice} 灵石";
            if(selected!=null) selection.text+=$" · 类别：{ShopCatalog.CategoryName(selected.Definition.category)}";
            var attraction=Session.Phase==DayPhase.Preparation?Session.PreviewAttraction():Session.TodayAttraction;
            string preferredCategory=attraction.BuyerCategory==ItemCategory.Unclassified?"随机类别":ShopCatalog.CategoryName(attraction.BuyerCategory);
            displaySummary.text=Session.Phase==DayPhase.Preparation?$"每日 5 位 · 买家 {1-attraction.SupplierChance:P0} / 卖家 {attraction.SupplierChance:P0}\n买家：{preferredCategory} · 资金 {attraction.MinimumBuyerBudget}–{attraction.MaximumBuyerBudget}":$"今日 5 位：买家 {Session.BuyersToday} / 卖家 {Session.SuppliersToday}\n已离场 {Session.ServedToday} · 待到访 {Session.RemainingCustomers} · 客源已确定";
            var offer=Session.Offer;
            bool canAccept=Session.CanAcceptTrade(out int total,out string tradeReason);
            if(offer==null)
            {
                customerTitle.text=Session.Phase==DayPhase.Open?"客人已离开":"等待开门";
                customerDetails.text=Session.Phase==DayPhase.Open?$"今天已接待 {Session.ServedToday} 位，剩余 {Session.RemainingCustomers} 位。\n点击「下一位顾客」，或结束营业。":$"每日固定 5 位，各自随机决定买卖方向；空展示柜也有客人。\n买家偏好：{preferredCategory} · 展示价值 {attraction.DisplayValue} · 档位 ≥{attraction.BudgetTierMinimum}\n资金范围 {attraction.MinimumBuyerBudget}–{attraction.MaximumBuyerBudget}；卖家供货：{attraction.SupplierDescription}。\n仅在开门时生成当天客源，之后可自由整理。";
            }
            else
            {
                var item=Session.Find(offer.ItemId);
                bool buying=offer.Direction==TradeDirection.CustomerSells;
                customerTitle.text=$"{offer.CustomerName}  /  {(buying?"向你出售":"向你购买")}";
                customerDetails.text=buying?$"{item.Definition.title} × 1  ·  顾客报价 {offer.Price} 灵石\n{tradeReason}\n付款后收入背包；柜台上的自有物品保持不变。":$"想要：{ShopCatalog.CategoryName(offer.RequestedCategory)}  ·  剩余资金：{offer.RemainingBudget}  ·  总价值：{total} 灵石\n{tradeReason}\n{Session.CounterSaleSummary}";
            }
            SetButtonTitle(acceptButton,offer!=null && offer.Direction==TradeDirection.CustomerSells?$"确认收购  −{offer.Price}":offer!=null?$"确认出售  +{total}":"确认交易");
            int cost=catalog.Find(catalog.herbId).purchasePrice+catalog.Find(catalog.dewId).purchasePrice;
            furnaceText.text=$"背包原料：凝气草 {Count(catalog.herbId)} / 灵露 {Count(catalog.dewId)}\n1 草 + 1 露 → 1 丹 · 成本 {cost} / 售价 {catalog.Find(catalog.productId).salePrice}";
            notice.text=localNotice??Session.Message;
            beginButton.interactable=Session.Phase==DayPhase.Preparation;
            nextButton.interactable=Session.Phase==DayPhase.Open && (offer!=null || Session.RemainingCustomers>0);
            endButton.interactable=Session.Phase==DayPhase.Open;
            sleepButton.interactable=Session.Phase==DayPhase.Closed;
            stageButton.interactable=offer!=null && offer.Direction==TradeDirection.CustomerBuys;
            acceptButton.interactable=canAccept;
            rejectButton.interactable=offer!=null;
            rotateButton.interactable=flipButton.interactable=selected!=null;
            cancelButton.interactable=IsDragging;
        }
        int Count(string definitionId)=>Session.In(ContainerId.Storage).Count(i=>i.Definition.id==definitionId && i.Owner==ItemOwner.Player);
        static void SetButtonTitle(Button b,string value)=>b.GetComponentInChildren<Text>().text=value;
        public void Run(Func<bool> action) { if(IsDragging) {localNotice="请先放下物品，或按 Esc 取消拖动。";notice.text=localNotice;return;} localNotice=null; action(); Refresh(); }
        public void SelectItem(int id) { if(IsDragging)return; selectedId=id;localNotice=null;Refresh(); }

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
            var name=Label(rect,"ItemName",first.x*cell+3,first.y*cell+18,cell-7,26,item.Definition.title,14,new Color(.08f,.12f,.14f)); name.alignment=TextAnchor.MiddleCenter;
            Label(rect,"Owner",first.x*cell+5,first.y*cell+3,cell-8,17,item.Owner==ItemOwner.Customer?"客":"己",11,new Color(.12f,.18f,.20f));
            return rect;
        }
        public Vector2 CellScreenPosition(ContainerId container,int x,int y)
        { return RectTransformUtility.WorldToScreenPoint(null,grids[container].TransformPoint(new Vector3((x+.5f)*cellSizes[container],-(y+.5f)*cellSizes[container],0))); }
        public RectTransform ItemView(int id)=>itemViews.TryGetValue(id,out var view)?view:null;
        public Button FindButton(string name)=>content.GetComponentsInChildren<Button>().First(b=>b.name==name);

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
            foreach(var pair in grids)
                if(RectTransformUtility.RectangleContainsScreenPoint(pair.Value,screen,null))
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
                bool valid=Session.CanMove(dragId,target,x,y,dragRotation,dragFlipped,out string reason);
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
            if(DropPosition(screen,out var target,out int x,out int y)) {localNotice=null;Session.Move(dragId,target,x,y,dragRotation,dragFlipped);}
            else localNotice="已返回原位：请将物品放在容器格子内。";
            ClearDrag();Refresh();
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
            var item=Session.Find(selectedId); if(item!=null)Run(()=>Session.Move(item.Id,item.Container,item.X,item.Y,(item.Rotation+1)%4,item.Flipped));
        }
        public void FlipSelected()
        {
            // Reflect in screen/grid space even after a quarter-turn: F R = R^-1 F.
            if(IsDragging) {dragRotation=(4-dragRotation)%4;dragFlipped=!dragFlipped;RebuildGhost();DragItem(pointer);return;}
            var item=Session.Find(selectedId);if(item!=null)Run(()=>Session.Move(item.Id,item.Container,item.X,item.Y,(4-item.Rotation)%4,!item.Flipped));
        }
    }
}
