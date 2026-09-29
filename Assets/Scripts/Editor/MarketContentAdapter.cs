using System;
using System.Globalization;
using System.Linq;

namespace XiuXianShop.Editor
{
    public sealed class MarketContentAdapter : ContentDomainAdapter
    {
        public override string FileName=>"market-events.csv";
        static float Number(ContentRow r,string field)
        {
            string text=r.Required(field);bool percentage=text.EndsWith("%");
            if(percentage)text=text.Substring(0,text.Length-1);
            if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value) || float.IsNaN(value) || float.IsInfinity(value))r.Error(field,"需要有限数字。");
            return percentage?value/100:value;
        }
        public override void Stage(ContentSyncPlan p,string csv)
        {
            p.staged.marketEvents=Merge(p,csv,"行情ID","行情名称|行情ID|说明|目标类别|交易方向|价格修正百分比|生成权重|最短持续回合|最长持续回合|冷却回合|数据状态|修改说明",p.staged.marketEvents,(r,old)=>{
                if(!Enum.TryParse<ItemCategory>(r.Required("目标类别"),out var category) || !Enum.IsDefined(typeof(ItemCategory),category) || category==ItemCategory.Unclassified || category==ItemCategory.BusinessSign)
                    r.Error("目标类别","需要现有商品类别枚举名，例如 Medicine / Material / Equipment。");
                var result=new MarketContent{title=r.Required("行情名称"),description=r.Required("说明"),category=category,direction=r.OneOf("交易方向","PlayerSells","PlayerBuys","Both"),
                    percent=Number(r,"价格修正百分比"),weight=Number(r,"生成权重"),minimumDuration=r.Number("最短持续回合",1,true).value,maximumDuration=r.Number("最长持续回合",1,true).value,cooldownTurns=r.Number("冷却回合",0,true).value,note=r.Get("修改说明")};
                if(result.weight<0)r.Error("生成权重","不得为负数。");
                if(Math.Abs(result.percent)>100)r.Error("价格修正百分比","超过现有报价/存档能力范围（绝对值100，即10000%）。");
                if(result.maximumDuration<result.minimumDuration || result.maximumDuration==int.MaxValue)r.Error("最长持续回合","必须不小于最短持续回合且不溢出。");
                if(result.cooldownTurns==int.MaxValue)r.Error("冷却回合","数值不能溢出。");
                return result;
            });
        }
        public override void Validate(ContentSyncPlan p)
        {
            p.stagedCatalog.marketEvents=p.staged.marketEvents.Select(e=>new MarketEventDefinition{id=e.id,title=e.title,description=e.description,enabled=e.enabled,weight=e.weight,
                minimumDuration=e.minimumDuration,maximumDuration=e.maximumDuration,cooldownTurns=e.cooldownTurns,
                effect=new PriceTag{id=e.id,title=e.title,category=e.category,percent=e.percent,playerSells=e.direction!="PlayerBuys",playerBuys=e.direction!="PlayerSells"}}).ToArray();
        }
    }
}
