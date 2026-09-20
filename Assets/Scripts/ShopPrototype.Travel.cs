using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform travelWindow,travelConfirmation;
        Text locationNotice;

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
                Label(travelWindow,"LocationTitle",100,110,1300,50,location.title+(location.id=="baishitang"?" · 领取区（可滚动）":" · 地点物品区"),29,gold);
                var size=Session.GridSize(ContainerId.Location);float cell=Mathf.Min(48,600f/size.x);
                var viewport=Rect(travelWindow,"LocationViewport",100,180,size.x*cell,235);
                Image(viewport,panel,true);
                viewport.gameObject.AddComponent<RectMask2D>();
                var grid=Rect(viewport,"LocationGrid",0,0,size.x*cell,size.y*cell);
                var scroll=viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport=viewport;scroll.content=grid;scroll.horizontal=false;scroll.scrollSensitivity=30;
                scroll.movementType=ScrollRect.MovementType.Clamped;
                grids[ContainerId.Location]=grid;cellSizes[ContainerId.Location]=cell;
                for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)Image(Rect(grid,$"Slot_{x}_{y}",x*cell,y*cell,cell-2,cell-2),line);
                if(!Session.IsAtAlchemy)Label(travelWindow,"LocationDescription",760,170,720,120,location.id=="tingfeng-teahouse"?Session.DescribeTeaNews(Session.LatestTeaVisit):location.preserveItemsBetweenVisits?
                    "长期地点：区域物品跨访问保留。\n物品不会自动送回店铺，请手动收入随身区域。":
                    "临时地点：离开时清理未带走物品。\n请拖入下方左右手或所带背包。",22,textColor);
                locationNotice=Label(travelWindow,"LocationNotice",100,425,1370,40,"",18,gold);
                if(!Session.IsAtAlchemy)CarryButton(travelWindow,"TravelLeave",1100,310,390,"离开 · 返回地点选择",RequestLocationLeave);
                if(Session.IsAtAlchemy)BuildAlchemyPanel();
                if(location.id=="tingfeng-teahouse")CarryButton(travelWindow,"TeaLocationNews",760,310,310,"查看坊市消息",OpenTeaNews);
                if(location.id=="baishitang")
                {
                    CarryButton(travelWindow,"CommissionOpen",760,310,310,Session.CommissionLimitReached?"查看已完成委托":"查看本月委托",OpenCommissions);
                    locationNotice.text=Session.CommissionResult;
                }
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

        void RequestLocationLeave()
        {
            CancelDrag();
            if(!Session.LocationLeaveNeedsConfirmation){FinishLocationLeave(false);return;}
            travelConfirmation=Rect(content,"LocationLeaveConfirmation",0,0,1600,1000);
            Image(travelConfirmation,new Color(.02f,.03f,.04f,.96f),true);
            Label(travelConfirmation,"Question",380,300,900,140,$"地点还有 {Session.CurrentLocationItemCount} 件未带走物品。\n确认离开将清理它们，无法取回。\n左右手和所带背包内的物品不会被清理。",27,gold);
            CarryButton(travelConfirmation,"LocationLeaveConfirm",380,490,390,"确认离开并清理",()=>FinishLocationLeave(true));
            CarryButton(travelConfirmation,"LocationLeaveCancel",820,490,390,"留下整理",CloseTravelConfirmation);
        }

        void FinishLocationLeave(bool confirmed)
        {
            if(!Session.LeaveLocation(confirmed))return;
            CloseTravelConfirmation();ShowTravelWindow();
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
            alchemyStatus=null;alchemyDebug=null;alchemyRecipeText=null;
            grids.Remove(ContainerId.AlchemyFuel);grids.Remove(ContainerId.AlchemyOutput);
            cellSizes.Remove(ContainerId.AlchemyFuel);cellSizes.Remove(ContainerId.AlchemyOutput);
            locationNotice=null;grids.Remove(ContainerId.Location);cellSizes.Remove(ContainerId.Location);
        }
    }
}
