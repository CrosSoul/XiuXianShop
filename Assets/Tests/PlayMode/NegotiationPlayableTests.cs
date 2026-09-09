#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP23")] public IEnumerator ConcessionDialogCancelsInvalidatesAndSettlesOnce()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=new[]{"pill","pill"};c.retailMarkup=0;c.Find("pill").baseValue=25;
                c.baseSupplierChance=0;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetVariation=0;c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=18}};
            },17);
            var s=shop.Session;yield return Drag(s.Items[0],ContainerId.Display,0,0);yield return Click("BeginBusiness");
            var item=s.Items[1];yield return Drag(item,ContainerId.Counter,0,0);yield return Click("NegotiationOpen");
            yield return Click("NegotiationConfirm");Assert.That(CalendarText("ConcessionText"),Does.Contain("少收7灵石"));
            Assert.That(s.Money,Is.EqualTo(120));yield return Click("ConcessionCancel");Assert.That(item.Owner,Is.EqualTo(ItemOwner.Player));
            yield return Click("NegotiationConfirm");s.SetPriceTag(new PriceTag{id="refresh",title="重新报价",percent=.2f});
            yield return null;yield return null;
            Assert.That(shop.FindButton("ConcessionContinue").gameObject.activeInHierarchy,Is.False);
            shop.FindButton("ConcessionContinue").onClick.Invoke();Assert.That(s.Money,Is.EqualTo(120));
            s.RemovePriceTag("refresh");yield return null;yield return null;
            yield return Click("NegotiationConfirm");yield return Click("ConcessionContinue");
            Assert.That(s.Money,Is.EqualTo(138));Assert.That(s.IncomeToday,Is.EqualTo(18));Assert.That(s.Offer.RemainingBudget,Is.Zero);
            Assert.That(s.Find(item.Id),Is.Null);Assert.That(shop.NegotiationView.IsOpen,Is.False);
            shop.FindButton("ConcessionContinue").onClick.Invoke();Assert.That(s.Money,Is.EqualTo(138));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP21")] public IEnumerator ManualSelectionOpensMixedModalAndEntryIncludesAllWithoutDuplicates()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=new[]{"pill","pill"};c.retailMarkup=0;c.Find("pill").baseValue=10;
                c.baseSupplierChance=1;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetVariation=0;c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=100}};
                foreach(var d in c.items)d.supplierAvailable=d.id=="dew";
                c.Find("dew").baseValue=15;
            },17);
            var s=shop.Session;yield return Drag(s.Items[0],ContainerId.Display,0,0);yield return Click("BeginBusiness");
            var own=s.Items[1];yield return Drag(own,ContainerId.Counter,0,0);
            var offer=s.Offer;var goods=offer.SupplierItems.ToArray();int money=s.Money;
            yield return Click("NegotiationOpen");
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("-20"));Assert.That(CalendarText("NegotiationTotal"),Does.Contain("3 件"));
            yield return Click("NegotiationClose");Assert.That(s.Money,Is.EqualTo(money));Assert.That(goods.All(i=>i.ForSale));
            yield return Drag(goods[1],ContainerId.Counter,2,0);Assert.That(shop.NegotiationView.IsOpen);
            Assert.That(CalendarText("NegotiationLines"),Does.Contain("卖出 +10"));Assert.That(CalendarText("NegotiationLines"),Does.Contain("买入 −15"));
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("-5"));Assert.That(CalendarText("NegotiationTotal"),Does.Contain("2 件"));
            s.SetPriceTag(new PriceTag{id="ui-rise",title="涨价",percent=.2f});yield return null;yield return null;
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("-6"));Assert.That(CalendarText("NegotiationLines"),Does.Contain("买入 −18"));
            yield return Click("NegotiationConfirm");Assert.That(s.Money,Is.EqualTo(money-6));
            Assert.That(goods[1].ForSale,Is.False);Assert.That(goods[1].PurchaseValue,Is.EqualTo(18));Assert.That(goods[0].ForSale);
            Assert.That(s.Offer,Is.SameAs(offer));Assert.That(offer.RemainingBudget,Is.EqualTo(100));
            shop.FindButton("NegotiationConfirm").onClick.Invoke();Assert.That(s.Money,Is.EqualTo(money-6),"A duplicate callback on a closed modal cannot buy more goods.");
            yield return Click("NegotiationOpen");Assert.That(CalendarText("NegotiationTotal"),Does.Contain("-18"));
            yield return Click("NegotiationConfirm");Assert.That(s.Purchases,Is.EqualTo(2));Assert.That(s.Money,Is.EqualTo(money-24));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP22")] public IEnumerator FiveCustomerDayRunsPureMixedZeroNetFailuresAndContinuousTrade()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=new[]{"pill","pill","pill","pill","pill","jade"};c.retailMarkup=0;c.Find("pill").baseValue=10;
                c.baseSupplierChance=1;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetVariation=0;c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=100}};
                foreach(var d in c.items)d.supplierAvailable=d.id=="dew";c.Find("dew").baseValue=15;
            },17);
            var s=shop.Session;var own=s.Items.Where(i=>i.Definition.id=="pill").ToArray();var jade=s.Items.Single(i=>i.Definition.id=="jade");
            yield return Drag(own[0],ContainerId.Display,0,0);yield return Click("BeginBusiness");var snapshot=s.TodayAttraction;
            // Visitor 1: one manually selected purchase, followed by explicit departure.
            var first=s.Offer.SupplierItem;yield return Drag(first,ContainerId.Counter,0,0);
            Assert.That(shop.NegotiationView.IsOpen);yield return Click("NegotiationConfirm");Assert.That(s.Money,Is.EqualTo(105));
            yield return Click("NextCustomer");Assert.That(s.Find(first.Id),Is.SameAs(first));
            // Visitor 2: choose only the player's sale from the default all-goods request.
            yield return Drag(own[1],ContainerId.Counter,0,0);yield return Click("NegotiationOpen");yield return Click("NegotiationSelection");
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("+10"));yield return Click("NegotiationConfirm");Assert.That(s.Money,Is.EqualTo(115));
            yield return Click("NextCustomer");
            // Visitor 3: default all request mixes 1 sale and 2 purchases, then another sale to the same visitor.
            var current=s.Offer;yield return Drag(own[2],ContainerId.Counter,0,0);yield return Click("NegotiationOpen");
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("-20"));yield return Click("NegotiationConfirm");Assert.That(s.Money,Is.EqualTo(95));
            yield return Drag(own[3],ContainerId.Counter,0,0);yield return Click("NegotiationOpen");yield return Click("NegotiationConfirm");
            Assert.That(s.Offer,Is.SameAs(current));Assert.That(s.Money,Is.EqualTo(105));Assert.That(current.RemainingBudget,Is.EqualTo(90));
            yield return Click("NextCustomer");
            // Visitor 4: invalid category and insufficient net funds, neither operation changes anything.
            yield return Drag(jade,ContainerId.Counter,0,0);yield return Click("NegotiationOpen");
            Assert.That(shop.FindButton("NegotiationConfirm").interactable,Is.False);Assert.That(CalendarText("NegotiationStatus"),Does.Contain("类别不符"));
            yield return Click("NegotiationClose");yield return Drag(jade,ContainerId.Display,2,0);
            s.SetPriceTag(new PriceTag{id="unaffordable",title="高价来货",percent=10,playerSells=false});yield return null;yield return null;
            yield return Click("NegotiationOpen");Assert.That(shop.FindButton("NegotiationConfirm").interactable,Is.False);
            Assert.That(CalendarText("NegotiationStatus"),Does.Contain("灵石不足"));yield return Click("NegotiationClose");
            Assert.That(s.Money,Is.EqualTo(105));s.RemovePriceTag("unaffordable");yield return Click("NextCustomer");
            // Visitor 5: 30 sale offsets 2 x 15 purchases. No cash changes hands.
            s.SetPriceTag(new PriceTag{id="zero-net",title="丹药需求",percent=2,category=ItemCategory.Medicine,playerBuys=false});
            yield return Drag(own[4],ContainerId.Counter,0,0);yield return Click("NegotiationOpen");
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("无需收付"));yield return Click("NegotiationConfirm");
            Assert.That(s.Money,Is.EqualTo(105));Assert.That(s.TodayAttraction,Is.SameAs(snapshot));
            yield return Click("NextCustomer");Assert.That(s.ServedToday,Is.EqualTo(5));Assert.That(s.Offer,Is.Null);
            yield return Click("EndBusiness");Assert.That(s.IncomeToday,Is.EqualTo(60));Assert.That(s.ExpensesToday,Is.EqualTo(75));
            Assert.That(s.BalanceChange,Is.EqualTo(-15));int count=s.Items.Count;
            yield return Click("Sleep");Assert.That(s.Day,Is.EqualTo(2));Assert.That(s.Items.Count,Is.EqualTo(count));Assert.That(s.Money,Is.EqualTo(105));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP20")] public IEnumerator CustomerGoodsUseIndependentCounterAndKeepOwnershipWhenMoved()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=new[]{"pill"};c.baseSupplierChance=1;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                foreach(var d in c.items)d.supplierAvailable=d.id=="pill";
            },17);
            var s=shop.Session;var own=s.Items[0];yield return Drag(own,ContainerId.Display,0,0);
            yield return Click("BeginBusiness");var offer=s.Offer;
            Assert.That(offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));Assert.That(offer.RemainingBudget,Is.GreaterThan(0));
            Assert.That(s.In(ContainerId.CustomerCounter).Count(),Is.EqualTo(2));Assert.That(s.In(ContainerId.Counter),Is.Empty);
            var goods=offer.SupplierItems[0];yield return Drag(goods,ContainerId.Counter,0,0);
            if(shop.NegotiationView.IsOpen)yield return Click("NegotiationClose");
            Assert.That(goods.ForSale);Assert.That(goods.Owner,Is.EqualTo(ItemOwner.Customer));
            yield return Drag(goods,ContainerId.Storage,0,0);Assert.That(goods.Container,Is.EqualTo(ContainerId.Counter));
            yield return Drag(goods,ContainerId.CustomerCounter,0,0);Assert.That(goods.Container,Is.EqualTo(ContainerId.CustomerCounter));
            Assert.That(own.ForSale,Is.False);Assert.That(s.Money,Is.EqualTo(120));
            yield return Click("NextCustomer");Assert.That(s.Find(goods.Id),Is.Null);Assert.That(s.Find(own.Id),Is.SameAs(own));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP19")] public IEnumerator NegotiationEntryCancelsAndConfirmsSaleWithoutChangingCustomer()
        {
            yield return CalendarFixture(1);
            var session=shop.Session;
            yield return Drag(session.Items[0],ContainerId.Display,0,0);
            yield return Click("BeginBusiness");var customer=session.Offer;
            var sold=session.Items[1];yield return Drag(sold,ContainerId.Counter,0,0);
            int money=session.Money;
            yield return Click("NegotiationOpen");
            Assert.That(shop.NegotiationView.IsOpen);
            Assert.That(CalendarText("NegotiationLines"),Does.Contain("卖出 +23"));
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("+23"));
            yield return Click("NextCustomer");Assert.That(session.Offer,Is.SameAs(customer),"Modal blocks underlying buttons.");
            yield return Click("NegotiationClose");
            Assert.That(session.Money,Is.EqualTo(money));Assert.That(sold.Owner,Is.EqualTo(ItemOwner.Player));
            yield return Click("NegotiationOpen");yield return Click("NegotiationConfirm");
            Assert.That(shop.NegotiationView.IsOpen,Is.False);
            Assert.That(session.Money,Is.EqualTo(money+23));Assert.That(session.Find(sold.Id),Is.Null);
            Assert.That(session.Offer,Is.SameAs(customer));Assert.That(customer.RemainingBudget,Is.EqualTo(27));
            yield return Click("NegotiationOpen");Assert.That(shop.FindButton("NegotiationConfirm").interactable,Is.False);
            yield return Click("NegotiationClose");
            Assert.That(session.Money,Is.EqualTo(money+23));Assert.That(session.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
