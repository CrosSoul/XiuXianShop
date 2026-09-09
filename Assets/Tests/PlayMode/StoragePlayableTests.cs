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
        [UnityTest] public IEnumerator StorageWindowDragsContentsAndBoxThenRestoresActualSave()
        {
            yield return RestartWithTestCatalog(c=>c.SetStorageVerificationDefaults(),17);
            shop.SavePath=System.IO.Path.Combine(Application.dataPath,"../Temp/DP30-storage-play.json");
            var s=shop.Session;var box=s.Items.Single(i=>i.Definition.id=="test-storage-case");
            var herb=s.Items.Single(i=>i.Definition.id=="herb");var sword=s.Items.Single(i=>i.Definition.id=="sword");
            var p=ItemPoint(box);yield return MouseAt(p,false);yield return MouseAt(p,true);yield return MouseAt(p,false);
            Assert.That(shop.OpenStorageId,Is.EqualTo(box.Id));
            yield return Drag(herb,ContainerId.Interior,0,0);
            yield return Drag(sword,ContainerId.Interior,4,0);
            Assert.That(herb.StorageItemId,Is.EqualTo(box.Id));Assert.That(sword.StorageItemId,Is.EqualTo(box.Id));
            shop.SelectItem(herb.Id);yield return Key(UnityEngine.InputSystem.Key.R);yield return Key(UnityEngine.InputSystem.Key.F);
            Assert.That(herb.Flipped);Assert.That(s.ValidateState(),Is.Null);
            var window=shop.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="StorageWindow");
            var title=window.Find("StorageTitleBar").GetComponent<RectTransform>();
            var from=RectTransformUtility.WorldToScreenPoint(null,title.TransformPoint(title.rect.center));var before=window.anchoredPosition;
            yield return MouseAt(from,false);yield return MouseAt(from,true);yield return MouseAt(from+new Vector2(-80,50),true);yield return MouseAt(from+new Vector2(-80,50),false);
            Assert.That(window.anchoredPosition,Is.Not.EqualTo(before));
            yield return Click("StorageClose");Assert.That(shop.OpenStorageId,Is.Zero);
            var spot=Enumerable.Range(0,70).Select(n=>new Vector2Int(n%10,n/10)).First(v=>(v.x!=box.X || v.y!=box.Y) && s.Fits(box,ContainerId.Storage,v.x,v.y,0,false));
            yield return Drag(box,ContainerId.Storage,spot.x,spot.y);
            p=ItemPoint(box);yield return MouseAt(p,false);yield return MouseAt(p,true);yield return MouseAt(p,false);
            Assert.That(s.Find(herb.Id),Is.SameAs(herb));Assert.That(shop.ItemView(herb.Id),Is.Not.Null);
            yield return Click("StorageClose");yield return Click("CalendarOpen");yield return Click("CalendarSave");
            string saved=System.IO.File.ReadAllText(shop.SavePath),legacy=saved.Replace("\"version\": 3","\"version\": 2");
            System.IO.File.WriteAllText(shop.SavePath,legacy);yield return Click("CalendarLoad");
            Assert.That(shop.Session,Is.SameAs(s));Assert.That(shop.CalendarMessage,Does.Contain("旧月度 v2"));
            Assert.That(System.IO.File.ReadAllText(shop.SavePath),Is.EqualTo(legacy));
            System.IO.File.WriteAllText(shop.SavePath,saved);yield return Click("CalendarLoad");yield return Click("CalendarClose");
            Assert.That(shop.Session.In(ContainerId.Interior,box.Id).Count(),Is.EqualTo(2));
            herb=shop.Session.Find(herb.Id);Assert.That(herb.Flipped);
            box=shop.Session.Find(box.Id);p=ItemPoint(box);yield return MouseAt(p,false);yield return MouseAt(p,true);yield return MouseAt(p,false);
            Assert.That(shop.Session.FindSpace(herb,ContainerId.Storage,out int x,out int y));
            yield return Drag(herb,ContainerId.Storage,x,y);Assert.That(shop.Session.Find(herb.Id),Is.SameAs(herb));Assert.That(herb.StorageItemId,Is.Zero);
            sword=shop.Session.Find(sword.Id);Assert.That(shop.Session.FindSpace(sword,ContainerId.Storage,out x,out y));
            yield return Drag(sword,ContainerId.Storage,x,y);Assert.That(shop.Session.Find(sword.Id),Is.SameAs(sword));
            Assert.That(shop.Session.In(ContainerId.Interior,box.Id),Is.Empty);
            System.IO.File.Delete(shop.SavePath);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
