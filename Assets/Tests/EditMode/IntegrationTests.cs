using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP59")]
    public sealed class IntegrationTests
    {
        ShopCatalog catalog;
        ShopSession session;
        [SetUp] public void Setup()
        {
            catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Data/ShopCatalog.asset"));
            session=new ShopSession(catalog,customerSeed:59);
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(catalog);

        [TestCase("recipe_pill_basic")]
        [TestCase("recipe_pill_fire_yang")]
        [TestCase("recipe_pill_metal_water")]
        public void DevelopmentKitExtendsCurrentInventoryWithoutReconfiguringExistingData(string recipeId)
        {
            var original=session.Items.ToArray();
            string settings=JsonUtility.ToJson(catalog.customers),market=JsonUtility.ToJson(catalog.teaHouse);
            var definitions=catalog.items.ToArray();var starting=catalog.startingItems;var events=catalog.marketEvents;
            AlchemyVerification.AddDefinitions(catalog);AlchemyVerification.AddDefinitions(catalog);
            Assert.That(catalog.items.Select(d=>d.id).Distinct().Count(),Is.EqualTo(catalog.items.Length));
            foreach(var definition in definitions)Assert.That(catalog.Find(definition.id),Is.SameAs(definition));
            Assert.That(catalog.startingItems,Is.SameAs(starting));Assert.That(catalog.marketEvents,Is.SameAs(events));
            Assert.That(JsonUtility.ToJson(catalog.customers),Is.EqualTo(settings));Assert.That(JsonUtility.ToJson(catalog.teaHouse),Is.EqualTo(market));
            Assert.That(session.UnlockedLocations.Any(l=>l.id==ShopSession.AlchemyLocationId),Is.False);
            Assert.That(session.UnlockLocation(ShopSession.AlchemyLocationId));
            Assert.That(session.GrantAlchemyTestMaterials(recipeId));
            var pack=session.PortableStorage.Single(i=>i.Definition.id==AlchemyVerification.MaterialPackId);
            var expected=catalog.alchemy.recipes.Single(r=>r.id==recipeId).targets.Where(t=>t.kind==AlchemyEventKind.Ingredient).Select(t=>t.itemId).Concat(new[]{"stone_mid"});
            Assert.That(session.In(ContainerId.Interior,pack.Id).Select(i=>i.Definition.id),Is.EquivalentTo(expected));
            foreach(var item in original)Assert.That(session.Find(item.Id),Is.SameAs(item));
            Assert.That(session.Turn,Is.EqualTo(1));Assert.That(session.Stamina,Is.EqualTo(100));Assert.That(session.HasTravelledThisTurn,Is.False);
            Assert.That(session.ValidateState(),Is.Null);
        }

        [Test] public void DevelopmentKitRejectsActiveBusinessCarryingAndFullWarehouseWithoutPartialInventory()
        {
            AlchemyVerification.AddDefinitions(catalog);
            int initial=session.Items.Count;
            Assert.That(session.BeginBusiness());
            Assert.That(session.GrantAlchemyTestMaterials("recipe_pill_basic"),Is.False);
            Assert.That(session.Items.Count,Is.EqualTo(initial+session.In(ContainerId.CustomerCounter).Count()));
            Assert.That(session.EndBusiness());Assert.That(session.BeginCarrying(0));
            int carried=session.Items.Count;
            Assert.That(session.GrantAlchemyTestMaterials("recipe_pill_basic"),Is.False);Assert.That(session.Items.Count,Is.EqualTo(carried));
            Assert.That(session.EndCarrying());
            for(int attempt=0;attempt<30;attempt++)
            {
                var before=session.Items.ToArray();
                if(session.GrantAlchemyTestMaterials("recipe_pill_metal_water"))continue;
                Assert.That(session.Items,Is.EqualTo(before));Assert.That(session.Message,Does.Contain("2×3"));
                Assert.That(session.ValidateState(),Is.Null);return;
            }
            Assert.Fail("Finite warehouse should reject further kits");
        }
    }
}
