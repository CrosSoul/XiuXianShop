using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop.Editor
{
    // Each domain supplies only its columns, typed parser and references. CSV, state, merge and diff are shared.
    public abstract class ContentDomainAdapter
    {
        public abstract string FileName { get; }
        public abstract void Stage(ContentSyncPlan plan,string csv);
        public abstract void Validate(ContentSyncPlan plan);

        protected T[] Merge<T>(ContentSyncPlan plan,string csv,string idField,string columns,T[] previous,Func<ContentRow,T,T> parse) where T:AuthoredRecord,new()
        {
            var rows=ContentCsv.Read(csv,FileName,idField,columns.Split('|'),plan.issues);
            var old=previous.ToDictionary(r=>r.id);var result=new List<T>();var seen=new HashSet<string>();
            foreach(var row in rows)
            {
                string id=row.Required(idField);
                if(string.IsNullOrWhiteSpace(id))continue;
                if(!seen.Add(id)){row.Error(idField,"稳定 ID 重复。");continue;}
                plan.rows[FileName+"/"+id]=row;
                string state=row.OneOf("数据状态","草稿","待同步","已生效","停用");
                old.TryGetValue(id,out var existing);
                if(state=="草稿")
                {
                    plan.changes.Add($"跳过草稿 · {FileName} · {id}"+(existing!=null?"（保留已导入版本）":""));
                    if(existing!=null)result.Add(existing);
                    continue;
                }
                if(state!="待同步" && state!="已生效" && state!="停用")continue;
                var candidate=state=="停用"?(existing==null?new T():JsonUtility.FromJson<T>(JsonUtility.ToJson(existing))):parse(row,existing);
                candidate.id=id;
                if(candidate is AuthoredScene scene && existing is AuthoredScene previousScene)scene.entryNodeId=previousScene.entryNodeId;
                if(state=="停用")candidate.enabled=false;
                if(state=="已生效" && (existing==null || JsonUtility.ToJson(existing)!=JsonUtility.ToJson(candidate)))
                    row.Error("数据状态","已生效应与已导入内容一致；新增或修改请明确标记待同步。");
                result.Add(candidate);
                if(existing==null)plan.changes.Add($"{(candidate.enabled?"新增":"停用标记")} · {FileName} · {id}");
                else if(JsonUtility.ToJson(previous.Single(r=>r.id==id))!=JsonUtility.ToJson(candidate))
                    plan.changes.Add($"{(candidate.enabled?"修改":"停用")} · {FileName} · {id}");
            }
            foreach(var entry in previous.Where(r=>!seen.Contains(r.id)))
            {
                result.Add(entry);plan.changes.Add($"遗漏保留 · {FileName} · {entry.id}（不会推断删除）");
            }
            return result.ToArray();
        }

        protected void Reference(ContentSyncPlan plan,string id,string field,string target,IEnumerable<string> allowed,bool optional=false)
        {
            if(optional && string.IsNullOrEmpty(target))return;
            if(!allowed.Contains(target))plan.Error(FileName,id,field,$"未知、草稿、停用或尚未接入的引用：{target}");
        }
        public static ContentDomainAdapter[] CreateDefaults() => new ContentDomainAdapter[]{new Visits(),new VisitItems(),new Scenes(),new Nodes()};
        public static ContentDomainAdapter[] CreateFor(string[] domains,ContentSyncPlan plan)
        {
            if(domains==null || domains.Length==0)return CreateDefaults();
            var result=new List<ContentDomainAdapter>();
            foreach(string domain in domains.Distinct())
            {
                if(domain=="visits")result.AddRange(CreateDefaults());
                else if(domain=="commissions")result.AddRange(CommissionContentAdapter.Create());
                else if(domain=="market")result.Add(new MarketContentAdapter());
                else if(domain=="alchemy")result.AddRange(AlchemyContentAdapter.Create());
                else if(domain=="new-game")result.AddRange(StartProfileContentAdapter.Create());
                else if(domain=="knowledge")result.AddRange(KnowledgeContentAdapter.Create());
                else plan.Error("snapshot.json",domain,"domains","未知内容域。");
            }
            return result.ToArray();
        }

        sealed class Visits : ContentDomainAdapter
        {
            public override string FileName=>"visits.csv";
            public override void Stage(ContentSyncPlan p,string csv) => p.staged.visits=Merge(p,csv,"来访ID",
                "来访名称|来访ID|顾客ID|显示名|立绘ID|类型|固定回合|最早回合|最晚回合|必须Flag|禁止Flag|前置来访ID|队列阶段|同阶段顺序|求购类别|预算|到店SceneID|交易成功SceneID|跳过SceneID|完成条件|数据状态",p.staged.visits,(r,old)=>{
                    var v=new AuthoredVisit{title=r.Required("来访名称"),customerId=r.Required("顾客ID"),displayName=r.Required("显示名"),portraitId=r.Optional("立绘ID",old?.portraitId),type=r.OneOf("类型","Special","Story"),
                        fixedTurn=r.Number("固定回合",1,previous:old?.fixedTurn??default),earliestTurn=r.Number("最早回合",1,previous:old?.earliestTurn??default),latestTurn=r.Number("最晚回合",1,previous:old?.latestTurn??default),budget=r.Number("预算",0,previous:old?.budget??default),
                        requiredFlags=r.Optional("必须Flag",old?.requiredFlags),forbiddenFlags=r.Optional("禁止Flag",old?.forbiddenFlags),prerequisiteVisitId=r.Optional("前置来访ID",old?.prerequisiteVisitId),queuePhase=r.OneOf("队列阶段","BeforeOrdinary","AfterOrdinary"),
                        order=r.Number("同阶段顺序",0,true).value,buyingCategory=r.Optional("求购类别",old?.buyingCategory),arrivalSceneId=r.Optional("到店SceneID",old?.arrivalSceneId),tradeSceneId=r.Optional("交易成功SceneID",old?.tradeSceneId),skipSceneId=r.Optional("跳过SceneID",old?.skipSceneId),completion=r.OneOf("完成条件","SceneEnd","TradeSuccess")};
                    v.repeatPolicy=r.Optional("重复策略",old?.repeatPolicy??"UntilCompleted");
                    if(v.repeatPolicy!="Once" && v.repeatPolicy!="UntilCompleted")r.Error("重复策略","允许值：Once / UntilCompleted。");
                    if(v.earliestTurn.hasValue && v.latestTurn.hasValue && v.earliestTurn.value>v.latestTurn.value)r.Error("最晚回合","不能早于最早回合。");
                    if(v.fixedTurn.hasValue && ((v.earliestTurn.hasValue && v.fixedTurn.value<v.earliestTurn.value) || (v.latestTurn.hasValue && v.fixedTurn.value>v.latestTurn.value)))r.Error("固定回合","不在指定回合范围内。");
                    if(v.buyingCategory!="" && (!Enum.TryParse<ItemCategory>(v.buyingCategory,out var category) || !Enum.IsDefined(typeof(ItemCategory),category) || category==ItemCategory.Unclassified))r.Error("求购类别","须为现有玩家可见类别的枚举名称。");
                    if(v.buyingCategory!="" && !v.budget.hasValue)r.Error("预算","有求购类别时必须明确预算。");
                    return v;
                });
            public override void Validate(ContentSyncPlan p)
            {
                var sceneIds=p.staged.scenes.Where(s=>s.enabled).Select(s=>s.id);
                foreach(var v in p.staged.visits.Where(v=>v.enabled))
                {
                    Reference(p,v.id,"前置来访ID",v.prerequisiteVisitId,p.staged.visits.Where(x=>x.enabled && x.id!=v.id).Select(x=>x.id),true);
                    Reference(p,v.id,"到店SceneID",v.arrivalSceneId,sceneIds,true);Reference(p,v.id,"交易成功SceneID",v.tradeSceneId,sceneIds,true);Reference(p,v.id,"跳过SceneID",v.skipSceneId,sceneIds,true);
                }
                foreach(var group in p.staged.visits.Where(v=>v.enabled).GroupBy(v=>v.customerId))
                    if(group.Select(v=>(v.displayName,v.portraitId)).Distinct().Count()>1)
                        foreach(var v in group)p.Error(FileName,v.id,"顾客ID","同一顾客身份的显示名与立绘须一致；允许不同 visitId 多次来访。");
            }
        }
        sealed class VisitItems : ContentDomainAdapter
        {
            public override string FileName=>"visit-items.csv";
            public override void Stage(ContentSyncPlan p,string csv)=>p.staged.visitItems=Merge(p,csv,"条目ID",
                "条目名称|条目ID|来访ID|用途|物品ID|数量|实例预设ID|数据状态",p.staged.visitItems,(r,old)=>new AuthoredVisitItem{
                    title=r.Required("条目名称"),visitId=r.Required("来访ID"),purpose=r.OneOf("用途","Sell","Carry"),itemId=r.Required("物品ID"),quantity=r.Number("数量",1,true).value,instancePresetId=r.Optional("实例预设ID",old?.instancePresetId)});
            public override void Validate(ContentSyncPlan p)
            {
                foreach(var item in p.staged.visitItems.Where(i=>i.enabled))
                {
                    Reference(p,item.id,"来访ID",item.visitId,p.staged.visits.Where(v=>v.enabled).Select(v=>v.id));
                    Reference(p,item.id,"物品ID",item.itemId,p.catalog.items.Select(i=>i.id));
                    if(item.instancePresetId!="")p.Error(FileName,item.id,"实例预设ID","当前没有已接入的实例预设目录，不能猜测预设内容。");
                }
            }
        }
        sealed class Scenes : ContentDomainAdapter
        {
            public override string FileName=>"scenes.csv";
            public override void Stage(ContentSyncPlan p,string csv)=>p.staged.scenes=Merge(p,csv,"SceneID","场景名称|SceneID|说明|数据状态",p.staged.scenes,(r,old)=>new AuthoredScene{title=r.Required("场景名称"),description=r.Optional("说明",old?.description),locationId=r.Optional("触发地点ID",old?.locationId)});
            public override void Validate(ContentSyncPlan p)
            {
                foreach(var s in p.staged.scenes.Where(s=>s.enabled))
                {
                    Reference(p,s.id,"触发地点ID",s.locationId,p.catalog.travelLocations.Select(l=>l.id),true);
                    if(!string.IsNullOrEmpty(s.locationId) && p.staged.scenes.Count(x=>x.enabled && x.locationId==s.locationId)>1)
                        p.Error(FileName,s.id,"触发地点ID","一个地点只能配置一个进入剧情。");
                    var nodes=p.staged.nodes.Where(n=>n.enabled && n.sceneId==s.id).ToArray();
                    string entry=nodes.FirstOrDefault()?.id;
                    if(s.entryNodeId!=entry && !p.changes.Any(c=>c.StartsWith("新增") && c.Contains(s.id)))
                        p.changes.Add($"修改 · {FileName} · {s.id} · 入口节点：{s.entryNodeId} → {entry}");
                    s.entryNodeId=entry;
                    if(nodes.Length==0){p.Error(FileName,s.id,"SceneID","场景缺少已批准节点。");continue;}
                    if(!nodes.Any(n=>n.type=="End"))p.Error(FileName,s.id,"SceneID","场景缺少 End 节点。");
                    var reached=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(s.entryNodeId);
                    while(queue.Count>0)
                    {
                        var id=queue.Dequeue();if(!reached.Add(id))continue;
                        var node=nodes.FirstOrDefault(n=>n.id==id);if(node==null)continue;
                        foreach(var next in node.nextNodeIds)queue.Enqueue(next);
                    }
                    foreach(var n in nodes.Where(n=>!reached.Contains(n.id)))p.Error("nodes.csv",n.id,"NextNodeID","从本场景首行入口无法到达此节点。");
                    var canEnd=new HashSet<string>(nodes.Where(n=>n.type=="End").Select(n=>n.id));
                    bool changed;
                    do {changed=false;foreach(var n in nodes)if(n.nextNodeIds.Any(canEnd.Contains) && canEnd.Add(n.id))changed=true;}while(changed);
                    foreach(var n in nodes.Where(n=>!canEnd.Contains(n.id)))p.Error("nodes.csv",n.id,"NextNodeID","此节点没有可达的 End。");
                    var visiting=new HashSet<string>();var checkedNodes=new HashSet<string>();
                    bool HasAutomaticCycle(string id)
                    {
                        var node=nodes.FirstOrDefault(n=>n.id==id);
                        if(node==null || node.type=="Dialogue" || node.type=="Choice" || node.type=="End")return false;
                        if(visiting.Contains(id))return true;
                        if(!checkedNodes.Add(id))return false;
                        visiting.Add(id);bool cycle=node.nextNodeIds.Any(HasAutomaticCycle);visiting.Remove(id);return cycle;
                    }
                    foreach(var n in nodes)if(HasAutomaticCycle(n.id)){p.Error("nodes.csv",n.id,"NextNodeID","自动节点形成循环；循环须经过可交互节点。");break;}
                }
            }
        }
        sealed class Nodes : ContentDomainAdapter
        {
            public override string FileName=>"nodes.csv";
            static string[] Lines(string value)=>value.Replace("\r","").Split('\n').Where(v=>!string.IsNullOrWhiteSpace(v)).Select(v=>v.Trim()).ToArray();
            static Sprite ReadSprite(ContentRow row,string field,Sprite previous)
            {
                string path=row.Get(field);if(path=="")return previous;
                var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(sprite==null)row.Error(field,"找不到已导入 Sprite 资源："+path);
                return sprite;
            }
            public override void Stage(ContentSyncPlan p,string csv)=>p.staged.nodes=Merge(p,csv,"NodeID",
                "节点名称|NodeID|SceneID|节点类型|说话者|文本|立绘ID|表情状态|选项文本|条件类型|条件键|条件值|Action类型|Action目标|Action值|NextNodeID|数据状态",p.staged.nodes,(r,old)=>{
                    var n=new AuthoredNode{title=r.Required("节点名称"),sceneId=r.Required("SceneID"),type=r.OneOf("节点类型","Dialogue","Visual","Choice","Branch","Action","End"),
                        speaker=r.Optional("说话者",old?.speaker),text=r.Optional("文本",old?.text),portraitId=r.Optional("立绘ID",old?.portraitId),expression=r.Optional("表情状态",old?.expression),choices=Lines(r.Get("选项文本")),nextNodeIds=Lines(r.Get("NextNodeID")),
                        conditionType=r.Optional("条件类型",old?.conditionType),conditionKey=r.Optional("条件键",old?.conditionKey),conditionValue=r.Optional("条件值",old?.conditionValue),actionType=r.Get("Action类型"),actionTarget=r.Get("Action目标"),actionValue=r.Get("Action值")};
                    n.background=r.Optional("背景",old?.background);n.comic=r.Optional("分镜",old?.comic);
                    n.backgroundSprite=ReadSprite(r,"背景图片",old?.backgroundSprite);n.portraitSprite=ReadSprite(r,"立绘图片",old?.portraitSprite);n.comicSprite=ReadSprite(r,"分镜图片",old?.comicSprite);
                    n.portraitSlot=r.Optional("立绘槽",old?.portraitSlot);n.portraitVisible=r.Optional("立绘显示",old?.portraitVisible);
                    n.choiceConditionTypes=Lines(r.Optional("选项条件类型",old==null?"":string.Join("\n",old.choiceConditionTypes)));
                    n.choiceConditionKeys=Lines(r.Optional("选项条件键",old==null?"":string.Join("\n",old.choiceConditionKeys)));
                    n.choiceConditionValues=Lines(r.Optional("选项条件值",old==null?"":string.Join("\n",old.choiceConditionValues)));
                    if(n.portraitSlot!="" && n.portraitSlot!="Left" && n.portraitSlot!="Right")r.Error("立绘槽","允许 Left / Right。");
                    if(n.portraitVisible!="" && n.portraitVisible!="true" && n.portraitVisible!="false")r.Error("立绘显示","允许 true / false。");
                    if(n.type=="Choice" && n.conditionType!="")r.Error("条件类型","Choice 请逐项填写选项条件，不使用整节点条件。");
                    if(n.choiceConditionTypes.Length>0)
                    {
                        if(n.type!="Choice" || n.choiceConditionTypes.Length!=n.choices.Length || n.choiceConditionKeys.Length!=n.choices.Length || n.choiceConditionValues.Length!=n.choices.Length)
                            r.Error("选项条件类型","三列须与选项逐行对应，无条件项填写 None / - / true。");
                        foreach(string type in n.choiceConditionTypes)
                            if(!new[]{"None","FlagExists","FlagAbsent","LocationUnlocked","ProfessionUnlocked","RecipeUnlocked"}.Contains(type))r.Error("选项条件类型","未知条件："+type);
                        if(n.choiceConditionValues.Any(v=>v!="true" && v!="false"))r.Error("选项条件值","允许 true / false。");
                    }
                    else if(n.choiceConditionKeys.Length>0 || n.choiceConditionValues.Length>0)r.Error("选项条件类型","缺少选项条件类型。");
                    if(n.conditionValue!="" && n.conditionValue!="true" && n.conditionValue!="false")r.Error("条件值","允许 true / false。");
                    if(n.type=="Dialogue")r.Required("文本");
                    int edges=n.type=="End"?0:n.type=="Branch"?2:n.type=="Choice"?n.choices.Length:1;
                    if(n.type=="Choice" && n.choices.Length<2)r.Error("选项文本","Choice 至少两个选项，每行一个。");
                    if(n.nextNodeIds.Length!=edges)r.Error("NextNodeID",$"此节点须有 {edges} 个后继，每行一个；Branch 顺序为真 / 假。");
                    if(n.type=="Branch" && n.conditionType=="")r.Error("条件类型","Branch 必须提供条件。");
                    if(n.conditionType!="")
                    {
                        if(!new[]{"FlagExists","FlagAbsent","LocationUnlocked","ProfessionUnlocked","KnowledgeUnlocked","RecipeUnlocked"}.Contains(n.conditionType))
                            r.Error("条件类型","不支持的条件类型："+n.conditionType);
                        if(string.IsNullOrWhiteSpace(n.conditionKey))r.Error("条件键","必填字段为空。");
                    }
                    else if(n.conditionKey!="" || n.conditionValue!="")r.Error("条件类型","有条件键 / 值但缺少条件类型。");
                    if(n.type=="Action")
                    {
                        r.OneOf("Action类型","SetFlag","UnlockLocation","UnlockProfession","UnlockKnowledge","UnlockRecipe");r.Required("Action目标");
                        if(n.actionValue!="true")r.Error("Action值","当前幂等写入 / 解锁动作只支持 true。");
                    }
                    else if(n.actionType!="" || n.actionTarget!="" || n.actionValue!="")r.Error("Action类型","只有 Action 节点可配置动作。");
                    return n;
                });
            public override void Validate(ContentSyncPlan p)
            {
                foreach(var n in p.staged.nodes.Where(n=>n.enabled))
                {
                    Reference(p,n.id,"SceneID",n.sceneId,p.staged.scenes.Where(s=>s.enabled).Select(s=>s.id));
                    foreach(var next in n.nextNodeIds)Reference(p,n.id,"NextNodeID",next,p.staged.nodes.Where(x=>x.enabled && x.sceneId==n.sceneId).Select(x=>x.id));
                    CheckTarget(p,n.id,"Action目标",n.actionType,n.actionTarget);
                    CheckTarget(p,n.id,"条件键",n.conditionType,n.conditionKey);
                    if(n.choiceConditionTypes.Length==n.choiceConditionKeys.Length)
                        for(int i=0;i<n.choiceConditionTypes.Length;i++)CheckTarget(p,n.id,"选项条件键",n.choiceConditionTypes[i],n.choiceConditionKeys[i]);
                }
            }
            void CheckTarget(ContentSyncPlan p,string id,string field,string type,string target)
            {
                if(type=="UnlockLocation" || type=="LocationUnlocked")Reference(p,id,field,target,p.catalog.travelLocations.Select(l=>l.id));
                if(type=="UnlockRecipe" || type=="RecipeUnlocked")Reference(p,id,field,target,p.catalog.alchemy.recipes.Select(r=>r.id));
                if(type=="UnlockProfession" || type=="ProfessionUnlocked")Reference(p,id,field,target,new[]{"alchemy"});
                if(type=="UnlockKnowledge" || type=="KnowledgeUnlocked")
                    p.Error(FileName,id,field,"知识目录尚未实现，不能把未声明 ID 当作合法内容。");
            }
        }
    }
}
