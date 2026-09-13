using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    [Serializable]
    public sealed class TravelLocation
    {
        public string id;
        public string title;
        public bool initiallyUnlocked;
        [UnityEngine.Tooltip("地点物品区的原型格子尺寸，不代表职业设备容量。")]
        public UnityEngine.Vector2Int itemGridSize = new UnityEngine.Vector2Int(6,4);
        [UnityEngine.Tooltip("长期地点保留物品状态；普通地点离开时清理。不是种植或养殖实现。")]
        public bool preserveItemsBetweenVisits;
    }

    public sealed partial class ShopSession
    {
        readonly HashSet<string> unlockedLocations = new HashSet<string>();
        readonly HashSet<string> visitedLocations = new HashSet<string>();
        public bool HasTravelledThisTurn { get; private set; }
        public bool IsTravelling { get; private set; }
        public string CurrentLocationId { get; private set; }
        public IEnumerable<TravelLocation> UnlockedLocations => catalog.travelLocations.Where(l=>unlockedLocations.Contains(l.id));
        public int NextLocationStaminaCost => visitedLocations.Count==0 ? catalog.firstLocationStaminaCost : catalog.extraLocationStaminaCost;
        public bool HasVisitedLocation(string id) => visitedLocations.Contains(id);
        public bool CanReturnWithoutConfirmation => !UnlockedLocations.Any(l=>CanEnterLocation(l.id,out _));

        void InitializeTravel()
        {
            if(catalog.firstLocationStaminaCost<0 || catalog.extraLocationStaminaCost<0 ||
                catalog.travelLocations.Any(l=>string.IsNullOrWhiteSpace(l.id) || l.itemGridSize.x<1 || l.itemGridSize.y<1) ||
                catalog.travelLocations.Select(l=>l.id).Distinct().Count()!=catalog.travelLocations.Length)
                throw new ArgumentException("外出地点配置无效。");
            foreach(var location in catalog.travelLocations.Where(l=>l.initiallyUnlocked))unlockedLocations.Add(location.id);
        }

        public bool UnlockLocation(string id)
        {
            if(!catalog.travelLocations.Any(l=>l.id==id))return Fail("地点不存在。");
            unlockedLocations.Add(id);
            return Success("地点已解锁。");
        }

        public bool BeginTravel()
        {
            if(HasTravelledThisTurn)return Fail("本回合已外出，回店后不能再次出发。");
            if(!IsCarrying || Phase==TurnPhase.Open)return Fail("请在营业外完成携带准备后出发。");
            HasTravelledThisTurn=true;IsTravelling=true;CurrentLocationId=null;visitedLocations.Clear();
            return Success("请选择已解锁地点；进入地点时才扣除体力。");
        }

        public bool CanEnterLocation(string id,out string reason)
        {
            if(!IsTravelling || CurrentLocationId!=null){reason="请先返回地点选择界面。";return false;}
            if(!unlockedLocations.Contains(id)){reason="地点尚未解锁。";return false;}
            if(visitedLocations.Contains(id)){reason="本次已访问";return false;}
            return CanSpendStamina(NextLocationStaminaCost,out reason);
        }

        public bool EnterLocation(string id)
        {
            if(!CanEnterLocation(id,out var reason))return Fail(reason);
            Stamina-=NextLocationStaminaCost;
            visitedLocations.Add(id);CurrentLocationId=id;
            return Success("已抵达"+catalog.travelLocations.Single(l=>l.id==id).title+"；本次访问体力已结算。");
        }

        public bool LeaveLocation(bool confirmed=false)
        {
            if(!IsTravelling || CurrentLocationId==null)return Fail("当前不在地点内。");
            if(LocationLeaveNeedsConfirmation && !confirmed)return Fail("地点仍有未带走物品，确认离开后将清理这些物品。");
            if(!CurrentLocation.preserveItemsBetweenVisits)
                items.RemoveAll(i=>i.Container==ContainerId.Location && i.LocationId==CurrentLocationId);
            CurrentLocationId=null;
            return Success("已返回地点选择界面；携带物与剩余体力保持不变。");
        }

        public bool ReturnToShop(bool confirmed=false)
        {
            if(!IsTravelling || CurrentLocationId!=null)return Fail("请先离开地点再回店。");
            if(!confirmed && !CanReturnWithoutConfirmation)return Fail("仍有可前往地点，请确认是否回店。");
            // Return is independent of warehouse space. Unloading uses the existing carry panel.
            IsTravelling=false;
            return Success("已回店。本回合不能再次外出；可整理并结束携带，继续店内活动。");
        }

        bool IsTravelArea(ContainerId area,int storageId) => IsHand(area) ||
            (area==ContainerId.Location && CurrentLocationId!=null) ||
            (area==ContainerId.Interior && storageId==CarriedPackId && CarriedPackId!=0);
    }
}
