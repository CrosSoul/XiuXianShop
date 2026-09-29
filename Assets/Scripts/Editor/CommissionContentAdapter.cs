using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XiuXianShop.Editor
{
    // Four authoring tables compile into the existing CommissionSettings; no runtime database lookup.
    public sealed class CommissionContentAdapter : ContentDomainAdapter
    {
        readonly string table;
        CommissionContentAdapter(string table){this.table=table;}
        public override string FileName=>"commission-"+table+".csv";
        public static ContentDomainAdapter[] Create()=>new[]{new CommissionContentAdapter("global"),new CommissionContentAdapter("pools"),new CommissionContentAdapter("templates"),new CommissionContentAdapter("members")};
        static float Decimal(ContentRow r,string field,float min,float max=float.MaxValue)
        {
            string text=r.Required(field);bool percent=text.EndsWith("%");
            if(percent)text=text.Substring(0,text.Length-1);
            if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value) || float.IsNaN(value) || float.IsInfinity(value))r.Error(field,"需要有限数字。");
            if(percent)value/=100;
            if(value<min || value>max)r.Error(field,$"范围须为 {min}–{max}。");
            return value;
        }
        public override void Stage(ContentSyncPlan p,string csv)
        {
            var c=p.staged;
            switch(table)
            {
                case "global":c.commissionGlobals=Merge(p,csv,"配置键","配置键|当前值|数据状态",c.commissionGlobals,(r,old)=>new CommissionConfigRow{value=r.Required("当前值")});break;
                case "templates":c.commissionTemplates=Merge(p,csv,"稳定ID","稳定ID|委托名|候选权重|场景文案|奖励池ID|选择前显示|数据状态",c.commissionTemplates,(r,old)=>new CommissionTemplateRow{
                    title=r.Required("委托名"),weight=Decimal(r,"候选权重",0),description=r.Required("场景文案"),rewardPoolId=r.Required("奖励池ID"),rewardHint=r.Required("选择前显示")});break;
                case "pools":c.commissionPools=Merge(p,csv,"奖励池ID","奖励池ID|类型|第一版内容|选择前显示|货币最小值|货币最大值|实物抽取组|数据状态",c.commissionPools,(r,old)=>new CommissionPoolRow{
                    title=r.Required("类型"),description=r.Required("第一版内容"),hint=r.Required("选择前显示"),minimumMoney=r.Number("货币最小值",0,true).value,maximumMoney=r.Number("货币最大值",0,true).value,groups=r.Get("实物抽取组")});break;
                case "members":c.commissionMembers=Merge(p,csv,"条目ID","条目ID|奖励池ID|抽取组ID|物品ID|权重|最小数量|最大数量|数据状态|备注",c.commissionMembers,(r,old)=>new CommissionMemberRow{
                    poolId=r.Required("奖励池ID"),groupId=r.Required("抽取组ID"),itemId=r.Required("物品ID"),weight=Decimal(r,"权重",0),minimumCount=r.Number("最小数量",1,true).value,maximumCount=r.Number("最大数量",1,true).value,note=r.Get("备注")});break;
            }
        }
        public override void Validate(ContentSyncPlan p)
        {
            // All four tables have been staged before the final cross-table validation.
            if(table!="members")return;
            var c=p.staged;var settings=p.stagedCatalog.commissions;
            var globals=c.commissionGlobals.Where(x=>x.enabled).ToDictionary(x=>x.id);
            string Value(string id)
            {
                if(globals.TryGetValue(id,out var row))return row.value;
                p.Error("commission-global.csv",id,"当前值","缺少有效配置。");return "";
            }
            int Integer(string id)
            {
                if(!int.TryParse(Value(id),out int value) || value<1 || value==int.MaxValue)p.Error("commission-global.csv",id,"当前值","需要正整数且不能溢出。");return value;
            }
            settings.candidateCount=Integer("candidateCount");settings.completionLimitPerTurn=Integer("completionLimitPerTurn");settings.extraGiftCount=Integer("extraGiftCount");
            string chance=Value("extraGiftChance");bool pct=chance.EndsWith("%");
            if(pct)chance=chance.Substring(0,chance.Length-1);
            if(!float.TryParse(chance,NumberStyles.Float,CultureInfo.InvariantCulture,out float probability) || float.IsNaN(probability) || float.IsInfinity(probability))p.Error("commission-global.csv","extraGiftChance","当前值","需要概率数字。");
            if(pct)probability/=100;
            if(probability<0 || probability>1)p.Error("commission-global.csv","extraGiftChance","当前值","范围须为 0–1 或百分数。");
            settings.extraGiftChance=probability;
            var fixedValues=new Dictionary<string,string>{{"refreshCadence","每回合"},{"templateDrawMode","按权重无放回抽取"},{"rewardDisclosure","方向与大致档位公开"},{"longTermProgression","关闭"}};
            foreach(var pair in fixedValues)if(Value(pair.Key)!=pair.Value)p.Error("commission-global.csv",pair.Key,"当前值","当前能力只支持："+pair.Value);
            var allowed=fixedValues.Keys.Concat(new[]{"candidateCount","completionLimitPerTurn","extraGiftCount","extraGiftChance","extraGiftPoolId"});
            foreach(string key in globals.Keys.Except(allowed))p.Error("commission-global.csv",key,"配置键","未知配置键。");
            if(globals.TryGetValue("extraGiftPoolId",out var giftId))settings.extraGiftPoolId=giftId.value;
            var pools=new List<CommissionRewardPool>();var groupKeys=new HashSet<string>();
            foreach(var row in c.commissionPools)
            {
                var pool=new CommissionRewardPool{id=row.id,enabled=row.enabled,minimumMoney=row.minimumMoney,maximumMoney=row.maximumMoney};pools.Add(pool);
                if(!row.enabled)continue;
                if(row.maximumMoney<row.minimumMoney || row.maximumMoney==int.MaxValue)p.Error("commission-pools.csv",row.id,"货币最大值","范围无效。");
                var groups=new List<CommissionItemReward>();
                foreach(string part in (row.groups??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] fields=part.Split(':');
                    if(fields.Length!=3 || string.IsNullOrWhiteSpace(fields[0]) || !int.TryParse(fields[1],out int min) || !int.TryParse(fields[2],out int max) || min<1 || max<min || max==int.MaxValue)
                    {p.Error("commission-pools.csv",row.id,"实物抽取组","格式为 组ID:最少抽取次数:最多抽取次数，组间用分号。");continue;}
                    if(!groupKeys.Add(row.id+"/"+fields[0]))p.Error("commission-pools.csv",row.id,"实物抽取组","组ID重复。");
                    var members=c.commissionMembers.Where(m=>m.enabled && m.poolId==row.id && m.groupId==fields[0] && m.weight>0).ToArray();
                    if(members.Length==0)p.Error("commission-pools.csv",row.id,"实物抽取组","缺少正权重有效成员："+fields[0]);
                    groups.Add(new CommissionItemReward{minimumCount=min,maximumCount=max,members=members});
                }
                pool.items=groups.ToArray();
                if(pool.maximumMoney==0 && groups.Count==0)p.Error("commission-pools.csv",row.id,"实物抽取组","奖励池为空。");
            }
            foreach(var member in c.commissionMembers.Where(m=>m.enabled))
            {
                if(!groupKeys.Contains(member.poolId+"/"+member.groupId))p.Error(FileName,member.id,"奖励池ID / 抽取组ID","引用未知或停用的池或组。");
                var item=p.catalog.items.SingleOrDefault(i=>i.id==member.itemId);
                if(item==null)p.Error(FileName,member.id,"物品ID","未知物品："+member.itemId);
                else if(!ShopSession.IsSaleItem(item) || item.IsStorage || item.category==ItemCategory.ProductionEquipment)p.Error(FileName,member.id,"物品ID","此物品不符合普通委托奖励能力。");
                if(member.maximumCount<member.minimumCount || member.maximumCount==int.MaxValue)p.Error(FileName,member.id,"最大数量","数量范围无效。");
            }
            var activePools=pools.Where(x=>x.enabled).Select(x=>x.id).ToArray();
            foreach(var t in c.commissionTemplates.Where(t=>t.enabled))
                if(!activePools.Contains(t.rewardPoolId))p.Error("commission-templates.csv",t.id,"奖励池ID","引用未知或停用奖励池："+t.rewardPoolId);
            if(c.commissionTemplates.Count(t=>t.enabled && t.weight>0)<settings.candidateCount)p.Error("commission-global.csv","candidateCount","当前值","有效模板不足以无放回抽取。");
            var gift=pools.SingleOrDefault(x=>x.enabled && x.id==settings.extraGiftPoolId);
            if(gift==null || gift.maximumMoney!=0 || gift.items.Length!=1 || gift.items[0].minimumCount!=1 || gift.items[0].maximumCount!=1 || gift.items[0].members.Any(m=>m.minimumCount!=1 || m.maximumCount!=1))p.Error("commission-global.csv","extraGiftPoolId","当前值","谢礼必须引用单次抽取一件实物的有效池。");
            settings.rewardPools=pools.ToArray();
            settings.templates=c.commissionTemplates.Select(t=>new CommissionTemplate{id=t.id,enabled=t.enabled,title=t.title,description=t.description,weight=t.weight,rewardPoolId=t.rewardPoolId,rewardHint=t.rewardHint}).ToArray();
        }
    }
}
