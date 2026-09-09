using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform carrySelection,carryWindow;
        int selectedPack;
        Text carryChoice,carryMessage;

        Button CarryButton(Transform parent,string name,float x,float y,float width,string title,Action action)
        {
            var button=MakeButton(name,x,y,width,36,title,action);
            button.transform.SetParent(parent,false);return button;
        }
        public void OpenCarrySelection()
        {
            CancelDrag();
            if(Session.IsCarrying){ShowCarryPanel();return;}
            if(Session.Phase==TurnPhase.Open){localNotice="请先结束营业再选择出门携带物。";Refresh();return;}
            CloseStorage();CloseCarrySelection();selectedPack=0;
            carrySelection=Rect(content,"CarrySelection",0,0,1600,1000);Image(carrySelection,new Color(.02f,.04f,.05f,.95f),true);
            Label(carrySelection,"Title",450,160,700,45,"出门携带 · 选择已有便携储存",27,gold);
            var viewport=Rect(carrySelection,"CarryOptions",450,225,700,350);Image(viewport,panel,true);viewport.gameObject.AddComponent<RectMask2D>();
            var choices=Session.PortableStorage.OrderBy(i=>i.Id).ToArray();
            var rows=Rect(viewport,"Rows",0,0,680,Mathf.Max(350,(choices.Length+1)*48));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.scrollSensitivity=30;
            CarryButton(rows,"CarryNone",8,4,655,"不带背包出门",()=>SelectPack(0));
            for(int i=0;i<choices.Length;i++)
            {
                var item=choices[i];int id=item.Id;
                CarryButton(rows,"CarryOption_"+id,8,52+i*48,655,$"{item.Definition.title} · 实例 #{id} · {item.Definition.storageSize.x}×{item.Definition.storageSize.y}",()=>SelectPack(id));
            }
            carryChoice=Label(carrySelection,"CarryChoice",450,600,700,50,"已选择：不带背包出门",21,textColor);
            CarryButton(carrySelection,"CarryConfirm",450,670,320,"确认携带",ConfirmCarry);
            CarryButton(carrySelection,"CarryCancel",800,670,320,"取消",CloseCarrySelection);
            Label(carrySelection,"Scope",450,735,700,60,"本界面仅配置携带状态；地点与行程尚未接入。取消不改变任何物品。",18,muted);
        }
        void SelectPack(int id){selectedPack=id;carryChoice.text=id==0?"已选择：不带背包出门":$"已选择：{Session.Find(id).Definition.title} · 实例 #{id}";}
        void ConfirmCarry()
        {
            if(!Session.BeginCarrying(selectedPack)){carryChoice.text=Session.Message;return;}
            CloseCarrySelection();ShowCarryPanel();
        }
        void CloseCarrySelection(){if(carrySelection!=null){carrySelection.gameObject.SetActive(false);Destroy(carrySelection.gameObject);}carrySelection=null;}
        void ShowCarryPanel()
        {
            CloseCarryPanel();CloseStorage();
            carryWindow=Rect(content,"CarryPanel",590,140,930,450);Image(carryWindow,panel,true);
            Label(carryWindow,"Title",16,10,740,34,"外出携带 · 左手 / 便携储存 / 右手",23,gold);
            CarryButton(carryWindow,"CarryHide",760,8,150,"收起",()=>{CloseCarryPanel();Refresh();});
            AddHandGrid(ContainerId.LeftHand,24,"左手 · 一件");AddHandGrid(ContainerId.RightHand,780,"右手 · 一件");
            if(Session.CarriedPackId!=0)
            {
                var box=Session.Find(Session.CarriedPackId);openStorageId=box.Id;var size=box.Definition.storageSize;
                Label(carryWindow,"CarriedPack",180,57,580,36,$"{box.Definition.title} · 原实例 #{box.Id}",19,gold);
                float cell=Mathf.Min(48,250f/Mathf.Max(size.x,size.y));
                var grid=Rect(carryWindow,"CarriedInterior",280,100,size.x*cell,size.y*cell);grids[ContainerId.Interior]=grid;cellSizes[ContainerId.Interior]=cell;
                for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)Image(Rect(grid,$"Slot_{x}_{y}",x*cell,y*cell,cell-2,cell-2),line);
            }
            else Label(carryWindow,"NoPack",270,130,410,80,"不带背包\n只有左右手，没有额外储存格子。",22,muted);
            carryMessage=Label(carryWindow,"CarryMessage",20,355,680,70,"可从仓库放入手持位，或整理预装内容。\n仅携带基础验证，未执行地点行程。",18,muted);
            CarryButton(carryWindow,"CarryReturn",725,390,180,"结束携带 / 返回",()=>
            {
                CancelDrag();if(!Session.EndCarrying()){carryMessage.text=Session.Message;return;}
                CloseCarryPanel();Refresh();
            });
            Refresh();
        }
        void AddHandGrid(ContainerId id,float x,string title)
        {
            Label(carryWindow,id+"Title",x,75,140,36,title,20,gold);
            var grid=Rect(carryWindow,id+"Slot",x,130,120,120);Image(grid,line,true);grids[id]=grid;cellSizes[id]=120;
        }
        void CloseCarryPanel()
        {
            bool hadPanel=carryWindow!=null;
            CancelDrag();if(carryWindow!=null){carryWindow.gameObject.SetActive(false);Destroy(carryWindow.gameObject);}carryWindow=null;
            grids.Remove(ContainerId.LeftHand);grids.Remove(ContainerId.RightHand);
            if(hadPanel || (Session!=null && Session.IsCarrying)){grids.Remove(ContainerId.Interior);cellSizes.Remove(ContainerId.Interior);openStorageId=0;selectedId=0;}
            cellSizes.Remove(ContainerId.LeftHand);cellSizes.Remove(ContainerId.RightHand);
        }
    }
}
