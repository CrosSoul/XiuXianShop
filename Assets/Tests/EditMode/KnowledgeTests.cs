using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XiuXianShop.Editor;

namespace XiuXianShop.Tests
{
    [Category("DP41")]
    public sealed class KnowledgeTests
    {
        const string Basic = "knowledge_test_basic", Slip = "test-jade-technique";
        ShopCatalog catalog;
        AuthoredContent content;
        string directory, assetDirectory;

        [SetUp] public void Setup()
        {
            string token = Guid.NewGuid().ToString("N");
            directory = Path.GetFullPath("Temp/DP41-" + token);Directory.CreateDirectory(directory);
            foreach(string path in Directory.GetFiles("ContentSources/DP41-Graybox"))File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
            assetDirectory = "Assets/Data/DP41-" + token;AssetDatabase.CreateFolder("Assets/Data", "DP41-" + token);
            catalog = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath));
            content = UnityEngine.Object.Instantiate(catalog.authoredContent);catalog.authoredContent = content;
            content.visits = Array.Empty<AuthoredVisit>();content.scenes = Array.Empty<AuthoredScene>();content.nodes = Array.Empty<AuthoredNode>();
            catalog.startingItems = new[] { Slip };
            AssetDatabase.CreateAsset(content, assetDirectory + "/Content.asset");AssetDatabase.CreateAsset(catalog, assetDirectory + "/Catalog.asset");
        }
        [TearDown] public void Cleanup()
        {
            AssetDatabase.DeleteAsset(assetDirectory);
            foreach(string path in Directory.GetFiles(directory))File.Delete(path);Directory.Delete(directory);
        }
        ShopSession New(bool closed = true)
        {
            var s = new ShopSession(catalog, customerSeed:41);
            if(closed){Assert.That(s.BeginBusiness());Assert.That(s.EndBusiness());}
            return s;
        }
        void Edit(string file, string key, Action<List<ContentRow>> edit)
        {
            string path = Path.Combine(directory, file + ".csv");var issues = new List<ContentIssue>();
            var rows = ContentCsv.Read(File.ReadAllText(path), file, key, Array.Empty<string>(), issues);Assert.That(issues, Is.Empty);
            edit(rows);var columns = rows[0].values.Keys.ToArray();
            string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
            File.WriteAllText(path, string.Join(",", columns) + "\n" + string.Join("\n", rows.Select(r => string.Join(",", columns.Select(c => Quote(r.values[c]))))));
        }
        [Test] public void ClosedLearningCostsOncePerKnowledgeAndCapsAtMastery()
        {
            var s = New();Assert.That(s.GrantJadeSlipForVerification(Slip));
            int[] stamina = {80,60,40,20};
            for(int n = 0; n < 4; n++)
            {
                Assert.That(s.StudyKnowledge(Basic));Assert.That(s.Stamina, Is.EqualTo(stamina[n]));
                Assert.That(s.GetKnowledgeProgress(Basic).Progress, Is.EqualTo((n+1)*25));
            }
            Assert.That(s.GetKnowledgeProgress(Basic).Mastered);Assert.That(s.StudyKnowledge(Basic), Is.False);Assert.That(s.Stamina, Is.EqualTo(20));
            Assert.That(s.Items.Count, Is.EqualTo(2));Assert.That(s.ValidateState(), Is.Null);
        }
        [Test] public void PreparationBusinessTextAndInsufficientStaminaDoNotSpend()
        {
            var s = New(false);Assert.That(s.StudyKnowledge(Basic), Is.False);Assert.That(s.BeginBusiness());Assert.That(s.StudyKnowledge(Basic), Is.False);
            Assert.That(s.EndBusiness());Assert.That(s.StudyKnowledge("text_test_market_notice"), Is.False);
            Assert.That(s.TrySpendStamina(81));Assert.That(s.StudyKnowledge(Basic), Is.False);Assert.That(s.Stamina, Is.EqualTo(19));Assert.That(s.GetKnowledgeProgress(Basic).Progress, Is.Zero);
        }
        [Test] public void StudyingDoesNotConsumeJadeSlipOrSpiritResource()
        {
            SpiritStoneVerification.AddDefinitions(catalog);catalog.startingItems = new[]{Slip,"stone_mid"};
            var s = New();var source = s.Items.Single(i => i.Definition.id == Slip);var stone = s.Items.Single(i => i.Definition.id == "stone_mid");
            int energy = stone.SpiritUnits;Assert.That(s.StudyKnowledge(Basic));
            Assert.That(s.Find(source.Id), Is.SameAs(source));Assert.That(stone.SpiritUnits, Is.EqualTo(energy));Assert.That(s.Money, Is.EqualTo(120));
        }
        void SellSource(ShopSession s)
        {
            Assert.That(s.AdvanceTurn());catalog.customers.buyingWeight = 1;catalog.customers.sellingWeight = catalog.customers.tradingWeight = 0;
            catalog.customers.displayedItemWeight = 1000000;
            var slip = s.Items.Single(i => i.Definition.id == Slip);Assert.That(s.Move(slip.Id, ContainerId.Display, 0, 0, 0, false));
            Assert.That(s.BeginBusiness());Assert.That(s.Offer.RequestedCategory, Is.EqualTo(ItemCategory.JadeSlip));
            Assert.That(s.Move(slip.Id, ContainerId.Counter, 0, 0, 0, false));Assert.That(s.AcceptTrade());Assert.That(s.EndBusiness());
            Assert.That(s.Find(slip.Id), Is.Null);Assert.That(s.HasKnowledgeSource(Basic), Is.False);
        }
        [Test] public void SoldLastSourceKeepsProgressAndRealPurchaseResumes()
        {
            var s = New();s.StudyKnowledge(Basic);s.StudyKnowledge(Basic);SellSource(s);
            Assert.That(s.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(50));int stamina = s.Stamina;
            Assert.That(s.StudyKnowledge(Basic), Is.False);Assert.That(s.Stamina, Is.EqualTo(stamina));
            catalog.Find(Slip).supplierAvailable = true;catalog.customers.buyingWeight = 0;catalog.customers.sellingWeight = 1;
            catalog.customers.supplyPools = new[] {new CustomerSupplyPool {category = ItemCategory.JadeSlip, itemIds = new[]{Slip}}};
            Assert.That(s.AdvanceTurn());Assert.That(s.BeginBusiness());var source = s.Offer.SupplierItem;
            Assert.That(s.HasKnowledgeSource(Basic), Is.False, "Customer property is not a readable player source.");
            Assert.That(s.Move(source.Id, ContainerId.Counter, 0, 0, 0, false));Assert.That(s.AcceptTrade());Assert.That(s.EndBusiness());
            Assert.That(s.HasKnowledgeSource(Basic));Assert.That(s.StudyKnowledge(Basic));Assert.That(s.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(75));
            Assert.That(s.ValidateState(), Is.Null);
        }
        [Test] public void MasteredSurvivesSellingAndSavingWithoutSource()
        {
            var s = New();for(int n = 0; n < 4; n++)Assert.That(s.StudyKnowledge(Basic));SellSource(s);
            string json = s.CaptureSave();Assert.That(json, Does.Not.Contain(content.knowledge[1].text));
            var restored = ShopSession.RestoreSave(catalog, json);Assert.That(restored.GetKnowledgeProgress(Basic).Mastered);
            Assert.That(restored.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(100));Assert.That(restored.HasKnowledgeSource(Basic), Is.False);
        }
        [Test] public void PartialProgressSaveAndChangedThresholdPreserveEarnedState()
        {
            var s = New();s.StudyKnowledge(Basic);s.StudyKnowledge(Basic);string json = s.CaptureSave();
            content.knowledge.Single(k => k.id == Basic).masteryThreshold = 40;
            var restored = ShopSession.RestoreSave(catalog, json);Assert.That(restored.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(50));
            Assert.That(restored.GetKnowledgeProgress(Basic).Mastered, Is.False);Assert.That(restored.Stamina, Is.EqualTo(60));
            Assert.That(restored.StudyKnowledge(Basic));Assert.That(restored.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(40));Assert.That(restored.GetKnowledgeProgress(Basic).Mastered);
        }
        [TestCase("unknown")][TestCase("duplicate")][TestCase("negative")]
        public void InvalidSavedKnowledgeCannotReplaceSourceSession(string damage)
        {
            var s = New();s.StudyKnowledge(Basic);string before = s.CaptureSave();var save = JsonUtility.FromJson<ShopSave>(before);
            if(damage == "unknown")save.knowledge[0].knowledgeId = "unknown";
            if(damage == "duplicate")save.knowledge = new[]{save.knowledge[0],save.knowledge[0]};
            if(damage == "negative")save.knowledge[0].progress = -1;
            Assert.Throws<ArgumentException>(() => ShopSession.RestoreSave(catalog, JsonUtility.ToJson(save)));Assert.That(s.CaptureSave(), Is.EqualTo(before));
        }
        [Test] public void PriorSchemaOneSaveHasNoKnowledgeAndStillLoads()
        {
            var s = New();var save = JsonUtility.FromJson<ShopSave>(s.CaptureSave());save.knowledge = null;
            var restored = ShopSession.RestoreSave(catalog, JsonUtility.ToJson(save));Assert.That(restored.GetKnowledgeProgress(Basic).Progress, Is.Zero);
        }
        [Test] public void UnifiedImportChangesTextLearningParametersAndMappingWithoutCode()
        {
            Edit("knowledge", "KnowledgeID", rows =>
            {
                var row = rows.Single(r => r.Get("KnowledgeID") == Basic);row.values["正文"] = "新正文";
                row.values["单次学习体力"] = "7";row.values["单次进度"] = "60";row.values["掌握阈值"] = "80";
            });
            Edit("jade-slips", "条目ID", rows => rows[0].values["KnowledgeID"] = Basic);
            string before = EditorJsonUtility.ToJson(content);
            using(var p = ContentSync.Validate(directory, catalog, content))
            {
                Assert.That(p.Valid, Is.True, string.Join("\n",p.issues));Assert.That(EditorJsonUtility.ToJson(content), Is.EqualTo(before));
                Assert.That(ContentSync.Import(p));Assert.That(ContentSync.Import(p), Is.False);
            }
            var s = New();s.GrantJadeSlipForVerification("test-jade-text");
            Assert.That(s.KnowledgeForItem(s.Items.Last()).text, Is.EqualTo("新正文"));
            Assert.That(s.StudyKnowledge(Basic));Assert.That(s.Stamina, Is.EqualTo(93));Assert.That(s.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(60));
            Assert.That(s.StudyKnowledge(Basic));Assert.That(s.GetKnowledgeProgress(Basic).Progress, Is.EqualTo(80));Assert.That(s.GetKnowledgeProgress(Basic).Mastered);
        }
        [TestCase("knowledge", "KnowledgeID", "KnowledgeID", "duplicate")]
        [TestCase("knowledge", "KnowledgeID", "单次学习体力", "-1")]
        [TestCase("knowledge", "KnowledgeID", "单次进度", "0")]
        [TestCase("knowledge", "KnowledgeID", "掌握阈值", "0")]
        [TestCase("jade-slips", "条目ID", "物品ID", "unknown")]
        [TestCase("jade-slips", "条目ID", "KnowledgeID", "unknown")]
        [TestCase("jade-slips", "条目ID", "物品ID", "herb")]
        public void InvalidContentBlocksAtomicImport(string file, string key, string field, string value)
        {
            string before = EditorJsonUtility.ToJson(content);
            Edit(file, key, rows =>
            {
                if(value == "duplicate")rows.Add(new ContentRow { values = new Dictionary<string,string>(rows[0].values) });
                else rows.First(r => file != "knowledge" || r.Get(key) == Basic).values[field] = value;
            });
            using(var p = ContentSync.Validate(directory, catalog, content)){Assert.That(p.Valid, Is.False);Assert.Throws<InvalidOperationException>(() => ContentSync.Import(p));}
            Assert.That(EditorJsonUtility.ToJson(content), Is.EqualTo(before));
        }
    }
}
