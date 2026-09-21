#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP50")]
        public IEnumerator GeneratedMixedCustomerUsesExistingNegotiationAndNetBudget()
        {
            yield return RestartWithTestCatalog(c=>{
                c.startingItems=new[]{"pill"};c.Find("pill").baseValue=10;c.retailMarkup=0;
                c.customers.buyingWeight=0;c.customers.sellingWeight=0;c.customers.tradingWeight=1;
                c.customers.displayedItemWeight=1000000;
                c.customers.ordinaryWeight=1;c.customers.wealthyWeight=0;c.customers.lavishWeight=0;
                c.customers.oneSupplyWeight=1;c.customers.twoSuppliesWeight=0;
                c.customers.supplyPools=new[]{new CustomerSupplyPool{category=ItemCategory.Material,itemIds=new[]{"dew"}}};
            },50);
            var s=shop.Session;var pill=s.Items.Single();
            yield return Drag(pill,ContainerId.Display,0,0);yield return Click("BeginBusiness");
            Assert.That(s.Offer.Behavior,Is.EqualTo(CustomerBehavior.Trading));Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));
            Assert.That(s.Offer.RemainingBudget,Is.EqualTo(45));var supply=s.Offer.SupplierItems.Single();
            yield return Drag(pill,ContainerId.Counter,0,0);yield return Click("NegotiationOpen");
            var quote=s.PreviewTrade(true);Assert.That(quote.Lines.Count,Is.EqualTo(2));Assert.That(quote.Net,Is.EqualTo(8));
            yield return Click("NegotiationConfirm");
            Assert.That(s.Money,Is.EqualTo(128));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(37));
            Assert.That(supply.Owner,Is.EqualTo(ItemOwner.Player));Assert.That(s.Find(pill.Id),Is.Null);
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP50")]
        public IEnumerator SellingOnlyCustomerHasNoDemandAndPurchasingDoesNotCreateBudget()
        {
            yield return RestartWithTestCatalog(c=>{
                c.customers.buyingWeight=0;c.customers.sellingWeight=1;c.customers.tradingWeight=0;
                c.customers.oneSupplyWeight=1;c.customers.twoSuppliesWeight=0;
                c.customers.supplyPools=new[]{new CustomerSupplyPool{category=ItemCategory.Material,itemIds=new[]{"dew"}}};
            },51);
            var s=shop.Session;yield return Click("BeginBusiness");int money=s.Money;
            Assert.That(s.Offer.Behavior,Is.EqualTo(CustomerBehavior.Selling));Assert.That(s.Offer.RemainingBudget,Is.Zero);
            yield return Click("NegotiationOpen");
            Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("只出售，无求购计划")));
            yield return Click("NegotiationConfirm");Assert.That(s.Money,Is.LessThan(money));Assert.That(s.Offer.RemainingBudget,Is.Zero);
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP50"),Category("DP27")]
        public IEnumerator PromotionAndOverflowCreateSevenVisitorsOnce()
        {
            yield return RestartWithTestCatalog(c=>{
                foreach(var e in c.teaHouse.effects)e.weight=e.effect==TeaEffect.Promotion?1:0;
                c.firstLocationStaminaCost=0;
            },52);
            var s=shop.Session;
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_tingfeng-teahouse");yield return Click("TravelLeave");
            yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            yield return Click("AdvanceTurn");
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(7));yield return Click("BeginBusiness");
            var offer=s.Offer;var snapshot=s.TodayAttraction;
            for(int i=0;i<3;i++){shop.Refresh();Assert.That(s.BeginBusiness(),Is.False);}
            Assert.That(s.Offer,Is.SameAs(offer));Assert.That(s.TodayAttraction,Is.SameAs(snapshot));Assert.That(s.RemainingCustomers,Is.EqualTo(6));
            for(int i=0;i<7;i++)yield return Click("NextCustomer");
            Assert.That(s.Offer,Is.Null);Assert.That(s.ServedToday,Is.EqualTo(7));LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
