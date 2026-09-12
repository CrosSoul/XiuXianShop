#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP33")] public IEnumerator ResourceChangesRefreshVisibleValueAndKeepDraggedInstance()
        {
            yield return RestartWithTestCatalog(SpiritStoneVerification.Configure,33);
            var s=shop.Session;var mid=s.Items.First(i=>i.Definition.id=="stone_mid");
            var point=ItemPoint(mid);yield return MouseAt(point,false);yield return MouseAt(point,true);yield return MouseAt(point,false);
            Assert.That(s.ConsumeSpirit(mid.Id,5000));shop.Refresh();yield return null;
            Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("剩余灵气 50 / 100")),Is.True);
            Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("基础价值 57")),Is.True);
            yield return Drag(mid,ContainerId.Display,0,0);Assert.That(s.PreviewAttraction().DisplayValue,Is.EqualTo(57));
            yield return Click("CarryOpen");yield return Click("CarryConfirm");
            yield return Drag(mid,ContainerId.LeftHand,0,0);Assert.That(s.Find(mid.Id),Is.SameAs(mid));
            Assert.That(s.ConsumeSpirit(mid.Id,5000));shop.Refresh();yield return null;
            Assert.That(mid.BaseValue,Is.EqualTo(7));Assert.That(s.RefillSpirit(mid.Id,10000));
            yield return Click("CarryReturn");Assert.That(mid.BaseValue,Is.EqualTo(107));
            var low=s.Items.First(i=>i.Definition.id=="stone_low");Assert.That(s.ConsumeSpirit(low.Id,100));shop.Refresh();yield return null;
            Assert.That(s.Find(low.Id),Is.Null);Assert.That(s.Items.Count,Is.EqualTo(4));Assert.That(s.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
