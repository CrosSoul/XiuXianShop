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
    [Category("DP65")]
    public sealed class CommissionContentTests
    {
        string directory,assetDirectory;
        ShopCatalog catalog;
        AuthoredContent content;
        [SetUp] public void Setup()
        {
            string id=Guid.NewGuid().ToString("N");directory=Path.GetFullPath("Temp/DP65-"+id);Directory.CreateDirectory(directory);
            foreach(string file in Directory.GetFiles("ContentSources/DP65-Migration"))File.Copy(file,Path.Combine(directory,Path.GetFileName(file)));
            assetDirectory="Assets/Data/DP65-"+id;AssetDatabase.CreateFolder("Assets/Data","DP65-"+id);
            catalog=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath));
            content=UnityEngine.Object.Instantiate(catalog.authoredContent);catalog.authoredContent=content;
            AssetDatabase.CreateAsset(content,assetDirectory+"/Content.asset");AssetDatabase.CreateAsset(catalog,assetDirectory+"/Catalog.asset");
        }
        [TearDown] public void Cleanup()
        {
            AssetDatabase.DeleteAsset(assetDirectory);
            foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);
        }
        void Edit(string table,Action<List<ContentRow>> edit)
        {
            string id=table=="templates"?"稳定ID":table=="pools"?"奖励池ID":table=="global"?"配置键":"条目ID";
            string path=Path.Combine(directory,"commission-"+table+".csv");var issues=new List<ContentIssue>();
            var rows=ContentCsv.Read(File.ReadAllText(path),table,id,Array.Empty<string>(),issues);Assert.That(issues,Is.Empty);
            edit(rows);string[] columns=rows[0].values.Keys.ToArray();
            string Quote(string v)=>"\""+v.Replace("\"","\"\"")+"\"";
            File.WriteAllText(path,string.Join(",",columns)+"\n"+string.Join("\n",rows.Select(r=>string.Join(",",columns.Select(c=>Quote(r.values[c]))))));
        }
        void Import()
        {
            using(var plan=ContentSync.Validate(directory,catalog,content))
            {Assert.That(plan.Valid,Is.True,string.Join("\n",plan.issues));ContentSync.Import(plan);}
        }
        ShopSession Visit()
        {
            var session=new ShopSession(catalog,false,65);Assert.That(session.BeginBusiness());
            if(session.PendingVisitScene!=null)
            {
                var runner=new StoryRunner(session,session.PendingVisitScene);
                while(session.PendingVisitScene!=null)Assert.That(runner.Continue());
            }
            Assert.That(session.EndBusiness());
            Assert.That(session.BeginCarrying(0));Assert.That(session.BeginTravel());Assert.That(session.EnterLocation("baishitang"));return session;
        }
        [Test] public void ChangesTextWeightNewTemplatePoolAndGiftThroughImportWithoutCode()
        {
            Edit("templates",rows=>{
                foreach(var r in rows)r.values["候选权重"]="0";
                var first=rows.Single(r=>r.id=="BSH-001");first.values["委托名"]="同步新标题";first.values["场景文案"]="同步新文案";first.values["候选权重"]="12";first.values["奖励池ID"]="BSH-R04";
                var added=new ContentRow{values=new Dictionary<string,string>(first.values)};added.values["稳定ID"]="BSH-NEW";added.values["候选权重"]="3";added.values["奖励池ID"]="BSH-R01";rows.Add(added);
            });
            Edit("global",rows=>{rows.Single(r=>r.id=="candidateCount").values["当前值"]="2";rows.Single(r=>r.id=="extraGiftChance").values["当前值"]="100%";});
            Import();Assert.That(catalog.commissions.templates.Single(t=>t.id=="BSH-001").weight,Is.EqualTo(12));
            var s=Visit();Assert.That(s.CommissionCandidates.Select(t=>t.id),Is.EquivalentTo(new[]{"BSH-001","BSH-NEW"}));
            Assert.That(s.CommissionCandidates.Single(t=>t.id=="BSH-001").description,Is.EqualTo("同步新文案"));
            int money=s.Money;Assert.That(s.CompleteCommission("BSH-001"));Assert.That(s.Money-money,Is.InRange(6,10));
            Assert.That(s.In(ContainerId.Location).Single().Definition.id,Is.EqualTo("pill"));
        }
        [TestCase("templates","奖励池ID","unknown")]
        [TestCase("members","物品ID","unknown")]
        [TestCase("members","权重","NaN")]
        [TestCase("pools","实物抽取组","bad:4:2")]
        public void InvalidReferencesAndRangesBlockImport(string table,string field,string value)
        {
            string before=EditorJsonUtility.ToJson(catalog);Edit(table,rows=>rows[0].values[field]=value);
            using(var plan=ContentSync.Validate(directory,catalog,content))
            {Assert.That(plan.Valid,Is.False);Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(plan));}
            Assert.That(EditorJsonUtility.ToJson(catalog),Is.EqualTo(before));
        }
        [TestCase("templates")][TestCase("pools")][TestCase("members")]
        public void DuplicateIdsBlockImport(string table)
        {
            Edit(table,rows=>rows.Add(rows[0]));
            using(var plan=ContentSync.Validate(directory,catalog,content))Assert.That(plan.Valid,Is.False);
        }
        [Test] public void PreviewIsReadOnlyAndImportIdempotentWithFixedSavedCandidates()
        {
            string before=File.ReadAllText(assetDirectory+"/Catalog.asset");
            using(var plan=ContentSync.Validate(directory,catalog,content))
            {Assert.That(plan.Valid,Is.True,string.Join("\n",plan.issues));Assert.That(File.ReadAllText(assetDirectory+"/Catalog.asset"),Is.EqualTo(before));ContentSync.Import(plan);Assert.That(ContentSync.Import(plan),Is.False);}
            var s=Visit();var ids=s.CommissionCandidates.Select(t=>t.id).ToArray();
            Assert.That(s.LeaveLocation());Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            var loaded=ShopSession.RestoreSave(catalog,s.CaptureSave());Assert.That(loaded.CommissionCandidates.Select(t=>t.id),Is.EqualTo(ids));
        }
    }
}
