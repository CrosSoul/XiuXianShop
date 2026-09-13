using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class TeaHouseTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup(){catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();catalog.marketEvents=Array.Empty<MarketEventDefinition>();}
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        void Force(TeaEffect effect)
        {
            foreach(var e in catalog.teaHouse.effects)e.weight=e.effect==effect?1000000:0;
            if(effect==TeaEffect.MarketSecret)catalog.teaHouse.effects.Single(e=>e.effect==TeaEffect.Promotion).weight=1;
        }
        ShopSession Visit(TeaEffect effect,int seed=48,MarketCalendar calendar=null)
        {
            Force(effect);var s=new ShopSession(catalog,customerSeed:seed,calendar:calendar);
            Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("tingfeng-teahouse"));
            Assert.That(s.Stamina,Is.EqualTo(40));Assert.That(s.LeaveLocation());Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            return s;
        }
        void NextTurn(ShopSession s){Assert.That(s.BeginBusiness());Assert.That(s.EndBusiness());Assert.That(s.AdvanceTurn());}

        [TestCase(TeaEffect.BuyerTrend)] [TestCase(TeaEffect.SupplierTrend)] [TestCase(TeaEffect.Travellers)]
        [TestCase(TeaEffect.Promotion)] [TestCase(TeaEffect.WealthyVisitor)]
        public void WeightedResultAppliesOnlyNextTurnAndDoesNotChangeOnReadOrSave(TeaEffect effect)
        {
            var s=Visit(effect);var result=s.LatestTeaVisit;
            Assert.That(result.effect,Is.EqualTo(effect));Assert.That(result.ApplyTurn,Is.EqualTo(2));
            Assert.That(s.ActiveTeaEffect,Is.Null);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
            string before=s.CaptureSave();for(int i=0;i<5;i++){_ =s.TeaNewsText;s.PreviewAttraction();}
            Assert.That(s.CaptureSave(),Is.EqualTo(before));s=ShopSession.RestoreSave(catalog,before);
            NextTurn(s);Assert.That(s.ActiveTeaEffect.effect,Is.EqualTo(effect));Assert.That(s.Stamina,Is.EqualTo(70));
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(effect==TeaEffect.Promotion?6:5));
            NextTurn(s);Assert.That(s.ActiveTeaEffect,Is.Null);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
        }

        [Test] public void NewVisitDoesNotOverwriteTheCurrentMonthsPromotion()
        {
            var s=Visit(TeaEffect.Promotion);NextTurn(s);
            Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("tingfeng-teahouse"));
            Assert.That(s.LatestTeaVisit.visitTurn,Is.EqualTo(2));Assert.That(s.ActiveTeaEffect.visitTurn,Is.EqualTo(1));
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(6));Assert.That(s.Stamina,Is.EqualTo(10));
        }

        [Test] public void TravellersAddConfiguredPercentagePointsThenCapWithoutGuaranteeingArrival()
        {
            catalog.baseSupplierChance=.75f;catalog.teaHouse.travellerChanceBonus=.25f;catalog.teaHouse.travellerChanceCap=.85f;
            var s=Visit(TeaEffect.Travellers);Assert.That(s.PreviewAttraction().SupplierChance,Is.EqualTo(.75f).Within(.0001));NextTurn(s);
            Assert.That(s.PreviewAttraction().SupplierChance,Is.EqualTo(.85f).Within(.0001));
            catalog.teaHouse.travellerChanceBonus=.05f;catalog.teaHouse.travellerChanceCap=.95f;
            Assert.That(s.PreviewAttraction().SupplierChance,Is.EqualTo(.8f).Within(.0001));
        }

        [TestCase(1,60)] [TestCase(2,150)] [TestCase(20,400)]
        public void ExactlyOneRandomVisitorGetsConfiguredBudgetTierIncrease(int increase,int budget)
        {
            catalog.buyerBudgetVariation=0;catalog.teaHouse.wealthyBudgetTierIncrease=increase;
            var s=Visit(TeaEffect.WealthyVisitor);NextTurn(s);Assert.That(s.BeginBusiness());
            var budgets=new System.Collections.Generic.List<int>();
            while(s.Offer!=null){budgets.Add(s.Offer.RemainingBudget);s.NextCustomer();}
            Assert.That(budgets.Count(b=>b==budget),Is.EqualTo(1));Assert.That(budgets.Count(b=>b==20),Is.EqualTo(4));
        }

        [Test] public void PromotionStacksWithStaminaOverflowAndDoesNotAppendOnPreview()
        {
            Force(TeaEffect.Promotion);catalog.firstLocationStaminaCost=0;catalog.teaHouse.extraCustomers=2;
            var s=new ShopSession(catalog,customerSeed:48);s.BeginCarrying(0);s.BeginTravel();s.EnterLocation("tingfeng-teahouse");s.LeaveLocation();s.ReturnToShop(true);s.EndCarrying();NextTurn(s);
            Assert.That(s.HasStaminaOverflowCustomer);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(8));
            for(int i=0;i<4;i++)s.PreviewAttraction();s.BeginBusiness();Assert.That(s.RemainingCustomers,Is.EqualTo(7));
            Assert.That(s.BeginBusiness(),Is.False);Assert.That(s.RemainingCustomers,Is.EqualTo(7));
        }

        [TestCase(TeaEffect.BuyerTrend)] [TestCase(TeaEffect.SupplierTrend)]
        public void CategoryMultipliersChangeNormalizedSamplingWithinExistingCandidates(TeaEffect effect)
        {
            catalog.baseSupplierChance=1;int matches=0,total=0;double expected=0;
            for(int seed=0;seed<160;seed++)
            {
                var s=Visit(effect,seed);NextTurn(s);
                var categories=effect==TeaEffect.BuyerTrend?
                    catalog.items.Where(d=>!d.procurementSign && d.FullBaseValue>0).Select(d=>d.category).Distinct().ToArray():
                    catalog.items.Where(d=>d.supplierAvailable && !d.procurementSign && d.FullBaseValue>0).Select(d=>d.category).ToArray();
                var target=s.ActiveTeaEffect.category;int baseCount=categories.Count(c=>c==target);
                double probability=2d*baseCount/(categories.Length+baseCount);
                s.BeginBusiness();while(s.Offer!=null)
                {
                    var draws=effect==TeaEffect.BuyerTrend?new[]{s.Offer.RequestedCategory}:s.Offer.SupplierItems.Select(i=>i.Definition.category).ToArray();
                    matches+=draws.Count(c=>c==target);total+=draws.Length;expected+=draws.Length*probability;s.NextCustomer();
                }
            }
            Assert.That((double)matches/total,Is.EqualTo(expected/total).Within(.05));
            Assert.That(matches,Is.GreaterThan(0));Assert.That(matches,Is.LessThan(total));
        }

        [Test] public void SecretFailureRedrawsOtherEffectWithoutCreatingIllegalMarket()
        {
            var calendar=new MarketCalendar(Array.Empty<MarketEventDefinition>(),48);
            var s=Visit(TeaEffect.MarketSecret,48,calendar);
            Assert.That(s.LatestTeaVisit.effect,Is.EqualTo(TeaEffect.Promotion));Assert.That(calendar.DisclosedEvents,Is.Empty);
            Assert.That(calendar.Between(1,24),Is.Empty);Assert.That(s.Stamina,Is.EqualTo(40));
        }

        [Test] public void InvalidSavedTeaTurnIsRejected()
        {
            var s=Visit(TeaEffect.Promotion);var save=JsonUtility.FromJson<ShopSave>(s.CaptureSave());
            save.latestTeaVisit.visitTurn=99;
            Assert.That(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(save)),Throws.ArgumentException);
        }

        [Test] public void SecretResultAndScheduleAreStableAndDoNotAffectPricesEarly()
        {
            var calendar=MarketCalendar.OverlapExample();var before=calendar.Capture();
            var s=Visit(TeaEffect.MarketSecret,48,calendar);Assert.That(s.LatestTeaVisit.effect,Is.EqualTo(TeaEffect.MarketSecret));
            var secret=calendar.DisclosedEvents.Single();Assert.That(secret.startTurn,Is.InRange(3,13));
            Assert.That(calendar.Capture().events.Length,Is.EqualTo(before.events.Length));Assert.That(calendar.ActiveTags(1),Is.Empty);
            var restored=ShopSession.RestoreSave(catalog,s.CaptureSave());
            Assert.That(restored.LatestTeaVisit.secretEventId,Is.EqualTo(secret.id));Assert.That(restored.Calendar.DisclosedEvents.Single().id,Is.EqualTo(secret.id));
        }
    }
}
