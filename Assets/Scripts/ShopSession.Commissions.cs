using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        string[] commissionCandidates = Array.Empty<string>();
        int commissionTurn, commissionsCompleted, commissionGridHeight;
        public IEnumerable<CommissionTemplate> CommissionCandidates => commissionCandidates.Select(id=>catalog.commissions.templates.Single(t=>t.id==id));
        public bool CommissionLimitReached => commissionsCompleted>=catalog.commissions.completionLimitPerTurn;
        public string CommissionResult { get; private set; } = "";

        void PrepareCommissions()
        {
            if(commissionTurn==Turn)return;
            var settings=catalog.commissions;
            var available=settings.templates.Where(t=>t.enabled && t.weight>0).ToList();
            if(settings.candidateCount<1 || settings.completionLimitPerTurn<1 ||
                available.Count<settings.candidateCount || settings.templates.Select(t=>t.id).Distinct().Count()!=settings.templates.Length ||
                settings.templates.Any(t=>string.IsNullOrWhiteSpace(t.id) || t.weight<0 || float.IsNaN(t.weight) || float.IsInfinity(t.weight)) ||
                settings.extraGiftChance<0 || settings.extraGiftChance>1 || float.IsNaN(settings.extraGiftChance) || settings.extraGiftCount<1)
                throw new ArgumentException("百事堂候选或谢礼配置无效。");
            foreach(var template in available)ValidateCommissionPool(template.rewardPoolId);
            ValidateCommissionPool(settings.extraGiftPoolId);
            var gift=CommissionPool(settings.extraGiftPoolId);
            if(gift.maximumMoney!=0 || gift.items.Length!=1 || gift.items[0].minimumCount!=1 || gift.items[0].maximumCount!=1)
                throw new ArgumentException("谢礼池须为单件实物池，件数由谢礼数量配置。");
            var selected=new List<string>();
            for(int n=0;n<settings.candidateCount;n++)
            {
                double roll=DrawCustomerChance()*available.Sum(t=>(double)t.weight);
                int index=available.Count-1;
                for(int i=0;i<available.Count;i++){roll-=available[i].weight;if(roll<0){index=i;break;}}
                selected.Add(available[index].id);available.RemoveAt(index);
            }
            commissionCandidates=selected.ToArray();commissionTurn=Turn;commissionsCompleted=0;CommissionResult="";
        }

        CommissionRewardPool CommissionPool(string id) => catalog.commissions.rewardPools.Single(p=>p.id==id && p.enabled);

        void ValidateCommissionPool(string id)
        {
            var pool=CommissionPool(id);
            if(pool.minimumMoney<0 || pool.maximumMoney<pool.minimumMoney || pool.maximumMoney==int.MaxValue ||
                (pool.maximumMoney==0 && pool.items.Length==0))
                throw new ArgumentException("百事堂奖励范围无效："+id);
            foreach(var group in pool.items)
            {
                if(group.minimumCount<1 || group.maximumCount<group.minimumCount || group.maximumCount==int.MaxValue || group.itemIds.Length==0)
                    throw new ArgumentException("百事堂实物数量无效："+id);
                foreach(var itemId in group.itemIds)
                {
                    var definition=catalog.Find(itemId);
                    if(!IsSaleItem(definition) || definition.IsStorage || definition.category==ItemCategory.ProductionEquipment ||
                        definition.Shape(0,false).Max(p=>p.x)>=CurrentLocation.itemGridSize.x)
                        throw new ArgumentException("百事堂奖励物品不可领取："+itemId);
                }
            }
        }

        public bool CompleteCommission(string templateId)
        {
            if(CurrentLocationId!="baishitang" || !IsTravelling)return Fail("请先进入百事堂。");
            if(CommissionLimitReached)return Fail("本月委托已完成，不能重复领取。");
            if(!commissionCandidates.Contains(templateId))return Fail("请选择本月公示的委托。");
            var template=catalog.commissions.templates.Single(t=>t.id==templateId);
            var pool=CommissionPool(template.rewardPoolId);
            // Check the maximum before drawing, so a rejected balance cannot reroll rewards.
            if((long)Money+pool.maximumMoney>int.MaxValue)return Fail("经营货币余额已达上限，无法结算。");
            int money=DrawCustomerNumber(pool.minimumMoney,pool.maximumMoney+1);
            var rewards=new List<string>();
            foreach(var group in pool.items)
            {
                int count=DrawCustomerNumber(group.minimumCount,group.maximumCount+1);
                for(int i=0;i<count;i++)rewards.Add(group.itemIds[DrawCustomerNumber(0,group.itemIds.Length)]);
            }
            bool gift=DrawCustomerChance()<catalog.commissions.extraGiftChance;
            if(gift)
            {
                var members=CommissionPool(catalog.commissions.extraGiftPoolId).items[0].itemIds;
                for(int i=0;i<catalog.commissions.extraGiftCount;i++)rewards.Add(members[DrawCustomerNumber(0,members.Length)]);
            }
            // The receiving area can grow downward; carried storage stays finite.
            // This guarantees all earned goods exist even when the player filled the floor.
            foreach(string id in rewards)
            {
                var definition=catalog.Find(id);
                int height=definition.Shape(0,false).Max(p=>p.y)+1;
                commissionGridHeight=Math.Max(commissionGridHeight,CurrentLocation.itemGridSize.y);
                if(!AddLocationItem(id))
                {
                    commissionGridHeight+=height;
                    if(!AddLocationItem(id))throw new InvalidOperationException("百事堂扩展领取区后仍无法放置已核对奖励。");
                }
            }
            Money+=money;commissionsCompleted++;
            CommissionResult=template.title+"已完成 · 经营货币 +"+money+" · 实物 "+rewards.Count+" 件"+
                (gift?"（含意外谢礼 "+catalog.commissions.extraGiftCount+" 件）":"")+"。请手动带走领取区物品。";
            return Success(CommissionResult);
        }
    }
}
