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
        [UnityTest,Category("DP31")] public IEnumerator NoPortableChoiceStillCarriesTwoDifferentShapesAndReopensWithoutCopying()
        {
            yield return RestartWithTestCatalog(c=>{c.startingItems=new[]{"sword","herb"};},17);
            var s=shop.Session;var sword=s.Items[0];var herb=s.Items[1];
            yield return Click("CarryOpen");Assert.That(shop.FindButton("CarryNone"),Is.Not.Null);
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("CarryOption_")),Is.Zero);
            yield return Click("CarryConfirm");Assert.That(s.IsCarrying);Assert.That(s.CarriedPackId,Is.Zero);
            Assert.That(shop.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="CarriedInterior"),Is.False);
            yield return Drag(sword,ContainerId.LeftHand,0,0);yield return Drag(herb,ContainerId.RightHand,0,0);
            Assert.That(s.In(ContainerId.LeftHand).Single(),Is.SameAs(sword));Assert.That(s.In(ContainerId.RightHand).Single(),Is.SameAs(herb));
            yield return Click("CarryHide");yield return Click("CarryOpen");Assert.That(s.Items.Count,Is.EqualTo(2));
            yield return Click("CarryReturn");Assert.That(s.IsCarrying,Is.False);Assert.That(s.In(ContainerId.Storage).Count(),Is.EqualTo(2));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP31")] public IEnumerator MultipleActualPacksCancelWithoutChangesAndChosenContentsMoveBetweenHandsAndPack()
        {
            yield return RestartWithTestCatalog(c=>{c.SetStorageVerificationDefaults();c.startingItems=new[]{"test-portable","test-portable","herb","sword"};},17);
            var s=shop.Session;var packs=s.PortableStorage.ToArray();var herb=s.Items.Single(i=>i.Definition.id=="herb");
            var p=ItemPoint(packs[1]);yield return MouseAt(p,false);yield return MouseAt(p,true);yield return MouseAt(p,false);
            yield return Drag(herb,ContainerId.Interior,0,0);yield return Click("StorageClose");string before=s.CaptureSave();
            yield return Click("CarryOpen");Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("CarryOption_")),Is.EqualTo(2));
            yield return Click("CarryOption_"+packs[1].Id);yield return Click("CarryCancel");Assert.That(s.CaptureSave(),Is.EqualTo(before));
            yield return Click("CarryOpen");yield return Click("CarryOption_"+packs[1].Id);yield return Click("CarryConfirm");
            Assert.That(s.CarriedPackId,Is.EqualTo(packs[1].Id));Assert.That(s.Find(packs[1].Id),Is.SameAs(packs[1]));
            Assert.That(packs[0].Container,Is.EqualTo(ContainerId.Storage));Assert.That(s.In(ContainerId.Interior,packs[1].Id).Single(),Is.SameAs(herb));
            yield return Drag(herb,ContainerId.LeftHand,0,0);yield return Drag(herb,ContainerId.Interior,0,0);
            yield return Click("CarryHide");yield return Click("CarryOpen");Assert.That(s.Items.Count,Is.EqualTo(4));
            yield return Click("CarryReturn");Assert.That(herb.StorageItemId,Is.EqualTo(packs[1].Id));Assert.That(s.Find(herb.Id),Is.SameAs(herb));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
