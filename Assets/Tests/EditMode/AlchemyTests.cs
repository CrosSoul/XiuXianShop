using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    [Category("DP58")]
    public sealed class AlchemyTests
    {
        ShopCatalog catalog;ShopSession s;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();AlchemyVerification.Configure(catalog);
            s=new ShopSession(catalog,customerSeed:58);
            var pack=s.Items.Single(i=>i.Definition.id=="test-portable");Assert.That(s.BeginCarrying(pack.Id));
            foreach(var item in s.Items.Where(i=>i.Definition.category==ItemCategory.Material).ToArray())Put(item,ContainerId.Interior,pack.Id);
            var fuel=s.Items.Single(i=>i.Definition.id=="stone_mid");Assert.That(s.Move(fuel.Id,ContainerId.RightHand,0,0,0,false));
            Assert.That(s.BeginTravel());Assert.That(s.EnterLocation(ShopSession.AlchemyLocationId));
            Assert.That(s.Move(fuel.Id,ContainerId.AlchemyFuel,0,0,0,false));
            foreach(var item in s.In(ContainerId.Interior,pack.Id).ToArray())Put(item,ContainerId.Location);
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        void Put(GridItem item,ContainerId area,int box=0)
        {
            var size=s.GridSize(area,box);
            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
                if(s.CanMove(item.Id,area,x,y,0,false,out _,box)){Assert.That(s.Move(item.Id,area,x,y,0,false,box));return;}
            Assert.Fail("No space: "+item.Definition.id);
        }
        int Id(string id)=>s.Items.Single(i=>i.Definition.id==id).Id;
        void Add(string id)=>Assert.That(s.AddAlchemyIngredient(Id(id)),Is.True,s.Message);
        void Select(string id)=>Assert.That(s.SelectAlchemyRecipe(id),Is.True,s.Message);

        [TestCase("recipe_pill_basic",48)]
        [TestCase("recipe_pill_fire_yang",48)]
        [TestCase("recipe_pill_metal_water",104)]
        public void ThreeRecipesFinishWithOriginalInstancesAndNoSecondStaminaCost(string recipe,int energy)
        {
            Select(recipe);int stamina=s.Stamina;int fuel=Id("stone_mid");
            s.TickAlchemy(50);Assert.That(s.Alchemy.Time,Is.Zero);
            string first=recipe=="recipe_pill_basic"?"herb":recipe=="recipe_pill_fire_yang"?"mat_fire_herb":"mat_metal_herb";
            Add(first);Assert.That(s.StartAlchemy());
            if(recipe=="recipe_pill_basic"){s.TickAlchemy(8);Add("dew");s.TickAlchemy(8);}
            else if(recipe=="recipe_pill_fire_yang")
            {
                Assert.That(s.GrindAlchemyIngredient(Id("mat_fire_fruit")));Assert.That(s.SetAlchemyHeat(AlchemyHeat.Low),Is.False);
                Assert.That(s.CollectAlchemy(),Is.False);s.TickAlchemy(8);Add("mat_fire_fruit");s.TickAlchemy(8);
            }
            else
            {
                Assert.That(s.GrindAlchemyIngredient(Id("mat_water_fruit")));s.TickAlchemy(8);Add("mat_water_fruit");
                Assert.That(s.SetAlchemyHeat(AlchemyHeat.High));Assert.That(s.GrindAlchemyIngredient(Id("mat_metal_fruit")));
                s.TickAlchemy(8);Add("mat_metal_fruit");s.TickAlchemy(8);Assert.That(s.SetAlchemyHeat(AlchemyHeat.Low));s.TickAlchemy(8);
            }
            Assert.That(s.CollectAlchemy());Assert.That(s.Alchemy.Quality,Is.EqualTo(PillQuality.Superior));
            var product=s.In(ContainerId.AlchemyOutput).Single();Assert.That(product.Definition,Is.SameAs(catalog.Find(s.Alchemy.Recipe.productId)));
            Assert.That(product.BaseValue,Is.EqualTo(product.Definition.baseValue*1.5m));Assert.That(s.Stamina,Is.EqualTo(stamina));
            Assert.That(s.Find(fuel).SpiritUnits,Is.EqualTo(10000-energy));Assert.That(s.StartAlchemy(),Is.False);
            Assert.That(s.LeaveLocation(),Is.False);Assert.That(s.ValidateState(),Is.Null);
            Assert.That(s.Move(product.Id,ContainerId.LeftHand,0,0,0,false));Assert.That(s.LeaveLocation(true));
            Assert.That(s.Find(product.Id),Is.SameAs(product));Assert.That(s.Items.Any(i=>s.IsAlchemyArea(i.Container)),Is.False);
        }

        [TestCase(7.5,1f)] [TestCase(8.5,1f)] [TestCase(6,.6f)] [TestCase(10,.6f)] [TestCase(5.9,0f)] [TestCase(10.1,0f)]
        public void TimingWindowBoundariesAreInclusive(double when,float score)
        {
            Select("recipe_pill_basic");Add("herb");Assert.That(s.StartAlchemy());s.TickAlchemy(when);Add("dew");
            Assert.That(s.Alchemy.Judgements.Last().Score,Is.EqualTo(score));Assert.That(s.Alchemy.StructuralErrors,Is.Empty);
        }

        [Test] public void StructureErrorsCannotBeCancelledByPerfectTiming()
        {
            Select("recipe_pill_fire_yang");Add("mat_fire_herb");s.StartAlchemy();s.TickAlchemy(8);Add("mat_fire_fruit");s.TickAlchemy(8);s.CollectAlchemy();
            Assert.That(s.Alchemy.StructuralErrors.Count,Is.EqualTo(1));Assert.That(s.Alchemy.Quality,Is.EqualTo(PillQuality.Ordinary));
            Assert.That(s.In(ContainerId.AlchemyOutput).Single().BaseValue,Is.EqualTo(48m));
        }

        [Test] public void HighHeatDelayConsumesMoreAndAbortKeepsUnspentMaterials()
        {
            Select("recipe_pill_metal_water");var unspent=s.Find(Id("mat_water_fruit"));Add("mat_metal_herb");s.StartAlchemy();
            s.SetAlchemyHeat(AlchemyHeat.High);s.TickAlchemy(10);
            Assert.That(s.Find(Id("stone_mid")).SpiritUnits,Is.EqualTo(9960));Assert.That(s.AbortAlchemy());
            Assert.That(s.Find(unspent.Id),Is.SameAs(unspent));Assert.That(s.StartAlchemy(),Is.False);Assert.That(s.SelectAlchemyRecipe("recipe_pill_basic"),Is.False);
        }

        [Test] public void SupplyExhaustionStopsAtAvailableEnergyAndPreservesReusableShell()
        {
            Select("recipe_pill_basic");int fuel=Id("stone_mid");Assert.That(s.ConsumeSpirit(fuel,9952));Add("herb");Assert.That(s.StartAlchemy());
            s.TickAlchemy(30);Assert.That(s.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Aborted));Assert.That(s.Alchemy.Time,Is.EqualTo(16).Within(.00001));
            Assert.That(s.Find(fuel).SpiritUnits,Is.Zero);Assert.That(s.Find(Id("dew")),Is.Not.Null);
        }

        [TestCase(8,16,PillQuality.Superior,1.5)]
        [TestCase(9,16,PillQuality.Good,1.2)]
        [TestCase(11,17,PillQuality.Ordinary,1)]
        [TestCase(11,20,PillQuality.Ruined,0)]
        public void QualityIsInstanceValueBeforeMarketTags(double ingredientTime,double collectTime,PillQuality quality,double multiplier)
        {
            Select("recipe_pill_basic");Add("herb");s.StartAlchemy();s.TickAlchemy(ingredientTime);Add("dew");s.TickAlchemy(collectTime-ingredientTime);s.CollectAlchemy();
            Assert.That(s.Alchemy.Quality,Is.EqualTo(quality));
            if(quality==PillQuality.Ruined){Assert.That(s.In(ContainerId.AlchemyOutput),Is.Empty);return;}
            var pill=s.In(ContainerId.AlchemyOutput).Single();Assert.That(pill.Definition,Is.SameAs(catalog.Find("pill")));
            Assert.That(pill.BaseValue,Is.EqualTo(18*(decimal)multiplier));
            s.SetPriceTag(new PriceTag{id="quality-test",title="行情测试",percent=.2f});
            Assert.That(s.Quote(pill).RawValue,Is.EqualTo(pill.BaseValue*(1+(decimal)catalog.retailMarkup+.2m)));
        }

        [Test] public void TwoStructureErrorsRuin()
        {
            Select("recipe_pill_fire_yang");Add("herb");s.StartAlchemy();s.TickAlchemy(8);Add("mat_fire_fruit");s.TickAlchemy(8);s.CollectAlchemy();
            Assert.That(s.Alchemy.StructuralErrors.Count,Is.EqualTo(2));Assert.That(s.Alchemy.Quality,Is.EqualTo(PillQuality.Ruined));
        }

        [Test] public void FirstIngredientMustPrecedeIgnitionAndMissingIngredientIsReported()
        {
            Select("recipe_pill_basic");Assert.That(s.StartAlchemy());Add("herb");s.TickAlchemy(16);s.CollectAlchemy();
            Assert.That(s.Alchemy.StructuralErrors.Any(e=>e.Contains("阶段")));
            Assert.That(s.Alchemy.StructuralErrors.Any(e=>e.Contains("漏投")));Assert.That(s.Alchemy.Quality,Is.EqualTo(PillQuality.Ruined));
        }

        [Test] public void TooLittleEnergyAndBlockedOutputDoNotStartOrConsume()
        {
            Select("recipe_pill_basic");int fuel=Id("stone_mid");s.ConsumeSpirit(fuel,9960);Add("herb");
            Assert.That(s.StartAlchemy(),Is.False);Assert.That(s.Find(fuel).SpiritUnits,Is.EqualTo(40));Assert.That(s.Alchemy.Time,Is.Zero);
            s.RefillSpirit(fuel,100);catalog.alchemy.outputSize=new Vector2Int(1,1);
            Assert.That(s.StartAlchemy(),Is.False);Assert.That(s.Find(fuel).SpiritUnits,Is.EqualTo(140));
        }

        [Test] public void GrindingLocksRecipeAndMovesAndUnpreparedMaterialCannotBeAdded()
        {
            Select("recipe_pill_fire_yang");var fruit=s.Find(Id("mat_fire_fruit"));
            Assert.That(s.Move(fruit.Id,ContainerId.LeftHand,0,0,0,false));Assert.That(s.AddAlchemyIngredient(fruit.Id),Is.False);Put(fruit,ContainerId.Location);
            Assert.That(s.GrindAlchemyIngredient(fruit.Id));Assert.That(s.SelectAlchemyRecipe("recipe_pill_basic"),Is.False);
            Assert.That(s.Move(fruit.Id,ContainerId.LeftHand,0,0,0,false),Is.False);Assert.That(s.AbortAlchemy(),Is.False);
            s.TickAlchemy(8);Assert.That(s.Alchemy.Time,Is.Zero);Assert.That(s.IsAlchemyGround(fruit.Id));Assert.That(s.AbortAlchemy());
        }
    }
}
