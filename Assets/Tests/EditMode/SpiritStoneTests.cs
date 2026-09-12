using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class SpiritStoneTests
    {
        ShopCatalog catalog;
        ShopSession session;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();SpiritStoneVerification.Configure(catalog);
            session=new ShopSession(catalog,customerSeed:33);
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        GridItem Stone(string id)=>session.Items.First(i=>i.Definition.id==id);

        [Test] public void GradesCapacityAndIndependentAmountsAreNotWalletOrItemCount()
        {
            var low=Stone("stone_low");var mid=Stone("stone_mid");var high=Stone("stone_high");
            Assert.That(new[]{low.Definition.category,mid.Definition.category,high.Definition.category}.Distinct().Count(),Is.EqualTo(3));
            Assert.That(low.Cells.Length,Is.EqualTo(1));Assert.That(low.BaseValue,Is.EqualTo(1m));
            Assert.That(mid.Cells,Is.EquivalentTo(new[]{Vector2Int.zero,Vector2Int.up}));
            Assert.That(high.Cells,Is.EquivalentTo(new[]{Vector2Int.zero,Vector2Int.right,Vector2Int.up,Vector2Int.one}));
            Assert.That(mid.BaseValue,Is.EqualTo(107m));Assert.That(high.BaseValue,Is.EqualTo(10023m));
            Assert.That(mid.Definition.IsStorage,Is.False);int money=session.Money;
            Assert.That(session.ConsumeSpirit(mid.Id,25));Assert.That(mid.BaseValue,Is.EqualTo(106.75m));
            Assert.That(session.Items.Last(i=>i.Definition.id=="stone_mid").SpiritUnits,Is.EqualTo(10000));
            Assert.That(session.Items.Count,Is.EqualTo(5));Assert.That(session.Money,Is.EqualTo(money));
            Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void ExhaustionShattersOnlyLowAndRefillKeepsReusableIdentity()
        {
            var low=Stone("stone_low");var mid=Stone("stone_mid");var high=Stone("stone_high");
            Assert.That(session.ConsumeSpirit(low.Id,100));Assert.That(session.Find(low.Id),Is.Null);
            Assert.That(session.RefillSpirit(low.Id,100),Is.False);
            foreach(var item in new[]{mid,high})
            {
                Assert.That(session.ConsumeSpirit(item.Id,item.SpiritUnits));
                Assert.That(session.Find(item.Id),Is.SameAs(item));Assert.That(item.BaseValue,Is.EqualTo(item.Definition.spiritResource.ContainerPrice));
                Assert.That(session.RefillSpirit(item.Id,1));Assert.That(session.Find(item.Id),Is.SameAs(item));
            }
            Assert.That(session.Items.Count,Is.EqualTo(4));Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void InvalidMutationsAndCustomerOwnershipLeaveStateUntouched()
        {
            var mid=Stone("stone_mid");string before=session.CaptureSave();
            Assert.That(session.ConsumeSpirit(mid.Id,0),Is.False);Assert.That(session.ConsumeSpirit(mid.Id,-1),Is.False);
            Assert.That(session.ConsumeSpirit(mid.Id,int.MaxValue),Is.False);Assert.That(session.RefillSpirit(mid.Id,1),Is.False);
            Assert.That(session.RefillSpirit(Stone("stone_low").Id,1),Is.False);Assert.That(session.CaptureSave(),Is.EqualTo(before));
            catalog.baseSupplierChance=1;catalog.displayedGoodsBuyerBonus=0;
            foreach(var d in catalog.items)d.supplierAvailable=d.id=="stone_mid";
            session=new ShopSession(catalog,customerSeed:33);Assert.That(session.BeginBusiness());
            var customer=session.Offer.SupplierItems[0];int units=customer.SpiritUnits;
            Assert.That(session.ConsumeSpirit(customer.Id,1),Is.False);Assert.That(session.RefillSpirit(customer.Id,1),Is.False);
            Assert.That(customer.SpiritUnits,Is.EqualTo(units));Assert.That(customer.Owner,Is.EqualTo(ItemOwner.Customer));
        }
        [Test] public void DynamicQuoteAndDisplayUseRemainingValueAndOpeningSnapshotStaysFixed()
        {
            var mid=Stone("stone_mid");Assert.That(session.Move(mid.Id,ContainerId.Display,0,0,0,false));
            Assert.That(session.ConsumeSpirit(mid.Id,8000));Assert.That(session.PreviewAttraction().DisplayValue,Is.EqualTo(27m));
            Assert.That(session.Estimate(mid).BaseValue,Is.EqualTo(27m));Assert.That(session.BeginBusiness());
            Assert.That(session.RefillSpirit(mid.Id,100));Assert.That(session.TodayAttraction.DisplayValue,Is.EqualTo(27m));
            Assert.That(session.Move(mid.Id,ContainerId.Counter,0,0,0,false));
            Assert.That(session.Quote(mid).BaseValue,Is.EqualTo(28m));Assert.That(session.Quote(mid).Amount,Is.EqualTo(32));
        }
        [Test] public void PurchasedResourceRetainsHistoryThroughStorageCarryAndSave()
        {
            catalog.baseSupplierChance=1;foreach(var d in catalog.items)d.supplierAvailable=d.id=="stone_mid";
            session=new ShopSession(catalog,customerSeed:33);Assert.That(session.BeginBusiness());
            var bought=session.Offer.SupplierItems[0];Assert.That(session.Move(bought.Id,ContainerId.Counter,0,0,0,false));
            Assert.That(session.AcceptTrade());Assert.That(bought.PurchaseValue,Is.EqualTo(107));
            Assert.That(session.ConsumeSpirit(bought.Id,9900));Assert.That(bought.BaseValue,Is.EqualTo(8));
            var pack=Stone("test-portable");Assert.That(session.Move(bought.Id,ContainerId.Interior,0,0,0,false,pack.Id));
            Assert.That(session.EndBusiness());Assert.That(session.BeginCarrying(pack.Id));
            Assert.That(session.Move(bought.Id,ContainerId.LeftHand,0,0,0,false));Assert.That(session.Find(bought.Id),Is.SameAs(bought));
            Assert.That(session.Move(bought.Id,ContainerId.Interior,0,0,0,false,pack.Id));Assert.That(session.EndCarrying());
            Assert.That(session.AdvanceTurn());var restored=ShopSession.RestoreSave(catalog,session.CaptureSave());
            var saved=restored.Find(bought.Id);Assert.That(saved.SpiritUnits,Is.EqualTo(100));Assert.That(saved.PurchaseValue,Is.EqualTo(107));
            Assert.That(saved.StorageItemId,Is.EqualTo(pack.Id));Assert.That(restored.RefillSpirit(saved.Id,100));
            Assert.That(saved.BaseValue,Is.EqualTo(9));Assert.That(saved.PurchaseValue,Is.EqualTo(107));
        }
        [Test] public void SaveRejectsInvalidResourceCountsAndPreservesEmptyShell()
        {
            var mid=Stone("stone_mid");Assert.That(session.ConsumeSpirit(mid.Id,mid.SpiritUnits));
            var json=session.CaptureSave();Assert.That(ShopSession.RestoreSave(catalog,json).Find(mid.Id).SpiritUnits,Is.Zero);
            var save=JsonUtility.FromJson<ShopSave>(json);save.items.Single(i=>i.id==mid.Id).spiritUnits=-1;
            Assert.Throws<ArgumentException>(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(save)));
            save.items.Single(i=>i.id==mid.Id).spiritUnits=10001;
            Assert.Throws<ArgumentException>(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(save)));
            Assert.That(session.CaptureSave(),Is.EqualTo(json));
        }
    }
}
