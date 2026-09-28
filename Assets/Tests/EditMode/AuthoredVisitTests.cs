using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP62")]
    public sealed class AuthoredVisitTests
    {
        ShopCatalog catalog;
        AuthoredContent content;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();
            content=ScriptableObject.CreateInstance<AuthoredContent>();catalog.authoredContent=content;
        }
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(catalog);UnityEngine.Object.DestroyImmediate(content);}
        static AuthoredVisit Visit(string id,int turn=1,int order=0,string phase="BeforeOrdinary") => new AuthoredVisit {
            id=id,customerId="same_customer",displayName="编排顾客",queuePhase=phase,order=order,
            fixedTurn=new ContentOptionalInt{hasValue=true,value=turn},buyingCategory="Medicine",
            budget=new ContentOptionalInt{hasValue=true,value=40},completion="TradeSuccess"};
        static List<string> Arrivals(ShopSession session)
        {
            Assert.That(session.BeginBusiness());var ids=new List<string>();
            while(session.Offer!=null){ids.Add(session.Offer.VisitId??"ordinary");Assert.That(session.NextCustomer());}
            return ids;
        }
        static void NextTurn(ShopSession session){Assert.That(session.EndBusiness());Assert.That(session.AdvanceTurn());}

        [Test] public void AllMatchingVisitsAppearInExplicitOrderWithoutReplacingOrdinaryCustomers()
        {
            content.visits=new[]{Visit("z",order:5),Visit("a",order:5),Visit("last",phase:"AfterOrdinary")};
            var session=new ShopSession(catalog,customerSeed:62);
            var ids=Arrivals(session);
            Assert.That(ids,Is.EqualTo(new[]{"a","z","ordinary","ordinary","ordinary","ordinary","ordinary","last"}));
            Assert.That(session.SpecialVisitsThisTurn,Is.EqualTo(3));
            Assert.That(session.BuyersToday+session.SuppliersToday+session.TradingCustomersToday,Is.EqualTo(5));
            Assert.That(session.BeginBusiness(),Is.False);
        }
        [Test] public void SameCustomerUsesSeparateFixedVisitsAndNeverAppearsOnNeighbourTurns()
        {
            content.visits=new[]{Visit("third",3),Visit("seventh",7)};
            var session=new ShopSession(catalog,customerSeed:62);
            for(int turn=1;turn<=8;turn++)
            {
                var special=Arrivals(session).Where(id=>id!="ordinary").ToArray();
                Assert.That(special,Is.EqualTo(turn==3?new[]{"third"}:turn==7?new[]{"seventh"}:Array.Empty<string>()));
                NextTurn(session);
            }
        }
        [Test] public void RequiredForbiddenAndFixedTurnConditionsMustAllMatch()
        {
            var visit=Visit("conditional",2);visit.requiredFlags="story.ready";visit.forbiddenFlags="story.blocked";
            content.visits=new[]{visit};
            foreach(int scenario in new[]{0,1,2})
            {
                var session=new ShopSession(catalog,customerSeed:62);
                if(scenario>0)session.SetProgressFlag("story.ready");
                if(scenario==2)session.SetProgressFlag("story.blocked");
                Assert.That(Arrivals(session).Contains("conditional"),Is.False);NextTurn(session);
                Assert.That(Arrivals(session).Contains("conditional"),Is.EqualTo(scenario==1));
            }
        }
        [Test] public void FixedGoodsBudgetAndIdentityComeFromContent()
        {
            content.visits=new[]{Visit("fixed")};
            content.visitItems=new[]{new AuthoredVisitItem{id="g1",visitId="fixed",itemId="herb",quantity=1},new AuthoredVisitItem{id="g2",visitId="fixed",itemId="dew",quantity=1}};
            var session=new ShopSession(catalog,customerSeed:62);Assert.That(session.BeginBusiness());
            Assert.That(session.Offer.SupplierItems.Select(i=>i.Definition.id),Is.EqualTo(new[]{"herb","dew"}));
            Assert.That(session.Offer.RemainingBudget,Is.EqualTo(40));
            Assert.That(session.Offer.CustomerId,Is.EqualTo("same_customer"));
            Assert.That(session.Offer.Behavior,Is.EqualTo(CustomerBehavior.Trading));
        }
        [Test] public void SceneHandoffRequiresMatchingRequestAndReachedEndForCompletion()
        {
            var visit=Visit("dialogue");visit.completion="SceneEnd";visit.arrivalSceneId="arrival";content.visits=new[]{visit};
            content.scenes=new[]{new AuthoredScene{id="arrival",entryNodeId="end"}};
            content.nodes=new[]{new AuthoredNode{id="end",sceneId="arrival",type="End"}};
            var session=new ShopSession(catalog);Assert.That(session.BeginBusiness());
            var request=session.PendingVisitScene;
            Assert.That(request.Scene.id,Is.EqualTo("arrival"));Assert.That(request.Nodes.Single().id,Is.EqualTo("end"));
            Assert.That(session.NextCustomer(),Is.False);Assert.That(session.EndBusiness(),Is.False);
            Assert.That(session.FinishVisitScene(new VisitSceneRequest(),true),Is.False);
            Assert.That(session.HasCompletedVisit("dialogue"),Is.False);
            Assert.That(session.FinishVisitScene(request,true));Assert.That(session.HasCompletedVisit("dialogue"));
            Assert.That(session.FinishVisitScene(request,true),Is.False);
            Assert.That(session.EndBusiness());
            Assert.That(ShopSession.RestoreSave(catalog,session.CaptureSave()).HasCompletedVisit("dialogue"));
        }

        [Test] public void GrayboxReadsAuthoredTextAndAppliesHintAndLocationOnlyOnEnd()
        {
            var visit=Visit("hint");visit.completion="SceneEnd";visit.arrivalSceneId="arrival";content.visits=new[]{visit};
            catalog.travelLocations=catalog.travelLocations.Concat(new[]{new TravelLocation{id="training",title="求学"}}).ToArray();
            content.scenes=new[]{new AuthoredScene{id="arrival",entryNodeId="line"}};
            content.nodes=new[]{
                new AuthoredNode{id="line",sceneId="arrival",type="Dialogue",speaker="访客",text="新导入的对白",nextNodeIds=new[]{"flag"}},
                new AuthoredNode{id="flag",sceneId="arrival",type="Action",actionType="SetFlag",actionTarget="story.hint",actionValue="true",nextNodeIds=new[]{"unlock"}},
                new AuthoredNode{id="unlock",sceneId="arrival",type="Action",actionType="UnlockLocation",actionTarget="training",actionValue="true",nextNodeIds=new[]{"end"}},
                new AuthoredNode{id="end",sceneId="arrival",type="End"}};
            var cancelled=new ShopSession(catalog);Assert.That(cancelled.BeginBusiness());
            var preview=new VisitSceneGraybox(cancelled,cancelled.PendingVisitScene);
            Assert.That(preview.Lines.Single(),Does.Contain("新导入的对白"));Assert.That(preview.Cancel());
            Assert.That(cancelled.HasProgressFlag("story.hint"),Is.False);
            Assert.That(cancelled.UnlockedLocations.Any(l=>l.id=="training"),Is.False);
            Assert.That(cancelled.HasCompletedVisit("hint"),Is.False);
            var completed=new ShopSession(catalog);Assert.That(completed.BeginBusiness());
            preview=new VisitSceneGraybox(completed,completed.PendingVisitScene);Assert.That(preview.Finish());
            Assert.That(completed.HasProgressFlag("story.hint"));Assert.That(completed.UnlockedLocations.Count(l=>l.id=="training"),Is.EqualTo(1));
            Assert.That(preview.Finish(),Is.False);Assert.That(completed.HasCompletedVisit("hint"));
        }

        [Test] public void TradeResultRequiresSuccessfulAtomicSettlementAndIsNotRepeated()
        {
            var visit=Visit("trade");visit.tradeSceneId="result";content.visits=new[]{visit};
            content.scenes=new[]{new AuthoredScene{id="result",entryNodeId="flag"}};
            content.nodes=new[]{new AuthoredNode{id="flag",sceneId="result",type="Action",actionType="SetFlag",actionTarget="story.traded",actionValue="true",nextNodeIds=new[]{"end"}},
                new AuthoredNode{id="end",sceneId="result",type="End"}};
            var session=new ShopSession(catalog);Assert.That(session.BeginBusiness());
            int money=session.Money;
            Assert.That(session.AcceptTrade(),Is.False);Assert.That(session.Money,Is.EqualTo(money));
            Assert.That(session.PendingVisitScene,Is.Null);Assert.That(session.HasCompletedVisit("trade"),Is.False);
            Assert.That(session.StageSale());Assert.That(session.AcceptTrade());
            Assert.That(session.HasCompletedVisit("trade"));Assert.That(session.PendingVisitScene.Trigger,Is.EqualTo("TradeSuccess"));
            var preview=new VisitSceneGraybox(session,session.PendingVisitScene);Assert.That(preview.Finish());
            Assert.That(session.HasProgressFlag("story.traded"));Assert.That(preview.Finish(),Is.False);
            var skipped=new ShopSession(catalog);Assert.That(skipped.BeginBusiness());Assert.That(skipped.RejectTrade());
            Assert.That(skipped.HasCompletedVisit("trade"),Is.False);Assert.That(skipped.HasProgressFlag("story.traded"),Is.False);
        }

        [Test] public void InvalidResultTargetDoesNotApplyEarlierFlag()
        {
            var visit=Visit("invalid");visit.completion="SceneEnd";visit.arrivalSceneId="result";content.visits=new[]{visit};
            content.scenes=new[]{new AuthoredScene{id="result",entryNodeId="flag"}};
            content.nodes=new[]{new AuthoredNode{id="flag",sceneId="result",type="Action",actionType="SetFlag",actionTarget="story.no_partial",actionValue="true",nextNodeIds=new[]{"bad"}},
                new AuthoredNode{id="bad",sceneId="result",type="Action",actionType="UnlockLocation",actionTarget="unknown",actionValue="true",nextNodeIds=new[]{"end"}},
                new AuthoredNode{id="end",sceneId="result",type="End"}};
            var session=new ShopSession(catalog);Assert.That(session.BeginBusiness());
            Assert.That(new VisitSceneGraybox(session,session.PendingVisitScene).Finish(),Is.False);
            Assert.That(session.HasProgressFlag("story.no_partial"),Is.False);Assert.That(session.HasCompletedVisit("invalid"),Is.False);
        }

        [Test] public void OneTimeTrainingDestinationCannotBeVisitedAgainAfterSave()
        {
            VisitVerification.Configure(catalog,content);
            var session=new ShopSession(catalog);Assert.That(session.UnlockLocation(VisitVerification.TrainingLocationId));
            Assert.That(session.BeginBusiness());Assert.That(session.EndBusiness());
            Assert.That(session.BeginCarrying(0));Assert.That(session.BeginTravel());Assert.That(session.EnterLocation(VisitVerification.TrainingLocationId));
            Assert.That(session.LeaveLocation());Assert.That(session.ReturnToShop(true));Assert.That(session.EndCarrying());
            var loaded=ShopSession.RestoreSave(catalog,session.CaptureSave());
            Assert.That(loaded.UnlockedLocations.Any(l=>l.id==VisitVerification.TrainingLocationId),Is.False);
            Assert.That(loaded.AdvanceTurn());Assert.That(loaded.BeginBusiness());Assert.That(loaded.EndBusiness());
            Assert.That(loaded.BeginCarrying(0));Assert.That(loaded.BeginTravel());
            Assert.That(loaded.EnterLocation(VisitVerification.TrainingLocationId),Is.False);
        }

        [Test] public void SpecialVisitsDoNotConsumeOrdinaryRandomDrawsAndUnresolvedVisitsReturn()
        {
            var visit=Visit("repeat");visit.fixedTurn=default;content.visits=new[]{visit};
            var authored=new ShopSession(catalog,customerSeed:62);
            Assert.That(authored.TotalCustomersThisTurn,Is.EqualTo(6));Assert.That(authored.BeginBusiness());
            Assert.That(authored.NextCustomer());
            catalog.authoredContent=null;var ordinary=new ShopSession(catalog,customerSeed:62);Assert.That(ordinary.BeginBusiness());
            while(ordinary.Offer!=null)
            {
                Assert.That(authored.Offer.VisitId,Is.Null);
                Assert.That(authored.Offer.RequestedCategory,Is.EqualTo(ordinary.Offer.RequestedCategory));
                Assert.That(authored.Offer.RemainingBudget,Is.EqualTo(ordinary.Offer.RemainingBudget));
                Assert.That(authored.Offer.SupplierItems.Select(i=>i.Definition.id),Is.EqualTo(ordinary.Offer.SupplierItems.Select(i=>i.Definition.id)));
                Assert.That(authored.NextCustomer());Assert.That(ordinary.NextCustomer());
            }
            catalog.authoredContent=content;NextTurn(authored);
            Assert.That(authored.BeginBusiness());Assert.That(authored.Offer.VisitId,Is.EqualTo("repeat"));
        }

        [Test] public void SkipSceneCannotUnlockBeforeSceneEndCompletion()
        {
            var visit=Visit("skip");visit.completion="SceneEnd";visit.skipSceneId="skip_scene";content.visits=new[]{visit};
            content.scenes=new[]{new AuthoredScene{id="skip_scene",entryNodeId="unlock"}};
            content.nodes=new[]{new AuthoredNode{id="unlock",sceneId="skip_scene",type="Action",actionType="UnlockLocation",actionTarget="baishitang",actionValue="true",nextNodeIds=new[]{"end"}},
                new AuthoredNode{id="end",sceneId="skip_scene",type="End"}};
            var session=new ShopSession(catalog);Assert.That(session.BeginBusiness());Assert.That(session.NextCustomer());
            Assert.That(session.PendingVisitScene.Trigger,Is.EqualTo("Skip"));
            var preview=new VisitSceneGraybox(session,session.PendingVisitScene);
            Assert.That(preview.Finish(),Is.False);Assert.That(session.Message,Does.Contain("不能提前解锁"));
            Assert.That(session.HasCompletedVisit("skip"),Is.False);Assert.That(preview.Cancel());Assert.That(session.NextCustomer());
        }

        [Test] public void ConditionalWindowAndPrerequisiteCompletionSurviveSave()
        {
            var first=Visit("first");first.completion="SceneEnd";first.arrivalSceneId="end_scene";
            var next=Visit("next");next.fixedTurn=default;next.earliestTurn=new ContentOptionalInt{hasValue=true,value=2};
            next.latestTurn=new ContentOptionalInt{hasValue=true,value=2};next.prerequisiteVisitId="first";
            content.visits=new[]{first,next};content.scenes=new[]{new AuthoredScene{id="end_scene",entryNodeId="end"}};
            content.nodes=new[]{new AuthoredNode{id="end",sceneId="end_scene",type="End"}};
            foreach(bool complete in new[]{false,true})
            {
                var session=new ShopSession(catalog);Assert.That(session.BeginBusiness());
                Assert.That(session.SpecialVisitsThisTurn,Is.EqualTo(1));Assert.That(session.FinishVisitScene(session.PendingVisitScene,complete));
                Assert.That(session.EndBusiness());session=ShopSession.RestoreSave(catalog,session.CaptureSave());
                Assert.That(session.AdvanceTurn());Assert.That(Arrivals(session).Contains("next"),Is.EqualTo(complete));
                NextTurn(session);Assert.That(Arrivals(session).Contains("next"),Is.False);
            }
        }

        [Test] public void DisplayTeaAndOverflowOnlyModifyOrdinaryVisitors()
        {
            content.visits=new[]{Visit("fixed",2)};
            content.visitItems=new[]{new AuthoredVisitItem{id="goods",visitId="fixed",itemId="dew",quantity=2}};
            catalog.firstLocationStaminaCost=0;
            foreach(var effect in catalog.teaHouse.effects)effect.weight=effect.effect==TeaEffect.Promotion?1:0;
            var session=new ShopSession(catalog,customerSeed:62);
            Assert.That(session.BeginBusiness());Assert.That(session.EndBusiness());Assert.That(session.BeginCarrying(0));
            Assert.That(session.BeginTravel());Assert.That(session.EnterLocation("tingfeng-teahouse"));Assert.That(session.LeaveLocation());
            Assert.That(session.ReturnToShop(true));Assert.That(session.EndCarrying());Assert.That(session.AdvanceTurn());
            Assert.That(session.HasStaminaOverflowCustomer);Assert.That(session.ActiveTeaEffect.effect,Is.EqualTo(TeaEffect.Promotion));
            var herb=session.Items.First(i=>i.Definition.id=="herb");Assert.That(session.Move(herb.Id,ContainerId.Display,0,0,0,false));
            int ordinary=session.CustomerCountThisTurn;Assert.That(ordinary,Is.GreaterThan(5));Assert.That(session.TotalCustomersThisTurn,Is.EqualTo(ordinary+1));
            Assert.That(session.BeginBusiness());Assert.That(session.Offer.VisitId,Is.EqualTo("fixed"));
            Assert.That(session.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));Assert.That(session.Offer.RemainingBudget,Is.EqualTo(40));
            Assert.That(session.Offer.SupplierItems.Select(i=>i.Definition.id),Is.EqualTo(new[]{"dew","dew"}));
            int count=0;while(session.Offer!=null){count++;Assert.That(session.NextCustomer());}
            Assert.That(count,Is.EqualTo(ordinary+1));
        }

        [Test] public void OnceAppearanceProgressRoundTripsWithoutCopyingDefinitions()
        {
            var visit=Visit("once");visit.fixedTurn=default;visit.repeatPolicy="Once";content.visits=new[]{visit};
            var session=new ShopSession(catalog,customerSeed:62);Assert.That(Arrivals(session).Contains("once"));
            Assert.That(session.EndBusiness());string json=session.CaptureSave();
            Assert.That(json,Does.Not.Contain("编排顾客"));
            var loaded=ShopSession.RestoreSave(catalog,json);Assert.That(loaded.HasAppearedVisit("once"));
            Assert.That(loaded.AdvanceTurn());Assert.That(Arrivals(loaded).Contains("once"),Is.False);
        }
    }
}
