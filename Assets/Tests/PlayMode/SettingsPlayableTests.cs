#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        bool settingsPreferenceCaptured,settingsPreferenceExisted;
        float previousTooltipPreference;
        void IsolateTooltipPreference()
        {
            settingsPreferenceCaptured=true;settingsPreferenceExisted=PlayerPrefs.HasKey(ShopPrototype.TooltipPreferenceKey);
            previousTooltipPreference=PlayerPrefs.GetFloat(ShopPrototype.TooltipPreferenceKey,1f);
            PlayerPrefs.DeleteKey(ShopPrototype.TooltipPreferenceKey);PlayerPrefs.Save();
            shop.TooltipHoverDelaySeconds=1f;IsolateAlchemyTestMouse();
        }
        [TearDown] public void RestoreTooltipPreference()
        {
            if(!settingsPreferenceCaptured)return;
            if(settingsPreferenceExisted)PlayerPrefs.SetFloat(ShopPrototype.TooltipPreferenceKey,previousTooltipPreference);
            else PlayerPrefs.DeleteKey(ShopPrototype.TooltipPreferenceKey);
            PlayerPrefs.Save();settingsPreferenceCaptured=false;
        }
        IEnumerator ClickSettingsControl(Transform control)
        {
            var rect=(RectTransform)control;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            yield return MouseAt(point,false);yield return MouseAt(point,true);yield return MouseAt(point,false);
        }
        IEnumerator SelectTooltipDelay(string label)
        {
            yield return Key(UnityEngine.InputSystem.Key.Escape);yield return Click("SystemSettings");
            var dropdown=shop.GetComponentInChildren<UnityEngine.UI.Dropdown>();
            yield return ClickSettingsControl(dropdown.transform);
            var option=shop.GetComponentsInChildren<UnityEngine.UI.Toggle>().Single(t=>t.GetComponentsInChildren<UnityEngine.UI.Text>().Any(text=>text.text==label));
            yield return ClickSettingsControl(option.transform);yield return new WaitForSecondsRealtime(.2f);
            yield return Click("SettingsBack");yield return Click("SystemResume");
        }
        [UnityTest,Category("DP63")] public IEnumerator SettingsDropdownChangesAllHoverDelaysAndBlocksUnderlyingInput()
        {
            IsolateTooltipPreference();var item=shop.Session.Items.First(i=>i.Definition.id=="herb");
            foreach(var pair in new[]{(0f,"0 秒"),(.5f,"0.5 秒"),(1f,"1 秒"),(1.5f,"1.5 秒")})
            {
                yield return SelectTooltipDelay(pair.Item2);
                Assert.That(shop.TooltipHoverDelaySeconds,Is.EqualTo(pair.Item1));
                Assert.That(PlayerPrefs.GetFloat(ShopPrototype.TooltipPreferenceKey),Is.EqualTo(pair.Item1));
                yield return MouseAt(ItemPoint(item),false);
                if(pair.Item1>0)
                {
                    yield return new WaitForSecondsRealtime(pair.Item1/2);Assert.That(TooltipText(),Is.Null);
                    yield return new WaitForSecondsRealtime(pair.Item1/2+.1f);
                }
                Assert.That(TooltipText(),Is.Not.Null);yield return MouseAt(Vector2.zero,false);Assert.That(TooltipText(),Is.Null);
            }
            yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(shop.IsSystemMenuOpen);
            var point=ItemPoint(item);yield return MouseAt(point,true);yield return MouseAt(point+Vector2.right*100,true);yield return MouseAt(point,false);
            Assert.That(shop.IsDragging,Is.False);Assert.That(item.Container,Is.EqualTo(ContainerId.Storage));
            yield return Click("BeginBusiness");Assert.That(shop.Session.Phase,Is.EqualTo(TurnPhase.Preparation));
            yield return Key(UnityEngine.InputSystem.Key.Escape);Assert.That(shop.IsSystemMenuOpen,Is.False);
            yield return Drag(item,ContainerId.Display,0,0);Assert.That(item.Container,Is.EqualTo(ContainerId.Display));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest,Category("DP63")] public IEnumerator PreferenceSurvivesFreshSessionAndLoadingADifferentProgressSlot()
        {
            IsolateTooltipPreference();shop.OpenSystemMenu();yield return Click("SaveSlot_1");yield return Click("SystemResume");
            string directory=shop.SaveDirectory;
            yield return SelectTooltipDelay("0.5 秒");
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ShopPrototype.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            shop=Object.FindFirstObjectByType<ShopPrototype>();shop.SaveDirectory=directory;
            Assert.That(shop.TooltipHoverDelaySeconds,Is.EqualTo(.5f));
            shop.OpenSystemMenu();yield return Click("LoadSlot_1");yield return Click("SaveConfirm");
            Assert.That(shop.TooltipHoverDelaySeconds,Is.EqualTo(.5f));
            Assert.That(shop.SaveSlots.Read(1),Does.Not.Contain("TooltipHoverDelay"));
            Assert.That(PlayerPrefs.GetFloat(ShopPrototype.TooltipPreferenceKey),Is.EqualTo(.5f));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
