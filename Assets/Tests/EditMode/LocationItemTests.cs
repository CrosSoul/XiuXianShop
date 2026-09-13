using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class LocationItemTests
    {
        ShopCatalog catalog;ShopSession session;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetStorageVerificationDefaults();
            catalog.travelLocations=new[]
            {
                new TravelLocation{id="temporary",title="临时测试",initiallyUnlocked=true},
                new TravelLocation{id="persistent",title="长期测试",initiallyUnlocked=true,preserveItemsBetweenVisits=true},
                new TravelLocation{id="other",title="另一长期测试",initiallyUnlocked=true,preserveItemsBetweenVisits=true}
            };
            catalog.firstLocationStaminaCost=0;catalog.extraLocationStaminaCost=0;
            session=new ShopSession(catalog,customerSeed:28);
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(catalog);
        GridItem Item(string id)=>session.Items.Single(i=>i.Definition.id==id);
        void Depart(string location)
        {
            Assert.That(session.BeginCarrying(Item("test-portable").Id));
            Assert.That(session.BeginTravel());Assert.That(session.EnterLocation(location));
        }
        [Test] public void OrdinaryLeaveCancelsWithoutChangesThenDeletesOnlyUncollectedItems()
        {
            var herb=Item("herb");var pack=Item("test-portable");
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,true,pack.Id));
            Depart("temporary");int money=session.Money,stamina=session.Stamina;
            Assert.That(session.Move(herb.Id,ContainerId.Location,0,0,1,true));
            Assert.That(session.AddLocationItem("pill"));var reward=session.In(ContainerId.Location).Single(i=>i.Id!=herb.Id);
            Assert.That(session.Move(reward.Id,ContainerId.RightHand,0,0,0,false));
            Assert.That(session.LocationLeaveNeedsConfirmation);Assert.That(session.LeaveLocation(),Is.False);
            Assert.That(session.CurrentLocationId,Is.EqualTo("temporary"));Assert.That(session.Find(herb.Id),Is.SameAs(herb));
            Assert.That(session.LeaveLocation(true));Assert.That(session.Find(herb.Id),Is.Null);
            Assert.That(session.Find(reward.Id),Is.SameAs(reward));Assert.That(session.Find(pack.Id),Is.SameAs(pack));
            Assert.That(session.In(ContainerId.Interior,pack.Id),Is.Empty);
            Assert.That(session.Money,Is.EqualTo(money));Assert.That(session.Stamina,Is.EqualTo(stamina));
            Assert.That(session.ValidateState(),Is.Null);Assert.That(session.ReturnToShop(true));Assert.That(session.EndCarrying());
            Assert.That(reward.Container,Is.EqualTo(ContainerId.Storage));
        }
        [Test] public void CollectingEverythingLeavesWithoutConfirmationAndPreservesOriginalState()
        {
            var herb=Item("herb");var pack=Item("test-portable");
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,true,pack.Id));Depart("temporary");
            Assert.That(session.Move(herb.Id,ContainerId.Location,0,0,1,true));
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,1,true,pack.Id));
            Assert.That(session.LocationLeaveNeedsConfirmation,Is.False);Assert.That(session.LeaveLocation());
            Assert.That(session.Find(herb.Id),Is.SameAs(herb));Assert.That(herb.Rotation,Is.EqualTo(1));Assert.That(herb.Flipped);
            Assert.That(herb.LocationId,Is.Null);Assert.That(herb.StorageItemId,Is.EqualTo(pack.Id));Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void PersistentItemsSurviveOtherLocationCleanupNextTurnAndSaveRestore()
        {
            Depart("persistent");Assert.That(session.AddLocationItem("pill"));var saved=session.In(ContainerId.Location).Single();int id=saved.Id;
            Assert.That(session.LeaveLocation());Assert.That(session.EnterLocation("other"));Assert.That(session.In(ContainerId.Location),Is.Empty);
            Assert.That(session.Move(id,ContainerId.LeftHand,0,0,0,false),Is.False);
            Assert.That(session.AddLocationItem("herb"));var other=session.In(ContainerId.Location).Single();Assert.That(session.LeaveLocation());
            Assert.That(session.EnterLocation("temporary"));Assert.That(session.AddLocationItem("dew"));Assert.That(session.LeaveLocation(true));
            Assert.That(session.Find(id),Is.SameAs(saved));Assert.That(session.Find(other.Id),Is.SameAs(other));
            Assert.That(session.ReturnToShop());Assert.That(session.EndCarrying());
            Assert.That(session.Move(id,ContainerId.Storage,0,0,0,false),Is.False);
            Assert.That(session.ValidateState(),Is.Null);
            session=ShopSession.RestoreSave(catalog,session.CaptureSave());Assert.That(session.Find(id).LocationId,Is.EqualTo("persistent"));
            Assert.That(session.BeginBusiness());Assert.That(session.EndBusiness());Assert.That(session.AdvanceTurn());
            Depart("persistent");Assert.That(session.In(ContainerId.Location).Single().Id,Is.EqualTo(id));
            Assert.That(session.Move(id,ContainerId.LeftHand,0,0,0,false));Assert.That(session.LeaveLocation());
            Assert.That(session.Find(other.Id).LocationId,Is.EqualTo("other"));Assert.That(session.ValidateState(),Is.Null);
        }
        [Test] public void FailedPlacementAndFullLocationDoNotMoveOrGenerateItems()
        {
            catalog.travelLocations[0].itemGridSize=new Vector2Int(2,2);Depart("temporary");
            Assert.That(session.AddLocationItem("pill"));var pill=session.In(ContainerId.Location).Single();int count=session.Items.Count;
            Assert.That(session.AddLocationItem("pill"),Is.False);Assert.That(session.Items.Count,Is.EqualTo(count));
            Assert.That(session.Move(pill.Id,ContainerId.Location,1,0,0,false),Is.False);Assert.That(pill.X,Is.Zero);
            Assert.That(session.Move(pill.Id,ContainerId.LeftHand,0,0,0,false));Assert.That(session.AddLocationItem("dew"));
            Assert.That(session.Move(pill.Id,ContainerId.Location,0,0,0,false),Is.False);Assert.That(pill.Container,Is.EqualTo(ContainerId.LeftHand));
            Assert.That(session.Move(Item("herb").Id,ContainerId.Location,0,0,0,false),Is.False);
            Assert.That(session.Move(pill.Id,ContainerId.Storage,0,0,0,false),Is.False);Assert.That(session.ValidateState(),Is.Null);
        }
    }
}
