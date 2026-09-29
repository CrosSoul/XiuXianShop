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
    [Category("DP67")]
    public sealed class AlchemyContentTests
    {
        string directory,assetDirectory;
        ShopCatalog catalog;AuthoredContent content;
        [SetUp] public void Setup()
        {
            string id=Guid.NewGuid().ToString("N");directory=Path.GetFullPath("Temp/DP67-"+id);Directory.CreateDirectory(directory);
            foreach(string file in Directory.GetFiles("ContentSources/DP67-Migration"))File.Copy(file,Path.Combine(directory,Path.GetFileName(file)));
            assetDirectory="Assets/Data/DP67-"+id;AssetDatabase.CreateFolder("Assets/Data","DP67-"+id);
            catalog=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath));
            content=UnityEngine.Object.Instantiate(catalog.authoredContent);catalog.authoredContent=content;
            AssetDatabase.CreateAsset(content,assetDirectory+"/Content.asset");AssetDatabase.CreateAsset(catalog,assetDirectory+"/Catalog.asset");
        }
        [TearDown] public void Cleanup(){AssetDatabase.DeleteAsset(assetDirectory);foreach(string f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
        void Edit(string file,string key,Action<List<ContentRow>> edit)
        {
            string path=Path.Combine(directory,file+".csv");var issues=new List<ContentIssue>();var rows=ContentCsv.Read(File.ReadAllText(path),file,key,Array.Empty<string>(),issues);
            Assert.That(issues,Is.Empty);edit(rows);var columns=rows[0].values.Keys.ToArray();string Quote(string v)=>"\""+v.Replace("\"","\"\"")+"\"";
            File.WriteAllText(path,string.Join(",",columns)+"\n"+string.Join("\n",rows.Select(r=>string.Join(",",columns.Select(c=>Quote(r.values[c]))))));
        }
        void Import(){using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.True,string.Join("\n",p.issues));ContentSync.Import(p);Assert.That(ContentSync.Import(p),Is.False);}}
        void Put(ShopSession s,GridItem item,ContainerId area,int box=0)
        {
            var size=s.GridSize(area,box);
            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)if(s.CanMove(item.Id,area,x,y,0,false,out _,box)){Assert.That(s.Move(item.Id,area,x,y,0,false,box));return;}
            Assert.Fail("No space: "+item.Definition.id);
        }
        void CloseBusiness(ShopSession s)
        {
            Assert.That(s.BeginBusiness());if(s.PendingVisitScene!=null){var runner=new StoryRunner(s,s.PendingVisitScene);while(s.PendingVisitScene!=null)Assert.That(runner.Continue());}Assert.That(s.EndBusiness());
        }
        ShopSession Prepare(bool shop,string recipe)
        {
            // Reuse existing transport/equipment fixtures, preserving the imported alchemy config.
            var config=JsonUtility.FromJson<AlchemySettings>(JsonUtility.ToJson(catalog.alchemy));AlchemyVerification.Configure(catalog);AlchemyVerification.AddShopFurnaceDefinition(catalog);catalog.alchemy=config;
            var s=new ShopSession(catalog,seed:!shop,customerSeed:67);
            if(shop)
            {
                Assert.That(s.GrantShopFurnace());int device=s.Items.Single().Id;Assert.That(s.GrantAlchemyTestMaterials(recipe));
                var pack=s.PortableStorage.Last();foreach(var item in s.In(ContainerId.Interior,pack.Id).ToArray())Put(s,item,ContainerId.Storage);
                CloseBusiness(s);Assert.That(s.OpenShopAlchemy(device));Assert.That(s.SelectAlchemyRecipe(recipe));
                foreach(var target in s.Alchemy.Recipe.targets.Where(t=>t.kind==AlchemyEventKind.Ingredient))Put(s,s.In(ContainerId.Storage).First(i=>i.Definition.id==target.itemId),ContainerId.AlchemyPreparation);
                Put(s,s.In(ContainerId.Storage).First(i=>i.Definition.id=="stone_mid"),ContainerId.AlchemyFuel);
            }
            else
            {
                var pack=s.PortableStorage.Single();CloseBusiness(s);Assert.That(s.BeginCarrying(pack.Id));
                foreach(var item in s.Items.Where(i=>i.Definition.category==ItemCategory.Material).ToArray())Put(s,item,ContainerId.Interior,pack.Id);
                var fuel=s.Items.Single(i=>i.Definition.id=="stone_mid");Put(s,fuel,ContainerId.RightHand);
                Assert.That(s.BeginTravel());Assert.That(s.EnterLocation(ShopSession.AlchemyLocationId));Put(s,fuel,ContainerId.AlchemyFuel);
                foreach(var item in s.In(ContainerId.Interior,pack.Id).ToArray())Put(s,item,ContainerId.Location);
                Assert.That(s.SelectAlchemyRecipe(recipe));
            }
            return s;
        }
        int Id(ShopSession s,string id)=>s.Items.Single(i=>i.Definition.id==id).Id;
        void Add(ShopSession s,string id)=>Assert.That(s.AddAlchemyIngredient(Id(s,id)),Is.True,s.Message);
        [TestCase(false)][TestCase(true)] public void ImportedGrindDurationAffectsBothEntrances(bool shop)
        {
            Edit("alchemy-global","配置键",rows=>rows.Single(r=>r.values["配置键"]=="grindDuration").values["当前值"]="4");Import();
            var s=Prepare(shop,"recipe_pill_fire_yang");int fruit=Id(s,"mat_fire_fruit");Assert.That(s.GrindAlchemyIngredient(fruit));
            s.TickAlchemy(3.9);Assert.That(s.Alchemy.Locked);s.TickAlchemy(.1);Assert.That(s.IsAlchemyGround(fruit));Assert.That(s.Alchemy.Locked,Is.False);
        }
        [TestCase(false)][TestCase(true)] public void ResidenceAndQualityValueChangesReachBothEntrances(bool shop)
        {
            Edit("alchemy-materials","条目ID",rows=>rows.Single(r=>r.values["条目ID"]=="alchemy_basic_dew").values["目标在炉时长"]="4");
            Edit("alchemy-global","配置键",rows=>rows.Single(r=>r.values["配置键"]=="qualityTopValueMultiplier").values["当前值"]="2");Import();
            var s=Prepare(shop,"recipe_pill_basic");Add(s,"herb");Assert.That(s.StartAlchemy());s.TickAlchemy(12);Add(s,"dew");s.TickAlchemy(4);Assert.That(s.CollectAlchemy());
            Assert.That(s.Alchemy.Quality,Is.EqualTo(PillQuality.Superior));var j=s.Alchemy.Judgements.Last();Assert.That(j.TargetTime,Is.EqualTo(4));Assert.That(j.ActualTime,Is.EqualTo(4));
            var pill=s.In(ContainerId.AlchemyOutput).Single();Assert.That(pill.Definition.id,Is.EqualTo("pill"));Assert.That(pill.QualityValueMultiplier,Is.EqualTo(2m));
        }
        [Test] public void StateRequirementAndHeatStepsAreImported()
        {
            Edit("alchemy-materials","条目ID",rows=>rows.Single(r=>r.values["条目ID"]=="alchemy_basic_dew").values["要求状态"]="研磨");
            Edit("alchemy-steps","步骤ID",rows=>{rows.Single(r=>r.values["步骤ID"]=="basic_03").values["要求状态"]="研磨";var heat=rows.Single(r=>r.values["步骤ID"]=="mw_05");heat.values["火候"]="低";heat.values["提示文本"]="验证低火提示";});Import();
            var s=Prepare(true,"recipe_pill_basic");Assert.That(s.Alchemy.Recipe.allowGrinding);Add(s,"herb");Assert.That(s.StartAlchemy());s.TickAlchemy(8);Add(s,"dew");s.TickAlchemy(8);Assert.That(s.CollectAlchemy());Assert.That(s.Alchemy.StructuralErrors.Single(),Does.Contain("研磨"));
            var high=catalog.alchemy.recipes.Single(r=>r.id=="recipe_pill_metal_water");Assert.That(high.targets.First(t=>t.kind==AlchemyEventKind.Heat).heat,Is.EqualTo(AlchemyHeat.Low));Assert.That(high.steps.Any(t=>t.hint=="验证低火提示"));
        }
        [TestCase("recipe-lines","条目ID","物品ID","unknown")]
        [TestCase("alchemy-materials","条目ID","配方ID","unknown")]
        [TestCase("alchemy-materials","条目ID","投料顺序","2")]
        [TestCase("alchemy-steps","步骤ID","步骤ID","basic_02")]
        [TestCase("alchemy-steps","步骤ID","动作类型","Script")]
        public void InvalidReferencesOrdersAndActionsDoNotMutateAssets(string file,string key,string field,string value)
        {
            string before=EditorJsonUtility.ToJson(catalog);Edit(file,key,rows=>rows[0].values[field]=value);
            using(var p=ContentSync.Validate(directory,catalog,content)){Assert.That(p.Valid,Is.False);Assert.Throws<InvalidOperationException>(()=>ContentSync.Import(p));}Assert.That(EditorJsonUtility.ToJson(catalog),Is.EqualTo(before));
        }
        [Test] public void BaselineHasOnlyThreeRecipesAndNoSpiritStoneIngredient()
        {
            Import();Assert.That(catalog.alchemy.recipes.Length,Is.EqualTo(3));Assert.That(content.recipeLines.Count(r=>r.enabled && r.purpose=="产物"),Is.EqualTo(3));
            Assert.That(catalog.alchemy.recipes.SelectMany(r=>r.targets).Where(t=>t.kind==AlchemyEventKind.Ingredient).All(t=>catalog.Find(t.itemId).category==ItemCategory.Material));
        }
    }
}
