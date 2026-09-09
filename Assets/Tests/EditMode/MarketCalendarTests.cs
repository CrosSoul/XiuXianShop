using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class MarketCalendarTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();
            catalog.Find("pill").baseValue=20;catalog.startingItems=new[]{"pill","pill","pill"};
            catalog.baseSupplierChance=0;catalog.advertisementSupplierBonus=0;catalog.displayedGoodsBuyerBonus=0;
            catalog.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=50}};catalog.buyerBudgetVariation=0;
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        ShopSession Session()=>new ShopSession(catalog,customerSeed:17,calendar:MarketCalendar.OverlapExample());
        [TestCase(5,10,20,-10)][TestCase(6,10,24,-14)][TestCase(7,12,20,-8)]
        [TestCase(8,11,18,-7)][TestCase(10,10,20,-10)]
        [Category("DP22")]
        public void MixedTradeUsesOnlyActiveMarketEffectsAndPreservesHistory(int day,int sale,int buy,int net)
        {
            catalog.retailMarkup=0;catalog.Find("pill").baseValue=10;
            catalog.baseSupplierChance=1;
            foreach(var d in catalog.items)d.supplierAvailable=d.id=="pill";
            var s=Session();Advance(s,day);
            var own=s.Items[0];Assert.That(s.Move(own.Id,ContainerId.Display,0,0,0,false));
            Assert.That(s.BeginBusiness());var offer=s.Offer;var goods=offer.SupplierItems.ToArray();
            Assert.That(s.Move(own.Id,ContainerId.Counter,0,0,0,false));
            var quote=s.PreviewTrade(true);
            Assert.That(quote.CanConfirm,Is.True,quote.Reason);
            Assert.That(quote.SaleTotal,Is.EqualTo(sale));Assert.That(quote.PurchaseTotal,Is.EqualTo(buy));
            Assert.That(quote.Net,Is.EqualTo(net));Assert.That(quote.Lines.Count,Is.EqualTo(3));
            int money=s.Money,budget=offer.RemainingBudget;
            Assert.That(s.AcceptTrade(true));Assert.That(s.Money,Is.EqualTo(money+net));
            Assert.That(offer.RemainingBudget,Is.EqualTo(budget-System.Math.Max(0,net)));
            Assert.That(goods.All(i=>i.Owner==ItemOwner.Player && i.PurchaseValue==buy/2));
            Assert.That(s.EndBusiness());Assert.That(s.Sleep());
            Assert.That(goods.All(i=>i.PurchaseValue==buy/2));Assert.That(s.ValidateState(),Is.Null);
        }
        static void Advance(ShopSession session,int day)
        {
            while(session.Day<day)
            {Assert.That(session.BeginBusiness(),Is.True);Assert.That(session.EndBusiness());Assert.That(session.Sleep());}
        }
        [TestCase(1,1)][TestCase(7,1)][TestCase(8,8)][TestCase(14,8)][TestCase(15,15)]
        public void TwoWeekWindowFollowsContinuousGameWeeks(int day,int first)
        {
            Assert.That(MarketCalendar.WeekStart(day),Is.EqualTo(first));
            var dates=Enumerable.Range(first,14).ToArray();Assert.That(dates,Does.Contain(day));Assert.That(dates[13],Is.EqualTo(first+13));
        }
        [Test] public void WeeklyGenerationHasTwoOrThreeStartsAndOneToThreeDaysWithoutRerolls()
        {
            var calendar=new MarketCalendar(MarketCalendar.PrototypeDefinitions(),19);
            var events=calendar.Between(1,560);
            for(int week=0;week<80;week++)Assert.That(events.Count(e=>(e.startDay-1)/7==week),Is.InRange(2,3));
            Assert.That(events.All(e=>e.Duration>=1 && e.Duration<=3));
            CollectionAssert.AreEquivalent(new[]{1,2,3},events.Select(e=>e.Duration).Distinct());
            string before=JsonUtility.ToJson(calendar.Capture());
            for(int n=0;n<20;n++){calendar.Between(8,21);calendar.Segments(1);calendar.ActiveTags(9).ToArray();}
            Assert.That(JsonUtility.ToJson(calendar.Capture()),Is.EqualTo(before));
            var restored=new MarketCalendar(JsonUtility.FromJson<MarketCalendarState>(before));
            Assert.That(JsonUtility.ToJson(restored.Capture()),Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(new MarketCalendarState{events=restored.Between(561,700)}),
                Is.EqualTo(JsonUtility.ToJson(new MarketCalendarState{events=calendar.Between(561,700)})));
        }
        [Test,Category("CalendarFeedback")] public void SameMarketTypeLeavesTwoFullDaysAndBrowsingOrderDoesNotReroll()
        {
            var definitions=MarketCalendar.PrototypeDefinitions();
            var calendar=new MarketCalendar(definitions,19);
            var events=calendar.Between(1,560);
            foreach(var group in events.GroupBy(e=>e.effect.id))
            {
                var ordered=group.OrderBy(e=>e.startDay).ToArray();
                for(int i=1;i<ordered.Length;i++)
                    Assert.That(ordered[i].startDay,Is.GreaterThanOrEqualTo(ordered[i-1].endDay+3));
            }
            var browsed=new MarketCalendar(definitions,19);browsed.Between(547,560);browsed.Between(1,14);
            Assert.That(JsonUtility.ToJson(browsed.Capture()),Is.EqualTo(JsonUtility.ToJson(calendar.Capture())));
            var restored=new MarketCalendar(calendar.Capture());
            CollectionAssert.AreEqual(calendar.Between(561,574).Select(e=>$"{e.id}:{e.effect.id}:{e.startDay}:{e.endDay}"),
                restored.Between(561,574).Select(e=>$"{e.id}:{e.effect.id}:{e.startDay}:{e.endDay}"));
            for(int week=0;week<80;week++)Assert.That(events.Count(e=>(e.startDay-1)/7==week),Is.InRange(2,3));
        }
        [Test,Category("CalendarFeedback")] public void MarketsRevealOnlyOnStartDayWithTheirWholeDurationAndStayVisibleInHistory()
        {
            var calendar=MarketCalendar.OverlapExample();
            string before=JsonUtility.ToJson(calendar.Capture());
            Assert.That(calendar.VisibleBetween(1,14,1),Is.Empty);
            Assert.That(calendar.Segments(1,6).Any(s=>s.Event.id=="example-rise"),Is.False);
            var revealed=calendar.Segments(1,7).Where(s=>s.Event.id=="example-rise").ToArray();
            Assert.That(revealed.Sum(s=>s.Days),Is.EqualTo(3));
            Assert.That(revealed.Length,Is.EqualTo(2),"Reveal the full cross-week bar on its start day.");
            Assert.That(calendar.VisibleBetween(1,14,7).Any(e=>e.id=="example-fall"),Is.False);
            Assert.That(calendar.VisibleBetween(1,14,8).Any(e=>e.id=="example-fall"));
            Assert.That(calendar.VisibleBetween(1,14,10).Single(e=>e.id=="example-rise").StatusOn(10),Is.EqualTo("已结束"));
            Assert.That(new MarketCalendar(calendar.Capture()).VisibleBetween(1,14,6).Any(e=>e.id=="example-rise"),Is.False);
            CollectionAssert.AreEqual(JsonUtility.FromJson<MarketCalendarState>(before).events.Select(e=>JsonUtility.ToJson(e)),
                calendar.Capture().events.Select(e=>JsonUtility.ToJson(e)));
        }
        [Test] public void CrossWeekOverlapAndClippingNeverShareOccupiedLane()
        {
            var c=MarketCalendar.OverlapExample();
            var segments=c.Segments(1);
            var rise=segments.Where(s=>s.Event.id=="example-rise").ToArray();
            Assert.That(rise.Length,Is.EqualTo(2));Assert.That(rise[0].Column,Is.EqualTo(6));Assert.That(rise[0].Days,Is.EqualTo(1));
            Assert.That(rise[1].Column,Is.Zero);Assert.That(rise[1].Days,Is.EqualTo(2));
            var fall=segments.Single(s=>s.Event.id=="example-fall");Assert.That(fall.Lane,Is.Not.EqualTo(rise[1].Lane));
            foreach(var a in segments)foreach(var b in segments.Where(b=>b!=a && b.Week==a.Week && b.Lane==a.Lane))
                Assert.That(a.Column+a.Days<=b.Column || b.Column+b.Days<=a.Column,Is.True,"Bars in the same lane must not overlap.");
            var clipped=c.Segments(8).Single(s=>s.Event.id=="example-rise");Assert.That(clipped.Column,Is.Zero);Assert.That(clipped.Days,Is.EqualTo(2));
            Assert.That(c.Segments(15),Is.Empty);
        }
        [TestCase(1,23,20)][TestCase(6,23,24)][TestCase(7,27,20)][TestCase(8,25,18)]
        [TestCase(9,25,18)][TestCase(10,23,20)][TestCase(11,19,20)][TestCase(12,23,20)]
        public void DateControlsFutureActiveExpiredTagsAndBothTradeDirections(int day,int sale,int buy)
        {
            var s=Session();Advance(s,day);var d=catalog.Find("pill");
            Assert.That(s.Quote(d,TradeDirection.CustomerBuys).Amount,Is.EqualTo(sale));
            Assert.That(s.Quote(d,TradeDirection.CustomerSells).Amount,Is.EqualTo(buy));
            Assert.That(d.baseValue,Is.EqualTo(20));
            catalog.Find("herb").baseValue=20;
            Assert.That(s.Quote(catalog.Find("herb"),TradeDirection.CustomerBuys).Amount,Is.EqualTo(23));
            Assert.That(s.Quote(catalog.Find("herb"),TradeDirection.CustomerSells).Amount,Is.EqualTo(20));
        }
        [Test] public void OverlapBasketUsesSameFiftyForBudgetIncomeAndActualSale()
        {
            var s=Session();Advance(s,8);Assert.That(s.Move(s.Items[0].Id,ContainerId.Display,0,0,0,false));Assert.That(s.BeginBusiness());
            var snapshot=s.TodayAttraction;
            Assert.That(s.Move(s.Items[0].Id,ContainerId.Counter,0,0,0,false));
            Assert.That(s.Move(s.Items[1].Id,ContainerId.Counter,2,0,0,false));
            Assert.That(s.Move(s.Items[2].Id,ContainerId.Counter,0,2,0,false));
            Assert.That(s.CanAcceptTrade(out int total,out _),Is.True);Assert.That(total,Is.EqualTo(75));
            Assert.That(s.PreviewTrade().Shortfall,Is.EqualTo(25));Assert.That(s.AcceptTrade(),Is.False);
            Assert.That(s.Move(s.Items[2].Id,ContainerId.Storage,0,0,0,false));
            Assert.That(s.CanAcceptTrade(out total,out _));Assert.That(total,Is.EqualTo(50));
            var quote=s.Quote(s.Items[0]);Assert.That(quote.Modifiers,Does.Contain("+20%"));Assert.That(quote.Modifiers,Does.Contain("-10%"));
            int money=s.Money;Assert.That(s.AcceptTrade());Assert.That(s.Money,Is.EqualTo(money+50));Assert.That(s.IncomeToday,Is.EqualTo(50));
            Assert.That(s.Offer.RemainingBudget,Is.Zero);Assert.That(s.Items.Count,Is.EqualTo(1));Assert.That(s.AcceptTrade(),Is.False);
            Assert.That(s.TodayAttraction,Is.SameAs(snapshot));Assert.That(snapshot.DisplayValue,Is.EqualTo(20));
        }
        [Test] public void PurchaseHistoryCalendarAndRentSurviveJsonRestoreWithoutDuplicateEffects()
        {
            catalog.baseSupplierChance=1;foreach(var d in catalog.items)d.supplierAvailable=d.id=="pill";
            var s=Session();Advance(s,6);Assert.That(s.BeginBusiness());Assert.That(s.Offer.Price,Is.EqualTo(24));
            var bought=s.Offer.SupplierItem;Assert.That(s.Move(bought.Id,ContainerId.Counter,0,0,0,false));
            Assert.That(s.AcceptTrade());Assert.That(bought.PurchaseValue,Is.EqualTo(24));
            Assert.That(s.EndBusiness());Assert.That(s.Sleep());Assert.That(s.Day,Is.EqualTo(7));
            string json=s.CaptureSave();var restored=ShopSession.RestoreSave(catalog,json);
            Assert.That(restored.Money,Is.EqualTo(96));Assert.That(restored.Rent,Is.EqualTo(20));Assert.That(restored.Day,Is.EqualTo(7));
            Assert.That(restored.Find(bought.Id).PurchaseValue,Is.EqualTo(24));
            Assert.That(JsonUtility.ToJson(restored.Calendar.Capture()),Is.EqualTo(JsonUtility.ToJson(s.Calendar.Capture())));
            for(int i=0;i<3;i++)restored=ShopSession.RestoreSave(catalog,restored.CaptureSave());
            Assert.That(restored.Money,Is.EqualTo(96));Advance(restored,8);Advance(s,8);
            Assert.That(restored.Money,Is.EqualTo(76));Assert.That(restored.Rent,Is.EqualTo(21));
            Assert.That(restored.Quote(bought.Definition,TradeDirection.CustomerBuys).Amount,Is.EqualTo(25));
            Assert.That(restored.Find(bought.Id).PurchaseValue,Is.EqualTo(24));
            restored=ShopSession.RestoreSave(catalog,restored.CaptureSave());Assert.That(restored.Money,Is.EqualTo(76));
            Assert.That(restored.ProjectedRent(14),Is.EqualTo(21));Assert.That(restored.Sleep(),Is.False);Assert.That(restored.Money,Is.EqualTo(76));
            Assert.That(restored.BeginBusiness());Assert.That(s.BeginBusiness());
            Assert.That(restored.Offer.SupplierItem.Definition.id,Is.EqualTo(s.Offer.SupplierItem.Definition.id));
        }
        [Test] public void BrowsingAndSaveRestoreDoNotChargeRentAndUseActualPeriod()
        {
            catalog.rentPeriod=3;catalog.firstRent=12;var s=Session();
            Assert.That(s.ProjectedRent(3),Is.EqualTo(12));Assert.That(s.ProjectedRent(6),Is.EqualTo(13));Assert.That(s.ProjectedRent(7),Is.Null);
            for(int n=0;n<15;n++){s.Calendar.Segments(1);s.Calendar.Segments(15);}
            Assert.That(s.Money,Is.EqualTo(120));Assert.That(s.Day,Is.EqualTo(1));Advance(s,4);
            Assert.That(s.Money,Is.EqualTo(108));Assert.That(s.ProjectedRent(6),Is.EqualTo(13));Assert.That(s.ProjectedRent(3),Is.Null);
            s=ShopSession.RestoreSave(catalog,s.CaptureSave());Assert.That(s.Money,Is.EqualTo(108));
        }
        [Test] public void InvalidSaveAndMiddaySaveAreRejectedRatherThanLosingInventory()
        {
            var s=Session();var save=JsonUtility.FromJson<ShopSave>(s.CaptureSave());
            save.items[1].id=save.items[0].id;
            Assert.Throws<ArgumentException>(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(save)));
            Assert.That(s.Items.Count,Is.EqualTo(3));Assert.That(s.Money,Is.EqualTo(120));
            Assert.That(s.BeginBusiness());Assert.Throws<InvalidOperationException>(()=>s.CaptureSave());
        }
        [Test] public void RestoredCustomerRandomSequenceIsUnaffectedByCalendarBrowsing()
        {
            catalog.baseSupplierChance=.5f;catalog.marketEvents=MarketCalendar.PrototypeDefinitions();
            var a=new ShopSession(catalog,customerSeed:452);Advance(a,9);
            a.Calendar.Between(71,84);var b=ShopSession.RestoreSave(catalog,a.CaptureSave());
            b.Calendar.Between(140,160);
            for(int day=0;day<3;day++)
            {
                Assert.That(a.BeginBusiness());Assert.That(b.BeginBusiness());
                for(int i=0;i<5;i++)
                {
                    Assert.That(b.Offer.Direction,Is.EqualTo(a.Offer.Direction));
                    Assert.That(b.Offer.RequestedCategory,Is.EqualTo(a.Offer.RequestedCategory));
                    Assert.That(b.Offer.RemainingBudget,Is.EqualTo(a.Offer.RemainingBudget));
                    Assert.That(b.Offer.SupplierItem?.Definition.id,Is.EqualTo(a.Offer.SupplierItem?.Definition.id));
                    a.NextCustomer();b.NextCustomer();
                }
                a.EndBusiness();b.EndBusiness();a.Sleep();b.Sleep();
            }
        }
    }
}
