using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP21")]
    public sealed class NegotiationTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();
            catalog.startingItems=new[]{"pill"};catalog.retailMarkup=0;
            catalog.baseSupplierChance=1;catalog.advertisementSupplierBonus=0;catalog.displayedGoodsBuyerBonus=0;
            catalog.buyerBudgetVariation=0;catalog.marketEvents=Array.Empty<MarketEventDefinition>();
            foreach(var d in catalog.items){d.cells=new[]{Vector2Int.zero};d.supplierAvailable=d.id=="herb" || d.id=="dew";}
            catalog.Find("pill").baseValue=10;catalog.Find("herb").baseValue=15;catalog.Find("dew").baseValue=20;
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        ShopSession Session(int cash=120,int budget=60)
        {
            catalog.startingMoney=cash;catalog.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=budget}};
            for(int seed=0;seed<100;seed++)
            {
                var s=new ShopSession(catalog,customerSeed:seed);
                Assert.That(s.Move(s.Items[0].Id,ContainerId.Display,0,0,0,false));Assert.That(s.BeginBusiness());
                if(s.Offer.SupplierItems.Select(i=>i.Definition.id).Distinct().Count()==2)return s;
            }
            throw new Exception("No deterministic two-definition fixture found.");
        }
        static string Snapshot(ShopSession s)=>$"{s.Money}/{s.Offer?.RemainingBudget}/{s.Sales}/{s.Purchases}/{s.IncomeToday}/{s.ExpensesToday}|"+
            string.Join(";",s.Items.Select(i=>$"{i.Id}:{i.Owner}:{i.Container}:{i.X}:{i.Y}:{i.Rotation}:{i.Flipped}:{i.PurchaseValue}"));
        static GridItem StageOwn(ShopSession s)
        {var item=s.Items.First(i=>!i.ForSale && i.Definition.id=="pill");Assert.That(s.Move(item.Id,ContainerId.Counter,0,0,0,false));return item;}

        [Test,Category("DP23")] public void FinalNetRatherThanSaleSubtotalConsumesCustomerBudget()
        {
            catalog.Find("pill").baseValue=21;catalog.Find("herb").baseValue=1;catalog.Find("dew").baseValue=2;
            var s=Session(120,18);StageOwn(s);
            Assert.That(s.PreviewTrade(true).Net,Is.EqualTo(18));Assert.That(s.AcceptTrade(true),Is.True,s.Message);
            Assert.That(s.Money,Is.EqualTo(138));Assert.That(s.Offer.RemainingBudget,Is.Zero);
        }
        [TestCase(18,7)][TestCase(0,25)][Category("DP23")]
        public void ConcessionNeedsCurrentExplicitApprovalAndRecordsOnlyActualIncome(int budget,int discount)
        {
            catalog.startingItems=new[]{"pill","pill"};catalog.Find("pill").baseValue=25;var s=Session(120,Math.Max(1,budget));
            if(budget==0)
            {
                catalog.Find("pill").baseValue=1;StageOwn(s);Assert.That(s.AcceptTrade());
                catalog.Find("pill").baseValue=25;
            }
            int cash=s.Money,income=s.IncomeToday;var item=StageOwn(s);string before=Snapshot(s);
            var quote=s.PreviewTrade();Assert.That(quote.CanConfirm);Assert.That(quote.Shortfall,Is.EqualTo(discount));
            Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.AcceptTrade(false,quote));Assert.That(s.Money,Is.EqualTo(cash+budget));
            Assert.That(s.IncomeToday,Is.EqualTo(income+budget));Assert.That(s.Offer.RemainingBudget,Is.Zero);
            Assert.That(item.Definition.baseValue,Is.EqualTo(25));Assert.That(s.LastCustomerResult,Does.Contain($"少收 {discount}"));
            before=Snapshot(s);Assert.That(s.AcceptTrade(false,quote),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
        }
        [Test,Category("DP23")] public void PriceOrBasketChangesInvalidateConcessionApproval()
        {
            catalog.Find("pill").baseValue=25;var s=Session(120,18);var item=StageOwn(s);var approval=s.PreviewTrade();
            s.SetPriceTag(new PriceTag{id="changed",title="变化",percent=.2f});var before=Snapshot(s);
            Assert.That(s.AcceptTrade(false,approval),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            approval=s.PreviewTrade();Assert.That(s.Move(item.Id,ContainerId.Counter,1,0,0,false));before=Snapshot(s);
            Assert.That(s.AcceptTrade(false,approval),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
        }

        [TestCase(10,25,-25,0)][TestCase(35,0,0,0)][TestCase(45,0,10,10)]
        public void MixedPositiveNegativeAndZeroNetSettleAtomically(int sale,int cash,int net,int balance)
        {
            catalog.Find("pill").baseValue=sale;var s=Session(cash,sale);var offer=s.Offer;var sold=StageOwn(s);
            var purchased=offer.SupplierItems.ToArray();string before=Snapshot(s);
            for(int i=0;i<3;i++){var q=s.PreviewTrade(true);Assert.That(q.CanConfirm,Is.True,q.Reason);Assert.That(q.Net,Is.EqualTo(net));Assert.That(q.Lines.Count,Is.EqualTo(3));}
            Assert.That(Snapshot(s),Is.EqualTo(before),"Opening and re-opening do not transact.");
            Assert.That(s.AcceptTrade(true),Is.True,s.Message);Assert.That(s.Money,Is.EqualTo(balance));
            Assert.That(s.IncomeToday,Is.EqualTo(sale));Assert.That(s.ExpensesToday,Is.EqualTo(35));Assert.That(offer.RemainingBudget,Is.EqualTo(sale-Math.Max(0,net)));
            Assert.That(s.Find(sold.Id),Is.Null);Assert.That(sold.Owner,Is.EqualTo(ItemOwner.Customer));
            foreach(var item in purchased){Assert.That(item.ForSale,Is.False);Assert.That(item.Container,Is.EqualTo(ContainerId.Storage));Assert.That(item.PurchaseValue,Is.EqualTo(item.Definition.baseValue));}
            Assert.That(s.Offer,Is.SameAs(offer));Assert.That(s.ServedToday,Is.Zero);Assert.That(s.ValidateState(),Is.Null);
            before=Snapshot(s);Assert.That(s.AcceptTrade(true),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
        }
        [TestCase(24,10,"灵石不足")]
        public void NetPlayerFundsGateTheWholeTrade(int cash,int budget,string message)
        {
            var s=Session(cash,budget);StageOwn(s);string before=Snapshot(s);
            Assert.That(s.PreviewTrade(true).Net,Is.EqualTo(-25));Assert.That(s.AcceptTrade(true),Is.False);
            Assert.That(s.Message,Does.Contain(message));Assert.That(Snapshot(s),Is.EqualTo(before));
        }
        [Test] public void ManualSelectionOverridesAllAndOnlyTheSelectedCopyIsBought()
        {
            var s=Session();var offer=s.Offer;var selected=offer.SupplierItems.Last();var remaining=offer.SupplierItems.First();StageOwn(s);
            Assert.That(s.Move(selected.Id,ContainerId.Counter,1,0,0,false));
            Assert.That(s.PreviewTrade(true).Lines.Count,Is.EqualTo(2));Assert.That(s.PreviewTrade().Lines.Count,Is.EqualTo(2));
            Assert.That(s.AcceptTrade(true));Assert.That(selected.ForSale,Is.False);Assert.That(remaining.ForSale);
            Assert.That(s.Find(remaining.Id),Is.SameAs(remaining));Assert.That(s.Offer,Is.SameAs(offer));
            Assert.That(s.AcceptTrade(true));Assert.That(remaining.ForSale,Is.False);Assert.That(s.Purchases,Is.EqualTo(2));
            Assert.That(s.Money,Is.EqualTo(95));Assert.That(s.ValidateState(),Is.Null);
        }
        [Test] public void PureSaleDoesNotRequireBuyingCustomerGoods()
        {
            var s=Session();StageOwn(s);Assert.That(s.AcceptTrade());Assert.That(s.Money,Is.EqualTo(130));
            Assert.That(s.Offer.SupplierItems.Count,Is.EqualTo(2));Assert.That(s.Purchases,Is.Zero);
            Assert.That(s.Offer.RemainingBudget,Is.EqualTo(50));
        }
        [Test] public void AllPurchaseUsesCombinedSpaceAndFailureDoesNotBuyTheFirstItem()
        {
            catalog.startingItems=new[]{"pill"}.Concat(Enumerable.Repeat("dew",69)).ToArray();
            var s=Session();string before=Snapshot(s);
            Assert.That(s.AcceptTrade(true),Is.False);Assert.That(s.Message,Does.Contain("空间不足"));Assert.That(Snapshot(s),Is.EqualTo(before));
            var freed=s.In(ContainerId.Storage).First();Assert.That(s.Move(freed.Id,ContainerId.Display,1,0,0,false));
            Assert.That(s.AcceptTrade(true),Is.True,s.Message);Assert.That(s.Purchases,Is.EqualTo(2));Assert.That(s.ValidateState(),Is.Null);
        }
        [Test] public void WrongCategoryAndSignArePlacedButBlockMixedSettlement()
        {
            var s=Session();var own=StageOwn(s);own.Definition.category=ItemCategory.Equipment;
            string before=Snapshot(s);Assert.That(s.AcceptTrade(true),Is.False);Assert.That(s.Message,Does.Contain("类别不符"));Assert.That(Snapshot(s),Is.EqualTo(before));
            own.Definition.category=ItemCategory.Medicine;own.Definition.procurementSign=true;
            Assert.That(s.AcceptTrade(true),Is.False);Assert.That(s.Message,Does.Contain("不可出售"));Assert.That(Snapshot(s),Is.EqualTo(before));
        }
        [Test] public void MovingBackCancellingAndNextCustomerNeverTransferUnboughtGoods()
        {
            var s=Session();var own=StageOwn(s);var goods=s.Offer.SupplierItems.ToArray();
            Assert.That(s.Move(goods[0].Id,ContainerId.Counter,1,0,0,false));Assert.That(goods[0].ForSale);
            Assert.That(s.Move(goods[0].Id,ContainerId.Storage,0,0,0,false),Is.False);
            Assert.That(s.Move(goods[0].Id,ContainerId.CustomerCounter,0,0,0,false));
            Assert.That(s.Move(own.Id,ContainerId.CustomerCounter,3,0,0,false),Is.False);
            Assert.That(s.NextCustomer());foreach(var item in goods)Assert.That(s.Find(item.Id),Is.Null);
            Assert.That(s.Find(own.Id),Is.SameAs(own));Assert.That(own.ForSale,Is.False);Assert.That(s.Money,Is.EqualTo(120));
            Assert.That(s.ValidateState(),Is.Null);
        }
        [Test] public void PriceChangesRefreshBothDirectionsAndConfirmedPurchaseHistoryIsStable()
        {
            var s=Session();StageOwn(s);var purchased=s.Offer.SupplierItems.ToArray();
            Assert.That(s.PreviewTrade(true).Net,Is.EqualTo(-25));
            s.SetPriceTag(new PriceTag{id="rise",title="上涨",percent=.2f});
            var q=s.PreviewTrade(true);Assert.That(q.SaleTotal,Is.EqualTo(12));Assert.That(q.PurchaseTotal,Is.EqualTo(42));Assert.That(q.Net,Is.EqualTo(-30));
            Assert.That(s.AcceptTrade(true));Assert.That(s.Money,Is.EqualTo(90));
            var history=purchased.Select(i=>i.PurchaseValue).ToArray();s.RemovePriceTag("rise");
            CollectionAssert.AreEqual(history,purchased.Select(i=>i.PurchaseValue));
            s.EndBusiness();s.AdvanceTurn();var restored=ShopSession.RestoreSave(catalog,s.CaptureSave());
            CollectionAssert.AreEqual(history,purchased.Select(i=>restored.Find(i.Id).PurchaseValue));Assert.That(restored.ValidateState(),Is.Null);
        }
    }
}
