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
            TravelTestSetup.CloseBusiness(s);Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("tingfeng-teahouse"));
            Assert.That(s.Stamina,Is.EqualTo(40));Assert.That(s.LeaveLocation());Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            return s;
        }
        void NextTurn(ShopSession s){TravelTestSetup.CloseBusiness(s);Assert.That(s.AdvanceTurn());}

        [TestCase(TeaEffect.BuyerTrend)] [TestCase(TeaEffect.SupplierTrend)] [TestCase(TeaEffect.Travellers)]
        [TestCase(TeaEffect.Promotion)] [TestCase(TeaEffect.WealthyVisitor)]
        public void WeightedResultAppliesOnlyNextTurnAndDoesNotChangeOnReadOrSave(TeaEffect effect)
        {
            var s=Visit(effect);var result=s.LatestTeaVisit;
            Assert.That(result.effect,Is.EqualTo(effect));Assert.That(result.ApplyTurn,Is.EqualTo(2));
            Assert.That(s.ActiveTeaEffect,Is.Null);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
            Assert.That(s.AdvanceTurn());string before=s.CaptureSave();for(int i=0;i<5;i++){_ =s.TeaNewsText;s.PreviewAttraction();}
            Assert.That(s.CaptureSave(),Is.EqualTo(before));s=ShopSession.RestoreSave(catalog,before);
            Assert.That(s.ActiveTeaEffect.effect,Is.EqualTo(effect));Assert.That(s.Stamina,Is.EqualTo(70));
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(effect==TeaEffect.Promotion?6:5));
            NextTurn(s);Assert.That(s.ActiveTeaEffect,Is.Null);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
        }

        [Test] public void NewVisitDoesNotOverwriteTheCurrentMonthsPromotion()
        {
            var s=Visit(TeaEffect.Promotion);NextTurn(s);
            TravelTestSetup.CloseBusiness(s);Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("tingfeng-teahouse"));
            Assert.That(s.LatestTeaVisit.visitTurn,Is.EqualTo(2));Assert.That(s.ActiveTeaEffect.visitTurn,Is.EqualTo(1));
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(6));Assert.That(s.Stamina,Is.EqualTo(10));
        }



        [Test] public void PromotionStacksWithStaminaOverflowAndDoesNotAppendOnPreview()
        {
            Force(TeaEffect.Promotion);catalog.firstLocationStaminaCost=0;catalog.teaHouse.extraCustomers=2;
            var s=new ShopSession(catalog,customerSeed:48);TravelTestSetup.CloseBusiness(s);s.BeginCarrying(0);s.BeginTravel();s.EnterLocation("tingfeng-teahouse");s.LeaveLocation();s.ReturnToShop(true);s.EndCarrying();NextTurn(s);
            Assert.That(s.HasStaminaOverflowCustomer);Assert.That(s.CustomerCountThisTurn,Is.EqualTo(8));
            for(int i=0;i<4;i++)s.PreviewAttraction();s.BeginBusiness();Assert.That(s.RemainingCustomers,Is.EqualTo(7));
            Assert.That(s.BeginBusiness(),Is.False);Assert.That(s.RemainingCustomers,Is.EqualTo(7));
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
            var s=Visit(TeaEffect.Promotion);Assert.That(s.AdvanceTurn());var save=JsonUtility.FromJson<ShopSave>(s.CaptureSave());
            save.latestTeaVisit.visitTurn=99;
            Assert.That(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(save)),Throws.ArgumentException);
        }

        [Test] public void SecretResultAndScheduleAreStableAndDoNotAffectPricesEarly()
        {
            var calendar=MarketCalendar.OverlapExample();var before=calendar.Capture();
            var s=Visit(TeaEffect.MarketSecret,48,calendar);Assert.That(s.LatestTeaVisit.effect,Is.EqualTo(TeaEffect.MarketSecret));
            var secret=calendar.DisclosedEvents.Single();Assert.That(secret.startTurn,Is.InRange(3,13));
            Assert.That(calendar.Capture().events.Length,Is.EqualTo(before.events.Length));Assert.That(calendar.ActiveTags(1),Is.Empty);
            Assert.That(s.AdvanceTurn());var restored=ShopSession.RestoreSave(catalog,s.CaptureSave());
            Assert.That(restored.LatestTeaVisit.secretEventId,Is.EqualTo(secret.id));Assert.That(restored.Calendar.DisclosedEvents.Single().id,Is.EqualTo(secret.id));
        }
    }
}
