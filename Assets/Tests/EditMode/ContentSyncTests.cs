using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XiuXianShop.Editor;

namespace XiuXianShop.Tests
{
    [Category("DP64")]
    public sealed class ContentSyncTests
    {
        string directory,assetPath;
        ShopCatalog catalog;
        AuthoredContent content;
        readonly Dictionary<string,string[]> columns=new Dictionary<string,string[]>{
            {"visits.csv","来访名称|来访ID|顾客ID|显示名|立绘ID|类型|固定回合|最早回合|最晚回合|必须Flag|禁止Flag|前置来访ID|队列阶段|同阶段顺序|求购类别|预算|到店SceneID|交易成功SceneID|跳过SceneID|完成条件|数据状态".Split('|')},
            {"visit-items.csv","条目名称|条目ID|来访ID|用途|物品ID|数量|实例预设ID|数据状态".Split('|')},
            {"scenes.csv","场景名称|SceneID|说明|数据状态".Split('|')},
            {"nodes.csv","节点名称|NodeID|SceneID|节点类型|说话者|文本|立绘ID|表情状态|选项文本|条件类型|条件键|条件值|Action类型|Action目标|Action值|NextNodeID|数据状态".Split('|')}
        };
        Dictionary<string,string> visit,item,scene,dialogue,end;
        [SetUp] public void Setup()
        {
            string id=Guid.NewGuid().ToString("N");directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/DP64-"+id));Directory.CreateDirectory(directory);
            assetPath="Assets/Data/DP64-test-"+id+".asset";
            content=ScriptableObject.CreateInstance<AuthoredContent>();content.name="DP64Test";AssetDatabase.CreateAsset(content,assetPath);
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();catalog.authoredContent=content;
            File.WriteAllText(Path.Combine(directory,"snapshot.json"),"{\"formatVersion\":1,\"snapshotId\":\"test-v1\"}");
            visit=Row("来访名称","初访","来访ID","visit_a","顾客ID","customer_a","显示名","消息客","类型","Story","固定回合","3","队列阶段","BeforeOrdinary","同阶段顺序","10","求购类别","Medicine","预算","30","到店SceneID","scene_a","完成条件","SceneEnd");
            item=Row("条目名称","携药","条目ID","goods_a","来访ID","visit_a","用途","Sell","物品ID","pill","数量","2");
            scene=Row("场景名称","场景甲","SceneID","scene_a","说明","隔离校验");
            dialogue=Row("节点名称","开场","NodeID","node_a","SceneID","scene_a","节点类型","Dialogue","说话者","消息客","文本","你好，\"修仙者\"。\n第二行。","NextNodeID","node_end");
            end=Row("节点名称","结束","NodeID","node_end","SceneID","scene_a","节点类型","End");
            WriteAll();
        }
        [TearDown] public void Cleanup()
        {
            AssetDatabase.DeleteAsset(assetPath);UnityEngine.Object.DestroyImmediate(catalog);
            foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);
        }
        static Dictionary<string,string> Row(params string[] pairs)
        {
            var r=new Dictionary<string,string>{{"数据状态","待同步"}};
            for(int i=0;i<pairs.Length;i+=2)r[pairs[i]]=pairs[i+1];return r;
        }
        void Write(string file,params Dictionary<string,string>[] rows)
        {
            string Quote(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
            File.WriteAllText(Path.Combine(directory,file),string.Join(",",columns[file])+"\n"+string.Join("\n",rows.Select(row=>string.Join(",",columns[file].Select(c=>Quote(row.TryGetValue(c,out var v)?v:""))))));
        }
        void WriteAll(){Write("visits.csv",visit);Write("visit-items.csv",item);Write("scenes.csv",scene);Write("nodes.csv",dialogue,end);}
        ContentSyncPlan Review()
        {
            var p=ContentSync.Validate(directory,catalog,content);
            Assert.That(p.Valid,Is.True,string.Join("\n",p.issues));return p;
        }
        [Test] public void PreviewDoesNotWriteAndImportUpdatesStableIdentityAndIsIdempotent()
        {
            string original=File.ReadAllText(assetPath);
            using(var p=Review())
            {
                Assert.That(File.ReadAllText(assetPath),Is.EqualTo(original));Assert.That(content.visits,Is.Empty);
                Assert.That(p.changes.Any(c=>c.Contains("新增") && c.Contains("visit_a")));Assert.That(ContentSync.Import(p));
                string once=File.ReadAllText(assetPath);Assert.That(ContentSync.Import(p),Is.False);Assert.That(File.ReadAllText(assetPath),Is.EqualTo(once));
            }
            Assert.That(content.nodes[0].text,Is.EqualTo(dialogue["文本"]));Assert.That(content.scenes[0].entryNodeId,Is.EqualTo("node_a"));
            visit["显示名"]="消息客改名";visit["固定回合"]="7";Write("visits.csv",visit);
            using(var p=Review()){Assert.That(p.changes.Any(c=>c.StartsWith("修改") && c.Contains("visit_a")));ContentSync.Import(p);}
            Assert.That(content.visits.Length,Is.EqualTo(1));Assert.That(content.visits[0].id,Is.EqualTo("visit_a"));Assert.That(content.visits[0].fixedTurn.value,Is.EqualTo(7));
            var second=new Dictionary<string,string>(visit);second["来访ID"]="visit_b";second["固定回合"]="12";
            Write("visits.csv",visit,second);
            using(var p=Review())ContentSync.Import(p);
            Assert.That(content.visits.Length,Is.EqualTo(2));Assert.That(content.visits.Select(v=>v.customerId).Distinct().Count(),Is.EqualTo(1));
        }
        [Test] public void EmptyOptionalCellsPreservePreviouslyImportedValues()
        {
            visit["立绘ID"]="portrait_a";
            dialogue["条件类型"]="FlagExists";dialogue["条件键"]="story.hint";
            WriteAll();using(var p=Review())ContentSync.Import(p);
            visit["固定回合"]="";visit["立绘ID"]="";visit["到店SceneID"]="";scene["说明"]="";
            dialogue["条件类型"]="";dialogue["条件键"]="";
            WriteAll();using(var p=Review())Assert.That(ContentSync.Import(p),Is.False);
            Assert.That(content.visits[0].fixedTurn.value,Is.EqualTo(3));
            Assert.That(content.visits[0].portraitId,Is.EqualTo("portrait_a"));
            Assert.That(content.visits[0].arrivalSceneId,Is.EqualTo("scene_a"));
            Assert.That(content.scenes[0].description,Is.EqualTo("隔离校验"));
            Assert.That(content.nodes[0].conditionKey,Is.EqualTo("story.hint"));
        }
        [Test] public void MissingRowsAreRetainedAndExplicitDisableKeepsIdentity()
        {
            using(var p=Review())ContentSync.Import(p);
            Write("visits.csv");
            using(var p=Review()){Assert.That(p.changes.Any(c=>c.Contains("遗漏保留") && c.Contains("visit_a")));ContentSync.Import(p);}
            Assert.That(content.visits.Single().enabled);
            visit["数据状态"]="停用";Write("visits.csv",visit);
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.That(p.issues.Any(i=>i.field=="来访ID"));}
            item["数据状态"]="停用";Write("visit-items.csv",item);
            using(var p=Review()){Assert.That(p.changes.Count(c=>c.StartsWith("停用")),Is.EqualTo(2));ContentSync.Import(p);}
            Assert.That(content.visits.Single().enabled,Is.False);Assert.That(content.visits.Single().id,Is.EqualTo("visit_a"));
        }
        [TestCase("duplicate","来访ID")]
        [TestCase("item","物品ID")]
        [TestCase("scene","到店SceneID")]
        [TestCase("next","NextNodeID")]
        [TestCase("action","Action类型")]
        [TestCase("target","Action目标")]
        [TestCase("number","数量")]
        [TestCase("state","数据状态")]
        public void InvalidImportReportsRowAndFieldWithoutPartialAssetWrite(string damage,string field)
        {
            using(var p=Review())ContentSync.Import(p);
            string before=File.ReadAllText(assetPath);visit["显示名"]="不应写入";
            if(damage=="item")item["物品ID"]="unknown";
            if(damage=="scene")visit["到店SceneID"]="unknown";
            if(damage=="next")dialogue["NextNodeID"]="unknown";
            if(damage=="number")item["数量"]="0";
            if(damage=="state")visit["数据状态"]="approved-ish";
            if(damage=="action" || damage=="target")
            {
                dialogue["节点类型"]="Action";dialogue["Action类型"]=damage=="action"?"RunCSharp":"UnlockLocation";
                dialogue["Action目标"]="unknown";dialogue["Action值"]="true";
            }
            WriteAll();if(damage=="duplicate")Write("visits.csv",visit,visit);
            using(var p=ContentSync.Validate(directory,catalog,content))
            {
                Assert.That(p.Valid,Is.False);Assert.That(p.issues.Any(i=>i.error && i.field==field && i.line>=2 && !string.IsNullOrEmpty(i.id)),string.Join("\n",p.issues));
                Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));
            }
            Assert.That(File.ReadAllText(assetPath),Is.EqualTo(before));Assert.That(content.visits.Single().displayName,Is.EqualTo("消息客"));
        }
        [Test] public void StaleSourceOrChangedCatalogRequiresNewPreview()
        {
            using(var p=Review())
            {
                visit["显示名"]="新文件";Write("visits.csv",visit);Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));Assert.That(content.visits,Is.Empty);
            }
            using(var p=Review())
            {
                catalog.items=catalog.items.Where(i=>i.id!="pill").ToArray();Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));Assert.That(content.visits,Is.Empty);
            }
        }
        [Test] public void DraftsCannotBeReferencedAndEffectiveChangesMustBeMarkedPending()
        {
            scene["数据状态"]="草稿";WriteAll();
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.That(p.issues.Any(i=>i.field=="到店SceneID"));}
            scene["数据状态"]="待同步";WriteAll();using(var p=Review())ContentSync.Import(p);
            visit["数据状态"]="已生效";WriteAll();using(var p=Review())Assert.That(ContentSync.Import(p),Is.False);
            visit["显示名"]="未标待同步的修改";WriteAll();using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.Valid,Is.False);
        }
        [Test] public void RealConfluenceExportReportsMisalignedStatusWithoutWriting()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../ContentSources/Confluence-2026-09-28"));
            using(var p=ContentSync.Validate(root,catalog,content))
            {
                Assert.That(p.Valid,Is.False);
                Assert.That(p.issues.Any(i=>i.table=="nodes.csv" && i.id=="alchemy_hint_001" && i.field=="数据状态"));
                Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));
                Assert.That(content.visits,Is.Empty);Assert.That(content.nodes,Is.Empty);Assert.That(content.scenes,Is.Empty);
            }
        }
        [Test] public void ValidAllDraftSnapshotImportsNoRuntimeDefinitions()
        {
            foreach(var row in new[]{visit,item,scene,dialogue,end})row["数据状态"]="草稿";
            WriteAll();using(var p=Review())
            {
                Assert.That(p.changes.Count(c=>c.StartsWith("跳过草稿")),Is.EqualTo(5));ContentSync.Import(p);
                Assert.That(content.visits,Is.Empty);Assert.That(content.visitItems,Is.Empty);Assert.That(content.scenes,Is.Empty);Assert.That(content.nodes,Is.Empty);
            }
        }
        [Test] public void ChoiceBranchAndActionAreTypedAndCrossSceneEdgesAreRejected()
        {
            dialogue["节点类型"]="Choice";dialogue["选项文本"]="学习\n离开";dialogue["NextNodeID"]="branch\nnode_end";
            var branch=Row("节点名称","条件","NodeID","branch","SceneID","scene_a","节点类型","Branch","条件类型","FlagExists","条件键","story.hint","NextNodeID","action\nnode_end");
            var action=Row("节点名称","解锁","NodeID","action","SceneID","scene_a","节点类型","Action","Action类型","UnlockLocation","Action目标","baishitang","Action值","true","NextNodeID","node_end");
            Write("nodes.csv",dialogue,branch,action,end);using(var p=Review())ContentSync.Import(p);
            Assert.That(content.nodes.First().choices.Length,Is.EqualTo(2));
            end["SceneID"]="other_scene";Write("nodes.csv",dialogue,branch,action,end);
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.That(p.issues.Any(i=>i.field=="NextNodeID"));}
        }
        [Test] public void MalformedCsvOrMissingTableBlocksImport()
        {
            File.WriteAllText(Path.Combine(directory,"nodes.csv"),"NodeID,SceneID\n\"unterminated");
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.That(p.issues.Any(i=>i.field=="CSV"));}
            File.Delete(Path.Combine(directory,"visit-items.csv"));using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.issues.Any(i=>i.field=="文件"));
            Assert.That(content.visits,Is.Empty);
        }
    }
}
