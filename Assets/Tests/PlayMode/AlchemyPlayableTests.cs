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
        readonly System.Collections.Generic.List<UnityEngine.InputSystem.Mouse> alchemyOtherMice=new System.Collections.Generic.List<UnityEngine.InputSystem.Mouse>();
        void IsolateAlchemyTestMouse()
        {
            // Keep desktop pointer events from interrupting a simulated continuous hover.
            foreach(var device in UnityEngine.InputSystem.InputSystem.devices)
                if(device is UnityEngine.InputSystem.Mouse otherMouse && otherMouse!=mouse && otherMouse.enabled)
                {
                    alchemyOtherMice.Add(otherMouse);
                    UnityEngine.InputSystem.InputSystem.DisableDevice(otherMouse);
                }
            mouse.MakeCurrent();
        }
        [TearDown] public void RestoreAlchemyTestMouse()
        {
            foreach(var device in alchemyOtherMice)UnityEngine.InputSystem.InputSystem.EnableDevice(device);
            alchemyOtherMice.Clear();
        }
        [UnityTest,Category("DP58"),Category("DP27")] public IEnumerator BasicAlchemyWithActualGridAndButtons()=>PlayAlchemy("recipe_pill_basic");
        [UnityTest,Category("DP58"),Category("DP27")] public IEnumerator GroundAlchemyWithActualGridAndButtons()=>PlayAlchemy("recipe_pill_fire_yang");
        [UnityTest,Category("DP58"),Category("DP27")] public IEnumerator ComplexAlchemyWithActualGridAndButtons()=>PlayAlchemy("recipe_pill_metal_water");

        UnityEngine.UI.Text TooltipText()=>shop.GetComponentsInChildren<UnityEngine.UI.Text>().FirstOrDefault(t=>t.name=="ItemTooltipText");
        [UnityTest,Category("DP58"),Category("DP27")] public IEnumerator AlchemyTooltipAcceptsRuntimeDelayOptions()
        {
            yield return RestartWithTestCatalog(AlchemyVerification.Configure,58);
            IsolateAlchemyTestMouse();
            Assert.That(shop.TooltipHoverDelaySeconds,Is.EqualTo(1f));
            var item=shop.Session.Items.First(i=>i.Definition.id=="herb");
            foreach(float delay in new[]{0f,.5f,1f,1.5f})
            {
                yield return MouseAt(Vector2.zero,false);
                shop.TooltipHoverDelaySeconds=delay;
                yield return MouseAt(ItemPoint(item),false);
                if(delay>0)
                {
                    yield return new WaitForSecondsRealtime(delay/2);
                    Assert.That(TooltipText(),Is.Null,$"Delay {delay}: must not show early");
                    yield return new WaitForSecondsRealtime(delay/2+.1f);
                }
                Assert.That(TooltipText(),Is.Not.Null,$"Delay {delay}: must show after configured delay");
            }
            yield return MouseAt(Vector2.zero,false);Assert.That(TooltipText(),Is.Null);
        }
        IEnumerator HoverItem(GridItem item,string expected)
        {
            yield return MouseAt(ItemPoint(item),false);
            Assert.That(TooltipText(),Is.Null,"Switching items must restart hover delay");
            yield return new WaitForSecondsRealtime(.4f);Assert.That(TooltipText(),Is.Null);
            yield return new WaitForSecondsRealtime(.7f);
            var tip=TooltipText();
            var hovered=typeof(ShopPrototype).GetField("hoveredItem",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(shop) as GridItem;
            var started=(float)typeof(ShopPrototype).GetField("hoverStarted",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(shop);
            Assert.That(tip,Is.Not.Null,$"Tooltip for {item.Definition.id}: hovered={hovered?.Id}, elapsed={Time.unscaledTime-started}, pointer={UnityEngine.InputSystem.Mouse.current.position.ReadValue()}, expected={ItemPoint(item)}");
            Assert.That(tip.text,Does.Contain(item.Definition.title).And.Contain("基础价值").And.Contain("占格").And.Contain(expected));
            Assert.That(tip.text,Does.Not.Contain(item.Definition.id).And.Not.Contain("评分"));
            var corners=new Vector3[4];((RectTransform)tip.transform.parent).GetWorldCorners(corners);
            Assert.That(corners.All(p=>p.x>=0 && p.y>=0 && p.x<=Screen.width && p.y<=Screen.height),"Tooltip stays on screen");
        }

        [UnityTest,Category("DP58"),Category("DP27")] public IEnumerator AlchemySharedHoverCoversCarryPreparationFuelAndGroundState()
        {
            yield return RestartWithTestCatalog(AlchemyVerification.Configure,58);
            IsolateAlchemyTestMouse();
            var s=shop.Session;var fruit=s.Items.Single(i=>i.Definition.id=="mat_fire_fruit");
            var fuel=s.Items.Single(i=>i.Definition.id=="stone_mid");
            yield return CloseBusinessForOuting();Assert.That(s.BeginCarrying(0));
            Assert.That(s.Move(fruit.Id,ContainerId.LeftHand,0,0,0,false));
            Assert.That(s.Move(fuel.Id,ContainerId.RightHand,0,0,0,false));
            shop.OpenCarrySelection();yield return null;
            yield return HoverItem(fruit,"完整");yield return HoverItem(fuel,"可重复充能");
            yield return MouseAt(Vector2.zero,false);Assert.That(TooltipText(),Is.Null,"Leaving hides immediately");
            yield return Click("TravelBegin");yield return Click("TravelLocation_"+ShopSession.AlchemyLocationId);
            yield return Click("AlchemyRecipe_recipe_pill_fire_yang");
            yield return Drag(fruit,ContainerId.Location,0,0);yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);
            yield return MouseAt(Vector2.zero,false);yield return HoverItem(fruit,"完整");yield return HoverItem(fuel,"品级：中品");
            shop.SelectItem(fruit.Id);yield return Click("AlchemyGrind");
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="AlchemyGrinding").text,Does.Contain("研磨中"));
            yield return new WaitUntil(()=>!s.Alchemy.Locked);
            Assert.That(shop.ItemView(fruit.Id).GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text=="已研磨"));
            yield return HoverItem(fruit,"已研磨");
            LogAssert.NoUnexpectedReceived();
        }

        IEnumerator PlayAlchemy(string recipeId)
        {
            yield return RestartWithTestCatalog(c=>{AlchemyVerification.Configure(c);c.alchemy.debugTimeScale=2;},58);
            IsolateAlchemyTestMouse();
            var s=shop.Session;var recipe=s.Catalog.alchemy.recipes.Single(r=>r.id==recipeId);
            var pack=s.PortableStorage.Single();yield return CloseBusinessForOuting();Assert.That(s.BeginCarrying(pack.Id));
            var ingredients=recipe.targets.Where(t=>t.kind==AlchemyEventKind.Ingredient).Select(t=>s.Items.Single(i=>i.Definition.id==t.itemId)).ToArray();
            for(int i=0;i<ingredients.Length;i++)Assert.That(s.Move(ingredients[i].Id,ContainerId.Interior,i*2,0,0,false,pack.Id));
            var fuel=s.Items.Single(i=>i.Definition.id=="stone_mid");Assert.That(s.Move(fuel.Id,ContainerId.RightHand,0,0,0,false));
            shop.OpenCarrySelection();yield return null;yield return Click("TravelBegin");yield return Click("TravelLocation_"+ShopSession.AlchemyLocationId);
            yield return Click("AlchemyRecipe_"+recipeId);
            for(int i=0;i<ingredients.Length;i++)yield return Drag(ingredients[i],ContainerId.Location,i*2,0);
            yield return Drag(fuel,ContainerId.AlchemyFuel,0,0);Assert.That(fuel.Container,Is.EqualTo(ContainerId.AlchemyFuel));
            shop.SelectItem(ingredients[0].Id);yield return Click("AlchemyAdd");yield return Click("AlchemyStart");Assert.That(s.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Running));
            if(recipeId=="recipe_pill_basic")yield return HoverItem(fuel,"可重复充能");
            foreach(var target in recipe.targets.Skip(1))
            {
                GridItem item=null;
                if(target.kind==AlchemyEventKind.Ingredient)
                {
                    item=s.Items.Single(i=>i.Definition.id==target.itemId);shop.SelectItem(item.Id);
                    if(target.ground)
                    {
                        yield return Click("AlchemyGrind");
                        Assert.That(s.Alchemy.Locked,$"{item.Definition.id}: {s.Message}; selected={shop.SelectedId}; phase={s.Alchemy.Phase}; time={s.Alchemy.Time}; pointer={UnityEngine.InputSystem.Mouse.current.position.ReadValue()}");
                        Assert.That(s.CollectAlchemy(),Is.False);yield return new WaitUntil(()=>!s.Alchemy.Locked);
                    }
                }
                yield return new WaitUntil(()=>s.Alchemy.Time>=target.breaths*s.Catalog.alchemy.breathSeconds);
                if(item!=null)yield return Click("AlchemyAdd");
                else if(target.kind==AlchemyEventKind.Heat)yield return Click("AlchemyHeat_"+target.heat);
                else yield return Click("AlchemyCollect");
            }
            Assert.That(s.Alchemy.Phase,Is.EqualTo(AlchemyPhase.Finished));Assert.That(s.Alchemy.Quality,Is.Not.EqualTo(PillQuality.Ruined));
            Assert.That(s.Stamina,Is.EqualTo(40));var product=s.In(ContainerId.AlchemyOutput).Single();
            if(recipeId=="recipe_pill_basic")
            {
                yield return HoverItem(product,"品相："+AlchemySettings.QualityName(product.Quality));
                Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="AlchemyStatus").text,Does.Contain("已结束").And.Contain("品相"));
            }
            yield return Drag(product,ContainerId.LeftHand,0,0);Assert.That(product.Container,Is.EqualTo(ContainerId.LeftHand));
            yield return Click("TravelLeave");yield return Click("LocationLeaveCancel");Assert.That(s.CurrentLocationId,Is.EqualTo(ShopSession.AlchemyLocationId));
            yield return Click("TravelLeave");yield return Click("LocationLeaveConfirm");Assert.That(s.CurrentLocationId,Is.Null);Assert.That(s.Find(product.Id),Is.SameAs(product));
            Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
