#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        IEnumerator CloseBusinessForOuting()
        {
            if(shop.Session.Phase==TurnPhase.Preparation)yield return Click("BeginBusiness");
            if(shop.Session.Phase==TurnPhase.Open)yield return Click("EndBusiness");
        }
        [UnityTest,Category("DP27")]
        public IEnumerator TravelEntryLocksBeforeBusinessDuringBusinessAndAfterMonthAdvance()
        {
            var s=shop.Session;
            Assert.That(shop.FindButton("CarryOpen").interactable,Is.False);
            shop.OpenCarrySelection();yield return null;
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="CarryConfirm"),Is.False);
            yield return Click("BeginBusiness");Assert.That(shop.FindButton("CarryOpen").interactable,Is.False);
            Assert.That(s.BeginTravel(),Is.False);Assert.That(s.Stamina,Is.EqualTo(100));
            yield return Click("EndBusiness");Assert.That(shop.FindButton("CarryOpen").interactable);
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            Assert.That(s.BeginTravel(),Is.False);yield return Click("AdvanceTurn");
            Assert.That(shop.FindButton("CarryOpen").interactable,Is.False);
            yield return Click("BeginBusiness");yield return Click("EndBusiness");
            Assert.That(shop.FindButton("CarryOpen").interactable);
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            Assert.That(s.IsTravelling);Assert.That(s.Stamina,Is.EqualTo(100));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP27")]
        public IEnumerator TravelButtonsChargeOncePreserveHandsAndDisableVisitedAndUnaffordableLocations()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.travelLocations=new[]
                {
                    new TravelLocation{id="a",title="测试地点 A",initiallyUnlocked=true},
                    new TravelLocation{id="b",title="测试地点 B",initiallyUnlocked=true},
                    new TravelLocation{id="c",title="测试地点 C",initiallyUnlocked=true},
                    new TravelLocation{id="locked",title="隐藏地点"}
                };
            },27);
            yield return Click("BeginBusiness");yield return Click("EndBusiness");
            var s=shop.Session;var sword=s.Items.Single(i=>i.Definition.id=="sword");
            yield return Click("CarryOpen");yield return Click("CarryConfirm");
            yield return Drag(sword,ContainerId.LeftHand,0,0);
            yield return Click("TravelBegin");Assert.That(s.IsTravelling);Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="TravelLocation_locked"),Is.False);
            yield return Click("TravelReturn");Assert.That(shop.FindButton("TravelReturnConfirm"),Is.Not.Null);
            yield return Click("TravelReturnCancel");Assert.That(s.IsTravelling);Assert.That(s.Stamina,Is.EqualTo(100));
            yield return Click("TravelLocation_a");Assert.That(s.Stamina,Is.EqualTo(40));
            yield return Drag(sword,ContainerId.RightHand,0,0);Assert.That(sword.Container,Is.EqualTo(ContainerId.RightHand));
            yield return Click("TravelLeave");Assert.That(s.Stamina,Is.EqualTo(40));
            Assert.That(shop.FindButton("TravelLocation_a").interactable,Is.False);
            yield return Click("TravelLocation_b");yield return Click("TravelLeave");
            Assert.That(s.Stamina,Is.EqualTo(10));Assert.That(shop.FindButton("TravelLocation_c").interactable,Is.False);
            yield return Click("TravelReturn");Assert.That(s.IsTravelling,Is.False);
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="TravelReturnConfirm"),Is.False);
            Assert.That(s.Find(sword.Id),Is.SameAs(sword));Assert.That(sword.Container,Is.EqualTo(ContainerId.RightHand));
            Assert.That(shop.FindButton("TravelBegin").gameObject.activeInHierarchy,Is.False);yield return Click("CarryReturn");
            Assert.That(s.IsCarrying,Is.False);Assert.That(s.Stamina,Is.EqualTo(10));Assert.That(s.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP27")]
        public IEnumerator EarlyReturnConfirmationEndsOutingWithoutPayingOrDuplicatingPack()
        {
            yield return RestartWithTestCatalog(c=>c.SetStorageVerificationDefaults(),27);
            yield return Click("BeginBusiness");yield return Click("EndBusiness");
            var s=shop.Session;var pack=s.PortableStorage.Single();int count=s.Items.Count;
            yield return Click("CarryOpen");yield return Click("CarryOption_"+pack.Id);yield return Click("CarryConfirm");
            yield return Click("TravelBegin");yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");
            Assert.That(s.IsTravelling,Is.False);Assert.That(s.HasTravelledThisTurn);Assert.That(s.Stamina,Is.EqualTo(100));
            yield return Click("CarryReturn");Assert.That(s.Items.Count,Is.EqualTo(count));Assert.That(s.Find(pack.Id),Is.SameAs(pack));
            yield return Click("CarryOpen");yield return Click("CarryConfirm");Assert.That(shop.FindButton("TravelBegin").gameObject.activeInHierarchy,Is.False);
            yield return Click("CarryReturn");Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
