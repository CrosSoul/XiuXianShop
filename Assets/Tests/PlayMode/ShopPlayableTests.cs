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
        [UnitySetUp] public IEnumerator Setup()
        {
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

        [UnityTest] public IEnumerator ActualButtonsAndDragsRunTwoDaysWithSupplyCraftAndSales()
        {
            var s=shop.Session;
            yield return Drag(s.Items.First(i=>i.Definition.id=="sign"),ContainerId.Display,0,0);
            yield return Drag(s.Items.First(i=>i.Definition.id=="pill"),ContainerId.Display,2,0);
            yield return Click("BeginBusiness");Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerSells));
            var offered=s.Find(s.Offer.ItemId);int initialMoney=s.Money;
            yield return Drag(offered,ContainerId.Storage,6,4);
            Assert.That(offered.Owner,Is.EqualTo(ItemOwner.Customer));Assert.That(offered.Container,Is.EqualTo(ContainerId.Counter));Assert.That(s.Money,Is.EqualTo(initialMoney));
            for(int n=0;n<4;n++){yield return Click("AcceptTrade");yield return Click("NextCustomer");}
            Assert.That(s.Money,Is.EqualTo(106));Assert.That(s.Offer.Direction,Is.EqualTo(TradeDirection.CustomerBuys));
            yield return Drag(s.Find(s.Offer.ItemId),ContainerId.Counter,0,0);
            yield return Click("AcceptTrade");Assert.That(s.Money,Is.EqualTo(124));Assert.That(s.Sales,Is.EqualTo(1));
            yield return Click("EndBusiness");
            for(int n=0;n<3;n++)yield return Click("Craft");
            Assert.That(s.Crafted,Is.EqualTo(3));yield return Click("Sleep");Assert.That(s.Day,Is.EqualTo(2));
            var pills=s.Items.Where(i=>i.Definition.id=="pill").ToArray();Assert.That(pills.Length,Is.EqualTo(3));
            yield return Drag(pills[0],ContainerId.Display,2,0);yield return Drag(pills[1],ContainerId.Display,4,0);yield return Drag(pills[2],ContainerId.Display,2,2);
            yield return Click("BeginBusiness");
            for(int n=0;n<4;n++){yield return Click("RejectTrade");yield return Click("NextCustomer");}
            for(int n=0;n<3;n++)
            {yield return Click("StageSale");yield return Click("AcceptTrade");if(n<2)yield return Click("NextCustomer");}
            Assert.That(s.Money,Is.EqualTo(178));Assert.That(s.Sales,Is.EqualTo(4));Assert.That(s.Purchases,Is.EqualTo(4));
            yield return Click("EndBusiness");yield return Click("Sleep");Assert.That(s.Day,Is.EqualTo(3));
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
