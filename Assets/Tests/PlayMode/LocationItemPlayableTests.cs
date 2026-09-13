#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP28")]
        public IEnumerator LocationDragPickupAndLeaveConfirmationKeepOnlyCarriedItems()
        {
            yield return RestartWithTestCatalog(c=>c.SetStorageVerificationDefaults(),28);
            var s=shop.Session;var pack=s.PortableStorage.Single();var herb=s.Items.Single(i=>i.Definition.id=="herb");
            Assert.That(s.Move(herb.Id,ContainerId.Interior,0,0,0,false,pack.Id));shop.Refresh();
            yield return Click("CarryOpen");yield return Click("CarryOption_"+pack.Id);yield return Click("CarryConfirm");
            yield return Click("TravelBegin");yield return Click("TravelLocation_baishitang");
            yield return Drag(herb,ContainerId.Location,0,0);Assert.That(herb.Container,Is.EqualTo(ContainerId.Location));
            Assert.That(s.AddLocationItem("pill"));var collected=s.In(ContainerId.Location).Single(i=>i.Id!=herb.Id);
            Assert.That(s.AddLocationItem("dew"));var abandoned=s.In(ContainerId.Location).Single(i=>i.Definition.id=="dew");shop.Refresh();yield return null;
            yield return Drag(collected,ContainerId.RightHand,0,0);Assert.That(collected.Container,Is.EqualTo(ContainerId.RightHand));
            yield return Click("TravelLeave");yield return Click("LocationLeaveCancel");
            Assert.That(s.CurrentLocationId,Is.EqualTo("baishitang"));Assert.That(s.Find(abandoned.Id),Is.SameAs(abandoned));
            yield return Drag(herb,ContainerId.Interior,0,0);Assert.That(herb.StorageItemId,Is.EqualTo(pack.Id));
            yield return Click("TravelLeave");yield return Click("LocationLeaveConfirm");
            Assert.That(s.Find(abandoned.Id),Is.Null);Assert.That(s.Find(collected.Id),Is.SameAs(collected));Assert.That(s.Find(herb.Id),Is.SameAs(herb));
            yield return Click("TravelLocation_tingfeng-teahouse");Assert.That(s.In(ContainerId.Location),Is.Empty);
            yield return Click("TravelLeave");Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="LocationLeaveConfirm"),Is.False);
            yield return Click("TravelReturn");yield return Click("CarryReturn");Assert.That(s.Stamina,Is.EqualTo(10));
            Assert.That(collected.Container,Is.EqualTo(ContainerId.Storage));Assert.That(herb.StorageItemId,Is.EqualTo(pack.Id));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP28")]
        public IEnumerator PersistentLocationShowsSameItemOnNextVisitAndNoOrdinaryCleanupPrompt()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.travelLocations=new[]{new TravelLocation{id="persistent-test",title="长期隔离测试",initiallyUnlocked=true,preserveItemsBetweenVisits=true}};
                c.firstLocationStaminaCost=0;
            },28);
            var s=shop.Session;
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_persistent-test");Assert.That(s.AddLocationItem("pill"));var item=s.In(ContainerId.Location).Single();shop.Refresh();
            yield return Click("TravelLeave");Assert.That(s.CurrentLocationId,Is.Null);Assert.That(s.Find(item.Id),Is.SameAs(item));
            yield return Click("TravelReturn");yield return Click("CarryReturn");
            yield return Click("BeginBusiness");yield return Click("EndBusiness");yield return Click("AdvanceTurn");
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_persistent-test");Assert.That(s.In(ContainerId.Location).Single(),Is.SameAs(item));
            yield return Drag(item,ContainerId.LeftHand,0,0);Assert.That(item.Container,Is.EqualTo(ContainerId.LeftHand));
            yield return Click("TravelLeave");yield return Click("TravelReturn");yield return Click("CarryReturn");
            Assert.That(s.Find(item.Id),Is.SameAs(item));Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
