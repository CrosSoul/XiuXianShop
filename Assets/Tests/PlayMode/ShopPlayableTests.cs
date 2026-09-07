#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed class ShopPlayableTests
    {
        ShopPrototype shop;
        Mouse mouse;
        Keyboard keyboard;
        ShopCatalog testCatalog;
        InputSettings.BackgroundBehavior originalBackgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
        bool inputSettingsCaptured;
        [UnitySetUp] public IEnumerator Setup()
        {
            // MCP may run tests with Game View unfocused. Route queued input to UGUI
            // for this test only; restore both values below and never save an input asset.
            originalBackgroundBehavior=InputSystem.settings.backgroundBehavior;
            originalEditorInputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            inputSettingsCaptured=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ShopPrototype.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            shop=Object.FindFirstObjectByType<ShopPrototype>();
            Assert.That(shop,Is.Not.Null);Assert.That(shop.Session,Is.Not.Null,"Saved scene must start without manual configuration.");
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(inputSettingsCaptured)
            {
                InputSystem.settings.backgroundBehavior=originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode=originalEditorInputBehavior;
                inputSettingsCaptured=false;
            }
            if(testCatalog!=null) {Object.Destroy(shop.gameObject);Object.Destroy(testCatalog);}
            yield return null;
        }
        IEnumerator MouseAt(Vector2 position,bool held)
        {
            var state=new MouseState{position=position};state=state.WithButton(MouseButton.Left,held);
            InputSystem.QueueStateEvent(mouse,state);
            yield return null;yield return null;
        }
        IEnumerator Key(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        }
        Vector2 ItemPoint(GridItem item)
        {
            var first=item.Cells.OrderBy(p=>p.y).ThenBy(p=>p.x).First();
            return shop.CellScreenPosition(item.Container,item.X+first.x,item.Y+first.y);
        }
        IEnumerator StartDrag(GridItem item,Vector2 target)
        {
            var from=ItemPoint(item);
            // Prove the visible occupied cell actually receives UGUI raycasts.
            var data=new PointerEventData(EventSystem.current){position=from};var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.FirstOrDefault().gameObject?.GetComponentInParent<GridDragHandle>()?.itemId,Is.EqualTo(item.Id));
            yield return MouseAt(from,false);yield return MouseAt(from,true);yield return MouseAt(target,true);
            Assert.That(shop.IsDragging,Is.True,"InputSystem mouse must start the drag through UGUI.");
        }
        IEnumerator Drag(GridItem item,ContainerId target,int x,int y)
        {
            var to=shop.CellScreenPosition(target,x,y);yield return StartDrag(item,to);yield return MouseAt(to,false);
            Assert.That(shop.IsDragging,Is.False);Assert.That(shop.Session.ValidateState(),Is.Null);
        }
        IEnumerator Click(string name)
        {
            var button=shop.FindButton(name);Assert.That(button.interactable,Is.True,$"{name} must be enabled at this step.");
            var rect=(RectTransform)button.transform;var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            yield return MouseAt(center,false);yield return MouseAt(center,true);yield return MouseAt(center,false);
        }

        [UnityTest] public IEnumerator RealMouseAndKeyboardHandleShapesPreviewsAndRollback()
        {
            var s=shop.Session;var dew=s.Items.First(i=>i.Definition.id=="dew");var herb=s.Items.First(i=>i.Definition.id=="herb");
            yield return Drag(dew,ContainerId.Display,0,0);
            Assert.That(dew.Container,Is.EqualTo(ContainerId.Display));
            var destination=shop.CellScreenPosition(ContainerId.Display,0,0);
            yield return StartDrag(herb,destination);
            Assert.That(shop.PreviewMessage,Does.Contain("放不下"));
            yield return Key(UnityEngine.InputSystem.Key.F);
            Assert.That(shop.PreviewMessage,Does.Contain("可以放置"));
            yield return MouseAt(destination,false);
            Assert.That(herb.Flipped);Assert.That(herb.Container,Is.EqualTo(ContainerId.Display));
            Assert.That(s.Occupied(ContainerId.Display),Is.EqualTo(4));
            var sword=s.Items.First(i=>i.Definition.id=="sword");destination=shop.CellScreenPosition(ContainerId.Display,0,3);
            yield return StartDrag(sword,destination);Assert.That(shop.PreviewMessage,Does.Contain("放不下"));
            yield return Key(UnityEngine.InputSystem.Key.R);Assert.That(shop.PreviewMessage,Does.Contain("可以放置"));yield return MouseAt(destination,false);
            Assert.That(sword.Rotation,Is.EqualTo(1));Assert.That(sword.Y,Is.EqualTo(3));
            yield return Drag(sword,ContainerId.Display,4,3);
            Assert.That(sword.X,Is.Zero);Assert.That(sword.Y,Is.EqualTo(3));Assert.That(s.Items.Count,Is.EqualTo(7));
            // A visible rotate button must use the same validation (vertical sword cannot fit here).
            yield return Click("Rotate");Assert.That(sword.Rotation,Is.EqualTo(1));
            yield return StartDrag(sword,shop.CellScreenPosition(ContainerId.Storage,2,4));
            yield return Key(UnityEngine.InputSystem.Key.Escape);yield return MouseAt(destination,false);
            Assert.That(shop.IsDragging,Is.False);Assert.That(sword.Container,Is.EqualTo(ContainerId.Display));Assert.That(s.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ActualButtonsRunFiveRandomVisitorsAcrossTwoDays()
        {
            var s=shop.Session;
            yield return Drag(s.Items.First(i=>i.Definition.id=="sign"),ContainerId.Display,0,0);
            yield return Drag(s.Items.First(i=>i.Definition.id=="pill"),ContainerId.Display,2,0);
            int expectedMoney=120;
            for(int day=1;day<=2;day++)
            {
                yield return Click("BeginBusiness");Assert.That(s.BuyersToday+s.SuppliersToday,Is.EqualTo(5));
                for(int n=0;n<5;n++)
                {
                    Assert.That(s.Offer,Is.Not.Null);
                    if(s.Offer.Direction==TradeDirection.CustomerSells)
                    {
                        var offered=s.Find(s.Offer.ItemId);
                        yield return Drag(offered,ContainerId.Storage,6,4);
                        Assert.That(offered.Owner,Is.EqualTo(ItemOwner.Customer));Assert.That(offered.Container,Is.EqualTo(ContainerId.Counter));
                        if(shop.FindButton("AcceptTrade").interactable) {expectedMoney-=s.Offer.Price;yield return Click("AcceptTrade");}
                    }
                    else
                    {
                        var item=s.Items.FirstOrDefault(i=>i.Owner==ItemOwner.Player && !i.Definition.procurementSign && i.Definition.category==s.Offer.RequestedCategory);
                        if(item!=null)
                        {
                            Assert.That(s.FindSpace(item,ContainerId.Counter,out int x,out int y));yield return Drag(item,ContainerId.Counter,x,y);
                            if(s.CanAcceptTrade(out int price,out _)) {expectedMoney+=price;yield return Click("AcceptTrade");}
                            else {Assert.That(s.FindSpace(item,ContainerId.Storage,out x,out y));yield return Drag(item,ContainerId.Storage,x,y);}
                        }
                    }
                    Assert.That(s.Money,Is.EqualTo(expectedMoney));
                    if(n<4 || s.Offer!=null)yield return Click("NextCustomer");
                }
                Assert.That(s.Offer,Is.Null);Assert.That(s.ServedToday,Is.EqualTo(5));Assert.That(shop.FindButton("NextCustomer").interactable,Is.False);
                yield return Click("EndBusiness");
                while(s.In(ContainerId.Storage).Any(i=>i.Definition.id=="herb") && s.In(ContainerId.Storage).Any(i=>i.Definition.id=="dew"))
                {int previous=s.Crafted;yield return Click("Craft");if(previous==s.Crafted)break;}
                yield return Click("Sleep");Assert.That(s.Day,Is.EqualTo(day+1));
            }
            Assert.That(s.HasFurnace);Assert.That(s.RemainingCustomers,Is.Zero);Assert.That(s.Offer,Is.Null);Assert.That(s.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator SavedSceneStartsFreshAndAllContainersAreVisible()
        {
            Assert.That(shop.Session.Day,Is.EqualTo(1));Assert.That(shop.Session.Money,Is.EqualTo(120));Assert.That(shop.Session.Items.Count,Is.EqualTo(7));
            Assert.That(shop.Session.Offer,Is.Null);
            foreach(ContainerId id in System.Enum.GetValues(typeof(ContainerId)))
            { var size=ShopSession.Size(id);foreach(var p in new[]{shop.CellScreenPosition(id,0,0),shop.CellScreenPosition(id,size.x-1,size.y-1)})
                {Assert.That(p.x,Is.InRange(0,Screen.width));Assert.That(p.y,Is.InRange(0,Screen.height));} }
            foreach(string button in new[]{"BeginBusiness","Craft","Rotate","Flip","Sleep","AcceptTrade"})Assert.That(shop.FindButton(button),Is.Not.Null);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        string CustomerText=>shop.GetComponentsInChildren<UnityEngine.UI.Text>().First(t=>t.name=="CustomerDetails").text;
        IEnumerator RestartWithTestCatalog(System.Action<ShopCatalog> configure)
        {
            testCatalog=Object.Instantiate(shop.Catalog);configure(testCatalog);
            Object.Destroy(shop.gameObject);yield return null;
            shop=new GameObject("Test Shop Prototype").AddComponent<ShopPrototype>();shop.Catalog=testCatalog;
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
        }
        IEnumerator PrepareThreePillsAndBuyer(int budget)
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=c.startingItems.Concat(new[]{"pill","pill"}).ToArray();
                c.baseSupplierChance=0;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=budget}};c.buyerBudgetVariation=0;
            });
            yield return Drag(shop.Session.Items.First(i=>i.Definition.id=="pill"),ContainerId.Display,2,0);
            yield return Click("BeginBusiness");Assert.That(shop.Session.Offer.RemainingBudget,Is.EqualTo(budget));
        }

        [UnityTest] public IEnumerator BuyerBudgetAndInvalidBasketControlRealConfirmButton()
        {
            yield return PrepareThreePillsAndBuyer(40);var s=shop.Session;
            yield return Click("NextCustomer"); // An unserved buyer can be skipped.
            var buyer=s.Offer;Assert.That(buyer.RemainingBudget,Is.EqualTo(40));
            var pills=s.Items.Where(i=>i.Definition.id=="pill").ToArray();var jade=s.Items.First(i=>i.Definition.id=="jade");
            yield return Drag(pills[2],ContainerId.Counter,0,0); // A different copy from the displayed one.
            yield return Drag(jade,ContainerId.Counter,2,0);
            Assert.That(jade.Container,Is.EqualTo(ContainerId.Counter));Assert.That(shop.FindButton("AcceptTrade").interactable,Is.False);
            Assert.That(CustomerText,Does.Contain("类别不符"));
            Assert.That(s.FindSpace(jade,ContainerId.Storage,out int x,out int y));yield return Drag(jade,ContainerId.Storage,x,y);
            Assert.That(shop.FindButton("AcceptTrade").interactable);yield return Click("AcceptTrade");
            Assert.That(buyer.RemainingBudget,Is.EqualTo(22));Assert.That(s.Offer,Is.SameAs(buyer));
            yield return Drag(pills[1],ContainerId.Counter,0,0);yield return Click("AcceptTrade");
            Assert.That(buyer.RemainingBudget,Is.EqualTo(4));
            yield return Drag(pills[0],ContainerId.Counter,0,0);
            Assert.That(shop.FindButton("AcceptTrade").interactable,Is.False);Assert.That(CustomerText,Does.Contain("资金不足"));
            yield return Click("NextCustomer");Assert.That(s.Offer.RemainingBudget,Is.EqualTo(40));
            Assert.That(pills[0].Container,Is.EqualTo(ContainerId.Counter));Assert.That(shop.FindButton("AcceptTrade").interactable);
            // Leaving and closing never move unsold player goods behind the player's back.
            yield return Click("EndBusiness");yield return Drag(pills[0],ContainerId.Display,2,0);
            Assert.That(s.FindSpace(pills[0],ContainerId.Storage,out x,out y));yield return Drag(pills[0],ContainerId.Storage,x,y);
            yield return Click("Sleep");yield return Drag(pills[0],ContainerId.Counter,0,0);
            Assert.That(pills[0].Container,Is.EqualTo(ContainerId.Counter));Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ThreeItemBasketAndOpenDisplayChangesWorkThroughRealDrags()
        {
            yield return PrepareThreePillsAndBuyer(60);var s=shop.Session;
            var sign=s.Items.First(i=>i.Definition.id=="sign");
            yield return Drag(sign,ContainerId.Display,0,0);
            Assert.That(s.FindSpace(sign,ContainerId.Storage,out int x,out int y));yield return Drag(sign,ContainerId.Storage,x,y);
            var jade=s.Items.First(i=>i.Definition.id=="jade");yield return Drag(jade,ContainerId.Display,0,0);
            Assert.That(jade.Container,Is.EqualTo(ContainerId.Display));Assert.That(s.BuyersToday,Is.EqualTo(5));
            yield return Click("NextCustomer");yield return Click("NextCustomer");
            Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(60));
            var pills=s.Items.Where(i=>i.Definition.id=="pill").ToArray();
            yield return Drag(pills[2],ContainerId.Counter,0,0);yield return Drag(pills[0],ContainerId.Counter,2,0);yield return Drag(pills[1],ContainerId.Counter,0,2);
            Assert.That(CustomerText,Does.Contain("18×3=54"));Assert.That(CustomerText,Does.Contain("交易成立"));
            int money=s.Money;yield return Click("AcceptTrade");
            Assert.That(s.Money,Is.EqualTo(money+54));Assert.That(s.Sales,Is.EqualTo(3));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(6));
            Assert.That(s.Items.Any(i=>i.Definition.id=="pill"),Is.False);Assert.That(shop.FindButton("AcceptTrade").interactable,Is.False);
            while(s.Offer!=null)yield return Click("NextCustomer");Assert.That(shop.FindButton("NextCustomer").interactable,Is.False);
            // A new category added after opening does not append new customers.
            Assert.That(s.RemainingCustomers,Is.Zero);Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator EmptyDisplayStillReceivesFiveAndRealDragsUpdateBudgetTierPreview()
        {
            var s=shop.Session;
            Assert.That(CustomerText,Does.Contain("空展示柜也有客人"));Assert.That(CustomerText,Does.Contain("18–22"));
            yield return Click("BeginBusiness");var snapshot=s.TodayAttraction;
            yield return Drag(s.Items.First(i=>i.Definition.id=="jade"),ContainerId.Display,0,0);
            Assert.That(s.TodayAttraction,Is.SameAs(snapshot));Assert.That(snapshot.BuyerCategory,Is.EqualTo(ItemCategory.Unclassified));
            for(int n=0;n<5;n++)
            {
                Assert.That(s.Offer,Is.Not.Null);if(s.Offer.Direction==TradeDirection.CustomerBuys) Assert.That(s.Offer.RemainingBudget,Is.InRange(18,22));
                yield return Click("NextCustomer");
            }
            Assert.That(s.ServedToday,Is.EqualTo(5));Assert.That(s.Offer,Is.Null);Assert.That(shop.FindButton("NextCustomer").interactable,Is.False);
            yield return Click("EndBusiness");yield return Click("Sleep");
            yield return Drag(s.Items.First(i=>i.Definition.id=="pill"),ContainerId.Display,2,0);
            Assert.That(CustomerText,Does.Contain("丹药"));Assert.That(CustomerText,Does.Contain("18–22"));
            yield return Click("Craft");
            yield return Drag(s.Items.First(i=>i.Definition.id=="pill" && i.Container==ContainerId.Storage),ContainerId.Display,2,2);
            Assert.That(CustomerText,Does.Contain("展示价值 36"));Assert.That(CustomerText,Does.Contain("档位 ≥30"));Assert.That(CustomerText,Does.Contain("54–66"));
            yield return Click("BeginBusiness");
            for(int n=0;n<5;n++)
            {
                if(s.Offer.Direction==TradeDirection.CustomerBuys) {Assert.That(s.Offer.RequestedCategory,Is.EqualTo(ItemCategory.Medicine));Assert.That(s.Offer.RemainingBudget,Is.InRange(54,66));}
                yield return Click("NextCustomer");
            }
            Assert.That(s.ServedToday,Is.EqualTo(5));Assert.That(s.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator FlipButtonReflectsHorizontallyAfterRotation()
        {
            var herb=shop.Session.Items.First(i=>i.Definition.id=="herb");
            yield return Drag(herb,ContainerId.Display,2,1);
            yield return Click("Rotate");
            var before=herb.Cells;int maxX=before.Max(p=>p.x);
            yield return Click("Flip");
            CollectionAssert.AreEquivalent(before.Select(p=>new Vector2Int(maxX-p.x,p.y)),herb.Cells);
            Assert.That(shop.Session.ValidateState(),Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
