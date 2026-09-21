#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP49"),Category("DP27")]
        public IEnumerator ExpandedReceivingAreaScrollsAndRewardsFitCarriedStorage()
        {
            yield return RestartWithTestCatalog(c=>{
                c.SetStorageVerificationDefaults();
                foreach(var t in c.commissions.templates)t.rewardPoolId="BSH-R04";
                c.commissions.extraGiftChance=1;
            },51);
            var s=shop.Session;var pack=s.PortableStorage.Single();
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryOption_"+pack.Id);yield return Click("CarryConfirm");
            yield return Click("TravelBegin");yield return Click("TravelLocation_baishitang");
            for(int i=0;i<24;i++)Assert.That(s.AddLocationItem("dew"));
            shop.Refresh();yield return null;
            yield return Click("CommissionOpen");yield return Click("CommissionComplete_"+s.CommissionCandidates.First().id);
            var reward=s.In(ContainerId.Location).Single(i=>i.Definition.id=="pill");
            Assert.That(s.GridSize(ContainerId.Location).y,Is.EqualTo(6));
            var scroll=shop.GetComponentsInChildren<ScrollRect>().Single(r=>r.name=="LocationViewport");
            var center=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            yield return MouseAt(center,false);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center,scroll=new Vector2(0,-120)});
            yield return null;yield return null;yield return null;
            Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.1f));
            yield return Drag(reward,ContainerId.Interior,0,0);
            Assert.That(reward.StorageItemId,Is.EqualTo(pack.Id));Assert.That(reward.Container,Is.EqualTo(ContainerId.Interior));
            yield return Click("TravelLeave");yield return Click("LocationLeaveConfirm");
            yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            Assert.That(s.Find(reward.Id),Is.SameAs(reward));Assert.That(reward.StorageItemId,Is.EqualTo(pack.Id));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP49"),Category("DP27")]
        public IEnumerator CommissionIsFixedAndPhysicalRewardsMustBeCarriedOrAbandoned()
        {
            yield return RestartWithTestCatalog(c=>{
                foreach(var t in c.commissions.templates)t.rewardPoolId="BSH-R01";
                var group=c.commissions.rewardPools.Single(p=>p.id=="BSH-R01").items[0];
                group.minimumCount=3;group.maximumCount=3;group.itemIds=new[]{"dew"};
                c.commissions.extraGiftChance=1;
            },49);
            var s=shop.Session;int initial=s.Items.Count;
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");yield return Click("TravelLocation_baishitang");
            var candidates=s.CommissionCandidates.Select(t=>t.id).ToArray();Assert.That(candidates.Length,Is.EqualTo(3));
            for(int i=0;i<2;i++)
            {
                yield return Click("CommissionOpen");
                Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text==s.CommissionCandidates.First().description));
                yield return Click("CommissionClose");Assert.That(s.CommissionCandidates.Select(t=>t.id),Is.EqualTo(candidates));
            }
            yield return Click("CommissionOpen");yield return Click("CommissionComplete_"+candidates[0]);
            Assert.That(s.Stamina,Is.EqualTo(40));Assert.That(s.Items.Count,Is.EqualTo(initial+4));
            Assert.That(s.In(ContainerId.Location).Count(),Is.EqualTo(4));Assert.That(s.CommissionResult,Does.Contain("意外谢礼"));
            yield return Click("CommissionOpen");Assert.That(shop.FindButton("CommissionComplete_"+candidates[1]).interactable,Is.False);
            yield return Click("CommissionClose");
            var reward=s.In(ContainerId.Location).First();yield return Drag(reward,ContainerId.LeftHand,0,0);
            Assert.That(reward.Container,Is.EqualTo(ContainerId.LeftHand));
            yield return Click("TravelLeave");yield return Click("LocationLeaveCancel");Assert.That(s.In(ContainerId.Location).Count(),Is.EqualTo(3));
            yield return Click("TravelLeave");yield return Click("LocationLeaveConfirm");Assert.That(s.Items.Count,Is.EqualTo(initial+1));
            yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            Assert.That(s.Find(reward.Id),Is.SameAs(reward));Assert.That(reward.Container,Is.EqualTo(ContainerId.Storage));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP49"),Category("DP27")]
        public IEnumerator CurrencyCommissionPaysOnceAndNextMonthOffersNewCommission()
        {
            yield return RestartWithTestCatalog(c=>{
                foreach(var t in c.commissions.templates){t.rewardPoolId="BSH-R04";t.title="抄录测试";t.description="配置文案";t.rewardHint="少量经营货币";}
                c.commissions.extraGiftChance=0;
            },50);
            var s=shop.Session;int money=s.Money,count=s.Items.Count;
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");yield return Click("TravelLocation_baishitang");
            yield return Click("CommissionOpen");
            Assert.That(shop.GetComponentsInChildren<Text>().Any(t=>t.text=="配置文案"));
            yield return Click("CommissionComplete_"+s.CommissionCandidates.First().id);
            Assert.That(s.Money-money,Is.InRange(6,10));Assert.That(s.Items.Count,Is.EqualTo(count));
            yield return Click("TravelLeave");yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
            yield return Click("AdvanceTurn");
            yield return CloseBusinessForOuting();yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");yield return Click("TravelLocation_baishitang");
            Assert.That(s.CommissionLimitReached,Is.False);yield return Click("CommissionOpen");
            Assert.That(shop.FindButton("CommissionComplete_"+s.CommissionCandidates.First().id).interactable,Is.True);
            yield return Click("CommissionComplete_"+s.CommissionCandidates.First().id);
            Assert.That(s.Money-money,Is.InRange(12,20));Assert.That(s.Stamina,Is.EqualTo(10));Assert.That(s.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
