using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XiuXianShop.Editor
{
    public sealed class AlchemyContentAdapter : ContentDomainAdapter
    {
        readonly string file;
        AlchemyContentAdapter(string file){this.file=file;}
        public override string FileName=>file+".csv";
        public static ContentDomainAdapter[] Create()=>new[]{new AlchemyContentAdapter("recipes"),new AlchemyContentAdapter("recipe-lines"),new AlchemyContentAdapter("alchemy-global"),new AlchemyContentAdapter("alchemy-materials"),new AlchemyContentAdapter("alchemy-steps")};
        static float Seconds(ContentRow r,string field,bool required=true)
        {
            string text=required?r.Required(field):r.Get(field);if(text=="" && !required)return 0;
            if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value) || float.IsNaN(value) || float.IsInfinity(value) || value<0)r.Error(field,"需要非负有限数字。");return value;
        }
        public override void Stage(ContentSyncPlan p,string csv)
        {
            var c=p.staged;
            switch(file)
            {
                case "recipes":c.recipes=Merge(p,csv,"配方ID","配方名称|配方ID|配方说明|数据状态",c.recipes,(r,old)=>new RecipeMasterRow{title=r.Required("配方名称"),description=r.Get("配方说明")});break;
                case "recipe-lines":c.recipeLines=Merge(p,csv,"条目ID","条目ID|所属配方ID|用途|物品ID|数量|数据状态",c.recipeLines,(r,old)=>new RecipeLineRow{recipeId=r.Required("所属配方ID"),itemId=r.Required("物品ID"),purpose=r.OneOf("用途","原料","产物"),quantity=r.Number("数量",1,true).value});break;
                case "alchemy-global":c.alchemyGlobals=Merge(p,csv,"配置键","配置键|当前值|数据状态",c.alchemyGlobals,(r,old)=>new AlchemyGlobalRow{value=Seconds(r,"当前值")});break;
                case "alchemy-materials":c.alchemyMaterials=Merge(p,csv,"条目ID","条目ID|配方ID|物品ID|数量|投料顺序|开炉前预置|要求状态|目标在炉时长|数据状态",c.alchemyMaterials,(r,old)=>new AlchemyMaterialRow{
                    recipeId=r.Required("配方ID"),itemId=r.Required("物品ID"),quantity=r.Number("数量",1,true).value,order=r.Number("投料顺序",1,true).value,preloaded=r.OneOf("开炉前预置","是","否")=="是",ground=r.OneOf("要求状态","完整","研磨")=="研磨",residenceSeconds=Seconds(r,"目标在炉时长")});break;
                case "alchemy-steps":c.alchemySteps=Merge(p,csv,"步骤ID","步骤ID|配方ID|顺序|动作类型|物品ID|要求状态|火候|提示文本|数据状态",c.alchemySteps,(r,old)=>{
                    string action=r.OneOf("动作类型","预置","开炉","研磨","投料","调火","收丹");bool material=action=="预置" || action=="研磨" || action=="投料";
                    string heat=action=="开炉" || action=="调火"?r.OneOf("火候","低","中","高"):"中";
                    return new AlchemyStepRow{recipeId=r.Required("配方ID"),order=r.Number("顺序",1,true).value,action=action,itemId=material?r.Required("物品ID"):r.Get("物品ID"),state=material?r.OneOf("要求状态","完整","研磨"):r.Get("要求状态"),heat=heat=="低"?AlchemyHeat.Low:heat=="高"?AlchemyHeat.High:AlchemyHeat.Medium,hint=r.Required("提示文本"),furnaceSeconds=action=="调火"?Seconds(r,"目标炉钟秒"):0};});break;
            }
        }
        public override void Validate(ContentSyncPlan p)
        {
            if(file!="alchemy-steps")return;
            var c=p.staged;var cfg=p.stagedCatalog.alchemy;
            var global=c.alchemyGlobals.Where(r=>r.enabled).ToDictionary(r=>r.id);
            float Value(string id)
            {if(global.TryGetValue(id,out var row))return row.value;p.Error("alchemy-global.csv",id,"当前值","缺少有效配置。");return 0;}
            cfg.grindSeconds=Value("grindDuration");cfg.perfectWindow=Value("perfectTolerance");cfg.acceptableWindow=Value("acceptableTolerance");
            cfg.lowHeatMultiplier=Value("heatLowEnergyMultiplier");cfg.mediumHeatMultiplier=Value("heatMediumEnergyMultiplier");cfg.highHeatMultiplier=Value("heatHighEnergyMultiplier");
            cfg.ordinaryValue=Value("qualityNormalValueMultiplier");cfg.goodValue=Value("qualityGoodValueMultiplier");cfg.superiorValue=Value("qualityTopValueMultiplier");
            var keys=new[]{"grindDuration","perfectTolerance","acceptableTolerance","heatLowEnergyMultiplier","heatMediumEnergyMultiplier","heatHighEnergyMultiplier","qualityNormalValueMultiplier","qualityGoodValueMultiplier","qualityTopValueMultiplier"};
            foreach(string key in global.Keys.Except(keys))p.Error("alchemy-global.csv",key,"配置键","当前炼丹能力没有此配置。");
            foreach(string key in keys.Where(k=>!k.EndsWith("Tolerance")))if(global.TryGetValue(key,out var row) && row.value<=0)p.Error("alchemy-global.csv",key,"当前值","须大于零。");
            if(cfg.acceptableWindow<cfg.perfectWindow)p.Error("alchemy-global.csv","acceptableTolerance","当前值","合格窗口不得小于完美窗口。");
            var ids=c.recipes.Where(r=>r.enabled).Select(r=>r.id).ToArray();var items=p.stagedCatalog.items;
            void Ref(string table,string id,string recipeId,string itemId)
            {
                if(!ids.Contains(recipeId))p.Error(table,id,"配方ID","未知或停用配方："+recipeId);
                if(!string.IsNullOrEmpty(itemId) && !items.Any(i=>i.id==itemId))p.Error(table,id,"物品ID","未知物品："+itemId);
            }
            foreach(var line in c.recipeLines.Where(r=>r.enabled))Ref("recipe-lines.csv",line.id,line.recipeId,line.itemId);
            foreach(var material in c.alchemyMaterials.Where(r=>r.enabled))Ref("alchemy-materials.csv",material.id,material.recipeId,material.itemId);
            foreach(var step in c.alchemySteps.Where(r=>r.enabled))Ref(FileName,step.id,step.recipeId,step.itemId);
            var recipes=new List<AlchemyRecipe>();
            foreach(var master in c.recipes.Where(r=>r.enabled))
            {
                var lines=c.recipeLines.Where(r=>r.enabled && r.recipeId==master.id).ToArray();
                var input=lines.Where(r=>r.purpose=="原料").ToArray();var output=lines.Where(r=>r.purpose=="产物").ToArray();
                var materials=c.alchemyMaterials.Where(r=>r.enabled && r.recipeId==master.id).OrderBy(r=>r.order).ToArray();
                var steps=c.alchemySteps.Where(r=>r.enabled && r.recipeId==master.id).OrderBy(r=>r.order).ToArray();
                void Error(string field,string reason)=>p.Error("recipes.csv",master.id,field,reason);
                if(lines.GroupBy(r=>(r.purpose,r.itemId)).Any(g=>g.Count()>1))Error("配方材料与产出","同一用途物品重复。");
                if(output.Length!=1 || output.Any(o=>o.quantity!=1)){Error("产物","当前内核要求一件合法产物。");continue;}
                var product=items.SingleOrDefault(i=>i.id==output[0].itemId);
                if(product==null || product.category!=ItemCategory.Medicine){Error("产物","产物须为已有丹药。");continue;}
                if(materials.Length==0 || materials.Select(m=>m.order).Distinct().Count()!=materials.Length){Error("投料顺序","材料不能为空且投料顺序不能重复。");continue;}
                if(materials.Count(m=>m.preloaded)!=1 || !materials[0].preloaded)Error("开炉前预置","当前内核要求仅首味材料预置。");
                if(materials.Any(m=>m.residenceSeconds<=0 || items.Any(i=>i.id==m.itemId && i.category!=ItemCategory.Material)))Error("材料目标","在炉时长须大于零，且物品须为材料，不能是供能灵石。");
                if(!input.OrderBy(i=>i.itemId).Select(i=>$"{i.itemId}:{i.quantity}").SequenceEqual(materials.GroupBy(m=>m.itemId).OrderBy(g=>g.Key).Select(g=>$"{g.Key}:{g.Sum(m=>m.quantity)}")))Error("材料目标","04.14 材料/数量必须与04.3原料一致。");
                if(steps.Select(s=>s.order).Distinct().Count()!=steps.Length)Error("炉程顺序","同配方步骤顺序不能重复。");
                if(steps.Count(s=>s.action=="开炉")!=1 || steps.Count(s=>s.action=="收丹")!=1 || steps.LastOrDefault()?.action!="收丹"){Error("炉程","须有一次开炉并以一次收丹结束。");continue;}
                var start=steps.Single(s=>s.action=="开炉");
                var additions=steps.Where(s=>s.action=="预置" || s.action=="投料").ToArray();
                if(additions.Length!=materials.Length){Error("炉程投料","炉程预置/投料行须逐项对应材料目标。");continue;}
                for(int i=0;i<materials.Length;i++)
                {
                    var m=materials[i];var s=additions[i];
                    if(s.itemId!=m.itemId || (s.state=="研磨")!=m.ground || (s.action=="预置")!=m.preloaded || (s.order<start.order)!=m.preloaded)Error("炉程投料","物品、状态、预置和顺序须与材料目标一致。");
                }
                foreach(var grind in steps.Where(s=>s.action=="研磨"))
                    if(grind.state!="研磨" || !materials.Any(m=>m.itemId==grind.itemId && m.ground) || !additions.Any(a=>a.itemId==grind.itemId && a.order>grind.order))Error("研磨步骤","研磨须先于对应研磨材料投料。");
                float collect=materials[0].residenceSeconds;
                var targets=new List<AlchemyTarget>();int materialIndex=0;
                foreach(var step in steps)
                {
                    if(step.action=="预置" || step.action=="投料")
                    {
                        var m=materials[materialIndex++];
                        for(int n=0;n<m.quantity;n++)targets.Add(new AlchemyTarget{kind=AlchemyEventKind.Ingredient,itemId=m.itemId,ground=m.ground,preloaded=m.preloaded,residenceSeconds=m.residenceSeconds});
                    }
                    else if(step.action=="调火")
                    {
                        if(step.order<start.order || step.furnaceSeconds>collect)Error("目标炉钟秒","火候计时须在标准炉程内；材料品质不使用该列。");
                        targets.Add(new AlchemyTarget{kind=AlchemyEventKind.Heat,heat=step.heat,breaths=step.furnaceSeconds/cfg.breathSeconds});
                    }
                    else if(step.action=="收丹")targets.Add(new AlchemyTarget{kind=AlchemyEventKind.Collect,breaths=collect/cfg.breathSeconds});
                }
                recipes.Add(new AlchemyRecipe{id=master.id,title=master.title,productId=product.id,allowGrinding=materials.Any(m=>m.ground),allowHeatChange=steps.Any(s=>s.action=="调火"),initialHeat=start.heat,targets=targets.ToArray(),steps=steps});
            }
            cfg.recipes=recipes.ToArray();
        }
    }
}
