#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP52")]
        public IEnumerator DefaultStoryTrainingRunsThroughRealBusinessTravelAndChoiceButtons()
        {
            IsolateAlchemyTestMouse();var session=shop.Session;
            var initialRecipes=session.ProgressFlags.Where(f=>f.StartsWith("recipe:")).ToArray();
            yield return Click("BeginBusiness");yield return null;
            Assert.That(session.Offer.VisitId,Is.EqualTo("visit_alchemy_hint_01"));
            AssertVisitButtonReceivesPointer("VisitSceneContinue");
            yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(shop.IsSystemMenuOpen,Is.False);
            Assert.That(session.CanSave(out _),Is.False);
            int served=session.ServedToday;yield return Click("NextCustomer");Assert.That(session.ServedToday,Is.EqualTo(served),"Overlay must block ordinary buttons");
            yield return Click("VisitSceneContinue");
            Assert.That(session.HasProgressFlag("story.alchemy_hint_known"));
            Assert.That(session.UnlockedLocations.Any(l=>l.id==VisitVerification.TrainingLocationId));
            yield return Click("NextCustomer");
            int ordinary=0;while(session.Offer!=null){Assert.That(session.Offer.VisitId,Is.Null);ordinary++;yield return Click("NextCustomer");}
            Assert.That(ordinary,Is.EqualTo(session.CustomerCountThisTurn));yield return Click("EndBusiness");
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_"+VisitVerification.TrainingLocationId);yield return null;
            Assert.That(session.PendingVisitScene.Trigger,Is.EqualTo("Location"));
            Assert.That(session.HasProgressFlag("profession:alchemy"),Is.False);
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="StoryLeftName").text,Does.Contain("当值人员"));
            yield return Click("VisitSceneContinue");
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="VisitSceneText").text,Does.Contain("年轻求学"));
            yield return Click("VisitSceneContinue");AssertVisitButtonReceivesPointer("StoryChoice_1");yield return Click("StoryChoice_1");
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="VisitSceneText").text,Does.Contain("认真学"));
            yield return Click("VisitSceneContinue");
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="StoryComicCaption").text,Does.Contain("观摩"));
            Assert.That(session.HasProgressFlag("profession:alchemy"),Is.False);
            yield return Click("VisitSceneContinue");
            Assert.That(session.PendingVisitScene,Is.Null);Assert.That(session.CurrentLocationId,Is.EqualTo(VisitVerification.TrainingLocationId));
            Assert.That(session.HasProgressFlag("profession:alchemy"));Assert.That(session.IsLocationUnlocked(ShopSession.AlchemyLocationId));
            Assert.That(session.ProgressFlags.Where(f=>f.StartsWith("recipe:")),Is.EqualTo(initialRecipes),"Training must not grant a recipe");
            yield return Click("TravelLeave");
            Assert.That(session.UnlockedLocations.Any(l=>l.id==VisitVerification.TrainingLocationId),Is.False);
            yield return Click("TravelLocation_"+ShopSession.AlchemyLocationId);
            Assert.That(session.IsAtAlchemy);Assert.That(session.PendingVisitScene,Is.Null);
            // Use the existing leave operation; no material was placed at the ordinary furnace.
            Assert.That(session.LeaveLocation());Assert.That(session.ReturnToShop(true));Assert.That(session.EndCarrying());
            var loaded=ShopSession.RestoreSave(shop.Catalog,session.CaptureSave());
            Assert.That(loaded.HasProgressFlag("scene:scene_alchemy_training"));Assert.That(loaded.HasProgressFlag("profession:alchemy"));
            Assert.That(loaded.UnlockedLocations.Count(l=>l.id==ShopSession.AlchemyLocationId),Is.EqualTo(1));
            Assert.That(loaded.UnlockedLocations.Any(l=>l.id==VisitVerification.TrainingLocationId),Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
