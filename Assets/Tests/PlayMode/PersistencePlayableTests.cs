#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP61")] public IEnumerator SaveMenuRequiresOverwriteAndLoadConfirmationAtSafeStages()
        {
            IsolateAlchemyTestMouse();yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(shop.IsSystemMenuOpen);
            Assert.That(shop.FindButton("LoadSlot_1").interactable,Is.False);
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("SaveSlot_")),Is.EqualTo(3));
            yield return Click("SaveSlot_1");string initial=shop.SaveSlots.Read(1);yield return Click("SystemResume");
            var original=shop.Session;var item=original.Items.First(i=>i.Definition.id=="herb");yield return Drag(item,ContainerId.Counter,0,0);
            shop.OpenSystemMenu();yield return Click("SaveSlot_1");Assert.That(shop.SaveSlots.Read(1),Is.EqualTo(initial));
            yield return Click("SaveCancel");Assert.That(shop.SaveSlots.Read(1),Is.EqualTo(initial));
            yield return Click("LoadSlot_1");yield return Click("SaveCancel");Assert.That(shop.Session,Is.SameAs(original));
            yield return Click("LoadSlot_1");yield return Click("SaveConfirm");Assert.That(shop.Session,Is.Not.SameAs(original));
            Assert.That(shop.Session.Find(item.Id).Container,Is.EqualTo(ContainerId.Storage));
            yield return Click("SystemResume");yield return Drag(shop.Session.Find(item.Id),ContainerId.Display,0,0);
            shop.OpenSystemMenu();yield return Click("SaveSlot_1");yield return Click("SaveConfirm");
            var overwritten=JsonUtility.FromJson<ShopSave>(shop.SaveSlots.Read(1));
            Assert.That(overwritten.items.Single(i=>i.id==item.Id).container,Is.EqualTo(ContainerId.Display));
            Assert.That(System.DateTimeOffset.TryParse(overwritten.savedAtUtc,out _));
            var slotLabel=shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="SaveSlotInfo_1").text;
            Assert.That(slotLabel,Does.Contain("1 年 1 月").And.Contain("营业准备").And.Not.Contain("保存时间缺失"));
            yield return Click("SystemResume");yield return Click("BeginBusiness");shop.OpenSystemMenu();Assert.That(shop.FindButton("SaveSlot_2").interactable,Is.False);
            yield return Click("SystemResume");
            if(shop.Session.PendingVisitScene!=null)Assert.That(shop.CanSaveGame(out _),Is.False);
            while(shop.Session.PendingVisitScene!=null)yield return Click("VisitSceneContinue");
            yield return Click("EndBusiness");shop.OpenSystemMenu();Assert.That(shop.FindButton("SaveSlot_2").interactable);
            yield return Click("SaveSlot_2");Assert.That(JsonUtility.FromJson<ShopSave>(shop.SaveSlots.Read(2)).phase,Is.EqualTo(TurnPhase.Closed));
            yield return Click("SystemResume");yield return Click("AdvanceTurn");int stamina=shop.Session.Stamina,count=shop.Session.CustomerCountThisTurn;
            var auto=JsonUtility.FromJson<ShopSave>(shop.SaveSlots.Read(0));Assert.That(auto.turn,Is.EqualTo(2));Assert.That(auto.phase,Is.EqualTo(TurnPhase.Preparation));
            shop.OpenSystemMenu();yield return Click("LoadSlot_0");yield return Click("SaveConfirm");
            Assert.That(shop.Session.Stamina,Is.EqualTo(stamina));Assert.That(shop.Session.CustomerCountThisTurn,Is.EqualTo(count));
            var current=shop.Session;var invalid=JsonUtility.FromJson<ShopSave>(shop.SaveSlots.Read(1));invalid.schemaVersion=999;
            System.IO.File.WriteAllText(shop.SaveSlots.PathFor(1),JsonUtility.ToJson(invalid));
            yield return Click("LoadSlot_1");yield return Click("SaveConfirm");Assert.That(shop.Session,Is.SameAs(current));LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP61")] public IEnumerator LoadingSafeSlotExitsTravelAndRunningFurnaceWithoutLeavingOldViews()
        {
            yield return CreateShopAlchemyKit("recipe_pill_basic");yield return CloseBusinessForOuting();
            shop.OpenSystemMenu();yield return Click("SaveSlot_1");string snapshot=shop.Session.CaptureSave();yield return Click("SystemResume");
            yield return Click("CarryOpen");yield return Click("CarryConfirm");yield return Click("TravelBegin");
            yield return Click("TravelLocation_tingfeng-teahouse");
            shop.OpenSystemMenu();Assert.That(shop.FindButton("SaveSlot_2").interactable,Is.False);
            yield return Click("LoadSlot_1");yield return Click("SaveConfirm");
            Assert.That(shop.Session.CaptureSave(),Is.EqualTo(snapshot));Assert.That(shop.Session.IsTravelling,Is.False);
            yield return Click("SystemResume");
            var device=shop.Session.Items.Single(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId);
            yield return ClickGridItem(device);yield return Click("AlchemyRecipe_recipe_pill_basic");
            var herb=shop.Session.In(ContainerId.Storage).Single(i=>i.Definition.id=="herb");
            var fuel=shop.Session.In(ContainerId.Storage).Single(i=>i.Definition.id=="stone_mid");
            yield return Drag(herb,ContainerId.AlchemyPreparation,0,0);yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);
            yield return ClickGridItem(herb);yield return Click("AlchemyAdd");yield return Click("AlchemyStart");
            Assert.That(shop.Session.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Running));
            shop.OpenSystemMenu();Assert.That(shop.FindButton("SaveSlot_2").interactable,Is.False);
            yield return Click("LoadSlot_1");yield return Click("SaveConfirm");
            Assert.That(shop.Session.CaptureSave(),Is.EqualTo(snapshot));Assert.That(shop.Session.IsUsingAlchemy,Is.False);
            yield return Click("SystemResume");Assert.That(shop.IsShopAlchemyWindowOpen,Is.False);
            Assert.That(shop.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="ShopAlchemyWindow" || t.name=="TravelWindow"),Is.False);
            yield return Click("AdvanceTurn");Assert.That(shop.Session.Phase,Is.EqualTo(TurnPhase.Preparation));LogAssert.NoUnexpectedReceived();
        }

        [UnityTest,Category("DP61")] public IEnumerator SavedAlchemyItemsLoadIntoFreshDefaultSessionWithStableIds()
        {
            // Actual manual alchemy, warehouse transfers and continuation use the established default-session path.
            yield return PlayShopAlchemy("recipe_pill_basic",1);
            Assert.That(shop.Session.Phase,Is.EqualTo(TurnPhase.Closed),"The completed default business must reach the closed save point.");
            var old=shop.Session;old.SetProgressFlag("profession:alchemy");old.SetProgressFlag("story:learned-alchemy");
            var product=old.Items.First(i=>i.QualityValueMultiplier>1);var stone=old.Items.First(i=>i.Definition.id=="stone_mid");
            var device=old.Items.Single(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId);
            string snapshot=old.CaptureSave(),directory=shop.SaveDirectory;
            shop.OpenSystemMenu();yield return Click("SaveSlot_1");
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ShopPrototype.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            shop=Object.FindFirstObjectByType<ShopPrototype>();shop.SaveDirectory=directory;
            Assert.That(shop.Session,Is.Not.SameAs(old));Assert.That(shop.Session.Items.Any(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId),Is.False);
            shop.OpenSystemMenu();yield return Click("LoadSlot_1");yield return Click("SaveConfirm");
            Assert.That(shop.Session.CaptureSave(),Is.EqualTo(snapshot));Assert.That(shop.Session.Find(device.Id).Definition.id,Is.EqualTo(device.Definition.id));
            Assert.That(shop.FindButton("AdvanceTurn").interactable,Is.True,"Restored closed phase must refresh the month button.");
            Assert.That(shop.Session.Find(product.Id).QualityValueMultiplier,Is.EqualTo(product.QualityValueMultiplier));
            Assert.That(shop.Session.Find(stone.Id).SpiritUnits,Is.EqualTo(stone.SpiritUnits));Assert.That(shop.Session.HasProgressFlag("profession:alchemy"));
            yield return Click("SystemResume");yield return Click("AdvanceTurn");yield return CompleteDefaultBusiness();LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
