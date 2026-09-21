using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP50")]
    public sealed class CustomerGenerationTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup(){catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();}
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(catalog);}
        ShopSession Session(int seed=50)=>new ShopSession(catalog,customerSeed:seed);
        void Tea(ShopSession s,TeaEffect effect)
        {
            foreach(var e in catalog.teaHouse.effects)e.weight=e.effect==effect?1:0;
            TravelTestSetup.CloseBusiness(s);Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("tingfeng-teahouse"));
            Assert.That(s.LeaveLocation());Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            Assert.That(s.AdvanceTurn());
        }
        string OfferState(ShopSession s)=>s.Offer.Behavior+"/"+s.Offer.RequestedCategory+"/"+s.Offer.BudgetTier+"/"+s.Offer.RemainingBudget+"/"+string.Join(",",s.Offer.SupplierItems.Select(i=>i.Definition.id));

        [Test] public void AllDisplayCategoriesContributeByCountAndPreviewDoesNotRerollQueue()
        {
            var a=Session();var b=Session();
            foreach(var s in new[]{a,b})
            {
                Assert.That(s.Move(s.Items.Single(i=>i.Definition.id=="herb").Id,ContainerId.Display,0,0,0,false));
                Assert.That(s.Move(s.Items.Single(i=>i.Definition.id=="pill").Id,ContainerId.Display,3,0,0,false));
            }
            var snapshot=a.PreviewAttraction();
            Assert.That(snapshot.BuyingCategories.Single(c=>c.Category==ItemCategory.Material).Weight,Is.EqualTo(15));
            Assert.That(snapshot.BuyingCategories.Single(c=>c.Category==ItemCategory.Medicine).Weight,Is.EqualTo(15));
            Assert.That(snapshot.BuyingCategories.Single(c=>c.Category==ItemCategory.Equipment).Weight,Is.EqualTo(10));
            for(int i=0;i<20;i++)a.PreviewAttraction();
            Assert.That(a.BeginBusiness());Assert.That(b.BeginBusiness());var first=a.Offer;
            Assert.That(a.BeginBusiness(),Is.False);Assert.That(a.Offer,Is.SameAs(first));
            var pill=a.Items.Single(i=>i.Owner==ItemOwner.Player && i.Definition.id=="pill");
            Assert.That(a.Move(pill.Id,ContainerId.Storage,8,4,0,false));
            while(a.Offer!=null){Assert.That(OfferState(a),Is.EqualTo(OfferState(b)));a.NextCustomer();b.NextCustomer();}
        }

        [Test] public void EmptyDisplaySupportsAllThreeBehaviorsAndConfiguredSupplyCounts()
        {
            int[] behaviors=new int[3],counts=new int[2];
            for(int seed=0;seed<200;seed++)
            {
                var s=Session(seed);Assert.That(s.BeginBusiness());
                Assert.That(s.BuyersToday+s.SuppliersToday+s.TradingCustomersToday,Is.EqualTo(5));
                while(s.Offer!=null)
                {
                    var o=s.Offer;behaviors[(int)o.Behavior]++;
                    if(o.Behavior==CustomerBehavior.Selling){Assert.That(o.RequestedCategory,Is.EqualTo(ItemCategory.Unclassified));Assert.That(o.RemainingBudget,Is.Zero);}
                    else Assert.That(o.RemainingBudget,Is.EqualTo(catalog.customers.budgets.Single(b=>b.category==o.RequestedCategory).Amount(o.BudgetTier)));
                    if(o.Behavior!=CustomerBehavior.Buying)
                    {
                        Assert.That(o.SupplierItems.Count,Is.InRange(1,2));counts[o.SupplierItems.Count-1]++;
                        Assert.That(o.SupplierItems.Select(i=>i.Definition.category).Distinct().Count(),Is.EqualTo(1));
                    }
                    s.NextCustomer();
                }
            }
            Assert.That(behaviors[0]/1000d,Is.EqualTo(.4).Within(.05));
            Assert.That(behaviors[1]/1000d,Is.EqualTo(.25).Within(.05));
            Assert.That(behaviors[2]/1000d,Is.EqualTo(.35).Within(.05));
            Assert.That(counts[0]/(double)counts.Sum(),Is.EqualTo(.75).Within(.06));
        }

        [Test] public void TravellersRedistributeOnlyRemainingSingleDirectionProbability()
        {
            var s=Session();Tea(s,TeaEffect.Travellers);var a=s.PreviewAttraction();
            Assert.That(a.TradingChance,Is.EqualTo(.6).Within(.00001));
            Assert.That(a.BuyingChance/a.SellingChance,Is.EqualTo(40d/25).Within(.00001));
            Assert.That(a.BuyingChance+a.SellingChance+a.TradingChance,Is.EqualTo(1).Within(.00001));
            catalog.teaHouse.travellerChanceBonus=.9f;a=s.PreviewAttraction();
            Assert.That(a.TradingChance,Is.EqualTo(.85).Within(.00001));Assert.That(a.BuyingChance,Is.GreaterThan(0));Assert.That(a.SellingChance,Is.GreaterThan(0));
        }

        [Test] public void OverflowAndPromotionAddToConfiguredBaseExactlyOnce()
        {
            catalog.firstLocationStaminaCost=0;var s=Session();Tea(s,TeaEffect.Promotion);
            Assert.That(s.HasStaminaOverflowCustomer);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(7));
            for(int i=0;i<10;i++)s.PreviewAttraction();
            Assert.That(s.BeginBusiness());Assert.That(s.RemainingCustomers,Is.EqualTo(6));
            Assert.That(s.BeginBusiness(),Is.False);Assert.That(s.RemainingCustomers,Is.EqualTo(6));
        }

        [Test] public void CategoryBudgetTableAndTierDistributionAreUsed()
        {
            catalog.customers.buyingWeight=1;catalog.customers.sellingWeight=0;catalog.customers.tradingWeight=0;
            catalog.customers.budgets.Single(b=>b.category==ItemCategory.Medicine).ordinary=57;
            int[] tiers=new int[3];bool medicine=false,material=false;
            for(int seed=0;seed<200;seed++)
            {
                var s=Session(seed);Assert.That(s.BeginBusiness());
                while(s.Offer!=null)
                {
                    var o=s.Offer;tiers[(int)o.BudgetTier]++;
                    Assert.That(o.RemainingBudget,Is.EqualTo(catalog.customers.budgets.Single(b=>b.category==o.RequestedCategory).Amount(o.BudgetTier)));
                    if(o.BudgetTier==CustomerBudgetTier.Ordinary && o.RequestedCategory==ItemCategory.Medicine){medicine=true;Assert.That(o.RemainingBudget,Is.EqualTo(57));}
                    if(o.BudgetTier==CustomerBudgetTier.Ordinary && o.RequestedCategory==ItemCategory.Material){material=true;Assert.That(o.RemainingBudget,Is.EqualTo(20));}
                    s.NextCustomer();
                }
            }
            Assert.That(medicine && material);Assert.That(tiers[0]/1000d,Is.EqualTo(.75).Within(.05));Assert.That(tiers[1]/1000d,Is.EqualTo(.23).Within(.05));Assert.That(tiers[2]/1000d,Is.EqualTo(.02).Within(.015));
        }

        [TestCase(TeaEffect.BuyerTrend)] [TestCase(TeaEffect.SupplierTrend)]
        public void WindsMultiplyCategoryWeightWithoutGuaranteeingSelection(TeaEffect effect)
        {
            catalog.customers.buyingWeight=0;catalog.customers.sellingWeight=0;catalog.customers.tradingWeight=1;
            int hits=0,total=0;double expected=0;
            for(int seed=0;seed<150;seed++)
            {
                var s=Session(seed);Tea(s,effect);var snapshot=s.PreviewAttraction();
                var choices=effect==TeaEffect.BuyerTrend?snapshot.BuyingCategories:snapshot.SellingCategories;
                var target=s.ActiveTeaEffect.category;var candidate=choices.Single(c=>c.Category==target);
                Assert.That(candidate.Weight,Is.EqualTo(20));
                Assert.That(s.BeginBusiness());
                while(s.Offer!=null){var cat=effect==TeaEffect.BuyerTrend?s.Offer.RequestedCategory:s.Offer.SupplierItems[0].Definition.category;if(cat==target)hits++;total++;expected+=candidate.Weight/choices.Sum(c=>c.Weight);s.NextCustomer();}
            }
            Assert.That(hits/(double)total,Is.EqualTo(expected/total).Within(.06));Assert.That(hits,Is.InRange(1,total-1));
        }

        [TestCase(0)] [TestCase(2)]
        public void WealthyPromotionMarksOneOrdinaryCustomerAndCapsTier(int baseTier)
        {
            catalog.customers.ordinaryWeight=baseTier==0?1:0;catalog.customers.wealthyWeight=0;catalog.customers.lavishWeight=baseTier==2?1:0;
            var s=Session();Tea(s,TeaEffect.WealthyVisitor);Assert.That(s.BeginBusiness());int promoted=0;
            while(s.Offer!=null)
            {
                var o=s.Offer;if(o.WealthyPromotion)promoted++;
                Assert.That((int)o.BudgetTier,Is.EqualTo(o.WealthyPromotion?Math.Min(2,baseTier+1):baseTier));s.NextCustomer();
            }
            Assert.That(promoted,Is.EqualTo(1));
        }

        [Test] public void EmptySupplyPoolsAreExcludedAndPurchaseDoesNotRefillDemandBudget()
        {
            catalog.customers.buyingWeight=0;catalog.customers.sellingWeight=0;catalog.customers.tradingWeight=1;
            catalog.customers.supplyPools=new[]{
                new CustomerSupplyPool{category=ItemCategory.Equipment,itemIds=Array.Empty<string>(),weight=1000},
                new CustomerSupplyPool{category=ItemCategory.Material,itemIds=new[]{"dew"}}};
            var s=Session();Assert.That(s.PreviewAttraction().SellingCategories.Single().Category,Is.EqualTo(ItemCategory.Material));
            Assert.That(s.BeginBusiness());int budget=s.Offer.RemainingBudget;
            Assert.That(s.AcceptTrade(true),Is.True,s.Message);
            Assert.That(s.Offer.RemainingBudget,Is.EqualTo(budget));Assert.That(s.ValidateState(),Is.Null);
        }
    }
}
