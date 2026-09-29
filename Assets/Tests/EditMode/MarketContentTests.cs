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
    [Category("DP66")]
    public sealed class MarketContentTests
    {
        string directory,assetDirectory;
        ShopCatalog catalog;AuthoredContent content;
        [SetUp] public void Setup()
        {
            string id=Guid.NewGuid().ToString("N");directory=Path.GetFullPath("Temp/DP66-"+id);Directory.CreateDirectory(directory);
            foreach(string file in Directory.GetFiles("ContentSources/DP66-Prototype"))File.Copy(file,Path.Combine(directory,Path.GetFileName(file)));
            assetDirectory="Assets/Data/DP66-"+id;AssetDatabase.CreateFolder("Assets/Data","DP66-"+id);
            catalog=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath));
            content=UnityEngine.Object.Instantiate(catalog.authoredContent);catalog.authoredContent=content;
            AssetDatabase.CreateAsset(content,assetDirectory+"/Content.asset");AssetDatabase.CreateAsset(catalog,assetDirectory+"/Catalog.asset");
        }
        [TearDown] public void Cleanup(){AssetDatabase.DeleteAsset(assetDirectory);foreach(string f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
        void Edit(Action<List<ContentRow>> edit)
        {
            string path=Path.Combine(directory,"market-events.csv");var issues=new List<ContentIssue>();var rows=ContentCsv.Read(File.ReadAllText(path),"market-events.csv","行情ID",Array.Empty<string>(),issues);
            Assert.That(issues,Is.Empty);edit(rows);var columns=rows[0].values.Keys.ToArray();string Quote(string v)=>"\""+v.Replace("\"","\"\"")+"\"";
            File.WriteAllText(path,string.Join(",",columns)+"\n"+string.Join("\n",rows.Select(r=>string.Join(",",columns.Select(c=>Quote(r.values[c]))))));
        }
        void Import(){using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.True,string.Join("\n",p.issues));ContentSync.Import(p);}}
        [Test] public void NewTypeControlsQuotesDurationsCooldownWeightAndSecretFromOnePool()
        {
            Edit(rows=>{
                foreach(var r in rows)r.values["生成权重"]="0";
                var n=new ContentRow{values=new Dictionary<string,string>(rows[0].values)};rows.Add(n);
                n.values["行情ID"]="new-market";n.values["行情名称"]="新材料行情";n.values["说明"]="来自同步的说明";n.values["目标类别"]="Material";n.values["交易方向"]="PlayerBuys";
                n.values["价格修正百分比"]="50%";n.values["生成权重"]="9";n.values["最短持续回合"]="4";n.values["最长持续回合"]="4";n.values["冷却回合"]="5";
            });Import();
            var calendar=new MarketCalendar(catalog.marketEvents,66);var events=calendar.Between(1,120);
            Assert.That(events.Length,Is.GreaterThan(1));Assert.That(events.All(e=>e.effect.id=="new-market" && e.Duration==4 && e.title=="新材料行情" && e.description=="来自同步的说明"));
            var ordered=events.OrderBy(e=>e.startTurn).ToArray();for(int i=1;i<ordered.Length;i++)Assert.That(ordered[i].startTurn,Is.GreaterThan(ordered[i-1].endTurn+5));
            var session=new ShopSession(catalog,false,66,calendar:calendar);var first=events.First();
            var tag=first.effect;Assert.That(tag.playerBuys);Assert.That(tag.playerSells,Is.False);Assert.That(tag.category,Is.EqualTo(ItemCategory.Material));
            Assert.That(new PriceQuote(catalog.Find("herb"),new[]{tag}).RawValue,Is.EqualTo(catalog.Find("herb").FullBaseValue*1.5m));
            var secretCalendar=new MarketCalendar(new MarketCalendarState{seed=66,fixedSchedule=true,definitions=catalog.marketEvents.Where(e=>e.weight>0).ToArray(),generatedYears=Array.Empty<int>(),events=Array.Empty<MarketEvent>()});
            var secret=secretCalendar.RevealSecret(1,2,12,new System.Random(66));Assert.That(secret.effect.id,Is.EqualTo("new-market"));Assert.That(secret.Duration,Is.EqualTo(4));
            Assert.That(calendar.ActiveTags(first.startTurn).Any(t=>t.title=="新材料行情"));
            string saved=JsonUtility.ToJson(calendar.Capture());Assert.That(JsonUtility.ToJson(new MarketCalendar(JsonUtility.FromJson<MarketCalendarState>(saved)).Capture()),Is.EqualTo(saved));
        }
        [TestCase("目标类别","Unknown")][TestCase("最长持续回合","0")][TestCase("生成权重","NaN")][TestCase("交易方向","Script")]
        public void InvalidDataBlocksImportWithoutAssetChanges(string field,string value)
        {
            string original=EditorJsonUtility.ToJson(catalog);Edit(rows=>rows[0].values[field]=value);
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));}
            Assert.That(EditorJsonUtility.ToJson(catalog),Is.EqualTo(original));
        }
        [Test] public void DuplicateIdAndReversedDurationAreRejected()
        {
            Edit(rows=>rows.Add(rows[0]));using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.Valid,Is.False);
            File.Copy("ContentSources/DP66-Prototype/market-events.csv",Path.Combine(directory,"market-events.csv"),true);
            Edit(rows=>rows[0].values["最短持续回合"]="3");using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.Valid,Is.False);
        }
        [Test] public void DraftsDoNotGenerateAndImportCannotRerollExistingSession()
        {
            var calendar=new MarketCalendar(catalog.marketEvents,66);calendar.Between(1,36);string saved=JsonUtility.ToJson(calendar.Capture());
            Edit(rows=>{
                rows[0].values["行情名称"]="已导入的新名称";
                var draft=new ContentRow{values=new Dictionary<string,string>(rows[0].values)};draft.values["行情ID"]="draft-new";draft.values["数据状态"]="草稿";rows.Add(draft);
            });Import();
            Assert.That(catalog.marketEvents.Any(e=>e.id=="draft-new"),Is.False);Assert.That(catalog.marketEvents[0].title,Is.EqualTo("已导入的新名称"));
            Assert.That(JsonUtility.ToJson(calendar.Capture()),Is.EqualTo(saved));
            Assert.That(JsonUtility.ToJson(new MarketCalendar(JsonUtility.FromJson<MarketCalendarState>(saved)).Capture()),Is.EqualTo(saved));
        }
    }
}
