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
        [UnityTest,Category("DP45")] public IEnumerator ZeroStaminaCanOpenAndMonthlyOverflowShowsExactlySixCustomers()
        {
            yield return RestartWithTestCatalog(c=>{},45);
            var s=shop.Session;Assert.That(s.TrySpendStamina(100));shop.Refresh();
            Assert.That(shop.FindButton("BeginBusiness").interactable);
            yield return Click("BeginBusiness");yield return Click("EndBusiness");yield return Click("AdvanceTurn");
            Assert.That(s.Stamina,Is.EqualTo(30));
            for(int n=0;n<3;n++){yield return Click("BeginBusiness");yield return Click("EndBusiness");yield return Click("AdvanceTurn");}
            Assert.That(s.Stamina,Is.EqualTo(100));Assert.That(s.CustomerCountThisTurn,Is.EqualTo(6));
            for(int n=0;n<3;n++){shop.Refresh();shop.CalendarView.Open();shop.CalendarView.Close();}
            Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("本月溢出客流 +1")),Is.True);
            yield return Click("BeginBusiness");Assert.That(s.BuyersToday+s.SuppliersToday+s.TradingCustomersToday,Is.EqualTo(6));
            for(int n=0;n<6;n++)yield return Click("NextCustomer");
            Assert.That(s.Offer,Is.Null);Assert.That(s.ServedToday,Is.EqualTo(6));Assert.That(s.Stamina,Is.EqualTo(100));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
