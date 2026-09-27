using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP61")]
    public sealed class PersistenceTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetStorageVerificationDefaults();
            AlchemyVerification.AddShopFurnaceDefinition(catalog);
            catalog.startingItems=new[]{"test-storage-case","pill","stone_mid",ShopSession.ShopFurnaceDefinitionId};
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        [Test] public void ClosedSnapshotKeepsItemRelationshipsQualityResourceAndProgress()
        {
            var s=new ShopSession(catalog,customerSeed:61);
            var setup=JsonUtility.FromJson<ShopSave>(s.CaptureSave());var savedPill=setup.items.Single(i=>i.definitionId=="pill");
            savedPill.quality=PillQuality.Superior;savedPill.qualityValueMultiplier="1.5";savedPill.hasPurchaseValue=true;savedPill.purchaseValue=18;
            s=ShopSession.RestoreSave(catalog,JsonUtility.ToJson(setup));
            var box=s.Items.Single(i=>i.Definition.IsStorage);var pill=s.Items.Single(i=>i.Definition.id=="pill");
            var stone=s.Items.Single(i=>i.Definition.id=="stone_mid");
            Assert.That(s.Move(pill.Id,ContainerId.Interior,1,1,1,true,box.Id));Assert.That(s.ConsumeSpirit(stone.Id,321));
            s.SetProgressFlag("profession:alchemy");s.SetProgressFlag("story:met-alchemist");s.SetProgressFlag("recipe:recipe_pill_basic");
            Assert.That(s.BeginBusiness());Assert.That(s.EndBusiness());Assert.That(s.CanSave(out _));
            var json=s.CaptureSave();var loaded=ShopSession.RestoreSave(catalog,json);
            Assert.That(loaded,Is.Not.SameAs(s));Assert.That(loaded.Phase,Is.EqualTo(TurnPhase.Closed));
            Assert.That(loaded.CaptureSave(),Is.EqualTo(json));
            Assert.That(loaded.Find(pill.Id).BaseValue,Is.EqualTo(pill.BaseValue));Assert.That(loaded.Find(stone.Id).SpiritUnits,Is.EqualTo(stone.SpiritUnits));
            Assert.That(loaded.In(ContainerId.Interior,box.Id).Single().Id,Is.EqualTo(pill.Id));Assert.That(loaded.HasProgressFlag("profession:alchemy"));
        }
        [Test] public void GeneratedTeaCommissionAndCalendarRestoreWithoutDrawingAgain()
        {
            catalog.firstLocationStaminaCost=0;catalog.extraLocationStaminaCost=0;
            var s=new ShopSession(catalog,customerSeed:61);s.BeginBusiness();s.EndBusiness();
            Assert.That(s.BeginCarrying(0));Assert.That(s.BeginTravel());Assert.That(s.EnterLocation("tingfeng-teahouse"));Assert.That(s.LeaveLocation());
            Assert.That(s.EnterLocation("baishitang"));string id=s.CommissionCandidates.First().id;Assert.That(s.CompleteCommission(id));
            Assert.That(s.LeaveLocation(true));Assert.That(s.ReturnToShop(true));Assert.That(s.EndCarrying());
            string before=s.CaptureSave();var loaded=ShopSession.RestoreSave(catalog,before);
            Assert.That(loaded.CaptureSave(),Is.EqualTo(before));Assert.That(loaded.CompletedCommissionId,Is.EqualTo(id));
            Assert.That(loaded.CommissionLimitReached);Assert.That(loaded.CommissionCandidates.Select(t=>t.id),Is.EqualTo(s.CommissionCandidates.Select(t=>t.id)));
            Assert.That(JsonUtility.ToJson(loaded.LatestTeaVisit),Is.EqualTo(JsonUtility.ToJson(s.LatestTeaVisit)));
        }
        [Test] public void RestoringNextMonthDoesNotReapplyRecoveryOrOverflow()
        {
            var s=new ShopSession(catalog,customerSeed:61);s.TrySpendStamina(10);s.BeginBusiness();s.EndBusiness();s.AdvanceTurn();
            int stamina=s.Stamina,count=s.CustomerCountThisTurn;
            for(int n=0;n<3;n++)s=ShopSession.RestoreSave(catalog,s.CaptureSave());
            Assert.That(s.Turn,Is.EqualTo(2));Assert.That(s.Stamina,Is.EqualTo(stamina));Assert.That(s.CustomerCountThisTurn,Is.EqualTo(count));Assert.That(s.HasStaminaOverflowCustomer);
        }
        [Test] public void SaveGateRejectsBusinessCarryAndOpenFurnace()
        {
            var s=new ShopSession(catalog,customerSeed:61);Assert.That(s.CanSave(out _));s.BeginBusiness();Assert.That(s.CanSave(out _),Is.False);
            s.EndBusiness();Assert.That(s.CanSave(out _));s.BeginCarrying(0);Assert.That(s.CanSave(out _),Is.False);s.EndCarrying();
            s.OpenShopAlchemy(s.Items.Single(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId).Id);Assert.That(s.CanSave(out _),Is.False);
        }
        [TestCase("schema")] [TestCase("duplicate")] [TestCase("unknown")] [TestCase("capacity")] [TestCase("quality")]
        public void InvalidSnapshotCannotChangeSourceSession(string damage)
        {
            var s=new ShopSession(catalog,customerSeed:61);string before=s.CaptureSave();var save=JsonUtility.FromJson<ShopSave>(before);
            if(damage=="schema")save.schemaVersion=99;
            if(damage=="duplicate")save.items[1].id=save.items[0].id;
            if(damage=="unknown")save.items[0].definitionId="unknown-item";
            if(damage=="capacity")save.items.Single(i=>i.definitionId=="stone_mid").spiritCapacityUnits++;
            if(damage=="quality")save.items[0].qualityValueMultiplier="not-a-number";
            Assert.Catch(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(save)));Assert.That(s.CaptureSave(),Is.EqualTo(before));
        }
        [Test] public void FailedSlotWritePreservesPreviouslySavedFile()
        {
            string directory=System.IO.Path.Combine(Application.dataPath,"../Temp/DP61-atomic-"+Guid.NewGuid().ToString("N"));
            var slots=new ShopSaveSlots(directory,3);string path=slots.PathFor(1);
            try
            {
                var s=new ShopSession(catalog,customerSeed:61);slots.Write(1,s);string original=slots.Read(1);
                // A blocked staging path is an actual filesystem failure before replacing the good slot.
                System.IO.Directory.CreateDirectory(path+".tmp");
                Assert.Catch(()=>slots.Write(1,s));Assert.That(slots.Read(1),Is.EqualTo(original));
                System.IO.Directory.Delete(path+".tmp");
                s.BeginBusiness();s.EndBusiness();slots.Write(1,s);
                Assert.That(JsonUtility.FromJson<ShopSave>(slots.Read(1)).phase,Is.EqualTo(TurnPhase.Closed));
                Assert.That(System.IO.File.Exists(path+".tmp"),Is.False);
            }
            finally
            {
                if(System.IO.Directory.Exists(path+".tmp"))System.IO.Directory.Delete(path+".tmp");
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.Directory.Exists(directory))System.IO.Directory.Delete(directory);
            }
        }
    }
}
