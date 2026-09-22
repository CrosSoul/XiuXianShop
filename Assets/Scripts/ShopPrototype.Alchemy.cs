using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        Text alchemyStatus,alchemyDebug,alchemyRecipeText,alchemyGrinding;
        Button[] alchemyActions;
        Text shopAlchemyNotice;
        public bool IsShopAlchemyWindowOpen => Session.IsAtShopAlchemy && travelWindow!=null;
        void OpenShopAlchemyPage(int deviceId)
        {
            if(!Session.OpenShopAlchemy(deviceId)){localNotice=Session.Message;return;}
            CloseStorage();CloseTravelWindow();CancelDrag();
            travelWindow=Rect(content,"ShopAlchemyWindow",790,105,790,870);Image(travelWindow,panel,true);
            var title=Rect(travelWindow,"ShopAlchemyTitleBar",0,0,660,44);Image(title,line,true);
            title.gameObject.AddComponent<StorageWindowDrag>().window=travelWindow;
            Label(title,"Title",12,8,640,30,"微缩炼丹炉 · 拖动标题移动窗口",21,gold);
            AddAlchemyGrid(ContainerId.AlchemyPreparation,20,165,"");
            Label(travelWindow,"ShopAlchemyHint",20,760,510,86,"直接从左侧主仓库拖入材料 / 灵石，收丹后拖回。\n关闭浮窗保留炉内内容，炼制仍继续。\n收回设施物品并关闭后，才能外出或推进月份。",17,textColor);
            shopAlchemyNotice=Label(travelWindow,"ShopAlchemyNotice",20,670,510,85,"",18,gold);
            BuildAlchemyPanel();Refresh();
        }
        void CloseShopAlchemyPage()
        {
            CancelDrag();
            // Closing a view never discards a loaded or running batch. Release the device only when empty.
            if(!Session.HasPendingShopAlchemy)Session.CloseShopAlchemy();
            CloseTravelWindow();Refresh();
        }
        void BuildAlchemyPanel()
        {
            // Fits above the existing carry panel; every item uses the same grid drag handler.
            Label(travelWindow,"PreparationTitle",100,150,300,30,"备料格 · 点击选中后投料/研磨",18,gold);
            AddAlchemyGrid(ContainerId.AlchemyFuel,430,190,"供能位");
            AddAlchemyGrid(ContainerId.AlchemyOutput,560,190,"收丹位");
            for(int i=0;i<Session.Catalog.alchemy.recipes.Length;i++)
            {
                var recipe=Session.Catalog.alchemy.recipes[i];
                CarryButton(travelWindow,"AlchemyRecipe_"+recipe.id,740+i*245,105,235,recipe.title,()=>AlchemyCommand(()=>Session.SelectAlchemyRecipe(recipe.id)));
            }
            alchemyRecipeText=Label(travelWindow,"AlchemyRecipe",740,145,735,78,"",16,textColor);
            alchemyStatus=Label(travelWindow,"AlchemyStatus",740,225,750,75,"",17,gold);
            alchemyActions=new[]{
                CarryButton(travelWindow,"AlchemyAdd",740,305,175,"投入选中材料",()=>AlchemyCommand(()=>Session.AddAlchemyIngredient(selectedId))),
                CarryButton(travelWindow,"AlchemyGrind",925,305,175,"研磨选中材料",()=>AlchemyCommand(()=>Session.GrindAlchemyIngredient(selectedId))),
                CarryButton(travelWindow,"AlchemyStart",1110,305,175,"开炉",()=>AlchemyCommand(Session.StartAlchemy)),
                CarryButton(travelWindow,"AlchemyCollect",1295,305,175,"收丹",()=>AlchemyCommand(Session.CollectAlchemy))};
            for(int i=0;i<3;i++){var heat=(AlchemyHeat)i;CarryButton(travelWindow,"AlchemyHeat_"+heat,740+i*135,350,125,new[]{"低火","中火","高火"}[i],()=>AlchemyCommand(()=>Session.SetAlchemyHeat(heat)));}
            CarryButton(travelWindow,"AlchemyAbort",1160,350,150,"中止本炉",()=>AlchemyCommand(Session.AbortAlchemy));
            if(Session.IsAtShopAlchemy)
            {
                CarryButton(travelWindow,"ShopAlchemyClose",1320,350,170,"关闭炼丹炉",CloseShopAlchemyPage);
                CarryButton(travelWindow,"AlchemyNextBatch",740,440,350,"准备下一炉",()=>AlchemyCommand(Session.PrepareNextShopBatch));
            }
            else CarryButton(travelWindow,"TravelLeave",1320,350,170,"离开炼丹房",RequestLocationLeave);
            alchemyGrinding=Label(travelWindow,"AlchemyGrinding",925,390,350,32,"",18,gold);
            var viewport=Rect(travelWindow,"AlchemyDebugViewport",1275,445,300,520);Image(viewport,panel,true);viewport.gameObject.AddComponent<RectMask2D>();
            alchemyDebug=Label(viewport,"AlchemyDebug",8,8,278,510,"",16,muted);
            alchemyDebug.alignment=TextAnchor.UpperLeft;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=alchemyDebug.rectTransform;scroll.horizontal=false;scroll.scrollSensitivity=30;
            if(Session.IsAtShopAlchemy)
            {
                // Same controls and handlers, arranged in a secondary window beside the real warehouse.
                void Place(string name,float x,float y,float width,float height)
                {
                    var rect=(RectTransform)travelWindow.Find(name);
                    rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);
                }
                Place("PreparationTitle",20,133,290,30);
                Place("AlchemyFuelTitle",330,133,100,30);Place("AlchemyFuelGrid",330,165,96,96);
                Place("AlchemyOutputTitle",440,133,100,30);Place("AlchemyOutputGrid",440,165,96,96);
                for(int i=0;i<Session.Catalog.alchemy.recipes.Length;i++)Place("AlchemyRecipe_"+Session.Catalog.alchemy.recipes[i].id,20+i*250,53,240,38);
                Place("AlchemyRecipe",20,95,745,38);Place("AlchemyStatus",20,385,510,85);
                for(int i=0;i<alchemyActions.Length;i++)Place(alchemyActions[i].name,20+i*130,475,125,38);
                for(int i=0;i<3;i++)Place("AlchemyHeat_"+(AlchemyHeat)i,20+i*130,523,125,38);
                Place("AlchemyAbort",410,523,125,38);Place("AlchemyGrinding",150,568,385,35);
                Place("ShopAlchemyClose",667,4,115,36);Place("AlchemyNextBatch",20,615,300,38);
                Place("AlchemyDebugViewport",550,165,225,675);
                alchemyDebug.rectTransform.sizeDelta=new Vector2(203,659);
            }
            RefreshAlchemyPanel();
        }
        void AddAlchemyGrid(ContainerId area,float x,float y,string title)
        {
            var size=Session.GridSize(area);const float cell=48;
            Label(travelWindow,area+"Title",x,y-30,125,28,title,18,gold);
            var grid=Rect(travelWindow,area+"Grid",x,y,size.x*cell,size.y*cell);Image(grid,line,true);
            grids[area]=grid;cellSizes[area]=cell;
            for(int row=0;row<size.y;row++)for(int col=0;col<size.x;col++)Image(Rect(grid,"Slot",col*cell,row*cell,cell-2,cell-2),panel);
        }
        void AlchemyCommand(Func<bool> action)
        {
            if(IsDragging){localNotice="请先放下正在拖动的物品。";return;}
            action();Refresh();RefreshAlchemyPanel();
        }
        void TickAlchemyPanel()
        {
            if(Session==null || !Session.IsUsingAlchemy)return;
            int before=Session.Items.Count;
            bool wasGrinding=Session.Alchemy!=null && Session.Alchemy.Locked;
            Session.TickAlchemy(Time.unscaledDeltaTime*Session.Catalog.alchemy.debugTimeScale);
            if(Session.Items.Count!=before || (wasGrinding && !Session.Alchemy.Locked))Refresh();
            RefreshAlchemyPanel();
        }
        void RefreshAlchemyPanel()
        {
            if(alchemyStatus==null || !Session.IsUsingAlchemy)return;
            if(shopAlchemyNotice!=null)shopAlchemyNotice.text=Session.Message;
            var a=Session.Alchemy;
            if(a==null){alchemyStatus.text="未开炉 · 中火\n"+Session.Message;return;}
            var cfg=Session.Catalog.alchemy;
            string Title(AlchemyTarget t)=>t.kind==AlchemyEventKind.Ingredient?Session.Catalog.Find(t.itemId).title+(t.ground?"（研磨）":""):t.kind==AlchemyEventKind.Heat?"换"+AlchemySettings.HeatName(t.heat):"收丹";
            alchemyRecipeText.text=string.Join(" → ",a.Recipe.targets.Select(t=>$"{t.breaths:0.#}炉息 {Title(t)}"));
            var fuel=Session.In(ContainerId.AlchemyFuel).FirstOrDefault();
            var selected=Session.Find(selectedId);
            alchemyGrinding.text=a.Locked?$"研磨中 · 剩余 {a.GrindingRemaining:0.0}s":Session.IsAlchemyGround(selectedId)?"已研磨":"";
            var worst=a.Judgements.OrderBy(j=>j.Score).ThenByDescending(j=>Math.Abs(j.ActualTime-j.TargetTime)).FirstOrDefault();
            string summary=a.StructuralErrors.FirstOrDefault() ?? (worst==null || worst.Result=="完美"?"无明显偏差":
                worst.Action+(worst.EntryTime.HasValue?(worst.ActualTime>worst.TargetTime?"在炉过久":"在炉不足"):"时机偏差"));
            alchemyStatus.text=$"{(a.Phase==AlchemyPhase.Preparing?"未开炉":AlchemySettings.PhaseName(a.Phase))} · {AlchemySettings.HeatName(a.Heat)} · 炉钟 {a.Time:0.0}s · 选中：{selected?.Definition.title??"无"}\n"+
                (a.Phase==AlchemyPhase.Finished?$"已结束 · 品相：{AlchemySettings.QualityName(a.Quality)} · {summary}":Session.Message);
            foreach(var button in alchemyActions)button.interactable=!a.Locked && a.Phase!=AlchemyPhase.Finished && a.Phase!=AlchemyPhase.Aborted;
            if(Session.IsAtShopAlchemy)
            {
                alchemyStatus.text+=$"\n体力 {Session.Stamina} · 每炉 {cfg.shopStaminaCost}（成功开炉时扣除）";
                alchemyActions[2].interactable &= Session.Phase==TurnPhase.Closed && Session.Stamina>=cfg.shopStaminaCost && a.Phase==AlchemyPhase.Preparing;
            }
            alchemyDebug.text=cfg.showDebug?$"开发调试（可滚动）\n炉钟 {a.Time:0.00}s\n灵气 {fuel?.SpiritUnits??0} 单位\n完美±{cfg.perfectWindow}s / 合格±{cfg.acceptableWindow}s\n分数{a.Score:0.00}\n目标：\n"+
                string.Join("\n",a.Recipe.targets.Where(t=>t.kind==AlchemyEventKind.Heat).Select(t=>$"{t.breaths*cfg.breathSeconds:0.00}s {Title(t)}"))+"\n逐材料在炉时长 / 火候判定：\n"+
                string.Join("\n",a.Judgements.Select(j=>j.EntryTime.HasValue && a.Phase!=AlchemyPhase.Finished?
                    $"{j.Action}：入炉{j.EntryTime:0.00}s / 目标在炉{j.TargetTime:0.00}s / 实际{a.Time-j.EntryTime.Value:0.00}s / 偏差{a.Time-j.EntryTime.Value-j.TargetTime:+0.00;-0.00;0.00}s · 待收丹":j.ToString()).Concat(a.Actions).Concat(a.StructuralErrors)):"";
            var debugViewport=(RectTransform)alchemyDebug.transform.parent;
            alchemyDebug.rectTransform.sizeDelta=new Vector2(debugViewport.rect.width-22,Mathf.Max(debugViewport.rect.height-16,alchemyDebug.preferredHeight+20));
        }
    }
}
