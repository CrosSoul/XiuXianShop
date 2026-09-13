using System;
using System.Linq;

namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        public TeaVisitResult LatestTeaVisit { get; private set; }
        public TeaVisitResult ActiveTeaEffect { get; private set; }
        bool HasTeaEffect(TeaEffect effect) => ActiveTeaEffect!=null && ActiveTeaEffect.ApplyTurn==Turn && ActiveTeaEffect.effect==effect;

        void ValidateSavedTeaState()
        {
            foreach(var result in new[]{LatestTeaVisit,ActiveTeaEffect}.Where(r=>r!=null))
            {
                if(result.visitTurn<1 || result.visitTurn>Turn || !Enum.IsDefined(typeof(TeaEffect),result.effect) ||
                    (result.effect==TeaEffect.MarketSecret && !Calendar.DisclosedEvents.Any(e=>e.id==result.secretEventId)) ||
                    ((result.effect==TeaEffect.BuyerTrend || result.effect==TeaEffect.SupplierTrend) &&
                        !catalog.items.Any(d=>IsSaleItem(d) && d.category==result.category)))
                    throw new ArgumentException("存档茶肆消息无效，当前会话未改变。");
            }
            if(ActiveTeaEffect!=null && ActiveTeaEffect.ApplyTurn!=Turn)throw new ArgumentException("存档茶肆生效回合不匹配。");
        }

        void ValidateTeaSettings()
        {
            var t=catalog.teaHouse;
            if(t.effects.Length!=6 || t.effects.Select(e=>e.effect).Distinct().Count()!=6 ||
                t.effects.Any(e=>!Enum.IsDefined(typeof(TeaEffect),e.effect) || e.weight<0 || float.IsNaN(e.weight) || float.IsInfinity(e.weight)) ||
                !t.effects.Any(e=>e.effect!=TeaEffect.MarketSecret && e.weight>0) ||
                t.buyerCategoryMultiplier<1 || float.IsNaN(t.buyerCategoryMultiplier) || float.IsInfinity(t.buyerCategoryMultiplier) ||
                t.supplierCategoryMultiplier<1 || float.IsNaN(t.supplierCategoryMultiplier) || float.IsInfinity(t.supplierCategoryMultiplier) ||
                t.travellerChanceBonus<0 || t.travellerChanceBonus>1 || float.IsNaN(t.travellerChanceBonus) ||
                t.travellerChanceCap<0 || t.travellerChanceCap>1 || float.IsNaN(t.travellerChanceCap) ||
                t.extraCustomers<0 || t.wealthyBudgetTierIncrease<0 || t.secretMinimumTurnsAhead<1 || t.secretMaximumTurnsAhead<t.secretMinimumTurnsAhead)
                throw new ArgumentException("茶肆配置无效；六种效果权重须合法，且至少一个非秘闻效果可抽取。");
        }

        TeaEffect DrawTeaEffect(bool excludeSecret=false)
        {
            var options=catalog.teaHouse.effects.Where(e=>e.weight>0 && (!excludeSecret || e.effect!=TeaEffect.MarketSecret)).ToArray();
            double roll=DrawCustomerChance()*options.Sum(e=>(double)e.weight);
            foreach(var option in options){roll-=option.weight;if(roll<0)return option.effect;}
            return options[options.Length-1].effect;
        }

        void ReceiveTeaNews()
        {
            if(LatestTeaVisit?.visitTurn==Turn)return;
            var result=new TeaVisitResult{visitTurn=Turn,effect=DrawTeaEffect()};
            if(result.effect==TeaEffect.MarketSecret)
            {
                var t=catalog.teaHouse;
                var secret=Calendar.RevealSecret(Turn,t.secretMinimumTurnsAhead,t.secretMaximumTurnsAhead,new Random(DrawCustomerNumber(0,int.MaxValue)));
                if(secret==null)result.effect=DrawTeaEffect(true);
                else result.secretEventId=secret.id;
            }
            if(result.effect==TeaEffect.BuyerTrend || result.effect==TeaEffect.SupplierTrend)
            {
                var categories=catalog.items.Where(d=>IsSaleItem(d) && (result.effect!=TeaEffect.SupplierTrend || d.supplierAvailable))
                    .Select(d=>d.category).Distinct().OrderBy(c=>(int)c).ToArray();
                if(categories.Length==0)throw new InvalidOperationException("茶肆风向没有有效商品类别。");
                result.category=categories[DrawCustomerNumber(0,categories.Length)];
            }
            LatestTeaVisit=result;
        }

        int DrawCategoryWeightedIndex(ItemCategory[] categories,TeaEffect effect)
        {
            if(!HasTeaEffect(effect))return DrawCustomerNumber(0,categories.Length);
            double multiplier=effect==TeaEffect.BuyerTrend?catalog.teaHouse.buyerCategoryMultiplier:catalog.teaHouse.supplierCategoryMultiplier;
            double roll=DrawCustomerChance()*categories.Sum(c=>c==ActiveTeaEffect.category?multiplier:1);
            for(int i=0;i<categories.Length;i++){roll-=categories[i]==ActiveTeaEffect.category?multiplier:1;if(roll<0)return i;}
            return categories.Length-1;
        }

        int WealthyBudget(DisplayAttraction attraction)
        {
            var tiers=catalog.buyerBudgetTiers.OrderBy(t=>t.minimumDisplayValue).ToArray();
            int index=Array.FindIndex(tiers,t=>t.minimumDisplayValue==attraction.BudgetTierMinimum);
            var tier=tiers[Math.Min(tiers.Length-1,index+catalog.teaHouse.wealthyBudgetTierIncrease)];
            float variation=UnityEngine.Mathf.Clamp(catalog.buyerBudgetVariation,0,.5f);
            int minimum=Math.Max(1,UnityEngine.Mathf.CeilToInt(tier.baseBudget*(1-variation)));
            int maximum=Math.Max(1,UnityEngine.Mathf.FloorToInt(tier.baseBudget*(1+variation)));
            return DrawCustomerNumber(minimum,maximum+1);
        }

        public string DescribeTeaNews(TeaVisitResult result)
        {
            if(result==null)return "尚未获得茶肆消息。";
            var t=catalog.teaHouse;string timing=$"第 {result.ApplyTurn} 回合营业：";
            switch(result.effect)
            {
                case TeaEffect.BuyerTrend:return timing+$"求购风向 · {ShopCatalog.CategoryName(result.category)}求购倾向 ×{t.buyerCategoryMultiplier:0.##}，仍受展示柜筛选影响。";
                case TeaEffect.SupplierTrend:return timing+$"供货风向 · {ShopCatalog.CategoryName(result.category)}供货倾向 ×{t.supplierCategoryMultiplier:0.##}，仍受展示柜筛选影响。";
                case TeaEffect.Travellers:return timing+$"商旅到访 · 携货求购概率 +{t.travellerChanceBonus*100:0.##} 个百分点，上限 {t.travellerChanceCap:P0}。";
                case TeaEffect.Promotion:return timing+$"宣传店铺 · 顾客 +{t.extraCustomers}。";
                case TeaEffect.WealthyVisitor:return timing+$"阔客风声 · 随机一名顾客预算提高 {t.wealthyBudgetTierIncrease} 档，最高不超过顶档。";
                default:
                    var secret=Calendar.DisclosedEvents.Single(e=>e.id==result.secretEventId);
                    return $"市场秘闻 · {secret.title} · 第 {secret.startTurn}–{secret.endTurn} 回合，共 {secret.Duration} 回合。已在日历提前公开；到期才影响价格。";
            }
        }

        public string TeaNewsText => "最近收到：\n"+DescribeTeaNews(LatestTeaVisit)+
            "\n\n本回合经营：\n"+(ActiveTeaEffect?.ApplyTurn==Turn && ActiveTeaEffect.effect!=TeaEffect.MarketSecret?DescribeTeaNews(ActiveTeaEffect):"本月暂无茶肆加成。")+
            "\n\n已获知行情（未来或生效中）：\n"+string.Join("\n",Calendar.DisclosedEvents.Where(e=>e.endTurn>=Turn)
                .Select(e=>$"{e.title} · 第 {e.startTurn}–{e.endTurn} 回合（{e.Duration} 回合）· {e.StatusOn(Turn)}"));
    }
}
