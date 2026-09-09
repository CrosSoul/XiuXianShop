using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class CarryTests
    {
        ShopCatalog catalog;ShopSession session;
        GridItem Item(string id)=>session.Items.Single(i=>i.Definition.id==id);
        [SetUp] public void Setup(){catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetStorageVerificationDefaults();session=new ShopSession(catalog,customerSeed:17);}
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(catalog);
        [Test] public void NoPackHasTwoIndependentSlotsWithoutGeometryOrExtraInventory()
        {
            var sword=Item("sword");var herb=Item("herb");int count=session.Items.Count;
            Assert.That(session.BeginCarrying(0));
            Assert.That(session.Move(sword.Id,ContainerId.LeftHand,0,0,0,false));
            Assert.That(session.Move(herb.Id,ContainerId.LeftHand,0,0,0,false),Is.False);
            Assert.That(session.Move(herb.Id,ContainerId.RightHand,0,0,0,false));
            Assert.That(session.Find(sword.Id),Is.SameAs(sword));Assert.That(session.CarriedPackId,Is.Zero);
            Assert.That(session.Items.Count,Is.EqualTo(count));Assert.That(session.ValidateState(),Is.Null);
            Assert.That(session.EndCarrying());Assert.That(session.Find(herb.Id),Is.SameAs(herb));
            Assert.That(session.Items.Count,Is.EqualTo(count));Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void SelectedPackKeepsItsPreloadedContentsAndCannotBeSwappedOrNested()
        {
            var pack=Item("test-portable");var herb=Item("herb");var box=Item("test-storage-case");
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,true,pack.Id));
            Assert.That(session.BeginCarrying(pack.Id));Assert.That(session.Find(pack.Id),Is.SameAs(pack));
            Assert.That(session.In(ContainerId.Interior,pack.Id).Single(),Is.SameAs(herb));
            Assert.That(session.BeginCarrying(0),Is.False);
            Assert.That(session.Move(pack.Id,ContainerId.Storage,0,0,0,false),Is.False);
            Assert.That(session.Move(box.Id,ContainerId.Interior,0,0,0,false,pack.Id),Is.False);
            Assert.That(session.Move(Item("test-production").Id,ContainerId.Interior,0,0,0,false,pack.Id),Is.False);
            Assert.That(session.Move(herb.Id,ContainerId.LeftHand,0,0,0,true));
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,true,pack.Id));
            Assert.That(session.ValidateState(),Is.Null);
            Assert.That(()=>session.CaptureSave(),Throws.InvalidOperationException);
            Assert.That(session.EndCarrying());
            Assert.That(pack.Container,Is.EqualTo(ContainerId.Storage));Assert.That(herb.StorageItemId,Is.EqualTo(pack.Id));
            Assert.That(ShopSession.RestoreSave(catalog,session.CaptureSave()).In(ContainerId.Interior,pack.Id).Single().Id,Is.EqualTo(herb.Id));
        }
        [Test] public void FailedReturnPreservesEveryCarryItemUntilWarehouseIsRearranged()
        {
            catalog.startingItems=new[]{"test-portable"}.Concat(Enumerable.Repeat("dew",64)).ToArray();
            session=new ShopSession(catalog,customerSeed:17);var pack=Item("test-portable");var dew=session.Items.First(i=>i.Definition.id=="dew");
            int x=pack.X,y=pack.Y;
            Assert.That(session.Move(dew.Id,ContainerId.Interior,0,0,0,false,pack.Id));
            Assert.That(session.BeginCarrying(pack.Id));
            Assert.That(session.Move(dew.Id,ContainerId.Storage,x,y,0,false));
            Assert.That(session.EndCarrying(),Is.False);Assert.That(session.IsCarrying);
            Assert.That(pack.Container,Is.EqualTo(ContainerId.CarriedPack));Assert.That(session.Find(pack.Id),Is.SameAs(pack));
            Assert.That(session.Move(dew.Id,ContainerId.Interior,0,0,0,false,pack.Id));
            Assert.That(session.EndCarrying());Assert.That(session.ValidateState(),Is.Null);
        }
    }
}
