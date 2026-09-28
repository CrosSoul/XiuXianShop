using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    // Temporary linear preview adapter for DP-62. DP-52 owns Choice/Branch and presentation.
    public sealed class VisitSceneGraybox
    {
        readonly ShopSession session;
        readonly List<AuthoredNode> actions=new List<AuthoredNode>();
        public VisitSceneRequest Request { get; }
        public IReadOnlyList<string> Lines { get; }
        public string UnsupportedReason { get; }

        public VisitSceneGraybox(ShopSession session,VisitSceneRequest request)
        {
            this.session=session;Request=request;
            var lines=new List<string>();Lines=lines;
            var seen=new HashSet<string>();string id=request.Scene.entryNodeId;
            while(true)
            {
                var node=request.Nodes.Single(n=>n.id==id);
                if(!seen.Add(id)){UnsupportedReason="此剧情包含循环，需要 DP-52 Runner。";return;}
                if(!string.IsNullOrEmpty(node.conditionType) || node.type=="Choice" || node.type=="Branch" || node.type=="Visual")
                {UnsupportedReason="此剧情需要 DP-52 Runner；当前仅提供线性对白灰盒。";return;}
                if(node.type=="Dialogue")lines.Add((string.IsNullOrEmpty(node.speaker)?"":node.speaker+"\n")+node.text);
                else if(node.type=="Action")actions.Add(node);
                else if(node.type=="End")return;
                else throw new InvalidOperationException("未识别的剧情节点类型："+node.type);
                id=node.nextNodeIds.Single();
            }
        }
        public bool Finish() => UnsupportedReason==null && session.FinishVisitScene(Request,true,actions);
        public bool Cancel() => session.FinishVisitScene(Request,false);
    }
}
