using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class CommissionTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup(){catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();}
        [TearDown] public void Teardown(){UnityEngine.Object.DestroyImmediate(catalog);}
        ShopSession Visit(int seed=49)
        {
            var s=new ShopSession(catalog,false,seed);
            TravelTestSetup.CloseBusiness(s);Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("baishitang"));
            return s;
        }
        void OnlyPool(string pool)
        {
            foreach(var t in catalog.commissions.templates)t.rewardPoolId=pool;
            catalog.commissions.extraGiftChance=0;
        }
        [Test] public void CandidatesAreDistinctFixedWeightedAndRefreshAfterMonth()
        {
            var s=Visit();var ids=s.CommissionCandidates.Select(t=>t.id).ToArray();
            Assert.That(ids.Length,Is.EqualTo(3));Assert.That(ids.Distinct().Count(),Is.EqualTo(3));
            Assert.That(s.CommissionCandidates.Select(t=>t.id),Is.EqualTo(ids));Assert.That(s.Stamina,Is.EqualTo(40));
            Assert.That(s.CompleteCommission("unknown"),Is.False);
            Assert.That(s.LeaveLocation());Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            Assert.That(s.AdvanceTurn());var restored=ShopSession.RestoreSave(catalog,s.CaptureSave());
            Assert.That(restored.CommissionCandidates.Select(t=>t.id),Is.EqualTo(ids));
            Assert.That(restored.BeginBusiness());Assert.That(restored.EndBusiness());
            foreach(var t in catalog.commissions.templates)t.enabled=false;
            catalog.commissions.templates[0].enabled=true;catalog.commissions.templates[1].enabled=true;catalog.commissions.templates[2].enabled=true;
            Assert.That(restored.BeginCarrying(0));Assert.That(restored.BeginTravel());Assert.That(restored.EnterLocation("baishitang"));
            Assert.That(restored.CommissionCandidates.Select(t=>t.id),Is.EquivalentTo(catalog.commissions.templates.Take(3).Select(t=>t.id)));
        }
        [Test] public void DataControlsNewTemplatesWeightsAndCandidateCount()
        {
            catalog.commissions.templates=new[]{
                new CommissionTemplate{id="new",title="新名称",description="新描述",rewardHint="自定义奖励",rewardPoolId="BSH-R04",weight=1000000},
                new CommissionTemplate{id="other",rewardPoolId="BSH-R01",weight=1},
                new CommissionTemplate{id="disabled",rewardPoolId="missing",enabled=false,weight=1000000}};
            catalog.commissions.candidateCount=1;
            var s=Visit();Assert.That(s.CommissionCandidates.Single().id,Is.EqualTo("new"));
            Assert.That(s.CommissionCandidates.Single().title,Is.EqualTo("新名称"));
        }
        [TestCase("BSH-R01",2,4,0,0)] [TestCase("BSH-R02",1,3,0,0)]
        [TestCase("BSH-R03",2,2,0,0)] [TestCase("BSH-R04",0,0,6,10)]
        [TestCase("BSH-R05",1,1,4,6)] [TestCase("BSH-R06",1,3,0,0)]
        public void NormalPoolsSettleOnceIntoLocationOrBalance(string pool,int min,int max,int minMoney,int maxMoney)
        {
            OnlyPool(pool);
            for(int seed=0;seed<20;seed++)
            {
                var s=Visit(seed);var id=s.CommissionCandidates.First().id;int money=s.Money;
                Assert.That(s.CompleteCommission(id));Assert.That(s.Money-money,Is.InRange(minMoney,maxMoney));
                var rewards=s.In(ContainerId.Location).ToArray();Assert.That(rewards.Length,Is.InRange(min,max));
                Assert.That(rewards.All(i=>!i.ForSale && i.PurchaseValue==null && i.LocationId=="baishitang"));
                if(pool=="BSH-R03"){Assert.That(rewards.Count(i=>i.Definition.category==ItemCategory.Material),Is.EqualTo(1));Assert.That(rewards.Count(i=>i.Definition.id=="sword"),Is.EqualTo(1));}
                int settledMoney=s.Money;
                Assert.That(s.CompleteCommission(id),Is.False);Assert.That(s.CompleteCommission(s.CommissionCandidates.Last().id),Is.False);
                Assert.That(s.Money,Is.EqualTo(settledMoney));Assert.That(s.Items.Count,Is.EqualTo(rewards.Length));Assert.That(s.Stamina,Is.EqualTo(40));
                Assert.That(s.ValidateState(),Is.Null);
            }
        }
        [Test] public void GiftChanceCountMembershipAndCurrencyRangesAreConfigurable()
        {
            OnlyPool("BSH-R04");var settings=catalog.commissions;settings.extraGiftChance=1;settings.extraGiftCount=2;
            var moneyPool=settings.rewardPools.Single(p=>p.id=="BSH-R04");moneyPool.minimumMoney=13;moneyPool.maximumMoney=13;
            settings.rewardPools.Single(p=>p.id=="BSH-R07").items[0].itemIds=new[]{"dew"};
            var s=Visit();Assert.That(s.CompleteCommission(s.CommissionCandidates.First().id));
            Assert.That(s.Money,Is.EqualTo(catalog.startingMoney+13));Assert.That(s.In(ContainerId.Location).Count(i=>i.Definition.id=="dew"),Is.EqualTo(2));
            Assert.That(s.CommissionResult,Does.Contain("意外谢礼 2 件"));
        }
        [Test] public void FilledFloorDoesNotLoseRewardsAndLeavingRequiresConfirmation()
        {
            OnlyPool("BSH-R02");catalog.commissions.extraGiftChance=1;
            var s=Visit();for(int i=0;i<24;i++)Assert.That(s.AddLocationItem("dew"));
            int before=s.Items.Count;Assert.That(s.CompleteCommission(s.CommissionCandidates.First().id));
            Assert.That(s.Items.Count,Is.InRange(before+2,before+4));Assert.That(s.GridSize(ContainerId.Location).y,Is.GreaterThan(4));
            var reward=s.Items.Last();Assert.That(s.Move(reward.Id,ContainerId.LeftHand,0,0,0,false));
            Assert.That(s.LeaveLocation(),Is.False);Assert.That(s.Items.Contains(reward));
            Assert.That(s.LeaveLocation(true));Assert.That(s.Items.Count,Is.EqualTo(1));
            Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());Assert.That(s.ValidateState(),Is.Null);
            Assert.That(s.AdvanceTurn());var restored=ShopSession.RestoreSave(catalog,s.CaptureSave());Assert.That(restored.CommissionLimitReached);
            Assert.That(restored.Items.Single().Definition.id,Is.EqualTo(reward.Definition.id));
        }
        [Test] public void InvalidCurrencyCapacityDoesNotPartiallyReward()
        {
            OnlyPool("BSH-R05");catalog.startingMoney=int.MaxValue;
            var s=Visit();Assert.That(s.CompleteCommission(s.CommissionCandidates.First().id),Is.False);
            Assert.That(s.Money,Is.EqualTo(int.MaxValue));Assert.That(s.Items,Is.Empty);Assert.That(s.CommissionLimitReached,Is.False);
        }
    }
}
