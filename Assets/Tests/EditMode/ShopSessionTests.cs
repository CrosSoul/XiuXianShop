using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class ShopSessionTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup() { catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults(); }
        [TearDown] public void Teardown() { UnityEngine.Object.DestroyImmediate(catalog); }
        ShopSession Session(params string[] ids) {catalog.startingItems=ids;return new ShopSession(catalog);}
        static string Snapshot(ShopSession s)=>s.Money+"|"+s.Day+"|"+string.Join(";",s.Items.OrderBy(i=>i.Id).Select(i=>$"{i.Id},{i.Definition.id},{i.Container},{i.Owner},{i.X},{i.Y},{i.Rotation},{i.Flipped}"));
        static void Valid(ShopSession s)=>Assert.That(s.ValidateState(),Is.Null);
        static void Display(ShopSession s,GridItem i,int x=0,int y=0) {Assert.That(s.Move(i.Id,ContainerId.Display,x,y,i.Rotation,i.Flipped),Is.True,s.Message);}

        [Test] public void RotationAndReflectionHaveExpectedOccupiedCells()
        {
            var herb=catalog.Find("herb");
            CollectionAssert.AreEquivalent(new[]{new Vector2Int(1,0),new Vector2Int(0,0),new Vector2Int(0,1)},herb.Shape(1,false));
            CollectionAssert.AreEquivalent(new[]{new Vector2Int(1,0),new Vector2Int(1,1),new Vector2Int(0,1)},herb.Shape(0,true));
            CollectionAssert.AreEquivalent(herb.cells,herb.Shape(4,false));
            foreach(var def in catalog.items) for(int r=0;r<4;r++) foreach(bool flip in new[]{false,true})
            { var cells=def.Shape(r,flip);Assert.That(cells.Distinct().Count(),Is.EqualTo(def.cells.Length));Assert.That(cells.All(p=>p.x>=0 && p.y>=0)); }
        }

        [Test] public void FlippedLCanEnterPocketThatOriginalCannot()
        {
            var s=Session("herb","dew");var herb=s.Items[0];var dew=s.Items[1];
            Display(s,dew,0,0);
            Assert.That(s.Move(herb.Id,ContainerId.Display,0,0,0,false),Is.False);
            Assert.That(s.Move(herb.Id,ContainerId.Display,0,0,0,true),Is.True);
            Assert.That(s.Occupied(ContainerId.Display),Is.EqualTo(4));Valid(s);
        }

        [Test] public void LongItemMustRotateAtBottomEdge()
        {
            var s=Session("sword");var item=s.Items[0];
            Assert.That(s.Move(item.Id,ContainerId.Display,0,3,0,false),Is.False);
            Assert.That(s.Move(item.Id,ContainerId.Display,0,3,1,false),Is.True);Valid(s);
        }

        [Test] public void InvalidDropAndRotationLeaveEveryItemUnchanged()
        {
            var s=Session("sword","jade");var item=s.Items[0];
            Assert.That(s.Move(item.Id,ContainerId.Storage,9,1,0,false),Is.True);
            string before=Snapshot(s);
            Assert.That(s.Move(item.Id,ContainerId.Storage,9,1,1,false),Is.False);
            Assert.That(s.Move(item.Id,ContainerId.Display,-1,0,0,false),Is.False);
            Assert.That(s.Move(item.Id,ContainerId.Storage,s.Items[1].X,s.Items[1].Y,0,false),Is.False);
            Assert.That(Snapshot(s),Is.EqualTo(before));Valid(s);
        }

        [Test] public void ThousandsOfRandomRearrangementsNeverLoseOrDuplicateItems()
        {
            var s=new ShopSession(catalog);var rng=new System.Random(178);
            var ids=s.Items.Select(i=>i.Id).ToArray();
            for(int n=0;n<2000;n++)
            {
                string before=Snapshot(s);int id=ids[rng.Next(ids.Length)];
                bool moved=s.Move(id,(ContainerId)rng.Next(3),rng.Next(-2,12),rng.Next(-2,9),rng.Next(4),rng.Next(2)==0);
                if(!moved)Assert.That(Snapshot(s),Is.EqualTo(before));
                CollectionAssert.AreEquivalent(ids,s.Items.Select(i=>i.Id));Valid(s);
            }
        }

        [Test] public void DisplayActuallyDeterminesTradeDirection()
        {
            var s=Session("pill","sign");Display(s,s.Items[0]);Assert.That(s.BeginBusiness());
            Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerBuys));Assert.That(s.RemainingCustomers,Is.Zero);
            Assert.That(s.EndBusiness());Assert.That(s.Sleep());
            var pill=s.Items.First(i=>i.Definition.id=="pill");Assert.That(s.Move(pill.Id,ContainerId.Storage,0,0,0,false));
            Display(s,s.Items.First(i=>i.Definition.id=="sign"));Assert.That(s.BeginBusiness());
            Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerSells));Assert.That(s.RemainingCustomers,Is.EqualTo(3));Valid(s);
        }

        [Test] public void CounterRejectsUnrelatedGoodsAndCustomerTheft()
        {
            var s=Session("sign","jade");Display(s,s.Items[0]);Assert.That(s.BeginBusiness());
            string before=Snapshot(s);var customer=s.Find(s.Offer.ItemId);
            Assert.That(s.Move(customer.Id,ContainerId.Storage,5,3,0,false),Is.False);
            Assert.That(s.Move(s.Items.First(i=>i.Definition.id=="jade").Id,ContainerId.Counter,3,2,0,false),Is.False);
            Assert.That(Snapshot(s),Is.EqualTo(before));Valid(s);
        }

        [Test] public void InsufficientMoneyCannotConsumeSupplyOrChargeAnything()
        {
            catalog.startingMoney=0;var s=Session("sign");Display(s,s.Items[0]);s.BeginBusiness();
            string before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Offer,Is.Not.Null);Valid(s);
        }

        [Test] public void FullStoragePreventsPurchaseAndKeepsCustomerOwnership()
        {
            var s=Session(new[]{"sign"}.Concat(Enumerable.Repeat("dew",67)).ToArray());
            Display(s,s.Items.First(i=>i.Definition.id=="sign"));
            // Rotate the herb to match the vacated sign footprint; the next purchase has no free cell.
            s.BeginBusiness();Assert.That(s.Move(s.Offer.ItemId,ContainerId.Counter,0,0,2,false));Assert.That(s.AcceptTrade(),Is.True,s.Message);s.NextCustomer();
            Assert.That(s.Occupied(ContainerId.Storage),Is.EqualTo(70));
            string before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Find(s.Offer.ItemId).Owner,Is.EqualTo(ItemOwner.Customer));Valid(s);
        }

        [Test] public void RefusalAndEarlyClosureRestoreExactSalePlacement()
        {
            var s=Session("herb");var item=s.Items[0];Assert.That(s.Move(item.Id,ContainerId.Display,2,1,1,true));
            string before=Snapshot(s);s.BeginBusiness();s.StageSale();
            Assert.That(s.Move(item.Id,ContainerId.Counter,2,1,2,false));s.RejectTrade();Assert.That(Snapshot(s),Is.EqualTo(before));
            s.EndBusiness();s.Sleep();s.BeginBusiness();s.StageSale();s.EndBusiness();
            Assert.That(item.Container,Is.EqualTo(ContainerId.Display));Assert.That(item.X,Is.EqualTo(2));Assert.That(item.Rotation,Is.EqualTo(1));Assert.That(item.Flipped);Valid(s);
        }

        [Test] public void SellingRequiresStagingAndCannotSettleTwice()
        {
            var s=Session("pill");var id=s.Items[0].Id;Display(s,s.Items[0]);s.BeginBusiness();
            string before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.StageSale());Assert.That(s.AcceptTrade());Assert.That(s.Find(id),Is.Null);Assert.That(s.Occupied(ContainerId.Display),Is.Zero);
            int money=s.Money;Assert.That(s.AcceptTrade(),Is.False);Assert.That(s.Money,Is.EqualTo(money));Valid(s);
        }

        [Test] public void CraftFailsAtomicallyInFragmentedFullStorage()
        {
            var s=Session(new[]{"dew","herb"}.Concat(Enumerable.Repeat("dew",66)).ToArray());
            Assert.That(s.Occupied(ContainerId.Storage),Is.EqualTo(70));
            string before=Snapshot(s);Assert.That(s.Craft(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));Assert.That(s.Crafted,Is.Zero);Valid(s);
        }

        [Test] public void CraftCanUseSpaceFreedByItsOwnIngredients()
        {
            var s=Session(new[]{"herb","dew"}.Concat(Enumerable.Repeat("dew",66)).ToArray());
            Assert.That(s.Craft(),Is.True,s.Message);Assert.That(s.Occupied(ContainerId.Storage),Is.EqualTo(70));
            Assert.That(s.Items.Count(i=>i.Definition.id=="pill"),Is.EqualTo(1));Assert.That(s.Items.All(i=>i.Definition.id!="herb"));Valid(s);
        }

        [Test] public void MissingIngredientsAndEmptyDisplayHaveHonestFeedback()
        {
            var s=Session("jade");string before=Snapshot(s);
            Assert.That(s.Craft(),Is.False);Assert.That(s.BeginBusiness(),Is.False);Assert.That(s.Sleep(),Is.False);
            Assert.That(Snapshot(s),Is.EqualTo(before));Valid(s);
        }

        [Test] public void TwoProcureCraftSellLoopsAndTwoDaysAreProfitable()
        {
            var s=Session("sign");Display(s,s.Items[0]);
            // Day 1 obtains two complete ingredient sets through normal suppliers.
            Assert.That(s.BeginBusiness());
            for(int i=0;i<4;i++){Assert.That(s.AcceptTrade(),Is.True,s.Message);if(i<3)Assert.That(s.NextCustomer());}
            Assert.That(s.Purchases,Is.EqualTo(4));Assert.That(s.Money,Is.EqualTo(106));
            Assert.That(s.Craft());Assert.That(s.Craft());Assert.That(s.EndBusiness());Assert.That(s.Sleep());
            var pills=s.In(ContainerId.Storage).Where(i=>i.Definition.id=="pill").ToArray();
            Display(s,pills[0],2,0);Display(s,pills[1],4,0);Assert.That(s.BeginBusiness());
            for(int i=0;i<4;i++){Assert.That(s.RejectTrade());Assert.That(s.NextCustomer());}
            for(int i=0;i<2;i++){Assert.That(s.StageSale());Assert.That(s.AcceptTrade());if(i==0)Assert.That(s.NextCustomer());}
            Assert.That(s.Day,Is.EqualTo(2));Assert.That(s.Crafted,Is.EqualTo(2));Assert.That(s.Sales,Is.EqualTo(2));Assert.That(s.Money,Is.EqualTo(142));
            Assert.That(s.Items.Count,Is.EqualTo(1));Assert.That(s.Items[0].Definition.id,Is.EqualTo("sign"));
            Assert.That(s.EndBusiness());Assert.That(s.Sleep());Assert.That(s.Day,Is.EqualTo(3));Assert.That(s.HasFurnace);Valid(s);
        }

        [Test] public void SleepChargesPeriodicRentAndNeverMakesNegativeMoney()
        {
            catalog.startingMoney=5;var s=Session("sign");Display(s,s.Items[0]);
            for(int day=1;day<=14;day++) {Assert.That(s.BeginBusiness());Assert.That(s.EndBusiness());Assert.That(s.Sleep());Valid(s);}
            Assert.That(s.Day,Is.EqualTo(15));Assert.That(s.Money,Is.Zero);Assert.That(s.RentDebt,Is.EqualTo(36));Assert.That(s.Rent,Is.EqualTo(23));
            Assert.That(s.RemainingCustomers,Is.Zero);Assert.That(s.Offer,Is.Null);Assert.That(s.Items.Count,Is.EqualTo(1));
        }
    }
}
