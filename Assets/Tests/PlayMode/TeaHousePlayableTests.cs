#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP48"),Category("DP27")]
        public IEnumerator TeaVisitReopensSameNewsAndPromotionChangesOnlyNextBusiness()
        {
            yield return RestartWithTestCatalog(c=>
            {foreach(var e in c.teaHouse.effects)e.weight=e.effect==TeaEffect.Promotion?1:0;},48);
            var s=shop.Session;
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_tingfeng-teahouse");var result=s.LatestTeaVisit;
            Assert.That(result.effect,Is.EqualTo(TeaEffect.Promotion));Assert.That(s.Stamina,Is.EqualTo(40));
            for(int i=0;i<2;i++)
            {
                yield return Click("TeaLocationNews");Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("第 2 回合营业：宣传店铺")));
                yield return Click("TeaNewsClose");Assert.That(s.LatestTeaVisit,Is.SameAs(result));Assert.That(s.Stamina,Is.EqualTo(40));
            }
            yield return Click("TravelLeave");yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            yield return Click("TeaNewsOpen");yield return Click("TeaNewsClose");Assert.That(s.LatestTeaVisit,Is.SameAs(result));
            Assert.That(s.BuyersToday+s.SuppliersToday+s.TradingCustomersToday,Is.EqualTo(5));
            yield return Click("AdvanceTurn");
            Assert.That(shop.FindButton("TeaNewsOpen").GetComponentInChildren<Text>().text,Does.Contain("本月加成"));
            yield return Click("BeginBusiness");Assert.That(s.BuyersToday+s.SuppliersToday+s.TradingCustomersToday,Is.EqualTo(6));
            for(int i=0;i<6;i++)yield return Click("NextCustomer");Assert.That(s.Offer,Is.Null);Assert.That(s.ServedToday,Is.EqualTo(6));
            yield return Click("EndBusiness");yield return Click("AdvanceTurn");Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP48"),Category("DP27")]
        public IEnumerator SecretIsVisibleInCalendarBeforeStartWithoutApplyingItsPrice()
        {
            yield return RestartWithTestCatalog(c=>
            {
                foreach(var e in c.teaHouse.effects)e.weight=e.effect==TeaEffect.MarketSecret?1000000:e.effect==TeaEffect.Promotion?1:0;
                c.marketEvents=MarketCalendar.PrototypeDefinitions();
            },48);
            var s=shop.Session;var before=s.Calendar.ActiveTags(1).Select(t=>t.id).ToArray();
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");yield return Click("TravelLocation_tingfeng-teahouse");
            Assert.That(s.LatestTeaVisit.effect,Is.EqualTo(TeaEffect.MarketSecret));var secret=s.Calendar.DisclosedEvents.Single();
            Assert.That(secret.startTurn,Is.InRange(3,13));Assert.That(s.Calendar.ActiveTags(1).Select(t=>t.id),Is.EqualTo(before));
            yield return Click("TeaLocationNews");Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(secret.title)),Is.True);yield return Click("TeaNewsClose");
            yield return Click("TravelLeave");yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            yield return Click("CalendarOpen");Assert.That(shop.CalendarView.IsOpen,Is.True,"Calendar button must open the view");shop.CalendarView.Show(secret.startTurn);yield return null;
            var bar=shop.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("MarketBar_"+secret.id+"_"));
            yield return Click(bar.name);Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.name=="CalendarEventDetails" && t.text.Contains("未开始") && t.text.Contains(secret.title)),Is.True);
            yield return Click("CalendarClose");yield return Click("TeaNewsOpen");yield return Click("TeaNewsClose");
            Assert.That(s.Calendar.DisclosedEvents.Single().id,Is.EqualTo(secret.id));Assert.That(s.Stamina,Is.EqualTo(40));LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
