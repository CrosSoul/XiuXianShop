using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        Text alchemyStatus,alchemyDebug,alchemyRecipeText;
        Button[] alchemyActions;
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
            alchemyStatus=Label(travelWindow,"AlchemyStatus",740,225,750,65,"",18,gold);
            alchemyActions=new[]{
                CarryButton(travelWindow,"AlchemyAdd",740,305,175,"投入选中材料",()=>AlchemyCommand(()=>Session.AddAlchemyIngredient(selectedId))),
                CarryButton(travelWindow,"AlchemyGrind",925,305,175,"研磨选中材料",()=>AlchemyCommand(()=>Session.GrindAlchemyIngredient(selectedId))),
                CarryButton(travelWindow,"AlchemyStart",1110,305,175,"开炉",()=>AlchemyCommand(Session.StartAlchemy)),
                CarryButton(travelWindow,"AlchemyCollect",1295,305,175,"收丹",()=>AlchemyCommand(Session.CollectAlchemy))};
            for(int i=0;i<3;i++){var heat=(AlchemyHeat)i;CarryButton(travelWindow,"AlchemyHeat_"+heat,740+i*135,350,125,new[]{"低火","中火","高火"}[i],()=>AlchemyCommand(()=>Session.SetAlchemyHeat(heat)));}
            CarryButton(travelWindow,"AlchemyAbort",1160,350,150,"中止本炉",()=>AlchemyCommand(Session.AbortAlchemy));
            CarryButton(travelWindow,"TravelLeave",1320,350,170,"离开炼丹房",RequestLocationLeave);
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
            if(Session==null || !Session.IsAtAlchemy)return;
            int before=Session.Items.Count;
            Session.TickAlchemy(Time.unscaledDeltaTime*Session.Catalog.alchemy.debugTimeScale);
            if(Session.Items.Count!=before)Refresh();
            RefreshAlchemyPanel();
        }
        void RefreshAlchemyPanel()
        {
            if(alchemyStatus==null || !Session.IsAtAlchemy)return;
            var a=Session.Alchemy;
            if(a==null){alchemyStatus.text="选择丹方；将随身材料拖到备料格，灵石拖到供能位。";return;}
            var cfg=Session.Catalog.alchemy;
            string Title(AlchemyTarget t)=>t.kind==AlchemyEventKind.Ingredient?Session.Catalog.Find(t.itemId).title+(t.ground?"（研磨）":""):t.kind==AlchemyEventKind.Heat?"换"+AlchemySettings.HeatName(t.heat):"收丹";
            alchemyRecipeText.text=string.Join(" → ",a.Recipe.targets.Select(t=>$"{t.breaths:0.#}炉息 {Title(t)}"));
            var fuel=Session.In(ContainerId.AlchemyFuel).FirstOrDefault();
            var selected=Session.Find(selectedId);
            alchemyStatus.text=$"{AlchemySettings.PhaseName(a.Phase)} · {AlchemySettings.HeatName(a.Heat)} · {(a.Locked?$"研磨中 {a.GrindingRemaining:0.0}s":"可操作")} · 选中：{selected?.Definition.title??"无"}{(Session.IsAlchemyGround(selectedId)?"（已研磨）":"")}\n"+
                (a.Phase==AlchemyPhase.Finished?$"品相：{AlchemySettings.QualityName(a.Quality)} · 请手动带走产物":Session.Message);
            foreach(var button in alchemyActions)button.interactable=!a.Locked && a.Phase!=AlchemyPhase.Finished && a.Phase!=AlchemyPhase.Aborted;
            alchemyDebug.text=cfg.showDebug?$"开发调试（可滚动）\n炉钟 {a.Time:0.00}s\n灵气 {fuel?.SpiritUnits??0} 单位\n完美±{cfg.perfectWindow}s / 合格±{cfg.acceptableWindow}s\n分数{a.Score:0.00}\n目标：\n"+
                string.Join("\n",a.Recipe.targets.Select(t=>$"{t.breaths*cfg.breathSeconds:0.00}s {Title(t)}"))+"\n实际：\n"+
                string.Join("\n",a.Actions.Concat(a.Judgements.Select(j=>j.ToString())).Concat(a.StructuralErrors)):"";
            alchemyDebug.rectTransform.sizeDelta=new Vector2(278,Mathf.Max(510,alchemyDebug.preferredHeight+20));
        }
    }
}
