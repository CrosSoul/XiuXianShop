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
        [UnityTest,Category("DP68")] public IEnumerator NewGameConfirmationReplacesSessionAndLoadKeepsSavedProgress()
        {
            IsolateAlchemyTestMouse();var original=shop.Session;original.SetProgressFlag("test:saved-progress");
            yield return Drag(original.Items.First(i=>i.Definition.id=="herb"),ContainerId.Display,0,0);
            shop.OpenSystemMenu();yield return Click("SaveSlot_1");yield return Click("SystemNewGame");yield return Click("SaveCancel");Assert.That(shop.Session,Is.SameAs(original));
            yield return Click("SystemNewGame");yield return Click("SaveConfirm");var fresh=shop.Session;
            Assert.That(fresh,Is.Not.SameAs(original));Assert.That(fresh.Items.Count,Is.EqualTo(9));Assert.That(fresh.ProgressFlags,Is.Empty);Assert.That(fresh.In(ContainerId.Display),Is.Empty);Assert.That(shop.IsSystemMenuOpen,Is.False);
            shop.OpenSystemMenu();yield return Click("SystemNewGame");yield return Click("SaveConfirm");Assert.That(shop.Session,Is.Not.SameAs(fresh));Assert.That(shop.Session.Items.Count,Is.EqualTo(9));
            shop.OpenSystemMenu();yield return Click("LoadSlot_1");yield return Click("SaveConfirm");
            Assert.That(shop.Session.HasProgressFlag("test:saved-progress"));Assert.That(shop.Session.In(ContainerId.Display).Single().Definition.id,Is.EqualTo("herb"));Assert.That(shop.Session.Items.Count,Is.EqualTo(9));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP68")] public IEnumerator CanonicalTwoTurnsTradeTravelAndAutoSave()
        {
            IsolateAlchemyTestMouse();shop.CustomerSeed=68;shop.NewGame();yield return null;var s=shop.Session;int trades=0;
            for(int turn=1;turn<=2;turn++)
            {
                yield return Click("BeginBusiness");while(s.PendingVisitScene!=null)yield return Click("VisitSceneContinue");
                while(s.Offer!=null)
                {
                    var own=s.Items.FirstOrDefault(i=>i.Owner==ItemOwner.Player && !i.Definition.procurementSign && i.Definition.category==s.Offer.RequestedCategory);
                    var offered=s.Offer.SupplierItems.FirstOrDefault(i=>i.ForSale);
                    var candidate=own??offered;
                    if(candidate!=null && s.FindSpace(candidate,ContainerId.Counter,out int x,out int y))
                    {
                        yield return Drag(candidate,ContainerId.Counter,x,y);
                        if(s.PreviewTrade().CanConfirm){yield return Click("AcceptTrade");trades++;}
                        else if(shop.NegotiationView.IsOpen)yield return Click("NegotiationClose");
                        if(candidate.Owner==ItemOwner.Player && s.Find(candidate.Id)!=null && candidate.Container==ContainerId.Counter)
                        {Assert.That(s.FindSpace(candidate,ContainerId.Storage,out x,out y));yield return Drag(candidate,ContainerId.Storage,x,y);}
                    }
                    yield return Click("NextCustomer");
                }
                yield return Click("EndBusiness");
                if(turn==1)
                {
                    yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");yield return Click("TravelLocation_baishitang");
                    Assert.That(s.CurrentLocationId,Is.EqualTo("baishitang"));yield return Click("TravelLeave");yield return Click("TravelReturn");yield return Click("TravelReturnConfirm");yield return Click("CarryReturn");
                }
                yield return Click("AdvanceTurn");Assert.That(s.Turn,Is.EqualTo(turn+1));Assert.That(shop.SaveSlots.Exists(0));
            }
            Assert.That(trades,Is.GreaterThan(0));Assert.That(s.ValidateState(),Is.Null);Assert.That(s.HasProgressFlag("profession:alchemy"),Is.False);
            var saved=JsonUtility.FromJson<ShopSave>(shop.SaveSlots.Read(0));Assert.That(saved.turn,Is.EqualTo(3));LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
