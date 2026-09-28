using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    // One Scene invocation. Results stay pending until End, including conditions later in this Scene.
    public sealed class StoryRunner
    {
        readonly ShopSession session;
        readonly Dictionary<string,AuthoredNode> nodes;
        readonly List<AuthoredNode> actions=new List<AuthoredNode>();
        public VisitSceneRequest Request { get; }
        public AuthoredNode Current { get; private set; }
        public bool Finished { get; private set; }
        public string Background { get; private set; }
        public string Comic { get; private set; }
        public string LeftPortrait { get; private set; }
        public string RightPortrait { get; private set; }
        public string LeftExpression { get; private set; }
        public string RightExpression { get; private set; }
        public UnityEngine.Sprite BackgroundSprite { get; private set; }
        public UnityEngine.Sprite LeftSprite { get; private set; }
        public UnityEngine.Sprite RightSprite { get; private set; }
        public UnityEngine.Sprite ComicSprite { get; private set; }

        public StoryRunner(ShopSession session,VisitSceneRequest request)
        {
            this.session=session;Request=request;nodes=request.Nodes.ToDictionary(n=>n.id);
            Enter(request.Scene.entryNodeId);
        }
        bool HasAction(string type,string target)=>actions.Any(a=>a.actionType==type && a.actionTarget==target);
        bool Condition(string type,string key,string value)
        {
            if(string.IsNullOrEmpty(type) || type=="None")return true;
            bool flag=session.HasProgressFlag(key)||HasAction("SetFlag",key);
            bool result;
            switch(type)
            {
                case "FlagExists":result=flag;break;
                case "FlagAbsent":result=!flag;break;
                case "LocationUnlocked":result=session.IsLocationUnlocked(key)||HasAction("UnlockLocation",key);break;
                case "ProfessionUnlocked":result=session.HasProgressFlag("profession:"+key)||HasAction("UnlockProfession",key);break;
                case "RecipeUnlocked":result=session.HasProgressFlag("recipe:"+key)||HasAction("UnlockRecipe",key);break;
                default:throw new InvalidOperationException("尚未支持的剧情条件："+type);
            }
            return value=="false"?!result:result;
        }
        public bool ChoiceAvailable(int index)
        {
            if(Current.type!="Choice" || index<0 || index>=Current.choices.Length)return false;
            if(Current.choiceConditionTypes.Length==0)return true;
            return Condition(Current.choiceConditionTypes[index],Current.choiceConditionKeys[index],Current.choiceConditionValues[index]);
        }
        void Visual(AuthoredNode node)
        {
            if(!string.IsNullOrEmpty(node.background))Background=node.background;
            if(node.backgroundSprite!=null)BackgroundSprite=node.backgroundSprite;
            if(node.comicSprite!=null)ComicSprite=node.comicSprite;
            if(node.comic=="Hide")ComicSprite=null;
            if(!string.IsNullOrEmpty(node.comic))Comic=node.comic=="Hide"?null:node.comic;
            if(!string.IsNullOrEmpty(node.portraitId) || node.portraitSprite!=null || node.portraitVisible=="false")
            {
                string portrait=node.portraitVisible=="false"?null:node.speaker;
                if(node.portraitSlot=="Right"){RightPortrait=portrait;RightExpression=node.expression;RightSprite=node.portraitVisible=="false"?null:node.portraitSprite;}
                else {LeftPortrait=portrait;LeftExpression=node.expression;LeftSprite=node.portraitVisible=="false"?null:node.portraitSprite;}
            }
        }
        void Enter(string id)
        {
            var automatic=new HashSet<string>();
            while(true)
            {
                if(!automatic.Add(id))throw new InvalidOperationException("剧情自动节点形成循环："+id);
                Current=nodes[id];
                bool allowed=Condition(Current.conditionType,Current.conditionKey,Current.conditionValue);
                switch(Current.type)
                {
                    case "Branch":id=Current.nextNodeIds[allowed?0:1];break;
                    case "Dialogue":
                    case "Choice":
                        if(allowed){Visual(Current);return;}
                        id=Current.nextNodeIds[Current.type=="Choice"?Current.nextNodeIds.Length-1:0];break;
                    case "Visual":if(allowed)Visual(Current);id=Current.nextNodeIds.Single();break;
                    case "Action":if(allowed)actions.Add(Current);id=Current.nextNodeIds.Single();break;
                    case "End":return;
                    default:throw new InvalidOperationException("未识别的剧情节点："+Current.type);
                }
            }
        }
        public bool Continue()
        {
            if(Finished || !ReferenceEquals(Request,session.PendingVisitScene))return false;
            if(Current.type=="End")
            {
                Finished=session.FinishVisitScene(Request,true,actions);return Finished;
            }
            if(Current.type!="Dialogue")return false;
            Enter(Current.nextNodeIds.Single());
            if(Current.type=="End")return Continue();
            return true;
        }
        public bool Choose(int index)
        {
            if(Finished || !ReferenceEquals(Request,session.PendingVisitScene) || !ChoiceAvailable(index))return false;
            Enter(Current.nextNodeIds[index]);
            if(Current.type=="End")return Continue();
            return true;
        }
        public bool Cancel()=>!Finished && session.FinishVisitScene(Request,false);
    }
}
