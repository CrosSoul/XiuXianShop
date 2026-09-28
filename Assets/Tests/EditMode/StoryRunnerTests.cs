using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP52")]
    public sealed class StoryRunnerTests
    {
        ShopCatalog catalog;
        AuthoredContent content;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();
            catalog.travelLocations=catalog.travelLocations.Concat(new[]{new TravelLocation{id=ShopSession.AlchemyLocationId,title="炼丹房"}}).ToArray();
            content=ScriptableObject.CreateInstance<AuthoredContent>();catalog.authoredContent=content;
            content.scenes=new[]{new AuthoredScene{id="scene",entryNodeId="line"}};
            content.visits=new[]{new AuthoredVisit{id="visit",customerId="person",displayName="访客",queuePhase="BeforeOrdinary",completion="SceneEnd",arrivalSceneId="scene"}};
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(content);Object.DestroyImmediate(catalog);}
        static AuthoredNode Node(string id,string type,params string[] next)=>new AuthoredNode{id=id,sceneId="scene",type=type,nextNodeIds=next};
        StoryRunner Start(out ShopSession session)
        {
            session=new ShopSession(catalog);Assert.That(session.BeginBusiness());return new StoryRunner(session,session.PendingVisitScene);
        }
        [TestCase(0,"left")][TestCase(1,"right")]
        public void ChoiceBranchUsesPendingFlagsAndCommitsOnlyAtEnd(int option,string expected)
        {
            var line=Node("line","Dialogue","choice");line.text="开场";
            var choice=Node("choice","Choice","set","branch");choice.choices=new[]{"接受","拒绝"};
            var set=Node("set","Action","branch");set.actionType="SetFlag";set.actionTarget="accepted";set.actionValue="true";
            var branch=Node("branch","Branch","left","right");branch.conditionType="FlagExists";branch.conditionKey="accepted";
            var left=Node("left","Dialogue","unlock");left.text="接受结果";
            var right=Node("right","Dialogue","end");right.text="拒绝结果";
            var unlock=Node("unlock","Action","end");unlock.actionType="UnlockProfession";unlock.actionTarget="alchemy";unlock.actionValue="true";
            content.nodes=new[]{line,choice,set,branch,left,right,unlock,Node("end","End")};
            var runner=Start(out var session);Assert.That(runner.Current.text,Is.EqualTo("开场"));Assert.That(runner.Continue());Assert.That(runner.Choose(option));
            Assert.That(runner.Current.id,Is.EqualTo(expected));Assert.That(session.HasProgressFlag("accepted"),Is.False);
            Assert.That(runner.Continue());Assert.That(runner.Finished);Assert.That(session.HasProgressFlag("profession:alchemy"),Is.EqualTo(option==0));
            Assert.That(session.HasProgressFlag("scene:scene"));Assert.That(session.PendingVisitScene,Is.Null);
            Assert.That(session.HasCompletedVisit("visit"));Assert.That(runner.Continue(),Is.False);
        }
        [Test] public void VisualAndConditionalChoicesCancelWithoutResults()
        {
            var visual=Node("line","Visual","choice");visual.background="炼丹房";visual.comic="静态分镜占位";visual.portraitId="elder";visual.speaker="长老";visual.portraitSlot="Right";visual.expression="温和";
            var choice=Node("choice","Choice","end","end");choice.choices=new[]{"已知线索选项","离开"};
            choice.choiceConditionTypes=new[]{"FlagExists","None"};choice.choiceConditionKeys=new[]{"missing","-"};choice.choiceConditionValues=new[]{"true","true"};
            content.nodes=new[]{visual,choice,Node("end","End")};var runner=Start(out var session);
            Assert.That(runner.Background,Is.EqualTo("炼丹房"));Assert.That(runner.RightPortrait,Is.EqualTo("长老"));Assert.That(runner.Comic,Is.EqualTo("静态分镜占位"));
            Assert.That(runner.ChoiceAvailable(0),Is.False);Assert.That(runner.Choose(0),Is.False);Assert.That(runner.ChoiceAvailable(1));
            Assert.That(session.CanSave(out _),Is.False);Assert.That(session.NextCustomer(),Is.False);
            var item=session.Items.First();Assert.That(session.Move(item.Id,ContainerId.Display,0,0,0,false),Is.False);
            Assert.That(runner.Cancel());Assert.That(session.HasProgressFlag("scene:scene"),Is.False);
        }
        [Test] public void LocationReturnsToSameContextAndCompletedSceneDoesNotReplayAfterLoad()
        {
            content.visits=System.Array.Empty<AuthoredVisit>();content.scenes[0].locationId="baishitang";
            var line=Node("line","Dialogue","unlock");line.text="结束后返回地点";
            var unlock=Node("unlock","Action","again");unlock.actionType="UnlockLocation";unlock.actionTarget=ShopSession.AlchemyLocationId;unlock.actionValue="true";
            var again=Node("again","Action","end");again.actionType=unlock.actionType;again.actionTarget=unlock.actionTarget;again.actionValue="true";
            content.nodes=new[]{line,unlock,again,Node("end","End")};var session=new ShopSession(catalog);
            Assert.That(session.BeginBusiness());Assert.That(session.EndBusiness());Assert.That(session.BeginCarrying(0));Assert.That(session.BeginTravel());Assert.That(session.EnterLocation("baishitang"));
            int stamina=session.Stamina;Assert.That(session.LeaveLocation(),Is.False);Assert.That(session.ReturnToShop(true),Is.False);
            var runner=new StoryRunner(session,session.PendingVisitScene);Assert.That(runner.Continue());
            Assert.That(session.CurrentLocationId,Is.EqualTo("baishitang"));Assert.That(session.Stamina,Is.EqualTo(stamina));
            Assert.That(session.UnlockedLocations.Count(l=>l.id==ShopSession.AlchemyLocationId),Is.EqualTo(1));
            Assert.That(session.LeaveLocation());Assert.That(session.ReturnToShop(true));Assert.That(session.EndCarrying());
            session=ShopSession.RestoreSave(catalog,session.CaptureSave());Assert.That(session.HasProgressFlag("scene:scene"));
            Assert.That(session.AdvanceTurn());Assert.That(session.BeginBusiness());Assert.That(session.EndBusiness());Assert.That(session.BeginCarrying(0));Assert.That(session.BeginTravel());
            Assert.That(session.EnterLocation("baishitang"));Assert.That(session.PendingVisitScene,Is.Null);
        }
    }
}
