using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class TravelTests
    {
        ShopCatalog catalog;
        ShopSession session;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetStorageVerificationDefaults();
            catalog.travelLocations=new[]
            {
                new TravelLocation{id="a",title="测试A",initiallyUnlocked=true},
                new TravelLocation{id="b",title="测试B",initiallyUnlocked=true},
                new TravelLocation{id="c",title="测试C",initiallyUnlocked=true},
                new TravelLocation{id="locked",title="未解锁测试"}
            };
            session=new ShopSession(catalog,customerSeed:27);
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(catalog);
        void Depart(){Assert.That(session.BeginCarrying(0));Assert.That(session.BeginTravel());}

        [Test] public void EntryPaysOnceAndVisitedOrUnaffordableLocationsCannotBeEntered()
        {
            Depart();Assert.That(session.Stamina,Is.EqualTo(100));
            Assert.That(session.UnlockedLocations.Select(l=>l.id),Is.EquivalentTo(new[]{"a","b","c"}));
            Assert.That(session.EnterLocation("locked"),Is.False);
            Assert.That(session.EnterLocation("a"));Assert.That(session.Stamina,Is.EqualTo(40));
            Assert.That(session.EnterLocation("a"),Is.False);Assert.That(session.EnterLocation("b"),Is.False);
            Assert.That(session.LeaveLocation());Assert.That(session.Stamina,Is.EqualTo(40));
            Assert.That(session.CanEnterLocation("a",out var reason),Is.False);Assert.That(reason,Does.Contain("已访问"));
            Assert.That(session.EnterLocation("b"));Assert.That(session.LeaveLocation());
            Assert.That(session.Stamina,Is.EqualTo(10));Assert.That(session.CanEnterLocation("c",out reason),Is.False);
            Assert.That(reason,Does.Contain("体力不足"));Assert.That(session.EnterLocation("c"),Is.False);
            Assert.That(session.CanReturnWithoutConfirmation);Assert.That(session.ReturnToShop());
            Assert.That(session.Stamina,Is.EqualTo(10));Assert.That(session.TrySpendStamina(10));Assert.That(session.Stamina,Is.Zero);
        }

        [Test] public void ConfirmedReturnConsumesThisTurnsOutingAndSurvivesPreparationSave()
        {
            Assert.That(session.BeginTravel(),Is.False);Depart();
            Assert.That(session.CanReturnWithoutConfirmation,Is.False);
            Assert.That(session.ReturnToShop(),Is.False);Assert.That(session.IsTravelling);
            Assert.That(session.ReturnToShop(true));Assert.That(session.Stamina,Is.EqualTo(100));
            Assert.That(session.BeginTravel(),Is.False);Assert.That(session.EndCarrying());
            session=ShopSession.RestoreSave(catalog,session.CaptureSave());
            Assert.That(session.HasTravelledThisTurn);Assert.That(session.BeginCarrying(0));
            Assert.That(session.BeginTravel(),Is.False);Assert.That(session.EndCarrying());
            Assert.That(session.BeginBusiness());Assert.That(session.EndBusiness());Assert.That(session.AdvanceTurn());
            Depart();Assert.That(session.HasVisitedLocation("a"),Is.False);
        }

        [Test] public void CarryInstancesStayContinuousAndShopActionsAreBlockedWhileAway()
        {
            var pack=session.Items.Single(i=>i.Definition.id=="test-portable");
            var herb=session.Items.Single(i=>i.Definition.id=="herb");
            var sword=session.Items.Single(i=>i.Definition.id=="sword");
            var warehouseItem=session.Items.Single(i=>i.Definition.id=="pill");
            Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,true,pack.Id));
            Assert.That(session.BeginCarrying(pack.Id));Assert.That(session.Move(sword.Id,ContainerId.LeftHand,0,0,0,false));
            var original=session.Items.ToArray();Assert.That(session.BeginTravel());
            Assert.That(session.EndCarrying(),Is.False);Assert.That(session.BeginBusiness(),Is.False);
            Assert.That(session.AdvanceTurn(),Is.False);Assert.That(session.Craft(),Is.False);
            Assert.That(session.Move(warehouseItem.Id,ContainerId.RightHand,0,0,0,false),Is.False);
            Assert.That(session.Move(sword.Id,ContainerId.Display,0,0,0,false),Is.False);
            Assert.That(()=>session.CaptureSave(),Throws.InvalidOperationException);
            foreach(var id in new[]{"a","b"})
            {
                Assert.That(session.EnterLocation(id));
                Assert.That(session.Move(herb.Id,ContainerId.RightHand,0,0,0,true));
                Assert.That(session.Move(herb.Id,ContainerId.Interior,0,0,0,true,pack.Id));
                Assert.That(session.LeaveLocation());Assert.That(session.Items,Is.EqualTo(original));
                Assert.That(session.Find(pack.Id),Is.SameAs(pack));Assert.That(session.Find(herb.Id),Is.SameAs(herb));
                Assert.That(session.In(ContainerId.LeftHand).Single(),Is.SameAs(sword));Assert.That(session.ValidateState(),Is.Null);
            }
            Assert.That(session.ReturnToShop());Assert.That(session.EndCarrying());
            Assert.That(herb.StorageItemId,Is.EqualTo(pack.Id));Assert.That(herb.Flipped);
        }

        [Test] public void UnlockingDoesNotGrantStaminaAndCostsReadConfiguration()
        {
            catalog.firstLocationStaminaCost=100;catalog.extraLocationStaminaCost=7;
            Depart();Assert.That(session.UnlockLocation("locked"));Assert.That(session.EnterLocation("locked"));
            Assert.That(session.Stamina,Is.Zero);Assert.That(session.LeaveLocation());
            Assert.That(session.NextLocationStaminaCost,Is.EqualTo(7));
            Assert.That(session.EnterLocation("a"),Is.False);Assert.That(session.ReturnToShop());
            Assert.That(session.EndCarrying());session=ShopSession.RestoreSave(catalog,session.CaptureSave());
            Assert.That(session.UnlockedLocations.Any(l=>l.id=="locked"));
        }

        [Test] public void NoAffordableOrUnlockedLocationsReturnsDirectlyWithoutSpending()
        {
            session.TrySpendStamina(41);Depart();
            Assert.That(session.EnterLocation("a"),Is.False);Assert.That(session.Stamina,Is.EqualTo(59));
            Assert.That(session.ReturnToShop());Assert.That(session.EndCarrying());
            catalog.travelLocations=new TravelLocation[0];session=new ShopSession(catalog,customerSeed:27);Depart();
            Assert.That(session.UnlockedLocations,Is.Empty);Assert.That(session.ReturnToShop());
        }
    }
}
