using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    [Serializable]
    public sealed class VisitProgress
    {
        public string visitId;
        public int lastQueuedTurn, lastAppearedTurn;
        public bool completed;
    }

    // A stable handoff to DP-52. Content is shared, never copied into the save.
    public sealed class VisitSceneRequest
    {
        public string VisitId { get; internal set; }
        public string Trigger { get; internal set; }
        public string LocationId { get; internal set; }
        public AuthoredScene Scene { get; internal set; }
        public IReadOnlyList<AuthoredNode> Nodes { get; internal set; }
    }

    public sealed partial class ShopSession
    {
        readonly Dictionary<string,VisitProgress> visitProgress = new Dictionary<string,VisitProgress>();
        public VisitSceneRequest PendingVisitScene { get; private set; }
        bool visitTradeSucceeded,visitSkipHandled;
        public int SpecialVisitsThisTurn { get; private set; }
        public int EligibleSpecialVisitCount => catalog.authoredContent==null?0:catalog.authoredContent.visits.Count(VisitEligible);
        public int TotalCustomersThisTurn => CustomerCountThisTurn + (Phase==TurnPhase.Preparation?EligibleSpecialVisitCount:SpecialVisitsThisTurn);
        public bool HasCompletedVisit(string id) => visitProgress.TryGetValue(id,out var p) && p.completed;
        public bool HasAppearedVisit(string id) => visitProgress.TryGetValue(id,out var p) && p.lastAppearedTurn>0;

        static string[] FlagIds(string ids) => (ids??"").Split(new[]{'\n','\r',','},StringSplitOptions.RemoveEmptyEntries).Select(id=>id.Trim()).ToArray();
        bool VisitEligible(AuthoredVisit visit)
        {
            if(!visit.enabled)return false;
            if(visitProgress.TryGetValue(visit.id,out var progress) &&
                (progress.completed || progress.lastQueuedTurn==Turn || (visit.repeatPolicy=="Once" && progress.lastAppearedTurn>0)))return false;
            return (!visit.fixedTurn.hasValue || visit.fixedTurn.value==Turn) &&
                (!visit.earliestTurn.hasValue || Turn>=visit.earliestTurn.value) &&
                (!visit.latestTurn.hasValue || Turn<=visit.latestTurn.value) &&
                FlagIds(visit.requiredFlags).All(HasProgressFlag) && !FlagIds(visit.forbiddenFlags).Any(HasProgressFlag) &&
                (string.IsNullOrEmpty(visit.prerequisiteVisitId) || HasCompletedVisit(visit.prerequisiteVisitId));
        }

        void AddAuthoredVisits()
        {
            var content=catalog.authoredContent;
            SpecialVisitsThisTurn=0;
            if(content==null)return;
            var visits=content.visits.Where(VisitEligible).OrderBy(v=>v.order).ThenBy(v=>v.id,StringComparer.Ordinal).ToArray();
            var ordinary=queue.ToArray();queue.Clear();
            void Add(AuthoredVisit visit)
            {
                var supplies=content.visitItems.Where(i=>i.enabled && i.visitId==visit.id)
                    .SelectMany(i=>Enumerable.Repeat(i.itemId,i.quantity)).ToArray();
                var category=string.IsNullOrEmpty(visit.buyingCategory)?ItemCategory.Unclassified:(ItemCategory)Enum.Parse(typeof(ItemCategory),visit.buyingCategory);
                var behavior=category==ItemCategory.Unclassified?CustomerBehavior.Selling:supplies.Length==0?CustomerBehavior.Buying:CustomerBehavior.Trading;
                queue.Enqueue(new CustomerRequest {visit=visit,definitionIds=supplies,category=category,budget=visit.budget.hasValue?visit.budget.value:0,
                    behavior=behavior,direction=behavior==CustomerBehavior.Buying?TradeDirection.CustomerBuys:TradeDirection.CustomerSells});
                if(!visitProgress.TryGetValue(visit.id,out var progress))visitProgress.Add(visit.id,progress=new VisitProgress {visitId=visit.id});
                progress.lastQueuedTurn=Turn;
            }
            foreach(var visit in visits.Where(v=>v.queuePhase=="BeforeOrdinary"))Add(visit);
            foreach(var request in ordinary)queue.Enqueue(request);
            foreach(var visit in visits.Where(v=>v.queuePhase=="AfterOrdinary"))Add(visit);
            SpecialVisitsThisTurn=visits.Length;
        }

        void RecordVisitArrival(AuthoredVisit visit)
        {
            visitProgress[visit.id].lastAppearedTurn=Turn;
            visitTradeSucceeded=false;visitSkipHandled=false;
            RequestVisitScene(visit,visit.arrivalSceneId,"Arrival");
        }

        AuthoredVisit CurrentVisit => string.IsNullOrEmpty(Offer?.VisitId)?null:catalog.authoredContent.visits.Single(v=>v.id==Offer.VisitId);
        void RequestVisitScene(AuthoredVisit visit,string sceneId,string trigger)
        {
            if(string.IsNullOrEmpty(sceneId))return;
            PendingVisitScene=new VisitSceneRequest {VisitId=visit.id,Trigger=trigger,
                Scene=catalog.authoredContent.scenes.Single(s=>s.enabled && s.id==sceneId),
                Nodes=catalog.authoredContent.nodes.Where(n=>n.enabled && n.sceneId==sceneId).ToArray()};
        }
        void RequestLocationScene(TravelLocation location,AuthoredScene scene)
        {
            PendingVisitScene=new VisitSceneRequest {LocationId=location.id,Trigger="Location",
                Scene=scene,Nodes=catalog.authoredContent.nodes.Where(n=>n.enabled && n.sceneId==scene.id).ToArray()};
        }
        public bool FinishVisitScene(VisitSceneRequest request,bool reachedEnd,IReadOnlyList<AuthoredNode> actions=null)
        {
            if(request==null || !ReferenceEquals(request,PendingVisitScene))return Fail("剧情请求已失效。");
            if(reachedEnd && actions!=null)
            {
                if(actions.Count>0 && CurrentVisit?.completion=="TradeSuccess" && !visitTradeSucceeded)
                    return Fail("该来访要求先完成交易，不能提前执行结果动作。");
                foreach(var action in actions)
                {
                    if(!request.Nodes.Contains(action) || action.type!="Action" || action.actionValue!="true")return Fail("剧情结果不属于当前请求。");
                    if(action.actionType.StartsWith("Unlock",StringComparison.Ordinal) &&
                        !(request.Trigger=="Location" || (CurrentVisit?.completion=="TradeSuccess"?visitTradeSucceeded:request.Trigger=="Arrival")))
                        return Fail("尚未达到来访完成条件，不能提前解锁。");
                    if(action.actionType=="SetFlag" && !string.IsNullOrWhiteSpace(action.actionTarget))continue;
                    if(action.actionType=="UnlockLocation" && catalog.travelLocations.Any(l=>l.id==action.actionTarget))continue;
                    if(action.actionType=="UnlockRecipe" && catalog.alchemy.recipes.Any(r=>r.id==action.actionTarget))continue;
                    if(action.actionType=="UnlockProfession" && action.actionTarget=="alchemy")continue;
                    return Fail("当前灰盒不支持此结果目标："+action.actionTarget);
                }
                foreach(var action in actions)
                    if(action.actionType=="UnlockLocation")UnlockLocation(action.actionTarget);
                    else SetProgressFlag(action.actionType=="UnlockRecipe"?"recipe:"+action.actionTarget:
                        action.actionType=="UnlockProfession"?"profession:"+action.actionTarget:action.actionTarget);
            }
            PendingVisitScene=null;
            if(reachedEnd)
            {
                SetProgressFlag("scene:"+request.Scene.id);
                if(request.Trigger=="Location" && catalog.travelLocations.Single(l=>l.id==request.LocationId).singleVisit)
                    SetProgressFlag("location-visited:"+request.LocationId);
            }
            if(reachedEnd && request.Trigger=="Arrival" && CurrentVisit.completion=="SceneEnd")
                visitProgress[request.VisitId].completed=true;
            return Success(reachedEnd?"来访剧情已结束。":"已取消来访剧情，未写入完成状态。");
        }
        void RecordVisitTrade()
        {
            var visit=CurrentVisit;
            if(visit==null || visitTradeSucceeded)return;
            visitTradeSucceeded=true;
            if(visit.completion=="TradeSuccess")visitProgress[visit.id].completed=true;
            RequestVisitScene(visit,visit.tradeSceneId,"TradeSuccess");
        }
        bool RequestVisitSkip()
        {
            var visit=CurrentVisit;
            if(visit==null || visitSkipHandled || HasCompletedVisit(visit.id))return false;
            visitSkipHandled=true;
            RequestVisitScene(visit,visit.skipSceneId,"Skip");
            return PendingVisitScene!=null;
        }

        void RestoreVisitProgress(VisitProgress[] saved)
        {
            // Existing DP-61 saves predate visits; absence means no visit progress yet.
            foreach(var progress in saved??Array.Empty<VisitProgress>())
            {
                if(progress==null || string.IsNullOrWhiteSpace(progress.visitId) || visitProgress.ContainsKey(progress.visitId) ||
                    progress.lastQueuedTurn<1 || progress.lastQueuedTurn>Turn || progress.lastAppearedTurn<0 ||
                    progress.lastAppearedTurn>progress.lastQueuedTurn || (progress.completed && progress.lastAppearedTurn==0))
                    throw new ArgumentException("存档来访进度无效。");
                visitProgress.Add(progress.visitId,progress);
            }
        }
    }
}
