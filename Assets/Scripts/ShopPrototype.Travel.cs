using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform travelWindow,travelConfirmation;

        void StartTravel()
        {
            CancelDrag();
            if(!Session.BeginTravel()){carryMessage.text=Session.Message;return;}
            ShowTravelWindow();
        }

        void ShowTravelWindow()
        {
            CloseCarryPanel();CloseTravelWindow();
            travelWindow=Rect(content,"TravelWindow",0,0,1600,1000);
            Image(travelWindow,new Color(.035f,.06f,.07f),true);
            Label(travelWindow,"TravelTitle",100,40,1350,55,$"{Session.DateLabel} · 外出 · 体力 {Session.Stamina}/{Session.MaximumStamina}",29,gold);
            if(Session.CurrentLocationId==null)
            {
                Label(travelWindow,"TravelHint",100,108,1350,46,$"选择地点 · 本次进入消耗 {Session.NextLocationStaminaCost} 体力 · 灰色地点暂不可前往",21,textColor);
                var viewport=Rect(travelWindow,"LocationList",100,175,1000,265);Image(viewport,panel,true);
                viewport.gameObject.AddComponent<RectMask2D>();
                var locations=Session.UnlockedLocations.ToArray();
                var rows=Rect(viewport,"Rows",0,0,980,Mathf.Max(265,locations.Length*64));
                var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.scrollSensitivity=30;
                for(int i=0;i<locations.Length;i++)
                {
                    var location=locations[i];
                    bool available=Session.CanEnterLocation(location.id,out var reason);
                    var button=CarryButton(rows,"TravelLocation_"+location.id,15,12+i*64,940,
                        location.title+" · "+(available?$"消耗 {Session.NextLocationStaminaCost} 体力":reason),()=>
                        {CancelDrag();if(Session.EnterLocation(location.id))ShowTravelWindow();});
                    button.interactable=available;
                }
                if(locations.Length==0)Label(rows,"NoLocations",20,20,920,70,"尚无已解锁地点，可直接回店。",23,muted);
                CarryButton(travelWindow,"TravelReturn",1160,185,330,"回店铺",RequestTravelReturn);
            }
            else
            {
                var location=Session.Catalog.travelLocations.Single(l=>l.id==Session.CurrentLocationId);
                Label(travelWindow,"LocationTitle",100,160,1300,50,location.title,32,gold);
                Label(travelWindow,"LocationDescription",100,230,980,110,"已抵达，访问体力已一次结算。\n当前可整理随身物品，或离开返回地点选择。\n此地点的具体事务尚未开放。",23,textColor);
                CarryButton(travelWindow,"TravelLeave",1160,230,330,"离开 · 返回地点选择",()=>
                {CancelDrag();if(Session.LeaveLocation())ShowTravelWindow();});
            }
            ShowCarryPanel();
        }

        void RequestTravelReturn()
        {
            CancelDrag();
            if(Session.CanReturnWithoutConfirmation){FinishTravel(false);return;}
            travelConfirmation=Rect(content,"TravelReturnConfirmation",0,0,1600,1000);
            Image(travelConfirmation,new Color(.02f,.03f,.04f,.96f),true);
            Label(travelConfirmation,"Question",380,320,900,100,"还有体力足够的未访问地点。\n确定回店？回店后本回合不能再次外出。",27,gold);
            CarryButton(travelConfirmation,"TravelReturnConfirm",380,470,390,"确定回店",()=>FinishTravel(true));
            CarryButton(travelConfirmation,"TravelReturnCancel",820,470,390,"继续外出",CloseTravelConfirmation);
        }

        void FinishTravel(bool confirmed)
        {
            if(!Session.ReturnToShop(confirmed))return;
            CloseTravelConfirmation();CloseTravelWindow();ShowCarryPanel();
        }

        void CloseTravelConfirmation()
        {
            if(travelConfirmation!=null){travelConfirmation.gameObject.SetActive(false);Destroy(travelConfirmation.gameObject);}
            travelConfirmation=null;
        }
        void CloseTravelWindow()
        {
            if(travelWindow!=null){travelWindow.gameObject.SetActive(false);Destroy(travelWindow.gameObject);}
            travelWindow=null;
        }
    }
}
