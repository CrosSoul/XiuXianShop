#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        IEnumerator DragIntoFreeSpace(GridItem item,ContainerId area,int box=0)
        {
            var size=shop.Session.GridSize(area,box);
            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
                if(shop.Session.CanMove(item.Id,area,x,y,0,false,out _,box))
                {yield return Drag(item,area,x,y);Assert.That(item.Container,Is.EqualTo(area));yield break;}
            Assert.Fail("No free space for "+item.Definition.id+" in "+area);
        }
        IEnumerator ClickGridItem(GridItem item)
        {
            var point=ItemPoint(item);yield return MouseAt(point,false);yield return MouseAt(point,true);yield return MouseAt(point,false);
        }
        IEnumerator CreateShopAlchemyKit(string recipe,int copies=1)
        {
            IsolateAlchemyTestMouse();var s=shop.Session;
            var chest=s.Items.Single(i=>i.Definition.category==ItemCategory.StorageContainer);
            // Real root inventory is limited; first use the existing chest to make room.
            yield return ClickGridItem(chest);
            foreach(var i in s.In(ContainerId.Storage).Where(i=>!i.Definition.IsStorage).ToArray())
                yield return DragIntoFreeSpace(i,ContainerId.Interior,chest.Id);
            yield return Click("StorageClose");
            Assert.That(EditorApplication.ExecuteMenuItem("XiuXianShop/Current Play Session/Grant Miniature Alchemy Furnace (no reset)"));
            Assert.That(s.Items.Count(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId),Is.EqualTo(1),s.Message);
            string title=recipe=="recipe_pill_basic"?"回气丹":recipe=="recipe_pill_fire_yang"?"赤阳丹":"金水凝元丹";
            for(int n=0;n<copies;n++)
            {
                Assert.That(EditorApplication.ExecuteMenuItem("XiuXianShop/Current Play Session/Grant Alchemy Kit/"+title));
                yield return null;
                var pack=s.PortableStorage.Last(i=>i.Definition.id==AlchemyVerification.MaterialPackId);
                yield return ClickGridItem(pack);
                foreach(var item in s.In(ContainerId.Interior,pack.Id).ToArray())yield return DragIntoFreeSpace(item,ContainerId.Storage);
                yield return Click("StorageClose");
            }
            Assert.That(shop.Session,Is.SameAs(s));Assert.That(s.IsTravelling,Is.False);Assert.That(s.HasTravelledThisTurn,Is.False);
        }
        [UnityTest,Category("DP60")] public IEnumerator ShopBasicAlchemyTwoBatchesAndNextMonth()=>PlayShopAlchemy("recipe_pill_basic",2);
        [UnityTest,Category("DP60")] public IEnumerator ShopGroundAlchemyUsesSameKernel()=>PlayShopAlchemy("recipe_pill_fire_yang",1);
        [UnityTest,Category("DP60")] public IEnumerator ShopComplexAlchemyUsesSameKernel()=>PlayShopAlchemy("recipe_pill_metal_water",1);
        [UnityTest,Category("DP60")] public IEnumerator ShopAlchemyWindowKeepsMainGridAndLoadedInstancesOnReopenAndFailedDrops()
        {
            yield return CreateShopAlchemyKit("recipe_pill_basic");yield return CloseBusinessForOuting();
            var s=shop.Session;var device=s.Items.Single(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId);
            var warehouse=shop.ItemView(device.Id).parent;var position=warehouse.position;
            yield return ClickGridItem(device);yield return Click("AlchemyRecipe_recipe_pill_basic");
            var batch=s.Alchemy;var herb=s.In(ContainerId.Storage).Single(i=>i.Definition.id=="herb");
            var dew=s.In(ContainerId.Storage).Single(i=>i.Definition.id=="dew");var fuel=s.In(ContainerId.Storage).Single(i=>i.Definition.id=="stone_mid");
            var ids=s.Items.ToArray();int spirit=fuel.SpiritUnits;
            yield return Drag(herb,ContainerId.AlchemyPreparation,0,0);
            yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);
            yield return Drag(dew,ContainerId.AlchemyPreparation,0,0);Assert.That(dew.Container,Is.EqualTo(ContainerId.Storage));
            yield return Drag(dew,ContainerId.AlchemyFuel,0,0);Assert.That(dew.Container,Is.EqualTo(ContainerId.Storage));
            yield return Drag(dew,ContainerId.AlchemyPreparation,5,3);Assert.That(dew.Container,Is.EqualTo(ContainerId.Storage));
            yield return Click("ShopAlchemyClose");Assert.That(shop.IsShopAlchemyWindowOpen,Is.False);
            Assert.That(s.HasPendingShopAlchemy);Assert.That(s.AdvanceTurn(),Is.False);
            yield return ClickGridItem(device);Assert.That(shop.IsShopAlchemyWindowOpen);
            Assert.That(s.Alchemy,Is.SameAs(batch));Assert.That(s.Items,Is.EqualTo(ids));Assert.That(fuel.SpiritUnits,Is.EqualTo(spirit));
            Assert.That(shop.ItemView(device.Id).parent,Is.SameAs(warehouse));Assert.That(warehouse.position,Is.EqualTo(position));
            var window=(RectTransform)shop.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="ShopAlchemyWindow");
            Assert.That(warehouse.IsChildOf(window),Is.False);Assert.That(window.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="StorageGrid"),Is.False);
            var title=(RectTransform)window.Find("ShopAlchemyTitleBar");var from=RectTransformUtility.WorldToScreenPoint(null,title.TransformPoint(title.rect.center));
            var before=window.anchoredPosition;
            yield return MouseAt(from,false);yield return MouseAt(from,true);yield return MouseAt(from+new Vector2(-30,20),true);yield return MouseAt(from+new Vector2(-30,20),false);
            Assert.That(window.anchoredPosition,Is.Not.EqualTo(before));
            yield return DragIntoFreeSpace(herb,ContainerId.Storage);yield return DragIntoFreeSpace(fuel,ContainerId.Storage);
            yield return Click("ShopAlchemyClose");Assert.That(s.IsAtShopAlchemy,Is.False);Assert.That(s.Items,Is.EqualTo(ids));
            Assert.That(s.Stamina,Is.EqualTo(100));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP60")] public IEnumerator ShopShortcutWithoutDeviceCannotUseOldInstantCraft()
        {
            IsolateAlchemyTestMouse();var s=shop.Session;var items=s.Items.ToArray();
            yield return Click("Craft");
            Assert.That(s.Items,Is.EqualTo(items));Assert.That(s.Crafted,Is.Zero);Assert.That(s.Stamina,Is.EqualTo(100));
            yield return CloseBusinessForOuting();yield return Click("Craft");
            Assert.That(s.Items,Is.EqualTo(items));Assert.That(s.Crafted,Is.Zero);Assert.That(s.IsAtShopAlchemy,Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator PlayShopAlchemy(string recipeId,int batches)
        {
            var s=shop.Session;var asset=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Data/ShopCatalog.asset");
            string assetBefore=JsonUtility.ToJson(asset);
            yield return CreateShopAlchemyKit(recipeId,batches);
            var device=s.Items.Single(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId);
            var warehouse=shop.ItemView(device.Id).parent;
            yield return ClickGridItem(device);Assert.That(s.ShopFurnaceId,Is.EqualTo(device.Id));
            Assert.That(shop.ItemView(device.Id).parent,Is.SameAs(warehouse),"Opening the furnace must not replace the main warehouse grid.");
            yield return Click("AlchemyRecipe_"+recipeId);
            Assert.That(s.StartAlchemy(),Is.False);Assert.That(shop.FindButton("AlchemyStart").interactable,Is.False);
            yield return Click("ShopAlchemyClose");Assert.That(s.Find(device.Id),Is.SameAs(device));
            yield return CloseBusinessForOuting();yield return ClickGridItem(device);
            yield return Click("AlchemyRecipe_"+recipeId);
            var recipe=s.Alchemy.Recipe;GridItem fuel=null,firstProduct=null;
            for(int batch=0;batch<batches;batch++)
            {
                var ingredients=recipe.targets.Where(t=>t.kind==AlchemyEventKind.Ingredient)
                    .Select(t=>s.In(ContainerId.Storage).First(i=>i.Definition.id==t.itemId)).ToArray();
                for(int n=0;n<ingredients.Length;n++)yield return Drag(ingredients[n],ContainerId.AlchemyPreparation,n*2,0);
                if(fuel==null)
                {
                    fuel=s.In(ContainerId.Storage).First(i=>i.Definition.id=="stone_mid");
                    yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);
                    yield return HoverItem(fuel,"品级：中品");
                }
                yield return ClickGridItem(ingredients[0]);yield return Click("AlchemyAdd");
                int stamina=s.Stamina;yield return Click("AlchemyStart");Assert.That(s.Stamina,Is.EqualTo(stamina-20));
                Assert.That(s.AdvanceTurn(),Is.False);Assert.That(s.CloseShopAlchemy(),Is.False);
                if(recipeId=="recipe_pill_basic" && batch==0)
                {
                    var running=s.Alchemy;double time=running.Time;
                    yield return Click("ShopAlchemyClose");Assert.That(shop.IsShopAlchemyWindowOpen,Is.False);
                    yield return new WaitForSeconds(.2f);Assert.That(running.Time,Is.GreaterThan(time));
                    yield return ClickGridItem(device);Assert.That(s.Alchemy,Is.SameAs(running));Assert.That(s.Stamina,Is.EqualTo(stamina-20));
                }
                foreach(var target in recipe.targets.Skip(1))
                {
                    GridItem item=null;
                    if(target.kind==AlchemyEventKind.Ingredient)
                    {
                        item=ingredients.First(i=>i.Definition.id==target.itemId);yield return ClickGridItem(item);
                        if(target.ground){yield return Click("AlchemyGrind");Assert.That(s.Alchemy.Locked);yield return new WaitUntil(()=>!s.Alchemy.Locked);}
                    }
                    yield return new WaitUntil(()=>s.Alchemy.Time>=target.breaths*s.Catalog.alchemy.breathSeconds);
                    if(item!=null)yield return Click("AlchemyAdd");
                    else if(target.kind==AlchemyEventKind.Heat)yield return Click("AlchemyHeat_"+target.heat);
                    else yield return Click("AlchemyCollect");
                }
                Assert.That(s.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Finished));Assert.That(s.Alchemy.Quality,Is.Not.EqualTo(PillQuality.Ruined));
                var product=s.In(ContainerId.AlchemyOutput).Single();if(firstProduct==null)firstProduct=product;
                var finished=s.Alchemy;var quality=product.QualityValueMultiplier;
                yield return Click("ShopAlchemyClose");yield return ClickGridItem(device);
                Assert.That(s.Alchemy,Is.SameAs(finished));Assert.That(s.In(ContainerId.AlchemyOutput).Single(),Is.SameAs(product));
                Assert.That(product.QualityValueMultiplier,Is.EqualTo(quality));Assert.That(shop.ItemView(device.Id).parent,Is.SameAs(warehouse));
                yield return HoverItem(product,"品相：");
                Assert.That(s.PrepareNextShopBatch(),Is.False);yield return DragIntoFreeSpace(product,ContainerId.Storage);
                Assert.That(s.Find(product.Id),Is.SameAs(product));Assert.That(s.Stamina,Is.EqualTo(stamina-20));
                if(batch+1<batches)yield return Click("AlchemyNextBatch");
            }
            int spirit=fuel.SpiritUnits;Assert.That(spirit,Is.LessThan(fuel.Definition.spiritResource.CapacityUnits));
            yield return DragIntoFreeSpace(fuel,ContainerId.Storage);yield return Click("ShopAlchemyClose");
            Assert.That(s.IsAtShopAlchemy,Is.False);Assert.That(s.Find(device.Id),Is.SameAs(device));
            yield return Click("AdvanceTurn");yield return CompleteDefaultBusiness();
            Assert.That(s.Find(firstProduct.Id),Is.SameAs(firstProduct));Assert.That(s.Find(fuel.Id),Is.SameAs(fuel));Assert.That(fuel.SpiritUnits,Is.EqualTo(spirit));
            Assert.That(shop.Session,Is.SameAs(s));Assert.That(s.ValidateState(),Is.Null);Assert.That(JsonUtility.ToJson(asset),Is.EqualTo(assetBefore));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
