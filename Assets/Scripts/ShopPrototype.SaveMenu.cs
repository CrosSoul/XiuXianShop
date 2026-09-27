using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        [SerializeField,Min(1),Tooltip("原型手动槽数量；自动槽独立保留一个。")]
        int manualSaveSlotCount=3;
        public string SaveDirectory { get; set; }
        public ShopSaveSlots SaveSlots => new ShopSaveSlots(SaveDirectory ?? (SavePath==null?
            Path.Combine(Application.persistentDataPath,"Saves"):
            Path.Combine(Path.GetDirectoryName(SavePath),Path.GetFileNameWithoutExtension(SavePath)+"-slots")),manualSaveSlotCount);
        RectTransform systemMenu;
        string saveMenuMessage;
        public bool IsSystemMenuOpen => systemMenu!=null;

        public bool CanSaveGame(out string reason)
        {
            if(SceneManager.GetActiveScene().name!="ShopPrototype") {reason="请返回店铺稳定状态再保存。";return false;}
            if(IsDragging || (NegotiationView!=null && NegotiationView.IsOpen)){reason="请先结束当前拖动或交易操作。";return false;}
            return Session.CanSave(out reason);
        }
        public void OpenSystemMenu()
        {
            CloseSystemMenu();
            systemMenu=Rect(content,"SystemMenu",0,0,1600,1000);Image(systemMenu,new Color(.02f,.035f,.04f),true);
            Label(systemMenu,"SystemTitle",210,120,1180,50,"系统菜单 · 保存 / 读取",30,gold);
            bool canSave=CanSaveGame(out string reason);
            Label(systemMenu,"SaveAvailability",210,190,1180,65,canSave?"当前为安全存档点。读取会替换当前未保存进度。":reason,21,textColor);
            for(int slot=0;slot<=manualSaveSlotCount;slot++)
            {
                int index=slot;float y=280+slot*100;
                bool exists=SaveSlots.Exists(slot);string description=slot==0?"自动槽":"手动槽 "+slot;
                if(exists)
                {
                    try
                    {
                        var saved=JsonUtility.FromJson<ShopSave>(SaveSlots.Read(slot));
                        if(saved==null || saved.schemaVersion!=ShopSave.CurrentSchemaVersion)description+=" · 不兼容存档";
                        else description+=$" · 第 {(saved.turn-1)/12+1} 年 {(saved.turn-1)%12+1} 月 · {(saved.phase==TurnPhase.Closed?"营业结束":"营业准备")}\n"+
                            (DateTimeOffset.TryParse(saved.savedAtUtc,out var time)?time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):"保存时间缺失");
                    }
                    catch(Exception e){description+=" · 无法读取信息："+e.Message;}
                }
                else description+=" · 空槽";
                Label(systemMenu,"SaveSlotInfo_"+slot,210,y,730,85,description,20,textColor);
                if(slot>0)CarryButton(systemMenu,"SaveSlot_"+slot,970,y+10,150,"保存",()=>RequestSaveSlot(index)).interactable=canSave;
                CarryButton(systemMenu,"LoadSlot_"+slot,1140,y+10,150,"读取",()=>RequestLoadSlot(index)).interactable=exists;
            }
            Label(systemMenu,"SaveMenuMessage",210,710,1150,65,saveMenuMessage??"",20,gold);
            CarryButton(systemMenu,"SystemResume",210,825,280,"继续游戏 / Esc",CloseSystemMenu);
            CarryButton(systemMenu,"SystemQuit",1010,825,280,"退出",()=>ShowSaveConfirmation("退出将丢失未保存进度。",()=>{
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            }));
        }
        public void CloseSystemMenu()
        {
            if(systemMenu!=null){systemMenu.gameObject.SetActive(false);Destroy(systemMenu.gameObject);}
            systemMenu=null;
        }
        void ShowSaveConfirmation(string message,Action action)
        {
            var blocker=Rect(systemMenu,"SaveConfirmationOverlay",0,0,1600,1000);Image(blocker,new Color(0,0,0,.65f),true);
            var dialog=Rect(blocker,"SaveConfirmation",160,250,1280,400);Image(dialog,panel,true);
            Label(dialog,"Message",45,55,1190,150,message,25,gold);
            CarryButton(dialog,"SaveConfirm",190,290,350,"确认",()=>{action();});
            CarryButton(dialog,"SaveCancel",700,290,350,"取消",OpenSystemMenu);
        }
        public void RequestSaveSlot(int slot)
        {
            if(slot==0)throw new ArgumentException("自动槽由成功推进月份后写入。");
            if(!CanSaveGame(out var reason)){saveMenuMessage=reason;OpenSystemMenu();return;}
            if(SaveSlots.Exists(slot))ShowSaveConfirmation("覆盖此槽的已有存档？",()=>SaveGameSlot(slot));
            else SaveGameSlot(slot);
        }
        void SaveGameSlot(int slot)
        {
            try
            {
                if(!CanSaveGame(out var reason))throw new InvalidOperationException(reason);
                SaveSlots.Write(slot,Session);saveMenuMessage="已保存手动槽 "+slot+"。";
            }
            catch(Exception e){saveMenuMessage="保存失败，原槽保留："+e.Message;}
            OpenSystemMenu();
        }
        public void RequestLoadSlot(int slot)
        {
            if(!SaveSlots.Exists(slot)){saveMenuMessage="空槽不能读取。";OpenSystemMenu();return;}
            ShowSaveConfirmation("读取会替换当前所有未保存进度。是否继续？",()=>LoadGameSlot(slot));
        }
        void LoadGameSlot(int slot)
        {
            ShopCatalog restoredCatalog=null;
            ShopSession restored;
            try
            {
                string json=SaveSlots.Read(slot);var snapshot=JsonUtility.FromJson<ShopSave>(json);
                restoredCatalog=Instantiate(catalog);
                // Rehydrate the already-used graybox ID definitions into the new runtime catalog only.
                if(snapshot!=null && snapshot.hasFurnaceDefinition)AlchemyVerification.AddShopFurnaceDefinition(restoredCatalog);
                else if(snapshot!=null && snapshot.hasAlchemyDefinitions)AlchemyVerification.AddDefinitions(restoredCatalog);
                else if(snapshot!=null && snapshot.hasSpiritDefinitions)SpiritStoneVerification.AddDefinitions(restoredCatalog);
                restored=ShopSession.RestoreSave(restoredCatalog,json);
            }
            catch(Exception e)
            {
                if(restoredCatalog!=null && restoredCatalog!=runtimeCatalog)Destroy(restoredCatalog);
                saveMenuMessage="未读取，当前会话保留："+e.Message;
                OpenSystemMenu();
                return;
            }
            CloseCommissions();CloseTeaNews();CloseTravelConfirmation();CloseCarryPanel();CloseTravelWindow();CloseCarrySelection();CloseStorage();CancelDrag();
            CalendarView.Close();NegotiationView.Close();
            if(runtimeCatalog!=null)Destroy(runtimeCatalog);
            runtimeCatalog=restoredCatalog;catalog=runtimeCatalog;Session=restored;
            selectedId=0;localNotice=null;CalendarMessage=null;saveMenuMessage="已读取，当前进度已替换。";Refresh();
            OpenSystemMenu();
        }
        void AutoSaveAfterAdvance()
        {
            try{SaveSlots.Write(0,Session);}
            catch(Exception e){localNotice="已进入新月份，但自动存档失败："+e.Message;}
        }
    }
}
