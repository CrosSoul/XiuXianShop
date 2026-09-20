using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    public sealed class AlchemyJudgement
    {
        public string Action;
        public double TargetTime, ActualTime;
        public double? EntryTime;
        public float Score;
        public string Result;
        public override string ToString() => (EntryTime.HasValue?$"{Action}：入炉{EntryTime:0.00}s / 目标在炉":$"{Action}：目标")+
            $"{TargetTime:0.00}s / 实际{ActualTime:0.00}s / 偏差{ActualTime-TargetTime:+0.00;-0.00;0.00}s · {Result}";
    }

    // One visit's furnace state; item ownership, grids and spirit remain in ShopSession.
    public sealed class AlchemyBatch
    {
        public AlchemyRecipe Recipe { get; internal set; }
        public AlchemyPhase Phase { get; internal set; }
        public AlchemyHeat Heat { get; internal set; }=AlchemyHeat.Medium;
        public double Time { get; internal set; }
        public int GrindingItemId { get; internal set; }
        public double GrindingRemaining { get; internal set; }
        public PillQuality Quality { get; internal set; }
        public float Score { get; internal set; }
        public readonly List<AlchemyJudgement> Judgements=new List<AlchemyJudgement>();
        public readonly List<string> StructuralErrors=new List<string>();
        public readonly List<string> Actions=new List<string>();
        internal readonly HashSet<int> completedTargets=new HashSet<int>();
        internal readonly HashSet<int> groundItems=new HashSet<int>();
        internal double energyUsed;
        internal int energyCharged, fuelId;
        public bool Locked => GrindingItemId!=0;
    }

    public sealed partial class ShopSession
    {
        public const string AlchemyLocationId="alchemy-room";
        public AlchemyBatch Alchemy { get; private set; }
        public bool IsAtAlchemy => IsTravelling && CurrentLocationId==AlchemyLocationId;
        public bool IsAlchemyArea(ContainerId area) => area==ContainerId.AlchemyFuel || area==ContainerId.AlchemyOutput;

        bool CanMoveAlchemy(GridItem item,ContainerId target,out string reason)
        {
            reason="";
            if((IsAlchemyArea(item.Container) || IsAlchemyArea(target)) && !IsAtAlchemy){reason="只能操作当前炼丹房设施。";return false;}
            if(!IsAtAlchemy)return true;
            if(Alchemy!=null && (Alchemy.Locked || Alchemy.Phase==AlchemyPhase.Running))
            {reason="炼制或研磨期间不能搬运物品，请操作已备好的材料。";return false;}
            if(target==ContainerId.AlchemyOutput){reason="收丹位仅供本炉产物使用。";return false;}
            if(target==ContainerId.AlchemyFuel && (item.Definition.spiritResource==null || In(target).Any(i=>i.Id!=item.Id)))
            {reason="供能位仅能安装一件实体灵石。";return false;}
            return true;
        }

        public bool SelectAlchemyRecipe(string recipeId)
        {
            if(!IsAtAlchemy)return Fail("请先进入炼丹房。");
            var cfg=catalog.alchemy;
            if(cfg.breathSeconds<=0 || cfg.grindSeconds<=0 || cfg.spiritEquivalentsPerSecond<=0 ||
                cfg.lowHeatMultiplier<=0 || cfg.mediumHeatMultiplier<=0 || cfg.highHeatMultiplier<=0 ||
                cfg.perfectWindow<0 || cfg.acceptableWindow<cfg.perfectWindow || cfg.debugTimeScale<=0)
                throw new ArgumentException("炼丹灰盒时长、窗口与耗能配置无效。");
            if(Alchemy!=null && (Alchemy.Locked || Alchemy.Phase!=AlchemyPhase.Preparing || Alchemy.completedTargets.Count>0))return Fail("本炉已操作，不能更换丹方；完成或中止后本次访问不能再开炉。");
            Alchemy=new AlchemyBatch {Recipe=catalog.alchemy.recipes.Single(r=>r.id==recipeId)};
            return Success("已选择丹方。先备料、安装灵石，并投入第一味材料。");
        }

        bool CanAlchemyAct(bool runningOnly=false)
        {
            if(!IsAtAlchemy || Alchemy==null)return Fail("请先选择丹方。");
            if(Alchemy.Phase==AlchemyPhase.Finished || Alchemy.Phase==AlchemyPhase.Aborted)return Fail("本次访问已用完一炉机会。");
            if(Alchemy.Locked)return Fail("正在研磨，完成前不能进行其他手动动作。");
            if(runningOnly && Alchemy.Phase!=AlchemyPhase.Running)return Fail("请先开炉。");
            return true;
        }

        void JudgeAlchemy(int index,string action)
        {
            var a=Alchemy;var cfg=catalog.alchemy;
            double target=a.Recipe.targets[index].breaths*cfg.breathSeconds;
            double error=Math.Abs(a.Time-target);
            bool perfect=error<=cfg.perfectWindow+1e-6, acceptable=error<=cfg.acceptableWindow+1e-6;
            a.Judgements.Add(new AlchemyJudgement {Action=action,TargetTime=target,ActualTime=a.Time,
                Score=perfect?cfg.perfectScore:acceptable?cfg.acceptableScore:cfg.severeScore,
                Result=perfect?"完美":acceptable?"合格（轻微偏差）":"严重偏差"});
            a.completedTargets.Add(index);
        }

        public bool AddAlchemyIngredient(int itemId)
        {
            if(!CanAlchemyAct())return false;
            var item=Find(itemId);
            if(item==null || item.Container!=ContainerId.Location || item.LocationId!=AlchemyLocationId || item.Definition.category!=ItemCategory.Material)
                return Fail("只能投入已摆到备料格中的材料，灵石不是原料。");
            var a=Alchemy;
            int index=Array.FindIndex(a.Recipe.targets,t=>t.kind==AlchemyEventKind.Ingredient && !a.completedTargets.Contains(Array.IndexOf(a.Recipe.targets,t)));
            if(index<0)a.StructuralErrors.Add("多投入材料："+item.Definition.title);
            else
            {
                var expected=a.Recipe.targets[index];
                if((expected.breaths==0)!=(a.Phase==AlchemyPhase.Preparing))a.StructuralErrors.Add("投料阶段错误："+item.Definition.title);
                if(expected.itemId!=item.Definition.id)a.StructuralErrors.Add("投料错误：需要"+catalog.Find(expected.itemId).title+"，投入"+item.Definition.title);
                if(expected.ground && !a.groundItems.Contains(itemId))a.StructuralErrors.Add("应研磨后投入："+item.Definition.title);
                // The recipe's recommended rhythm defines residence duration, not an insertion-time score.
                double collectBreaths=a.Recipe.targets.Single(t=>t.kind==AlchemyEventKind.Collect).breaths;
                a.Judgements.Add(new AlchemyJudgement {Action=item.Definition.title,EntryTime=a.Time,
                    TargetTime=(collectBreaths-expected.breaths)*catalog.alchemy.breathSeconds,Result="待收丹"});
                a.completedTargets.Add(index);
            }
            items.Remove(item);a.groundItems.Remove(itemId);
            return Success("材料已入炉；中止不会返还已投入材料。");
        }

        public double AlchemyMinimumEnergy()
        {
            var cfg=catalog.alchemy;var recipe=Alchemy.Recipe;
            double time=0,total=0;var heat=AlchemyHeat.Medium;
            foreach(var t in recipe.targets.Where(t=>t.kind==AlchemyEventKind.Heat || t.kind==AlchemyEventKind.Collect).OrderBy(t=>t.breaths))
            {
                double next=t.breaths*cfg.breathSeconds;
                total+=(next-time)*cfg.HeatMultiplier(heat)*cfg.spiritEquivalentsPerSecond;time=next;
                if(t.kind==AlchemyEventKind.Heat)heat=t.heat;
            }
            return total;
        }

        public bool StartAlchemy()
        {
            if(!CanAlchemyAct())return false;
            if(Alchemy.Phase!=AlchemyPhase.Preparing)return Fail("本炉已经运行。");
            var fuel=In(ContainerId.AlchemyFuel).SingleOrDefault();
            if(fuel==null || fuel.SpiritUnits<Math.Ceiling(AlchemyMinimumEnergy()*fuel.Definition.spiritResource.UnitsPerEquivalent-1e-6))
                return Fail("安装灵石的剩余灵气不足标准炉程需要。");
            var output=new GridItem {Definition=catalog.Find(Alchemy.Recipe.productId)};
            if(In(ContainerId.AlchemyOutput).Any() || !Fits(output,ContainerId.AlchemyOutput,0,0,0,false))return Fail("收丹位必须为空并能容纳本炉产物。");
            Alchemy.Phase=AlchemyPhase.Running;Alchemy.fuelId=fuel.Id;
            return Success("已开炉，中火；炉钟与灵气持续运行。");
        }

        public bool GrindAlchemyIngredient(int itemId)
        {
            if(!CanAlchemyAct())return false;
            if(!Alchemy.Recipe.allowGrinding)return Fail("本丹方不需要研磨。");
            var item=Find(itemId);
            if(item==null || item.Container!=ContainerId.Location || item.LocationId!=AlchemyLocationId || item.Definition.category!=ItemCategory.Material)return Fail("请选择备料区材料。");
            if(Alchemy.groundItems.Contains(itemId))return Fail("这件材料已经研磨。");
            Alchemy.GrindingItemId=itemId;Alchemy.GrindingRemaining=catalog.alchemy.grindSeconds;
            Alchemy.Actions.Add($"{Alchemy.Time:0.00}s 开始研磨 {item.Definition.title}");
            return Success("开始研磨，期间不能执行其他手动动作。");
        }

        public bool SetAlchemyHeat(AlchemyHeat heat)
        {
            if(!CanAlchemyAct(true))return false;
            var a=Alchemy;
            if(!a.Recipe.allowHeatChange)return Fail("本丹方固定中火。");
            if(a.Heat==heat)return Success("火候未变。");
            int index=Array.FindIndex(a.Recipe.targets,t=>t.kind==AlchemyEventKind.Heat && t.heat==heat && !a.completedTargets.Contains(Array.IndexOf(a.Recipe.targets,t)));
            if(index>=0)JudgeAlchemy(index,"切换"+heat);
            else a.Judgements.Add(new AlchemyJudgement {Action="额外换火"+heat,TargetTime=a.Time,ActualTime=a.Time,Score=catalog.alchemy.severeScore,Result="严重偏差"});
            a.Heat=heat;return Success("已切换火候。");
        }

        public void TickAlchemy(double seconds)
        {
            if(!IsAtAlchemy || Alchemy==null || seconds<=0)return;
            var a=Alchemy;
            if(a.Phase==AlchemyPhase.Finished || a.Phase==AlchemyPhase.Aborted)return;
            bool exhausted=false;
            if(a.Phase==AlchemyPhase.Running)
            {
                var fuel=Find(a.fuelId);
                if(fuel==null){a.Phase=AlchemyPhase.Aborted;a.GrindingItemId=0;Success("灵石已耗尽，本炉中止。");return;}
                double rate=catalog.alchemy.spiritEquivalentsPerSecond*catalog.alchemy.HeatMultiplier(a.Heat)*fuel.Definition.spiritResource.UnitsPerEquivalent;
                double available=fuel.SpiritUnits+a.energyCharged-a.energyUsed;
                if(rate*seconds>available+1e-7){seconds=available/rate;exhausted=true;}
                a.Time+=seconds;a.energyUsed+=rate*seconds;
                int charge=(int)Math.Ceiling(a.energyUsed-1e-7)-a.energyCharged;
                if(charge>0){ConsumeSpirit(fuel.Id,charge);a.energyCharged+=charge;}
            }
            if(a.Locked)
            {
                a.GrindingRemaining=Math.Max(0,a.GrindingRemaining-seconds);
                if(a.GrindingRemaining<=1e-6){a.groundItems.Add(a.GrindingItemId);a.Actions.Add($"{a.Time:0.00}s 研磨完成");a.GrindingItemId=0;}
            }
            if(exhausted){a.Phase=AlchemyPhase.Aborted;a.GrindingItemId=0;Success("供能耗尽，本炉中止；未投入备料保留，已用资源不返还。");}
        }

        public bool CollectAlchemy()
        {
            if(!CanAlchemyAct(true))return false;
            var a=Alchemy;var cfg=catalog.alchemy;
            foreach(var j in a.Judgements.Where(j=>j.EntryTime.HasValue))
            {
                j.ActualTime=a.Time-j.EntryTime.Value;
                double error=Math.Abs(j.ActualTime-j.TargetTime);
                bool perfect=error<=cfg.perfectWindow+1e-6, acceptable=error<=cfg.acceptableWindow+1e-6;
                j.Score=perfect?cfg.perfectScore:acceptable?cfg.acceptableScore:cfg.severeScore;
                j.Result=perfect?"完美":acceptable?"合格（轻微偏差）":"严重偏差";
            }
            for(int i=0;i<a.Recipe.targets.Length;i++)
            {
                var t=a.Recipe.targets[i];
                if(t.kind==AlchemyEventKind.Collect)continue;
                if(a.completedTargets.Contains(i))continue;
                if(t.kind==AlchemyEventKind.Ingredient)a.StructuralErrors.Add("漏投："+catalog.Find(t.itemId).title);
                a.Judgements.Add(new AlchemyJudgement {Action="遗漏"+(t.kind==AlchemyEventKind.Heat?"换火":t.itemId),TargetTime=t.breaths*cfg.breathSeconds,ActualTime=a.Time,Score=cfg.severeScore,Result="严重偏差"});
            }
            a.Score=a.Judgements.Average(j=>j.Score);
            a.Quality=a.Score>=cfg.superiorThreshold?PillQuality.Superior:a.Score>=cfg.goodThreshold?PillQuality.Good:a.Score>=cfg.ordinaryThreshold?PillQuality.Ordinary:PillQuality.Ruined;
            if(a.StructuralErrors.Count>=2)a.Quality=PillQuality.Ruined;
            else if(a.StructuralErrors.Count==1 && a.Quality!=PillQuality.Ruined)a.Quality=PillQuality.Ordinary;
            if(a.Quality!=PillQuality.Ruined)
            {
                var product=NewItem(catalog.Find(a.Recipe.productId),ItemOwner.Player);
                product.Quality=a.Quality;product.QualityValueMultiplier=(decimal)cfg.ValueMultiplier(a.Quality);
                Place(product,ContainerId.AlchemyOutput,0,0,0,false);items.Add(product);Crafted++;
            }
            a.Phase=AlchemyPhase.Finished;
            return Success("本炉结果："+AlchemySettings.QualityName(a.Quality)+"。请带走产物、剩余备料和灵石。");
        }

        public bool AbortAlchemy()
        {
            if(!CanAlchemyAct())return false;
            Alchemy.Phase=AlchemyPhase.Aborted;
            return Success("本炉中止，未投入材料保留，已投入材料与已消耗灵气不返还。");
        }
        public bool IsAlchemyGround(int id)=>Alchemy!=null && Alchemy.groundItems.Contains(id);
    }
}
