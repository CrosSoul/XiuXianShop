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
        RectTransform originalStorageGrid;
        float originalStorageCell;
        Text shopAlchemyNotice;
        void OpenShopAlchemyPage(int deviceId)
        {
            if(!Session.OpenShopAlchemy(deviceId)){localNotice=Session.Message;return;}
            CloseStorage();CloseTravelWindow();CancelDrag();
            travelWindow=Rect(content,"ShopAlchemyWindow",0,0,1600,1000);Image(travelWindow,new Color(.035f,.06f,.07f),true);
            Label(travelWindow,"ShopAlchemyTitle",100,40,1370,55,"店内微缩炼丹炉 · 仓库手动备料",29,gold);
            AddAlchemyGrid(ContainerId.AlchemyPreparation,100,180,"");
            originalStorageGrid=grids[ContainerId.Storage];originalStorageCell=cellSizes[ContainerId.Storage];
            AddAlchemyGrid(ContainerId.Storage,100,525,"仓库根层");
            Label(travelWindow,"ShopAlchemyHint",740,525,490,180,"从仓库手动拖入备料和供能位。\n收丹后把产物拖回仓库，再准备下一炉。\n关闭前请收回备料、灵石及产物。",22,textColor);
            shopAlchemyNotice=Label(travelWindow,"ShopAlchemyNotice",740,725,490,150,"",21,gold);
            BuildAlchemyPanel();Refresh();
        }
        void CloseShopAlchemyPage()
        {
            if(!Session.CloseShopAlchemy()){RefreshAlchemyPanel();return;}
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
            alchemyDebug.rectTransform.sizeDelta=new Vector2(278,Mathf.Max(510,alchemyDebug.preferredHeight+20));
        }
    }
}
