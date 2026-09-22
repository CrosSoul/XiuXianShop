#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        // Setup loads the saved scene and lets Start run. No replacement Session or test catalog.
        IEnumerator CompleteDefaultBusiness()
        {
            var s=shop.Session;
            Assert.That(shop.FindButton("CarryOpen").interactable,Is.False);
            int count=s.CustomerCountThisTurn,stamina=s.Stamina;bool purchased=false;
            yield return Click("BeginBusiness");
            Assert.That(s.BuyersToday+s.SuppliersToday+s.TradingCustomersToday,Is.EqualTo(count));
            Assert.That(shop.FindButton("CarryOpen").interactable,Is.False);
            for(int n=0;n<count;n++)
            {
                Assert.That(s.Offer,Is.Not.Null);
                // Exercise actual ownership/payment when this randomly generated visitor has goods.
                var supplied=s.In(ContainerId.CustomerCounter).FirstOrDefault();
                if(!purchased && supplied!=null && s.Money>=s.Quote(supplied).Amount)
                {
                    Assert.That(s.FindSpace(supplied,ContainerId.Counter,out int x,out int y));
                    yield return Drag(supplied,ContainerId.Counter,x,y);
                    if(s.PreviewTrade().CanConfirm)
                    {
                        yield return Click("AcceptTrade");
                        Assert.That(supplied.Owner,Is.EqualTo(ItemOwner.Player));
                        purchased=true;
                    }
                    if(shop.NegotiationView.IsOpen)yield return Click("NegotiationClose");
                }
                yield return Click("NextCustomer");
            }
            Assert.That(s.Offer,Is.Null);Assert.That(s.ServedToday,Is.EqualTo(count));
            yield return Click("EndBusiness");
            Assert.That(s.Stamina,Is.EqualTo(stamina));Assert.That(s.ValidateState(),Is.Null);
        }

        IEnumerator ReturnCurrentOuting()
        {
            yield return Click("TravelLeave");
            if(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="LocationLeaveConfirm"))
                yield return Click("LocationLeaveConfirm");
            yield return Click("TravelReturn");
            if(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="TravelReturnConfirm"))
                yield return Click("TravelReturnConfirm");
            Assert.That(shop.Session.BeginTravel(),Is.False,"Only one outing per month");
            yield return Click("CarryReturn");
        }

        [UnityTest,Category("DP59")]
        public IEnumerator DefaultSessionKeepsTeaCommissionsAndItemsAcrossContinuousMonths()
        {
            IsolateAlchemyTestMouse();
            var s=shop.Session;var calendar=s.Calendar;
            var pack=s.PortableStorage.Single();
            var dew=s.Items.First(i=>i.Definition.id=="dew");
            var sword=s.Items.First(i=>i.Definition.id=="sword");
            var herb=s.Items.First(i=>i.Definition.id=="herb");
            Assert.That(s.UnlockedLocations.Any(l=>l.id==ShopSession.AlchemyLocationId),Is.False);
            // Preload the existing portable container before business, through its ordinary grid.
            shop.SelectItem(pack.Id);yield return null;
            yield return Drag(dew,ContainerId.Interior,0,0);
            yield return Click("StorageClose");
            yield return CompleteDefaultBusiness();
            yield return Click("CarryOpen");yield return Click("CarryOption_"+pack.Id);yield return Click("CarryConfirm");
            yield return Drag(sword,ContainerId.LeftHand,0,0);yield return Drag(herb,ContainerId.RightHand,0,0);
            yield return Click("TravelBegin");yield return Click("TravelLocation_tingfeng-teahouse");
            Assert.That(s.Stamina,Is.EqualTo(40));var news=s.LatestTeaVisit;
            yield return Drag(sword,ContainerId.Location,0,0);yield return Drag(sword,ContainerId.LeftHand,0,0);
            yield return Click("TeaLocationNews");yield return Click("TeaNewsClose");
            Assert.That(s.LatestTeaVisit,Is.SameAs(news));
            yield return ReturnCurrentOuting();
            Assert.That(s.Find(pack.Id),Is.SameAs(pack));Assert.That(s.Find(dew.Id),Is.SameAs(dew));
            Assert.That(dew.StorageItemId,Is.EqualTo(pack.Id));Assert.That(s.Find(sword.Id),Is.SameAs(sword));
            Assert.That(s.Find(herb.Id),Is.SameAs(herb));Assert.That(s.Stamina,Is.EqualTo(40));
            yield return Click("AdvanceTurn");
            Assert.That(s.Turn,Is.EqualTo(2));Assert.That(s.Stamina,Is.EqualTo(70));
            Assert.That(s.ActiveTeaEffect,Is.SameAs(news));Assert.That(s.HasTravelledThisTurn,Is.False);
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5+(news.effect==TeaEffect.Promotion?s.Catalog.teaHouse.extraCustomers:0)));
            yield return CompleteDefaultBusiness();
            // No pack this month; both hand slots still use the real inventory.
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_baishitang");Assert.That(s.Stamina,Is.EqualTo(10));
            var candidates=s.CommissionCandidates.ToArray();Assert.That(candidates.Length,Is.EqualTo(3));
            var commission=candidates.First(t=>s.Catalog.commissions.rewardPools.Single(p=>p.id==t.rewardPoolId).items.Length>0);
            yield return Click("CommissionOpen");yield return Click("CommissionComplete_"+commission.id);
            var reward=s.In(ContainerId.Location).First();yield return Drag(reward,ContainerId.LeftHand,0,0);
            Assert.That(s.CompleteCommission(commission.id),Is.False);
            yield return ReturnCurrentOuting();Assert.That(s.Find(reward.Id),Is.SameAs(reward));
            Assert.That(reward.Container,Is.EqualTo(ContainerId.Storage));
            yield return Click("AdvanceTurn");Assert.That(s.Stamina,Is.EqualTo(40));Assert.That(s.ActiveTeaEffect,Is.Null);
            yield return CompleteDefaultBusiness();
            // Existing costs require a recovery month. Integration must not silently refill stamina.
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            Assert.That(shop.FindButton("TravelLocation_baishitang").interactable,Is.False);
            yield return Click("TravelReturn");yield return Click("CarryReturn");yield return Click("AdvanceTurn");
            Assert.That(s.Stamina,Is.EqualTo(70));yield return CompleteDefaultBusiness();
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_baishitang");
            Assert.That(s.CommissionLimitReached,Is.False);Assert.That(s.CommissionCandidates.Count(),Is.EqualTo(3));
            yield return Click("CommissionOpen");yield return Click("CommissionComplete_"+s.CommissionCandidates.First().id);
            Assert.That(s.CommissionLimitReached);yield return ReturnCurrentOuting();
            Assert.That(s.TrySpendStamina(5));Assert.That(s.Stamina,Is.EqualTo(5));
            yield return Click("AdvanceTurn");Assert.That(s.Stamina,Is.EqualTo(35));
            Assert.That(shop.Session,Is.SameAs(s));Assert.That(s.Calendar,Is.SameAs(calendar));
            Assert.That(s.Find(reward.Id),Is.SameAs(reward));Assert.That(s.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP59")]
        public IEnumerator DefaultSessionOverflowDoesNotAccumulateExtraVisitors()
        {
            IsolateAlchemyTestMouse();var s=shop.Session;
            // The scene-open command must also be safe when invoked directly during Play.
            var editorType=System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a=>a.GetType("XiuXianShop.Editor.PrototypeBuilder")).First(t=>t!=null);
            editorType.GetMethod("OpenPrototype").Invoke(null,null);
            Assert.That(shop.Session,Is.SameAs(s));
            for(int month=1;month<=3;month++)
            {
                Assert.That(s.CustomerCountThisTurn,Is.EqualTo(month==1?5:6));
                yield return CompleteDefaultBusiness();yield return Click("AdvanceTurn");
                Assert.That(s.Stamina,Is.EqualTo(100));Assert.That(s.HasStaminaOverflowCustomer);
                Assert.That(s.ServedToday,Is.Zero);Assert.That(s.RemainingCustomers,Is.Zero);Assert.That(s.Offer,Is.Null);
                Assert.That(shop.Session,Is.SameAs(s));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP59")]
        public IEnumerator DefaultSessionAlchemyDevelopmentMenusPreserveProgressAndContinueBusiness()
        {
            IsolateAlchemyTestMouse();var s=shop.Session;
            var asset=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Data/ShopCatalog.asset");
            string assetBefore=JsonUtility.ToJson(asset);
            var original=s.Items.ToArray();var calendar=s.Calendar;
            // The normal warehouse is intentionally finite. Make room using its existing chest.
            var chest=s.Items.Single(i=>i.Definition.category==ItemCategory.StorageContainer);
            shop.SelectItem(chest.Id);yield return null;
            yield return Drag(original.First(i=>i.Definition.id=="sword"),ContainerId.Interior,0,0);
            yield return Drag(original.First(i=>i.Definition.id=="herb"),ContainerId.Interior,2,0);
            yield return Drag(original.First(i=>i.Definition.id=="dew"),ContainerId.Interior,4,0);
            yield return Click("StorageClose");
            yield return CompleteDefaultBusiness();
            int money=s.Money,count=s.Items.Count,served=s.ServedToday;
            Assert.That(EditorApplication.ExecuteMenuItem("XiuXianShop/Current Play Session/Unlock Alchemy Room (no reset)"));
            Assert.That(EditorApplication.ExecuteMenuItem("XiuXianShop/Current Play Session/Unlock Alchemy Room (no reset)"));
            Assert.That(s.Items.Count,Is.EqualTo(count));
            Assert.That(EditorApplication.ExecuteMenuItem("XiuXianShop/Current Play Session/Grant Alchemy Kit/回气丹"));
            yield return null;
            Assert.That(s.Message,Does.Contain("测试材料包已放入"));
            Assert.That(shop.Session,Is.SameAs(s));Assert.That(s.Calendar,Is.SameAs(calendar));
            Assert.That(s.Phase,Is.EqualTo(TurnPhase.Closed));Assert.That(s.Money,Is.EqualTo(money));
            Assert.That(s.ServedToday,Is.EqualTo(served));Assert.That(s.Stamina,Is.EqualTo(100));
            foreach(var item in original)Assert.That(s.Find(item.Id),Is.SameAs(item));
            Assert.That(s.Catalog.items.Select(d=>d.id).Distinct().Count(),Is.EqualTo(s.Catalog.items.Length));
            Assert.That(JsonUtility.ToJson(asset),Is.EqualTo(assetBefore));
            var pack=s.PortableStorage.Single(i=>i.Definition.id==AlchemyVerification.MaterialPackId);
            var ingredients=s.In(ContainerId.Interior,pack.Id).Where(i=>i.Definition.spiritResource==null).ToArray();
            var fuel=s.In(ContainerId.Interior,pack.Id).Single(i=>i.Definition.spiritResource!=null);
            yield return Click("CarryOpen");yield return Click("CarryOption_"+pack.Id);yield return Click("CarryConfirm");
            yield return Click("TravelBegin");yield return Click("TravelLocation_"+ShopSession.AlchemyLocationId);
            yield return Click("AlchemyRecipe_recipe_pill_basic");
            for(int i=0;i<ingredients.Length;i++)yield return Drag(ingredients[i],ContainerId.Location,i*2,0);
            yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);
            shop.SelectItem(ingredients[0].Id);yield return Click("AlchemyAdd");yield return Click("AlchemyStart");
            yield return new WaitUntil(()=>s.Alchemy.Time>=8);
            shop.SelectItem(ingredients[1].Id);yield return Click("AlchemyAdd");
            yield return new WaitUntil(()=>s.Alchemy.Time>=16);yield return Click("AlchemyCollect");
            Assert.That(s.Alchemy.Quality,Is.Not.EqualTo(PillQuality.Ruined));
            var product=s.In(ContainerId.AlchemyOutput).Single();
            yield return Drag(product,ContainerId.LeftHand,0,0);yield return Drag(fuel,ContainerId.RightHand,0,0);
            int spirit=fuel.SpiritUnits;Assert.That(spirit,Is.LessThan(fuel.Definition.spiritResource.CapacityUnits));
            yield return ReturnCurrentOuting();yield return Click("AdvanceTurn");
            yield return CompleteDefaultBusiness();yield return Click("AdvanceTurn");
            Assert.That(s.Turn,Is.EqualTo(3));Assert.That(shop.Session,Is.SameAs(s));
            Assert.That(s.Find(product.Id),Is.SameAs(product));Assert.That(s.Find(fuel.Id),Is.SameAs(fuel));
            Assert.That(fuel.SpiritUnits,Is.EqualTo(spirit));Assert.That(JsonUtility.ToJson(asset),Is.EqualTo(assetBefore));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
