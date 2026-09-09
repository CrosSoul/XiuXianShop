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
    public sealed partial class ShopPlayableTests
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
            if(name=="AcceptTrade")
            {
                if(!shop.NegotiationView.IsOpen)yield return Click("NegotiationOpen");
                yield return Click("NegotiationConfirm");yield break;
            }
            var button=shop.FindButton(name);Assert.That(button.interactable,Is.True,$"{name} must be enabled at this step.");
            var rect=(RectTransform)button.transform;var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            yield return MouseAt(center,false);yield return MouseAt(center,true);yield return MouseAt(center,false);
        }
        IEnumerator CheckConfirm(bool expected)
        {
            if(!shop.NegotiationView.IsOpen)yield return Click("NegotiationOpen");
            Assert.That(shop.FindButton("NegotiationConfirm").interactable,Is.EqualTo(expected));
            yield return Click("NegotiationClose");
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
                        Assert.That(offered.Owner,Is.EqualTo(ItemOwner.Customer));Assert.That(offered.Container,Is.EqualTo(ContainerId.CustomerCounter));
                        Assert.That(s.FindSpace(offered,ContainerId.Counter,out int cx,out int cy));
                        yield return Drag(offered,ContainerId.Counter,cx,cy);
                        if(s.PreviewTrade().CanConfirm) {expectedMoney-=s.Offer.Price;yield return Click("AcceptTrade");}
                        else yield return Click("NegotiationClose");
                    }
                    else
                    {
                        var item=s.Items.FirstOrDefault(i=>i.Owner==ItemOwner.Player && !i.Definition.procurementSign && i.Definition.category==s.Offer.RequestedCategory);
                        if(item!=null)
                        {
                            Assert.That(s.FindSpace(item,ContainerId.Counter,out int x,out int y));yield return Drag(item,ContainerId.Counter,x,y);
                            if(s.CanAcceptTrade(out _,out _))
                            {
                                expectedMoney+=(int)s.PreviewTrade().ActualNet;yield return Click("AcceptTrade");
                                if(shop.FindButton("ConcessionContinue").gameObject.activeInHierarchy)yield return Click("ConcessionContinue");
                            }
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
            foreach(string button in new[]{"BeginBusiness","Craft","Rotate","Flip","Sleep","NegotiationOpen"})Assert.That(shop.FindButton(button),Is.Not.Null);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        string CustomerText=>string.Join("\n",shop.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name=="CustomerDetails" || t.name=="TradeDetails" || t.name=="TradeStatus" || t.name=="DisplaySummary").Select(t=>t.text));
        string DetailText=>shop.GetComponentsInChildren<UnityEngine.UI.Text>().First(t=>t.name=="Selection").text;

        string CalendarText(string name)=>shop.GetComponentsInChildren<UnityEngine.UI.Text>().First(t=>t.name==name).text;
        IEnumerator CalendarFixture(int day=6)
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.Find("pill").baseValue=20;c.startingItems=new[]{"pill","pill","pill"};
                c.baseSupplierChance=0;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=50}};c.buyerBudgetVariation=0;
            },17);
            shop.StartVerificationSession(testCatalog,17,MarketCalendar.OverlapExample());
            shop.SavePath=System.IO.Path.Combine(Application.dataPath,"../Temp/DP17_UI_Test_"+System.Guid.NewGuid()+".json");
            while(shop.Session.Day<day){shop.Session.BeginBusiness();shop.Session.EndBusiness();shop.Session.Sleep();}
            shop.Refresh();yield return null;yield return null;
        }
        [UnityTest] public IEnumerator CalendarRealClicksShowFourteenDatesOverlapDetailsAndBlockUnderlyingInput()
        {
            yield return CalendarFixture();
            var item=shop.Session.Items[0];shop.SelectItem(item.Id);
            yield return Click("CalendarOpen");Assert.That(shop.CalendarView.IsOpen);
            Assert.That(shop.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("CalendarDay_")),Is.EqualTo(14));
            Assert.That(CalendarText("CalendarHeading"),Does.Contain("今天：第 6 天"));
            var markers=shop.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name=="RentMarker").Select(t=>t.text).ToArray();
            CollectionAssert.AreEquivalent(new[]{"收租 20 灵石","收租 21 灵石"},markers);
            Assert.That(shop.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.Contains("example-rise") || b.name.Contains("example-fall")),Is.False);
            Assert.That(CalendarText("CalendarEventDetails"),Does.Not.Contain("丹药集中到货"));
            while(shop.Session.Day<8){shop.Session.BeginBusiness();shop.Session.EndBusiness();shop.Session.Sleep();}
            shop.Refresh();yield return null;yield return null;
            var a=(RectTransform)shop.FindButton("MarketBar_example-rise_1").transform;
            var b=(RectTransform)shop.FindButton("MarketBar_example-fall_1").transform;
            Assert.That(a.anchoredPosition.y,Is.Not.EqualTo(b.anchoredPosition.y));
            Assert.That(a.sizeDelta.x,Is.EqualTo(2*198-12));
            yield return Click("MarketBar_example-fall_1");
            Assert.That(CalendarText("CalendarEventDetails"),Does.Contain("丹药集中到货与集市临时让利"));
            Assert.That(CalendarText("CalendarEventDetails"),Does.Contain("生效中"));Assert.That(CalendarText("CalendarEventDetails"),Does.Contain("-10%"));
            int rotation=item.Rotation;yield return Key(UnityEngine.InputSystem.Key.R);Assert.That(item.Rotation,Is.EqualTo(rotation));
            yield return Click("BeginBusiness");Assert.That(shop.Session.Phase,Is.EqualTo(DayPhase.Preparation),"Modal must intercept clicks on the underlying business button.");
            yield return Click("CalendarNext");Assert.That(shop.CalendarView.FirstDay,Is.EqualTo(15));
            yield return Click("CalendarPrevious");Assert.That(shop.CalendarView.FirstDay,Is.EqualTo(1));
            yield return Click("CalendarToday");Assert.That(shop.CalendarView.FirstDay,Is.EqualTo(8));
            Assert.That(shop.Session.Day,Is.EqualTo(8));Assert.That(shop.Session.Money,Is.EqualTo(100));
            yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(shop.CalendarView.IsOpen,Is.False);
            yield return Click("CalendarOpen");yield return Click("CalendarClose");Assert.That(shop.CalendarView.IsOpen,Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator CalendarSaveLoadButtonsRestoreActualFileAndDoNotRepeatRent()
        {
            yield return CalendarFixture(7);var s=shop.Session;
            string before=JsonUtility.ToJson(s.Calendar.Capture());
            yield return Click("CalendarOpen");yield return Click("CalendarSave");
            Assert.That(System.IO.File.Exists(shop.SavePath));Assert.That(CalendarText("CalendarMessage"),Does.Contain("已保存第 7 天"));
            yield return Click("CalendarClose");
            yield return Drag(s.Items[0],ContainerId.Counter,0,0);
            yield return Click("CalendarOpen");yield return Click("CalendarLoad");
            Assert.That(shop.Session.Items[0].Container,Is.EqualTo(ContainerId.Storage));Assert.That(shop.Session.Money,Is.EqualTo(120));
            Assert.That(JsonUtility.ToJson(shop.Session.Calendar.Capture()),Is.EqualTo(before));
            yield return Click("CalendarClose");yield return Click("BeginBusiness");
            yield return Click("CalendarOpen");Assert.That(shop.FindButton("CalendarSave").interactable,Is.False);Assert.That(shop.FindButton("CalendarLoad").interactable,Is.False);
            yield return Click("CalendarClose");yield return Click("EndBusiness");yield return Click("Sleep");
            Assert.That(shop.Session.Day,Is.EqualTo(8));Assert.That(shop.Session.Money,Is.EqualTo(100));
            yield return Click("CalendarOpen");yield return Click("CalendarSave");yield return Click("CalendarLoad");yield return Click("CalendarLoad");
            Assert.That(shop.Session.Day,Is.EqualTo(8));Assert.That(shop.Session.Money,Is.EqualTo(100));
            Assert.That(CalendarText("CalendarRent"),Does.Contain("第 14 天夜间，21 灵石"));
            Assert.That(CalendarText("CalendarHeading"),Does.Contain("今天：第 8 天"));
            System.IO.File.WriteAllText(shop.SavePath,"broken save");yield return Click("CalendarLoad");
            Assert.That(CalendarText("CalendarMessage"),Does.Contain("当前经营保留"));Assert.That(shop.Session.Money,Is.EqualTo(100));
            System.IO.File.Delete(shop.SavePath);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator CalendarDayChangesRefreshRealPricesBudgetAndSettlement()
        {
            yield return CalendarFixture(6);var s=shop.Session;
            yield return Drag(s.Items[0],ContainerId.Display,0,0);
            yield return Drag(s.Items[1],ContainerId.Counter,0,0);Assert.That(DetailText,Does.Contain("预估价值 23"));
            yield return Click("BeginBusiness");yield return Click("EndBusiness");yield return Click("Sleep");
            Assert.That(DetailText,Does.Contain("预估价值 27"));Assert.That(DetailText,Does.Contain("+20%"));
            yield return Click("BeginBusiness");yield return Drag(s.Items[2],ContainerId.Counter,2,0);
            Assert.That(CustomerText,Does.Contain("54 灵石"));yield return CheckConfirm(true);
            yield return Click("EndBusiness");yield return Click("Sleep");
            Assert.That(DetailText,Does.Contain("预估价值 25"));Assert.That(DetailText,Does.Contain("-10%"));
            yield return Click("BeginBusiness");Assert.That(CustomerText,Does.Contain("50 灵石"));yield return CheckConfirm(true);
            int before=s.Money;yield return Click("AcceptTrade");Assert.That(s.Money,Is.EqualTo(before+50));Assert.That(s.Offer.RemainingBudget,Is.Zero);
            yield return Click("EndBusiness");Assert.That(CustomerText,Does.Contain("本日收入    +50"));yield return Click("Sleep");
            yield return Drag(s.Items[0],ContainerId.Counter,0,0);Assert.That(DetailText,Does.Contain("预估价值 25"));
            yield return Click("BeginBusiness");yield return Click("EndBusiness");yield return Click("Sleep");
            Assert.That(s.Day,Is.EqualTo(10));Assert.That(DetailText,Does.Contain("预估价值 23"));Assert.That(DetailText,Does.Not.Contain("-10%"));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator CrowdedCalendarScrollKeepsAllEventRowsReachable()
        {
            yield return CalendarFixture();
            var state=MarketCalendar.OverlapExample().Capture();
            var source=state.events.First(e=>e.id=="example-rise");
            state.events=Enumerable.Range(0,5).Select(i=>{var e=source.Copy();e.id="crowded-"+i;e.title="重叠事件"+i;return e;}).ToArray();
            shop.StartVerificationSession(testCatalog,17,new MarketCalendar(state));
            while(shop.Session.Day<7){shop.Session.BeginBusiness();shop.Session.EndBusiness();shop.Session.Sleep();}
            shop.Refresh();yield return null;
            yield return Click("CalendarOpen");
            var viewport=shop.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single(s=>s.name=="CalendarViewport");
            Assert.That(viewport.content.rect.height,Is.GreaterThan(viewport.viewport.rect.height));
            var bars=shop.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.name.StartsWith("MarketBar_")).ToArray();
            Assert.That(bars.Length,Is.EqualTo(10));
            Assert.That(bars.Where(b=>b.name.EndsWith("_1")).Select(b=>((RectTransform)b.transform).anchoredPosition.y).Distinct().Count(),Is.EqualTo(5));
            var point=RectTransformUtility.WorldToScreenPoint(null,viewport.viewport.TransformPoint(viewport.viewport.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,-30)});yield return null;yield return null;
            Assert.That(viewport.verticalNormalizedPosition,Is.LessThan(.1f));
            yield return Click("MarketBar_crowded-4_1");
            Assert.That(CalendarText("CalendarEventDetails"),Does.Contain("重叠事件4"));
            yield return Click("CalendarClose");LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator TwentyItemListScrollsAndSettlementResetsToTop()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=Enumerable.Repeat("dew",20).ToArray();c.baseSupplierChance=0;
                c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=60}};c.buyerBudgetVariation=0;
            });
            var s=shop.Session;var items=s.Items.ToArray();
            yield return Drag(items[0],ContainerId.Display,0,0);yield return Click("BeginBusiness");
            // Populate the stress case directly; regular dragging is covered by the other Play tests.
            for(int n=0;n<20;n++)Assert.That(s.Move(items[n].Id,ContainerId.Counter,n%5,n/5,0,false));
            shop.Refresh();yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var scroll=shop.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().First(r=>r.name=="TradeDetailsViewport");
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(CustomerText,Does.Contain("合计 20 件 / 40"));
            var point=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            yield return MouseAt(point,false);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,-120)});
            yield return null;yield return null;
            Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.5f),"Mouse wheel must scroll the actual trade viewport.");
            yield return Click("AcceptTrade");Assert.That(s.Money,Is.EqualTo(160));
            yield return Click("EndBusiness");yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(CustomerText,Does.Contain("起始余额    120"));Assert.That(scroll.content.anchoredPosition.y,Is.LessThanOrEqualTo(6));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DynamicQuotesRefreshVisibleDetailsAndConfirmWithoutManualRefresh()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.Find("pill").baseValue=20;c.startingItems=new[]{"pill","pill","pill"};
                c.baseSupplierChance=0;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=60}};c.buyerBudgetVariation=0;
            });
            var s=shop.Session;var pills=s.Items.ToArray();shop.SelectItem(pills[0].Id);
            Assert.That(DetailText,Does.Contain("基础价值 20 · 预估价值 20"));Assert.That(DetailText,Does.Not.Contain("购买价值"));
            yield return null;Canvas.ForceUpdateCanvases();
            yield return Drag(pills[0],ContainerId.Display,0,0);yield return Click("BeginBusiness");
            yield return Drag(pills[0],ContainerId.Counter,0,0);
            Assert.That(DetailText,Does.Contain("预估价值 23"));Assert.That(DetailText,Does.Contain("+15%"));
            yield return Drag(pills[1],ContainerId.Counter,2,0);yield return Drag(pills[2],ContainerId.Counter,0,2);
            Assert.That(CustomerText,Does.Contain("合计 3 件 / 69"));yield return CheckConfirm(true);
            s.SetPriceTag(new PriceTag{id="market-test",title="测试降价",percent=-.3f});
            yield return null;yield return null;
            Assert.That(CustomerText,Does.Contain("合计 3 件 / 51"));Assert.That(DetailText,Does.Contain("预估价值 17"));
            yield return CheckConfirm(true);
            yield return Click("AcceptTrade");Assert.That(s.Money,Is.EqualTo(171));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(9));
            Assert.That(s.Items,Is.Empty);yield return CheckConfirm(false);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator CompleteDayBuysAndSellsWithPurchaseHistorySettlementAndNextDay()
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.Find("pill").baseValue=20;c.startingItems=new[]{"pill","pill","pill"};
                foreach(var d in c.items)d.supplierAvailable=d.id=="pill";
                c.baseSupplierChance=.5f;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
                c.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=60}};c.buyerBudgetVariation=0;
                c.priceTags=new[]{new PriceTag{id="buy-test",title="测试收购修正",percent=-.2f,playerSells=false}};
            },0);
            var s=shop.Session;yield return Drag(s.Items[0],ContainerId.Display,0,0);yield return Click("BeginBusiness");
            int bought=0,sold=0;GridItem purchased=null;
            for(int n=0;n<5;n++)
            {
                Assert.That(s.Offer,Is.Not.Null);
                if(s.Offer.Direction==TradeDirection.CustomerSells)
                {
                    purchased=s.Offer.SupplierItem;
                    yield return Drag(purchased,ContainerId.Counter,0,0);
                    Assert.That(CalendarText("NegotiationLines"),Does.Contain("买入 −16"));
                    yield return Click("AcceptTrade");bought++;
                    shop.SelectItem(purchased.Id);Assert.That(DetailText,Does.Contain("购买价值 16"));
                    Assert.That(DetailText,Does.Contain("预估价值 20"));
                }
                else
                {
                    var item=s.Items.FirstOrDefault(i=>i.Owner==ItemOwner.Player && i.Definition.id=="pill");
                    if(item!=null)
                    {
                        yield return Drag(item,ContainerId.Counter,0,0);Assert.That(CustomerText,Does.Contain("卖出 +23"));
                        yield return Click("AcceptTrade");sold++;
                    }
                }
                if(n<4 || s.Offer!=null)yield return Click("NextCustomer");
            }
            Assert.That(bought,Is.GreaterThan(0));Assert.That(sold,Is.GreaterThan(0));Assert.That(s.RemainingCustomers,Is.Zero);
            Assert.That(s.Money,Is.EqualTo(120+23*sold-16*bought));Assert.That(s.ValidateState(),Is.Null);
            yield return Click("EndBusiness");Assert.That(CustomerText,Does.Contain($"本日收入    +{23*sold}"));
            Assert.That(CustomerText,Does.Contain($"本日支出    −{16*bought}"));Assert.That(shop.FindButton("NegotiationOpen").gameObject.activeSelf,Is.False);
            int money=s.Money,count=s.Items.Count;
            yield return Click("Sleep");Assert.That(s.Day,Is.EqualTo(2));Assert.That(s.Money,Is.EqualTo(money));Assert.That(s.Items.Count,Is.EqualTo(count));
            Assert.That(s.IncomeToday,Is.Zero);Assert.That(s.ExpensesToday,Is.Zero);
            yield return Click("BeginBusiness");Assert.That(s.Phase,Is.EqualTo(DayPhase.Open));
            LogAssert.NoUnexpectedReceived();
        }
        IEnumerator RestartWithTestCatalog(System.Action<ShopCatalog> configure,int seed=-1)
        {
            testCatalog=Object.Instantiate(shop.Catalog);testCatalog.marketEvents=System.Array.Empty<MarketEventDefinition>();configure(testCatalog);
            Object.Destroy(shop.gameObject);yield return null;
            shop=new GameObject("Test Shop Prototype").AddComponent<ShopPrototype>();shop.Catalog=testCatalog;shop.CustomerSeed=seed;
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
        }
        IEnumerator PrepareThreePillsAndBuyer(int budget)
        {
            yield return RestartWithTestCatalog(c=>
            {
                c.startingItems=c.startingItems.Concat(new[]{"pill","pill"}).ToArray();
                c.retailMarkup=0;c.baseSupplierChance=0;c.advertisementSupplierBonus=0;c.displayedGoodsBuyerBonus=0;
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
            Assert.That(jade.Container,Is.EqualTo(ContainerId.Counter));yield return CheckConfirm(false);
            Assert.That(CustomerText,Does.Contain("类别不符"));
            Assert.That(s.FindSpace(jade,ContainerId.Storage,out int x,out int y));yield return Drag(jade,ContainerId.Storage,x,y);
            yield return CheckConfirm(true);yield return Click("AcceptTrade");
            Assert.That(buyer.RemainingBudget,Is.EqualTo(22));Assert.That(s.Offer,Is.SameAs(buyer));
            yield return Drag(pills[1],ContainerId.Counter,0,0);yield return Click("AcceptTrade");
            Assert.That(buyer.RemainingBudget,Is.EqualTo(4));
            yield return Drag(pills[0],ContainerId.Counter,0,0);
            yield return CheckConfirm(true);Assert.That(CustomerText,Does.Contain("预算不足"));
            yield return Click("NegotiationOpen");yield return Click("NegotiationConfirm");
            Assert.That(CalendarText("ConcessionText"),Does.Contain("少收14灵石"));
            yield return Click("ConcessionCancel");yield return Click("NegotiationClose");
            yield return Click("NextCustomer");Assert.That(s.Offer.RemainingBudget,Is.EqualTo(40));
            Assert.That(pills[0].Container,Is.EqualTo(ContainerId.Counter));yield return CheckConfirm(true);
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
            Assert.That(CustomerText,Does.Contain("合计 3 件 / 54"));Assert.That(CustomerText,Does.Contain("交易成立"));
            int money=s.Money;yield return Click("AcceptTrade");
            Assert.That(s.Money,Is.EqualTo(money+54));Assert.That(s.Sales,Is.EqualTo(3));Assert.That(s.Offer.RemainingBudget,Is.EqualTo(6));
            Assert.That(s.Items.Any(i=>i.Definition.id=="pill"),Is.False);yield return CheckConfirm(false);
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
            Assert.That(CustomerText,Does.Contain("基础价值 36"));Assert.That(CustomerText,Does.Contain("档位 ≥30"));Assert.That(CustomerText,Does.Contain("54–66"));
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
