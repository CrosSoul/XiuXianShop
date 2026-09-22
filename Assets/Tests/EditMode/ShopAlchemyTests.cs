using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP60")]
    public sealed class ShopAlchemyTests
    {
        ShopCatalog catalog;ShopSession s;GridItem device,fuel;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetStorageVerificationDefaults();
            AlchemyVerification.AddShopFurnaceDefinition(catalog);
            s=new ShopSession(catalog,seed:false,customerSeed:60);
            Assert.That(s.GrantShopFurnace());device=s.Items.Single();
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        void Kit(string recipe)
        {
            Assert.That(s.GrantAlchemyTestMaterials(recipe));
            var pack=s.PortableStorage.Last();
            foreach(var item in s.In(ContainerId.Interior,pack.Id).ToArray())Put(item,ContainerId.Storage);
        }
        void Put(GridItem item,ContainerId area,int box=0)
        {
            var size=s.GridSize(area,box);
            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
                if(s.CanMove(item.Id,area,x,y,0,false,out _,box)) {Assert.That(s.Move(item.Id,area,x,y,0,false,box));return;}
            Assert.Fail("No space for "+item.Definition.id+" to "+area);
        }
        void Prepare(string recipe)
        {
            Assert.That(s.OpenShopAlchemy(device.Id));Assert.That(s.SelectAlchemyRecipe(recipe));
            foreach(var t in s.Alchemy.Recipe.targets.Where(t=>t.kind==AlchemyEventKind.Ingredient))
                Put(s.In(ContainerId.Storage).First(i=>i.Definition.id==t.itemId),ContainerId.AlchemyPreparation);
            if(!s.In(ContainerId.AlchemyFuel).Any())Put(s.In(ContainerId.Storage).First(i=>i.Definition.id=="stone_mid"),ContainerId.AlchemyFuel);
            fuel=s.In(ContainerId.AlchemyFuel).Single();
        }
        void Add(string id)=>Assert.That(s.AddAlchemyIngredient(s.In(ContainerId.AlchemyPreparation).Single(i=>i.Definition.id==id).Id),Is.True,s.Message);
        void Finish(string recipe)
        {
            Add(recipe=="recipe_pill_basic"?"herb":recipe=="recipe_pill_fire_yang"?"mat_fire_herb":"mat_metal_herb");
            Assert.That(s.StartAlchemy());
            if(recipe=="recipe_pill_basic") {s.TickAlchemy(8);Add("dew");s.TickAlchemy(8);}
            else if(recipe=="recipe_pill_fire_yang")
            {Assert.That(s.GrindAlchemyIngredient(s.In(ContainerId.AlchemyPreparation).Single().Id));s.TickAlchemy(8);Add("mat_fire_fruit");s.TickAlchemy(8);}
            else
            {
                Assert.That(s.GrindAlchemyIngredient(s.In(ContainerId.AlchemyPreparation).Single(i=>i.Definition.id=="mat_water_fruit").Id));s.TickAlchemy(8);Add("mat_water_fruit");Assert.That(s.SetAlchemyHeat(AlchemyHeat.High));
                Assert.That(s.GrindAlchemyIngredient(s.In(ContainerId.AlchemyPreparation).Single().Id));s.TickAlchemy(8);Add("mat_metal_fruit");s.TickAlchemy(8);Assert.That(s.SetAlchemyHeat(AlchemyHeat.Low));s.TickAlchemy(8);
            }
            Assert.That(s.CollectAlchemy());
        }

        [TestCase("recipe_pill_basic",48)]
        [TestCase("recipe_pill_fire_yang",48)]
        [TestCase("recipe_pill_metal_water",104)]
        public void ThreeRecipesUseSameResidenceQualityAndEnergyWithOneShopCharge(string recipe,int energy)
        {
            Kit(recipe);TravelTestSetup.CloseBusiness(s);Prepare(recipe);
            s.TickAlchemy(9);Assert.That(s.Alchemy.Time,Is.Zero);
            int before=fuel.SpiritUnits;Finish(recipe);
            Assert.That(s.Alchemy.Quality,Is.EqualTo(PillQuality.Superior));Assert.That(s.Stamina,Is.EqualTo(80));
            Assert.That(before-fuel.SpiritUnits,Is.EqualTo(energy));
            Assert.That(s.Alchemy.Judgements.Where(j=>j.EntryTime.HasValue).Select(j=>j.ActualTime),Is.EqualTo(recipe=="recipe_pill_metal_water"?new double[]{32,24,16}:new double[]{16,8}));
            var product=s.In(ContainerId.AlchemyOutput).Single();Assert.That(product.QualityValueMultiplier,Is.EqualTo(1.5m));
            Assert.That(s.CloseShopAlchemy(),Is.False);Put(product,ContainerId.Storage);Put(fuel,ContainerId.Storage);
            Assert.That(s.CloseShopAlchemy());Assert.That(s.Find(device.Id),Is.SameAs(device));Assert.That(s.Find(product.Id),Is.SameAs(product));
            Assert.That(s.AdvanceTurn());Assert.That(s.BeginBusiness());Assert.That(s.ValidateState(),Is.Null);
        }

        [TestCase(20,true)] [TestCase(19,false)]
        public void ExactStaminaStartsOnceAndFailureChangesNoResource(int stamina,bool allowed)
        {
            Kit("recipe_pill_basic");TravelTestSetup.CloseBusiness(s);Assert.That(s.TrySpendStamina(100-stamina));Prepare("recipe_pill_basic");Add("herb");
            var items=s.Items.ToArray();int spirit=fuel.SpiritUnits;
            Assert.That(s.StartAlchemy(),Is.EqualTo(allowed));Assert.That(s.Stamina,Is.EqualTo(allowed?0:19));
            Assert.That(s.Items,Is.EqualTo(items));Assert.That(fuel.SpiritUnits,Is.EqualTo(spirit));
            Assert.That(s.StartAlchemy(),Is.False);Assert.That(s.Stamina,Is.EqualTo(allowed?0:19));
            Assert.That(s.Alchemy.Time,Is.Zero);
        }

        [Test] public void StageAndDeviceGuardsKeepIdentityAndDisallowCrossMonthWork()
        {
            Kit("recipe_pill_basic");Prepare("recipe_pill_basic");
            var before=s.Items.ToArray();Assert.That(s.StartAlchemy(),Is.False);Assert.That(s.AddAlchemyIngredient(s.In(ContainerId.AlchemyPreparation).First().Id),Is.False);
            Assert.That(s.Items,Is.EqualTo(before));Assert.That(s.Stamina,Is.EqualTo(100));
            Assert.That(s.Move(device.Id,ContainerId.Storage,7,3,0,false),Is.False);
            Assert.That(s.BeginBusiness(),Is.False);Assert.That(s.BeginCarrying(0),Is.False);Assert.That(s.AdvanceTurn(),Is.False);
            foreach(var i in s.Items.Where(i=>s.IsAlchemyArea(i.Container)).ToArray())Put(i,ContainerId.Storage);
            Assert.That(s.CloseShopAlchemy());Assert.That(s.BeginBusiness());
            Assert.That(s.OpenShopAlchemy(device.Id));Assert.That(s.SelectAlchemyRecipe("recipe_pill_basic"));Assert.That(s.StartAlchemy(),Is.False);
            Assert.That(s.CloseShopAlchemy());Assert.That(s.EndBusiness());Prepare("recipe_pill_basic");Add("herb");Assert.That(s.StartAlchemy());
            Assert.That(s.CloseShopAlchemy(),Is.False);Assert.That(s.AdvanceTurn(),Is.False);Assert.Throws<InvalidOperationException>(()=>s.CaptureSave());
            Assert.That(s.AbortAlchemy());foreach(var i in s.Items.Where(i=>s.IsAlchemyArea(i.Container)).ToArray())Put(i,ContainerId.Storage);
            Assert.That(s.CloseShopAlchemy());Assert.That(s.AdvanceTurn());
            Assert.That(s.OpenShopAlchemy(device.Id));Assert.That(s.SelectAlchemyRecipe("recipe_pill_basic"));Assert.That(s.StartAlchemy(),Is.False);
            Assert.That(s.Find(device.Id),Is.SameAs(device));Assert.That(s.ValidateState(),Is.Null);
        }

        [Test] public void TwoManualBatchesChargeSeparatelyAndOutputMustBeRemoved()
        {
            Kit("recipe_pill_basic");Kit("recipe_pill_basic");TravelTestSetup.CloseBusiness(s);Prepare("recipe_pill_basic");Finish("recipe_pill_basic");
            Assert.That(s.PrepareNextShopBatch(),Is.False);var first=s.In(ContainerId.AlchemyOutput).Single();Put(first,ContainerId.Storage);
            Assert.That(s.PrepareNextShopBatch());Prepare("recipe_pill_basic");Finish("recipe_pill_basic");
            Assert.That(s.Stamina,Is.EqualTo(60));Assert.That(s.Crafted,Is.EqualTo(2));Assert.That(s.Find(first.Id),Is.SameAs(first));
            Put(s.In(ContainerId.AlchemyOutput).Single(),ContainerId.Storage);Assert.That(s.PrepareNextShopBatch());
            Assert.That(s.ConsumeSpirit(fuel.Id,fuel.SpiritUnits));int count=s.Items.Count;
            Assert.That(s.StartAlchemy(),Is.False);Assert.That(s.Stamina,Is.EqualTo(60));Assert.That(s.Items.Count,Is.EqualTo(count));
            Assert.That(s.ValidateState(),Is.Null);
        }

        [Test] public void ProductionEquipmentRejectsOrdinaryAndPortableStorageButKeepsEquipmentCompatibility()
        {
            // Existing fixture supplies all three storage kinds; no new equipment-box mechanics.
            s=new ShopSession(catalog,customerSeed:60);Assert.That(s.GrantShopFurnace());device=s.Items.Single(i=>i.Definition.id==ShopSession.ShopFurnaceDefinitionId);
            foreach(var box in s.Items.Where(i=>i.Definition.IsStorage))
                Assert.That(s.Move(device.Id,ContainerId.Interior,0,0,0,false,box.Id),Is.False);
            var equipmentBox=s.Items.Single(i=>i.Definition.category==ItemCategory.EquipmentContainer);
            equipmentBox.Definition.compatibleEquipmentIds=new[]{ShopSession.ShopFurnaceDefinitionId};
            Assert.That(s.Move(device.Id,ContainerId.Interior,0,0,0,false,equipmentBox.Id));Assert.That(s.Find(device.Id),Is.SameAs(device));
            Assert.That(s.OpenShopAlchemy(device.Id),Is.False,"v1 interaction is root-level only");Assert.That(s.ValidateState(),Is.Null);
        }
    }
}
