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
    [Category("DP68")]
    public sealed class NewGameTests
    {
        ShopCatalog catalog;AuthoredContent content;string directory,assetDirectory;
        [SetUp] public void Setup()
        {
            string id=Guid.NewGuid().ToString("N");directory=Path.GetFullPath("Temp/DP68-"+id);Directory.CreateDirectory(directory);
            foreach(string file in Directory.GetFiles("ContentSources/DP68-Migration"))File.Copy(file,Path.Combine(directory,Path.GetFileName(file)));
            assetDirectory="Assets/Data/DP68-"+id;AssetDatabase.CreateFolder("Assets/Data","DP68-"+id);
            catalog=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath));content=UnityEngine.Object.Instantiate(catalog.authoredContent);catalog.authoredContent=content;
            AssetDatabase.CreateAsset(content,assetDirectory+"/Content.asset");AssetDatabase.CreateAsset(catalog,assetDirectory+"/Catalog.asset");
        }
        [TearDown] public void Cleanup(){AssetDatabase.DeleteAsset(assetDirectory);foreach(string f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
        ShopSession New()=>ShopSession.NewGame(catalog,catalog.startProfileId,68);
        void Edit(string file,string key,Action<List<ContentRow>> edit)
        {
            string path=Path.Combine(directory,file+".csv");var issues=new List<ContentIssue>();var rows=ContentCsv.Read(File.ReadAllText(path),file,key,Array.Empty<string>(),issues);Assert.That(issues,Is.Empty);edit(rows);
            var columns=rows[0].values.Keys.ToArray();string Quote(string v)=>"\""+v.Replace("\"","\"\"")+"\"";
            File.WriteAllText(path,string.Join(",",columns)+"\n"+string.Join("\n",rows.Select(r=>string.Join(",",columns.Select(c=>Quote(r.values[c]))))));
        }
        void Import(){using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.True,string.Join("\n",p.issues));ContentSync.Import(p);Assert.That(ContentSync.Import(p),Is.False);}}
        [Test] public void CanonicalBaselineAndRestartAreDeterministic()
        {
            var first=New();Assert.That(first.Year,Is.EqualTo(1));Assert.That(first.Month,Is.EqualTo(1));Assert.That(first.Money,Is.EqualTo(120));Assert.That(first.Stamina,Is.EqualTo(100));
            string positions=string.Join("|",first.Items.Select(i=>$"{i.Id}:{i.Definition.id}:{i.X}:{i.Y}:{i.Rotation}"));
            Assert.That(first.Items.Select(i=>i.Definition.id),Is.EqualTo(new[]{"sign","herb","dew","pill","cinnabar","jade","sword","test-storage-case","test-portable"}));
            first.SetProgressFlag("old-session");Assert.That(first.Move(first.Items.First().Id,ContainerId.Display,0,0,0,false));
            var second=New();Assert.That(second.HasProgressFlag("old-session"),Is.False);Assert.That(string.Join("|",second.Items.Select(i=>$"{i.Id}:{i.Definition.id}:{i.X}:{i.Y}:{i.Rotation}")),Is.EqualTo(positions));
            Assert.That(second.UnlockedLocations.Select(l=>l.id),Is.EquivalentTo(new[]{"baishitang","tingfeng-teahouse"}));
            Assert.That(second.HasProgressFlag("profession:alchemy"),Is.False);Assert.That(second.IsLocationUnlocked(ShopSession.AlchemyLocationId),Is.False);Assert.That(second.Items.Any(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId),Is.False);
        }
        [Test] public void ImportedBalanceDateItemsAndStateAffectOnlyNewSessions()
        {
            var old=New();old.SetProgressFlag("profession:alchemy");Assert.That(old.UnlockLocation(ShopSession.AlchemyLocationId));string saved=old.CaptureSave();
            Edit("start-profiles","ProfileID",rows=>{rows[0].values["初始卡内余额"]="321";rows[0].values["开始年份"]="2";rows[0].values["开始月份"]="5";rows[0].values["初始体力"]="35";});
            Edit("start-items","条目ID",rows=>{var n=new ContentRow{values=new Dictionary<string,string>(rows[0].values)};n.values["条目ID"]="new-item";n.values["物品ID"]="dew";n.values["数量"]="2";n.values["目标区域"]="Display";rows.Add(n);});
            Edit("start-states","条目ID",rows=>{rows[0].values["状态值"]="false";var n=new ContentRow{values=new Dictionary<string,string>(rows[0].values)};n.values["条目ID"]="new-flag";n.values["状态类型"]="Flag";n.values["状态ID"]="start:test";n.values["状态值"]="true";rows.Add(n);});Import();
            var next=New();Assert.That(next.Money,Is.EqualTo(321));Assert.That(next.Year,Is.EqualTo(2));Assert.That(next.Month,Is.EqualTo(5));Assert.That(next.Stamina,Is.EqualTo(35));Assert.That(next.In(ContainerId.Display).Count(),Is.EqualTo(2));Assert.That(next.HasProgressFlag("start:test"));Assert.That(next.IsLocationUnlocked("baishitang"),Is.False);
            var loaded=ShopSession.RestoreSave(catalog,saved);Assert.That(loaded.Money,Is.EqualTo(120));Assert.That(loaded.Items.Count,Is.EqualTo(9));Assert.That(loaded.HasProgressFlag("profession:alchemy"));Assert.That(loaded.IsLocationUnlocked(ShopSession.AlchemyLocationId));Assert.That(loaded.HasProgressFlag("start:test"),Is.False);
        }
        [TestCase("start-items","条目ID","物品ID","unknown")]
        [TestCase("start-items","条目ID","数量","0")]
        [TestCase("start-items","条目ID","目标区域","Location")]
        [TestCase("start-states","条目ID","状态ID","unknown-location")]
        [TestCase("start-profiles","ProfileID","开始月份","13")]
        public void InvalidContentCannotPartiallyImport(string file,string key,string field,string value)
        {
            string before=EditorJsonUtility.ToJson(content);Edit(file,key,rows=>rows[0].values[field]=value);
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));}Assert.That(EditorJsonUtility.ToJson(content),Is.EqualTo(before));
        }
        [Test] public void ForcedPositionIsHonoredAndOverlapsRejectCreation()
        {
            Edit("start-items","条目ID",rows=>{rows[0].values["目标区域"]="Display";rows[0].values["X"]="1";rows[0].values["Y"]="1";rows[0].values["旋转"]="1";});Import();
            var s=New();var sign=s.Items.Single(i=>i.Definition.id=="sign");Assert.That(sign.Container,Is.EqualTo(ContainerId.Display));Assert.That(sign.X,Is.EqualTo(1));Assert.That(sign.Rotation,Is.EqualTo(1));
            var row=content.startItems.Single(i=>i.itemId=="sign");row.quantity=2;Assert.Throws<ArgumentException>(()=>New());
        }
        [TestCase("Profession")][TestCase("Recipe")][TestCase("Knowledge")]
        public void UnknownStateTargetsAreRejected(string type)
        {
            Edit("start-states","条目ID",rows=>{rows[0].values["状态类型"]=type;rows[0].values["状态ID"]="unknown";});
            using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.Valid,Is.False);
        }
        [Test] public void ContainerCannotStartOnDisplayAndPartialCoordinatesFail()
        {
            Edit("start-items","条目ID",rows=>rows.Single(r=>r.values["物品ID"]=="test-storage-case").values["目标区域"]="Display");
            using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.Valid,Is.False);
            File.Copy("ContentSources/DP68-Migration/start-items.csv",Path.Combine(directory,"start-items.csv"),true);
            Edit("start-items","条目ID",rows=>rows[0].values["X"]="1");using(var p=ContentSync.Validate(directory,catalog,content))Assert.That(p.Valid,Is.False);
        }
        [Test] public void CanonicalStoryUnlocksAlchemyAndLoadKeepsResults()
        {
            var s=New();Assert.That(s.BeginBusiness());Assert.That(s.PendingVisitScene,Is.Not.Null);
            var r=new StoryRunner(s,s.PendingVisitScene);while(s.PendingVisitScene!=null)Assert.That(r.Continue());Assert.That(s.EndBusiness());
            Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("location_alchemy_training_visit"));
            r=new StoryRunner(s,s.PendingVisitScene);while(s.PendingVisitScene!=null){if(r.Current.type=="Choice")Assert.That(r.Choose(0));else Assert.That(r.Continue());}
            Assert.That(s.HasProgressFlag("profession:alchemy"));Assert.That(s.IsLocationUnlocked(ShopSession.AlchemyLocationId));Assert.That(s.LeaveLocation());Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            var loaded=ShopSession.RestoreSave(catalog,s.CaptureSave());Assert.That(loaded.HasProgressFlag("profession:alchemy"));Assert.That(loaded.IsLocationUnlocked(ShopSession.AlchemyLocationId));Assert.That(loaded.Items.Count,Is.EqualTo(9));
        }
    }
}
