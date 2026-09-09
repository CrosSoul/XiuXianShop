using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class ShopPricingTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();
            catalog.Find("pill").baseValue=20;
            catalog.startingItems=new[]{"pill","pill","pill"};
            catalog.baseSupplierChance=0;catalog.advertisementSupplierBonus=0;catalog.displayedGoodsBuyerBonus=0;
            catalog.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=60}};catalog.buyerBudgetVariation=0;
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(catalog);
        static PriceTag Discount(float value=-.3f)=>new PriceTag{id="test-market",title="测试降价",percent=value};
        static void Stage(ShopSession s,GridItem item,int x=0)=>Assert.That(s.Move(item.Id,ContainerId.Counter,x,0,0,false));
        static void OpenBuyer(ShopSession s)
        {Assert.That(s.Move(s.Items[0].Id,ContainerId.Display,0,0,0,false));Assert.That(s.BeginBusiness());}

        [Test] public void WarehouseBaseAndSaleMarkupUseDifferentContexts()
        {
            var s=new ShopSession(catalog);var item=s.Items[0];
            Assert.That(s.Estimate(item).Amount,Is.EqualTo(20));Assert.That(item.PurchaseValue,Is.Null);
            Stage(s,item);Assert.That(s.Estimate(item).Amount,Is.EqualTo(23));
            Assert.That(s.Estimate(item).Modifiers,Does.Contain("+15%"));Assert.That(item.Definition.baseValue,Is.EqualTo(20));
        }
        [Test] public void ModifiersAddAndReplaceRatherThanMultiplyOrStackDuplicateIds()
        {
            var s=new ShopSession(catalog);var item=s.Items[0];Stage(s,item);
            Assert.That(s.SetPriceTag(Discount()));Assert.That(s.Quote(item).RawValue,Is.EqualTo(17m));
            Assert.That(s.Quote(item).Amount,Is.EqualTo(17));
            s.SetPriceTag(Discount());Assert.That(s.Quote(item).RawValue,Is.EqualTo(17m));
            s.RemovePriceTag("test-market");Assert.That(s.Quote(item).Amount,Is.EqualTo(23));
            Assert.That(item.Definition.baseValue,Is.EqualTo(20));
        }
        [Test] public void EveryBasketLineUsesLatestQuoteForValidationAndMoney()
        {
            var s=new ShopSession(catalog);OpenBuyer(s);var basket=s.Items.ToArray();
            Stage(s,basket[0]);Stage(s,basket[1],2);
            Assert.That(s.Move(basket[2].Id,ContainerId.Counter,0,2,0,false));
            Assert.That(s.CanAcceptTrade(out int total,out _),Is.False);Assert.That(total,Is.EqualTo(69));
            s.SetPriceTag(Discount());Assert.That(s.CanAcceptTrade(out total,out _));Assert.That(total,Is.EqualTo(51));
            Assert.That(s.AcceptTrade());Assert.That(s.Offer.RemainingBudget,Is.EqualTo(9));Assert.That(s.Money,Is.EqualTo(171));
            Assert.That(s.IncomeToday,Is.EqualTo(51));Assert.That(s.Items,Is.Empty);
            Assert.That(s.AcceptTrade(),Is.False);Assert.That(s.Money,Is.EqualTo(171));
        }
        [Test] public void ActualPurchaseValueIsSixteenAndDoesNotChangeWithTagsOrLocation()
        {
            catalog.startingItems=new string[0];catalog.baseSupplierChance=1;
            foreach(var d in catalog.items)d.supplierAvailable=d.id=="pill";
            var s=new ShopSession(catalog);s.SetPriceTag(Discount(-.2f));Assert.That(s.BeginBusiness());
            var item=s.Offer.SupplierItem;
            Assert.That(s.Offer.Price,Is.EqualTo(16));Stage(s,item);Assert.That(s.AcceptTrade());
            Assert.That(item.PurchaseValue,Is.EqualTo(16));Assert.That(s.Money,Is.EqualTo(104));
            Assert.That(s.Estimate(item).Amount,Is.EqualTo(20));s.RemovePriceTag("test-market");Stage(s,item);
            Assert.That(s.Estimate(item).Amount,Is.EqualTo(23));Assert.That(item.PurchaseValue,Is.EqualTo(16));
            Assert.That(s.AcceptTrade(),Is.False);Assert.That(s.ExpensesToday,Is.EqualTo(16));
        }
        [Test] public void DisplaySnapshotUsesBaseValuesAndDoesNotRecalculateFromQuotes()
        {
            var s=new ShopSession(catalog);OpenBuyer(s);var snapshot=s.TodayAttraction;
            s.SetPriceTag(Discount(-.8f));Assert.That(s.PreviewAttraction().DisplayValue,Is.EqualTo(20));
            Stage(s,s.Items[0]);Assert.That(s.TodayAttraction,Is.SameAs(snapshot));Assert.That(snapshot.DisplayValue,Is.EqualTo(20));
        }
        [Test] public void LowerBoundRoundingAndDirectionalTagsAreExplicit()
        {
            var s=new ShopSession(catalog);var item=s.Items[0];
            s.SetPriceTag(Discount(-2));Assert.That(s.Quote(item).RawValue,Is.EqualTo(-17m));Assert.That(s.Quote(item).Amount,Is.EqualTo(1));
            s.SetPriceTag(new PriceTag{id="test-market",title="收购专用",percent=-.2f,playerSells=false});
            Assert.That(s.Quote(item).Amount,Is.EqualTo(23));
            Assert.That(s.Quote(item.Definition,TradeDirection.CustomerSells).Amount,Is.EqualTo(16));
            s.RemovePriceTag("test-market");catalog.Find("pill").baseValue=10;
            Assert.That(s.Quote(item).RawValue,Is.EqualTo(11.5m));Assert.That(s.Quote(item).Amount,Is.EqualTo(12));
        }
        [Test] public void SettlementIncludesIncomeExpensesAndResetsOnlyOnNextDay()
        {
            var s=new ShopSession(catalog);OpenBuyer(s);Stage(s,s.Items[1]);s.AcceptTrade();
            Assert.That(s.EndBusiness());Assert.That(s.IncomeToday,Is.EqualTo(23));
            Assert.That(s.ExpensesToday,Is.Zero);Assert.That(s.BalanceChange,Is.EqualTo(23));Assert.That(s.OpeningMoney,Is.EqualTo(120));
            int count=s.Items.Count;Assert.That(s.Sleep());Assert.That(s.Day,Is.EqualTo(2));Assert.That(s.OpeningMoney,Is.EqualTo(143));
            Assert.That(s.IncomeToday,Is.Zero);Assert.That(s.BalanceChange,Is.Zero);Assert.That(s.Items.Count,Is.EqualTo(count));
        }
        [Test] public void FullCounterSupplierCanBeSkippedWithoutFreeItemsOrStuckQueue()
        {
            catalog.startingItems=Enumerable.Repeat("dew",20).ToArray();catalog.baseSupplierChance=1;
            foreach(var d in catalog.items)d.supplierAvailable=d.id=="pill";
            var s=new ShopSession(catalog);
            for(int i=0;i<20;i++)Assert.That(s.Move(s.Items[i].Id,ContainerId.Counter,i%5,i/5,0,false));
            Assert.That(s.BeginBusiness());Assert.That(s.Offer,Is.Not.Null);Assert.That(s.RemainingCustomers,Is.EqualTo(4));
            Assert.That(s.In(ContainerId.CustomerCounter).Count(),Is.EqualTo(2));
            Assert.That(s.CanMove(s.Offer.ItemId,ContainerId.Counter,0,0,0,false,out string reason),Is.False);Assert.That(reason,Does.Contain("放不下"));
            Assert.That(s.NextCustomer());Assert.That(s.RemainingCustomers,Is.EqualTo(3));Assert.That(s.Money,Is.EqualTo(120));
            Assert.That(s.Items.Count(i=>i.Owner==ItemOwner.Player),Is.EqualTo(20));Assert.That(s.ValidateState(),Is.Null);
        }
    }
}
