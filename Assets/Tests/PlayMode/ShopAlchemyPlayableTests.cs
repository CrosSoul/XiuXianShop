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
            yield return ClickGridItem(device);Assert.That(s.ShopFurnaceId,Is.EqualTo(device.Id));
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
