using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class StorageTests
    {
        ShopCatalog catalog;ShopSession session;
        GridItem Item(string id)=>session.Items.Single(i=>i.Definition.id==id);
        [SetUp] public void Setup(){catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetStorageVerificationDefaults();session=new ShopSession(catalog,customerSeed:17);}
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(catalog);
        [Test] public void TwoShapesRemainSameInstancesAfterBoxMovesAndSaveRestoresContents()
        {
            var box=Item("test-storage-case");var herb=Item("herb");var sword=Item("sword");
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,1,true,box.Id));
            Assert.That(session.Move(sword.Id,ContainerId.Interior,4,0,1,false,box.Id));
            Assert.That(session.Move(box.Id,ContainerId.Storage,box.X,box.Y,1,true));
            Assert.That(session.Find(herb.Id),Is.SameAs(herb));Assert.That(herb.Rotation,Is.EqualTo(1));Assert.That(herb.Flipped);
            var restored=ShopSession.RestoreSave(catalog,session.CaptureSave());
            Assert.That(restored.In(ContainerId.Interior,box.Id).Count(),Is.EqualTo(2));
            Assert.That(restored.Find(herb.Id).Flipped);Assert.That(restored.Find(sword.Id).Rotation,Is.EqualTo(1));
            Assert.That(session.FindSpace(herb,ContainerId.Storage,out int x,out int y));
            Assert.That(session.Move(herb.Id,ContainerId.Storage,x,y,herb.Rotation,herb.Flipped));
            Assert.That(session.Find(herb.Id),Is.SameAs(herb));Assert.That(herb.StorageItemId,Is.Zero);
            Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void OccupiedOutOfBoundsNestingAndEquipmentRulesRejectWithoutMoving()
        {
            var box=Item("test-storage-case");var herb=Item("herb");var sword=Item("sword");var equipment=Item("test-production");var eqBox=Item("test-equipment-case");var portable=Item("test-portable");
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,false,box.Id));
            Assert.That(session.Move(sword.Id,ContainerId.Interior,0,0,0,false,box.Id),Is.False);
            Assert.That(session.Move(sword.Id,ContainerId.Interior,9,9,0,false,box.Id),Is.False);
            Assert.That(session.Move(portable.Id,ContainerId.Interior,4,4,0,false,box.Id),Is.False);
            Assert.That(session.Move(box.Id,ContainerId.Counter,0,0,0,false),Is.False);
            Assert.That(session.Move(equipment.Id,ContainerId.Interior,4,4,0,false,box.Id),Is.False);
            Assert.That(session.Move(equipment.Id,ContainerId.Interior,0,0,0,false,portable.Id),Is.False);
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,false,eqBox.Id),Is.False);
            eqBox.Definition.compatibleEquipmentIds=System.Array.Empty<string>();
            Assert.That(session.Move(equipment.Id,ContainerId.Interior,0,0,0,false,eqBox.Id),Is.False);
            eqBox.Definition.compatibleEquipmentIds=new[]{equipment.Definition.id};
            Assert.That(session.Move(equipment.Id,ContainerId.Interior,0,0,0,false,eqBox.Id));
            Assert.That(sword.Container,Is.EqualTo(ContainerId.Storage));Assert.That(herb.StorageItemId,Is.EqualTo(box.Id));
            Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void PurchasedContentsKeepHistoryWhenWarehouseIsFullAndAfterRestore()
        {
            catalog.startingItems=new[]{"test-storage-case"}.Concat(Enumerable.Repeat("dew",60)).ToArray();
            catalog.baseSupplierChance=1;catalog.displayedGoodsBuyerBonus=0;
            foreach(var d in catalog.items)d.supplierAvailable=d.id=="dew";
            session=new ShopSession(catalog,customerSeed:17);
            var box=Item("test-storage-case");Assert.That(session.BeginBusiness());
            var bought=session.Offer.SupplierItems[0];var next=session.Offer.SupplierItems[1];
            Assert.That(session.Move(bought.Id,ContainerId.Interior,0,0,0,false,box.Id),Is.False);
            Assert.That(session.Move(bought.Id,ContainerId.Counter,0,0,0,false));Assert.That(session.AcceptTrade());
            int? history=bought.PurchaseValue;Assert.That(history,Is.GreaterThan(0));
            Assert.That(session.Move(bought.Id,ContainerId.Interior,0,0,0,false,box.Id));
            Assert.That(session.Move(next.Id,ContainerId.Counter,0,0,0,false));Assert.That(session.AcceptTrade());
            Assert.That(session.FindSpace(bought,ContainerId.Storage,out _,out _),Is.False);
            Assert.That(session.Move(bought.Id,ContainerId.Storage,0,0,0,false),Is.False);
            Assert.That(bought.StorageItemId,Is.EqualTo(box.Id));Assert.That(session.Find(bought.Id),Is.SameAs(bought));
            session.SetPriceTag(new PriceTag{id="test",title="测试",percent=.2f,category=ItemCategory.Material,playerBuys=true,playerSells=true});
            Assert.That(session.EndBusiness());Assert.That(session.AdvanceTurn());
            var restored=ShopSession.RestoreSave(catalog,session.CaptureSave());
            Assert.That(restored.Find(bought.Id).PurchaseValue,Is.EqualTo(history));
            Assert.That(restored.Find(bought.Id).StorageItemId,Is.EqualTo(box.Id));Assert.That(restored.ValidateState(),Is.Null);
        }
    }
}
