#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP58")] public IEnumerator BasicAlchemyWithActualGridAndButtons()=>PlayAlchemy("recipe_pill_basic");
        [UnityTest,Category("DP58")] public IEnumerator GroundAlchemyWithActualGridAndButtons()=>PlayAlchemy("recipe_pill_fire_yang");
        [UnityTest,Category("DP58")] public IEnumerator ComplexAlchemyWithActualGridAndButtons()=>PlayAlchemy("recipe_pill_metal_water");

        IEnumerator PlayAlchemy(string recipeId)
        {
            yield return RestartWithTestCatalog(c=>{AlchemyVerification.Configure(c);c.alchemy.debugTimeScale=2;},58);
            var s=shop.Session;var recipe=s.Catalog.alchemy.recipes.Single(r=>r.id==recipeId);
            var pack=s.PortableStorage.Single();Assert.That(s.BeginCarrying(pack.Id));
            var ingredients=recipe.targets.Where(t=>t.kind==AlchemyEventKind.Ingredient).Select(t=>s.Items.Single(i=>i.Definition.id==t.itemId)).ToArray();
            for(int i=0;i<ingredients.Length;i++)Assert.That(s.Move(ingredients[i].Id,ContainerId.Interior,i*2,0,0,false,pack.Id));
            var fuel=s.Items.Single(i=>i.Definition.id=="stone_mid");Assert.That(s.Move(fuel.Id,ContainerId.RightHand,0,0,0,false));
            shop.OpenCarrySelection();yield return null;yield return Click("TravelBegin");yield return Click("TravelLocation_"+ShopSession.AlchemyLocationId);
            yield return Click("AlchemyRecipe_"+recipeId);
            for(int i=0;i<ingredients.Length;i++)yield return Drag(ingredients[i],ContainerId.Location,i*2,0);
            yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);Assert.That(fuel.Container,Is.EqualTo(ContainerId.AlchemyFuel));
            shop.SelectItem(ingredients[0].Id);yield return Click("AlchemyAdd");yield return Click("AlchemyStart");Assert.That(s.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Running));
            foreach(var target in recipe.targets.Skip(1))
            {
                GridItem item=null;
                if(target.kind==AlchemyEventKind.Ingredient)
                {
                    item=s.Items.Single(i=>i.Definition.id==target.itemId);shop.SelectItem(item.Id);
                    if(target.ground){yield return Click("AlchemyGrind");Assert.That(s.Alchemy.Locked);Assert.That(s.CollectAlchemy(),Is.False);yield return new WaitUntil(()=>!s.Alchemy.Locked);}
                }
                yield return new WaitUntil(()=>s.Alchemy.Time>=target.breaths*s.Catalog.alchemy.breathSeconds);
                if(item!=null)yield return Click("AlchemyAdd");
                else if(target.kind==AlchemyEventKind.Heat)yield return Click("AlchemyHeat_"+target.heat);
                else yield return Click("AlchemyCollect");
            }
            Assert.That(s.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Finished));Assert.That(s.Alchemy.Quality,Is.Not.EqualTo(PillQuality.Ruined));
            Assert.That(s.Stamina,Is.EqualTo(40));var product=s.In(ContainerId.AlchemyOutput).Single();
            yield return Drag(product,ContainerId.LeftHand,0,0);Assert.That(product.Container,Is.EqualTo(ContainerId.LeftHand));
            yield return Click("TravelLeave");yield return Click("LocationLeaveCancel");Assert.That(s.CurrentLocationId,Is.EqualTo(ShopSession.AlchemyLocationId));
            yield return Click("TravelLeave");yield return Click("LocationLeaveConfirm");Assert.That(s.CurrentLocationId,Is.Null);Assert.That(s.Find(product.Id),Is.SameAs(product));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
