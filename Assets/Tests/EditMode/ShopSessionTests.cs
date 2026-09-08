using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class ShopSessionTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup() { catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();catalog.retailMarkup=0;BuyersOnly(); }
        [TearDown] public void Teardown() { UnityEngine.Object.DestroyImmediate(catalog); }
        ShopSession Session(params string[] ids) {catalog.startingItems=ids;return new ShopSession(catalog);}
        static string Snapshot(ShopSession s)=>s.Money+"|"+s.Day+"|"+string.Join(";",s.Items.OrderBy(i=>i.Id).Select(i=>$"{i.Id},{i.Definition.id},{i.Container},{i.Owner},{i.X},{i.Y},{i.Rotation},{i.Flipped}"));
        static void Valid(ShopSession s)=>Assert.That(s.ValidateState(),Is.Null);
        static void Display(ShopSession s,GridItem i,int x=0,int y=0) {Assert.That(s.Move(i.Id,ContainerId.Display,x,y,i.Rotation,i.Flipped),Is.True,s.Message);}
        void BuyersOnly(int budget=20) {catalog.baseSupplierChance=0;catalog.advertisementSupplierBonus=0;catalog.displayedGoodsBuyerBonus=0;catalog.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=budget}};catalog.buyerBudgetVariation=0;}
        void SuppliersOnly(string id="herb")
        {
            catalog.baseSupplierChance=1;catalog.advertisementSupplierBonus=0;catalog.displayedGoodsBuyerBonus=0;
            foreach(var d in catalog.items) if(d.id!=id)d.supplierAvailable=false;
        }

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
            Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerBuys));Assert.That(s.RemainingCustomers,Is.EqualTo(4));
            Assert.That(s.EndBusiness());Assert.That(s.Sleep());
            SuppliersOnly();
            var pill=s.Items.First(i=>i.Definition.id=="pill");Assert.That(s.Move(pill.Id,ContainerId.Storage,0,0,0,false));
            Display(s,s.Items.First(i=>i.Definition.id=="sign"));Assert.That(s.BeginBusiness());
            Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerSells));Assert.That(s.RemainingCustomers,Is.EqualTo(4));Valid(s);
        }

        [Test] public void CounterAcceptsPlayerGoodsButPreventsCustomerTheft()
        {
            SuppliersOnly();
            var s=Session("sign","jade");Display(s,s.Items[0]);Assert.That(s.BeginBusiness());
            string before=Snapshot(s);var customer=s.Find(s.Offer.ItemId);
            Assert.That(s.Move(customer.Id,ContainerId.Storage,5,3,0,false),Is.False);
            Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Move(s.Items.First(i=>i.Definition.id=="jade").Id,ContainerId.Counter,3,2,0,false),Is.True);
            Assert.That(s.AcceptTrade());Assert.That(s.In(ContainerId.Counter).Single().Definition.id,Is.EqualTo("jade"));Valid(s);
        }

        [Test] public void InsufficientMoneyCannotConsumeSupplyOrChargeAnything()
        {
            SuppliersOnly();
            catalog.startingMoney=0;var s=Session("sign");Display(s,s.Items[0]);s.BeginBusiness();
            string before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Offer,Is.Not.Null);Valid(s);
        }

        [Test] public void FullStoragePreventsPurchaseAndKeepsCustomerOwnership()
        {
            SuppliersOnly();
            var s=Session(new[]{"sign"}.Concat(Enumerable.Repeat("dew",67)).ToArray());
            Display(s,s.Items.First(i=>i.Definition.id=="sign"));
            // Rotate the herb to match the vacated sign footprint; the next purchase has no free cell.
            s.BeginBusiness();Assert.That(s.Move(s.Offer.ItemId,ContainerId.Counter,0,0,2,false));Assert.That(s.AcceptTrade(),Is.True,s.Message);s.NextCustomer();
            Assert.That(s.Occupied(ContainerId.Storage),Is.EqualTo(70));
            string before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Find(s.Offer.ItemId).Owner,Is.EqualTo(ItemOwner.Customer));Valid(s);
        }

        [Test] public void PlayerGoodsMoveInEveryPhaseAndStayPlacedOnCustomerDeparture()
        {
            var s=Session("herb");var item=s.Items[0];Assert.That(s.Move(item.Id,ContainerId.Display,2,1,1,true));
            Assert.That(s.BeginBusiness());Assert.That(s.StageSale());
            Assert.That(s.Move(item.Id,ContainerId.Counter,2,1,2,false));
            string before=Snapshot(s);Assert.That(s.NextCustomer());Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.RejectTrade());Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Move(item.Id,ContainerId.Storage,0,0,0,false));Display(s,item);
            Assert.That(s.NextCustomer());Assert.That(s.StageSale());Assert.That(s.EndBusiness());
            Assert.That(item.Container,Is.EqualTo(ContainerId.Counter));
            foreach(var phase in new[]{DayPhase.Closed,DayPhase.Preparation})
            {
                if(phase==DayPhase.Preparation) Assert.That(s.Sleep());
                Assert.That(s.Phase,Is.EqualTo(phase));
                Assert.That(s.Move(item.Id,ContainerId.Storage,0,0,0,false));Display(s,item);
                Assert.That(s.Move(item.Id,ContainerId.Counter,0,0,0,false));
            }
            Valid(s);
        }

        [TestCase(20,1,2)]
        [TestCase(40,2,4)]
        public void BuyerCanBuyRepeatedlyUntilRemainingBudgetIsInsufficient(int budget,int sales,int remaining)
        {
            BuyersOnly(budget);var s=Session("pill","pill","pill");
            var pills=s.Items.ToArray();Display(s,pills[0]);Assert.That(s.BeginBusiness());var buyer=s.Offer;
            // Choose the last copy first: display advertisement must not reserve an instance.
            for(int n=0;n<sales;n++)
            {
                Assert.That(s.Move(pills[2-n].Id,ContainerId.Counter,0,0,0,false));
                Assert.That(s.CanAcceptTrade(out int total,out _));Assert.That(total,Is.EqualTo(18));
                Assert.That(s.AcceptTrade());Assert.That(s.Offer,Is.SameAs(buyer));
            }
            Assert.That(buyer.RemainingBudget,Is.EqualTo(remaining));Assert.That(s.ServedToday,Is.Zero);
            Assert.That(s.StageSale());string before=Snapshot(s);
            Assert.That(s.CanAcceptTrade(out _,out string reason),Is.False);Assert.That(reason,Does.Contain("资金不足"));
            Assert.That(s.AcceptTrade(),Is.False);Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(buyer.RemainingBudget,Is.EqualTo(remaining));Assert.That(s.Money,Is.EqualTo(120+18*sales));
            Assert.That(s.NextCustomer());Assert.That(s.Offer,Is.Not.SameAs(buyer));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(budget));Assert.That(s.ServedToday,Is.EqualTo(1));
            Assert.That(Snapshot(s),Is.EqualTo(before));Valid(s);
        }

        [Test] public void BudgetSixtyBuysThreePillsAsOneAtomicBasket()
        {
            BuyersOnly(60);var s=Session("pill","pill","pill");Display(s,s.Items[0]);s.BeginBusiness();
            var buyer=s.Offer;for(int n=0;n<3;n++)Assert.That(s.StageSale());
            Assert.That(s.CounterSaleSummary,Does.Contain("18×3=54"));
            Assert.That(s.CanAcceptTrade(out int total,out _));Assert.That(total,Is.EqualTo(54));
            Assert.That(s.AcceptTrade());Assert.That(s.Items,Is.Empty);Assert.That(s.Money,Is.EqualTo(174));
            Assert.That(s.Sales,Is.EqualTo(3));Assert.That(buyer.RemainingBudget,Is.EqualTo(6));Assert.That(s.Offer,Is.SameAs(buyer));Valid(s);
        }

        [Test] public void MixedCategoryAndNonSaleBasketsDisableTheWholeSale()
        {
            var s=Session("pill","jade","sign");Display(s,s.Items[0]);s.BeginBusiness();
            var jade=s.Items.First(i=>i.Definition.id=="jade");var sign=s.Items.First(i=>i.Definition.id=="sign");
            Assert.That(s.StageSale());Assert.That(s.Move(jade.Id,ContainerId.Counter,2,0,0,false));
            string before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(s.Message,Does.Contain("类别不符"));Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Move(jade.Id,ContainerId.Storage,2,2,0,false));Assert.That(s.Move(sign.Id,ContainerId.Counter,2,0,0,false));
            before=Snapshot(s);Assert.That(s.AcceptTrade(),Is.False);Assert.That(s.Message,Does.Contain("不可出售"));Assert.That(Snapshot(s),Is.EqualTo(before));
            Assert.That(s.Move(sign.Id,ContainerId.Storage,5,2,0,false));Assert.That(s.AcceptTrade());Valid(s);
        }

        [Test] public void CategoryAttractionIsDeduplicatedAndSnapshotSurvivesDisplayChanges()
        {
            var s=Session("herb","dew","pill");var herb=s.Items[0];var dew=s.Items[1];var pill=s.Items[2];
            Display(s,herb);Display(s,dew,3,0);Assert.That(s.BeginBusiness());Assert.That(s.BuyersToday,Is.EqualTo(5));
            Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Material));
            Assert.That(s.Move(herb.Id,ContainerId.Storage,0,3,0,false));Assert.That(s.Move(dew.Id,ContainerId.Counter,0,0,0,false));
            Display(s,pill);Assert.That(s.AcceptTrade()); // Different definition, same requested category.
            Assert.That(s.Money,Is.EqualTo(122));
            for(int n=0;n<4;n++) {Assert.That(s.NextCustomer());Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Material));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(20));}
            Assert.That(s.NextCustomer());Assert.That(s.Offer,Is.Null);
            s.EndBusiness();s.Sleep();s.BeginBusiness();Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));Valid(s);
        }

        [Test] public void RemovingSignAfterOpeningKeepsSuppliersAndFullCounterCanBeCleared()
        {
            SuppliersOnly();
            var s=Session(new[]{"sign"}.Concat(Enumerable.Repeat("dew",20)).ToArray());var sign=s.Items[0];Display(s,sign);
            var dew=s.Items.Where(i=>i.Definition.id=="dew").ToArray();
            for(int n=0;n<20;n++)Assert.That(s.Move(dew[n].Id,ContainerId.Counter,n%5,n/5,0,false));
            Assert.That(s.BeginBusiness(),Is.True);Assert.That(s.Phase,Is.EqualTo(DayPhase.Open));Assert.That(s.RemainingCustomers,Is.EqualTo(4));
            Assert.That(s.CanAcceptTrade(out _,out _),Is.False);
            Assert.That(s.Move(sign.Id,ContainerId.Storage,0,0,0,false));
            foreach(var item in dew) {Assert.That(s.FindSpace(item,ContainerId.Storage,out int x,out int y));Assert.That(s.Move(item.Id,ContainerId.Storage,x,y,0,false));}
            Assert.That(s.NextCustomer());Assert.That(s.SupplyAdvertisedToday);Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerSells));
            Assert.That(s.NextCustomer());Assert.That(s.Purchases,Is.Zero);Assert.That(s.Items.Count(i=>i.Owner==ItemOwner.Player),Is.EqualTo(21));Valid(s);
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

        [Test] public void EmptyDisplayCanOpenButMissingIngredientsAndPrematureSleepFail()
        {
            var s=Session("jade");string before=Snapshot(s);
            Assert.That(s.Craft(),Is.False);Assert.That(s.BeginBusiness(),Is.True);Assert.That(s.Sleep(),Is.False);
            Assert.That(Snapshot(s),Is.EqualTo(before));Valid(s);
        }

        [Test] public void TwoProcureCraftSellLoopsAndTwoDaysAreProfitable()
        {
            catalog.baseSupplierChance=1;catalog.startingItems=new[]{"sign"};catalog.Find("cinnabar").supplierAvailable=false;
            var s=new ShopSession(catalog,customerSeed:0);Display(s,s.Items[0]);
            // Day 1 obtains two complete ingredient sets through normal suppliers.
            Assert.That(s.BeginBusiness());
            for(int i=0;i<5;i++)
            {
                string id=s.Find(s.Offer.ItemId).Definition.id;
                if(s.Items.Count(item=>item.Owner==ItemOwner.Player && item.Definition.id==id)<2) Assert.That(s.AcceptTrade(),Is.True,s.Message);
                else Assert.That(s.RejectTrade());
                if(i<4)Assert.That(s.NextCustomer());
            }
            Assert.That(s.Purchases,Is.EqualTo(4));Assert.That(s.Money,Is.EqualTo(110));
            Assert.That(s.Craft());Assert.That(s.Craft());Assert.That(s.EndBusiness());Assert.That(s.Sleep());
            BuyersOnly();var pills=s.In(ContainerId.Storage).Where(i=>i.Definition.id=="pill").ToArray();
            Display(s,pills[0],2,0);Display(s,pills[1],4,0);Assert.That(s.BeginBusiness());
            for(int i=0;i<2;i++){Assert.That(s.StageSale());Assert.That(s.AcceptTrade());if(i==0)Assert.That(s.NextCustomer());}
            Assert.That(s.Day,Is.EqualTo(2));Assert.That(s.Crafted,Is.EqualTo(2));Assert.That(s.Sales,Is.EqualTo(2));Assert.That(s.Money,Is.EqualTo(146));
            Assert.That(s.Items.Count,Is.EqualTo(1));Assert.That(s.Items[0].Definition.id,Is.EqualTo("sign"));
            Assert.That(s.EndBusiness());Assert.That(s.Sleep());Assert.That(s.Day,Is.EqualTo(3));Assert.That(s.HasFurnace);Valid(s);
        }

        [TestCase(false,false,.4f)]
        [TestCase(false,true,.2f)]
        [TestCase(true,false,.8f)]
        [TestCase(true,true,.6f)]
        public void FiveVisitorsAreSampledIndividuallyWithDisplayWeightedProbabilities(bool sign,bool goods,float chance)
        {
            catalog.baseSupplierChance=.4f;catalog.advertisementSupplierBonus=.4f;catalog.displayedGoodsBuyerBonus=.2f;
            catalog.startingItems=new[]{"sign","pill"};
            int totalSuppliers=0;var dailyCounts=new System.Collections.Generic.HashSet<int>();
            for(int seed=0;seed<100;seed++)
            {
                var s=new ShopSession(catalog,customerSeed:seed);
                if(sign)Display(s,s.Items[0]);if(goods)Display(s,s.Items[1],2,0);
                Assert.That(s.PreviewAttraction().SupplierChance,Is.EqualTo(chance).Within(.0001));
                Assert.That(s.BeginBusiness());Assert.That(s.BuyersToday+s.SuppliersToday,Is.EqualTo(5));
                dailyCounts.Add(s.SuppliersToday);totalSuppliers+=s.SuppliersToday;
                for(int n=0;n<5;n++)
                {
                    Assert.That(s.Offer,Is.Not.Null);
                    if(sign && s.Offer.Direction==TradeDirection.CustomerSells) Assert.That(s.Find(s.Offer.ItemId).Definition.category,Is.EqualTo(ItemCategory.Material));
                    if(goods && s.Offer.Direction==TradeDirection.CustomerBuys) Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));
                    Assert.That(s.NextCustomer());Valid(s);
                }
                Assert.That(s.Offer,Is.Null);Assert.That(s.RemainingCustomers,Is.Zero);Assert.That(s.ServedToday,Is.EqualTo(5));
                Assert.That(s.BeginBusiness(),Is.False);Assert.That(s.NextCustomer());Assert.That(s.Offer,Is.Null);
            }
            Assert.That(dailyCounts.Count,Is.GreaterThan(2),"Do not replace random visitors with a fixed daily ratio.");
            Assert.That(totalSuppliers/500f,Is.EqualTo(chance).Within(.08f));
        }

        [Test] public void OnlyHighestCategoryCountsAndTiesUseStableCategoryOrder()
        {
            catalog.Find("herb").baseValue=10;catalog.Find("pill").baseValue=50;
            var s=Session("herb","herb","herb","pill");
            Display(s,s.Items[0]);Display(s,s.Items[1],2,0);Display(s,s.Items[2],4,0);Display(s,s.Items[3],0,2);
            var preview=s.PreviewAttraction();Assert.That(preview.BuyerCategory,Is.EqualTo(ItemCategory.Medicine));Assert.That(preview.DisplayValue,Is.EqualTo(50));
            catalog.Find("pill").baseValue=30;
            Assert.That(s.PreviewAttraction().BuyerCategory,Is.EqualTo(ItemCategory.Medicine),"Equal category totals use enum order, not placement order.");
            catalog.Find("herb").baseValue=11;
            Assert.That(s.PreviewAttraction().BuyerCategory,Is.EqualTo(ItemCategory.Material));Assert.That(s.PreviewAttraction().DisplayValue,Is.EqualTo(33));
        }

        [TestCase(0,0,18,22)]
        [TestCase(20,0,18,22)]
        [TestCase(22,0,18,22)]
        [TestCase(29,0,18,22)]
        [TestCase(30,30,54,66)]
        [TestCase(99,30,54,66)]
        [TestCase(100,100,135,165)]
        [TestCase(299,100,135,165)]
        [TestCase(300,300,360,440)]
        [TestCase(600,300,360,440)]
        public void BudgetUsesDiscreteTierAndEachBuyerVariesWithinTenPercent(int value,int threshold,int minimum,int maximum)
        {
            var defaults=ScriptableObject.CreateInstance<ShopCatalog>();
            catalog.buyerBudgetTiers=defaults.buyerBudgetTiers;UnityEngine.Object.DestroyImmediate(defaults);catalog.buyerBudgetVariation=.1f;
            catalog.Find("pill").baseValue=value;catalog.startingItems=new[]{"pill"};
            var observed=new System.Collections.Generic.HashSet<int>();
            for(int seed=0;seed<30;seed++)
            {
                var s=new ShopSession(catalog,customerSeed:seed);if(value>0)Display(s,s.Items[0]);
                var preview=s.PreviewAttraction();Assert.That(preview.BudgetTierMinimum,Is.EqualTo(threshold));
                Assert.That(preview.MinimumBuyerBudget,Is.EqualTo(minimum));Assert.That(preview.MaximumBuyerBudget,Is.EqualTo(maximum));
                Assert.That(s.BeginBusiness());
                for(int n=0;n<5;n++) {observed.Add(s.Offer.RemainingBudget);Assert.That(s.Offer.RemainingBudget,Is.InRange(minimum,maximum));s.NextCustomer();}
            }
            Assert.That(observed.Count,Is.GreaterThan(1),"Budgets must fluctuate per visitor, not stay at the tier base.");
        }

        [Test] public void PreviewDoesNotConsumeRandomnessAndOpeningFreezesTheWholeQueue()
        {
            catalog.baseSupplierChance=.4f;catalog.advertisementSupplierBonus=.4f;catalog.displayedGoodsBuyerBonus=.2f;catalog.buyerBudgetVariation=.1f;
            catalog.startingItems=new[]{"sign","pill","jade"};
            var a=new ShopSession(catalog,customerSeed:17);var b=new ShopSession(catalog,customerSeed:17);
            Display(a,a.Items[0]);Display(b,b.Items[0]);Display(a,a.Items[1],2,0);Display(b,b.Items[1],2,0);
            for(int n=0;n<50;n++) a.PreviewAttraction();
            a.BeginBusiness();b.BeginBusiness();var captured=a.TodayAttraction;
            Assert.That(a.Move(a.Items[0].Id,ContainerId.Storage,0,4,0,false));Assert.That(a.Move(a.Items[1].Id,ContainerId.Storage,3,4,0,false));Display(a,a.Items[2]);
            for(int n=0;n<5;n++)
            {
                Assert.That(a.Offer.Direction,Is.EqualTo(b.Offer.Direction));Assert.That(a.Offer.RequestedCategory,Is.EqualTo(b.Offer.RequestedCategory));
                Assert.That(a.Offer.RemainingBudget,Is.EqualTo(b.Offer.RemainingBudget));
                if(a.Offer.Direction==TradeDirection.CustomerSells)Assert.That(a.Find(a.Offer.ItemId).Definition.id,Is.EqualTo(b.Find(b.Offer.ItemId).Definition.id));
                a.NextCustomer();b.NextCustomer();
            }
            Assert.That(a.TodayAttraction,Is.SameAs(captured));a.EndBusiness();a.Sleep();Assert.That(a.TodayAttraction,Is.Null);
            a.BeginBusiness();Assert.That(a.TodayAttraction.BuyerCategory,Is.EqualTo(ItemCategory.Container));Assert.That(a.BuyersToday+a.SuppliersToday,Is.EqualTo(5));
        }

        [Test] public void AdvertisementSelectsItsConfiguredCategoryAndDuplicatesDoNotStackProbability()
        {
            catalog.baseSupplierChance=.2f;catalog.advertisementSupplierBonus=.4f;catalog.startingItems=new[]{"sign","sign"};
            catalog.Find("sign").advertisedCategory=ItemCategory.Equipment;var s=new ShopSession(catalog,customerSeed:1);
            Display(s,s.Items[0]);float probability=s.PreviewAttraction().SupplierChance;Display(s,s.Items[1],2,0);
            Assert.That(s.PreviewAttraction().SupplierChance,Is.EqualTo(probability));Assert.That(s.PreviewAttraction().SupplierDescription,Is.EqualTo("装备"));
            catalog.baseSupplierChance=1;s.BeginBusiness();
            for(int n=0;n<5;n++) {Assert.That(s.Find(s.Offer.ItemId).Definition.id,Is.EqualTo("sword"));s.NextCustomer();}
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
