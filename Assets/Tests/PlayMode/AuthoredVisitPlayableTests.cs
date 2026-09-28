#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        void AssertVisitButtonReceivesPointer(string name)
        {
            var rect=(RectTransform)shop.FindButton(name).transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);
            Assert.That(hits.FirstOrDefault().gameObject?.name,Is.EqualTo(name),string.Join(",",hits.Select(h=>h.gameObject.name))+" at "+point);
        }
        [UnityTest,Category("DP62")]
        public IEnumerator AuthoredVisitDialogueClicksUnlockOnlyAfterCompletionAndPersist()
        {
            testCatalog=Object.Instantiate(shop.Catalog);
            var authored=UnityEditor.AssetDatabase.LoadAssetAtPath<AuthoredContent>("Assets/Data/DP62VerificationContent.asset");
            VisitVerification.Configure(testCatalog,authored);
            shop.StartVerificationSession(testCatalog,62);
            Assert.That(shop.Session.BeginBusiness());shop.Refresh();yield return null;yield return null;
            Assert.That(shop.Session.Offer.VisitId,Is.EqualTo("visit_alchemy_hint_01"));
            var text=shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="VisitSceneText");
            Assert.That(text.text,Does.Contain("炼丹房有求学的机会"));
            Assert.That(shop.Session.HasProgressFlag("story.alchemy_hint_known"),Is.False);
            Assert.That(shop.Session.NextCustomer(),Is.False);
            AssertVisitButtonReceivesPointer("VisitSceneContinue");yield return Click("VisitSceneContinue");yield return null;
            Assert.That(shop.Session.PendingVisitScene,Is.Null);
            Assert.That(shop.Session.HasProgressFlag("story.alchemy_hint_known"));
            Assert.That(shop.Session.UnlockedLocations.Count(l=>l.id==VisitVerification.TrainingLocationId),Is.EqualTo(1));
            Assert.That(shop.Session.NextCustomer());Assert.That(shop.Session.Offer.VisitId,Is.EqualTo("test_visit_trade"));
            Assert.That(shop.Session.Offer.SupplierItems.Select(i=>i.Definition.id),Is.EqualTo(new[]{"herb","dew"}));
            Assert.That(shop.Session.EndBusiness());
            var restored=ShopSession.RestoreSave(testCatalog,shop.Session.CaptureSave());
            Assert.That(restored.HasCompletedVisit("visit_alchemy_hint_01"));
            Assert.That(restored.UnlockedLocations.Any(l=>l.id==VisitVerification.TrainingLocationId));
        }

        [UnityTest,Category("DP62")]
        public IEnumerator AuthoredVisitDialogueCancelDoesNotUnlockOrComplete()
        {
            testCatalog=Object.Instantiate(shop.Catalog);
            VisitVerification.Configure(testCatalog,UnityEditor.AssetDatabase.LoadAssetAtPath<AuthoredContent>("Assets/Data/DP62VerificationContent.asset"));
            shop.StartVerificationSession(testCatalog,62);Assert.That(shop.Session.BeginBusiness());shop.Refresh();
            yield return null;yield return null;AssertVisitButtonReceivesPointer("VisitSceneCancel");yield return Click("VisitSceneCancel");
            Assert.That(shop.Session.PendingVisitScene,Is.Null);
            Assert.That(shop.Session.HasCompletedVisit("visit_alchemy_hint_01"),Is.False);
            Assert.That(shop.Session.HasProgressFlag("story.alchemy_hint_known"),Is.False);
            Assert.That(shop.Session.UnlockedLocations.Any(l=>l.id==VisitVerification.TrainingLocationId),Is.False);
        }
    }
}
#endif
