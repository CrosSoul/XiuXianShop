using System;
using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        public const string TooltipPreferenceKey="XiuXianShop.TooltipHoverDelaySeconds";
        static readonly float[] tooltipDelayOptions={0f,.5f,1f,1.5f};

        void LoadTooltipPreference()
        {
            float saved=PlayerPrefs.GetFloat(TooltipPreferenceKey,1f);
            TooltipHoverDelaySeconds=Array.IndexOf(tooltipDelayOptions,saved)>=0?saved:1f;
        }
        void OpenSettings()
        {
            CloseSystemMenu();
            systemMenu=Rect(content,"SystemSettings",0,0,1600,1000);
            Image(systemMenu,new Color(.02f,.035f,.04f),true);
            Label(systemMenu,"SettingsTitle",210,120,1180,55,"设置",30,gold);
            Label(systemMenu,"TooltipDelayTitle",210,300,680,65,"弹出提示悬浮光标时长",26,textColor);
            var host=Rect(systemMenu,"TooltipDelayControl",930,300,380,65);
            var control=UnityEngine.UI.DefaultControls.CreateDropdown(new UnityEngine.UI.DefaultControls.Resources());
            control.name="TooltipDelayDropdown";control.transform.SetParent(host,false);
            var rect=(RectTransform)control.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
            foreach(var text in control.GetComponentsInChildren<UnityEngine.UI.Text>(true)){text.font=font;text.fontSize=24;}
            Label(rect,"DropdownIndicator",330,12,40,40,"▼",22,Color.black);
            var dropdown=control.GetComponent<UnityEngine.UI.Dropdown>();
            dropdown.template.sizeDelta=new Vector2(dropdown.template.sizeDelta.x,190);
            var option=dropdown.template.GetComponentInChildren<UnityEngine.UI.Toggle>(true);
            var optionRect=(RectTransform)option.transform;optionRect.sizeDelta=new Vector2(optionRect.sizeDelta.x,42);
            dropdown.ClearOptions();dropdown.AddOptions(new System.Collections.Generic.List<string>{"0 秒","0.5 秒","1 秒","1.5 秒"});
            int index=Array.IndexOf(tooltipDelayOptions,TooltipHoverDelaySeconds);
            dropdown.SetValueWithoutNotify(index<0?2:index);
            dropdown.onValueChanged.AddListener(value=>{
                TooltipHoverDelaySeconds=tooltipDelayOptions[value];
                PlayerPrefs.SetFloat(TooltipPreferenceKey,TooltipHoverDelaySeconds);PlayerPrefs.Save();
            });
            Label(systemMenu,"SettingsHint",210,430,1100,95,"修改后立即生效，并在下次启动时保留。\n此偏好不随游戏存档槽改变。",22,muted);
            CarryButton(systemMenu,"SettingsBack",210,825,360,"返回系统菜单",OpenSystemMenu);
        }
    }
}
