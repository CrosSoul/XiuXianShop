using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        public TravelLocation CurrentLocation => catalog.travelLocations.Single(l=>l.id==CurrentLocationId);
        public int CurrentLocationItemCount => In(ContainerId.Location).Count() + (IsAtAlchemy?items.Count(i=>IsAlchemyArea(i.Container)):0);
        public bool LocationLeaveNeedsConfirmation => CurrentLocationId!=null &&
            !CurrentLocation.preserveItemsBetweenVisits && CurrentLocationItemCount>0;

        Vector2Int LocationGridSize(string id)
        {
            var size=catalog.travelLocations.Single(l=>l.id==id).itemGridSize;
            if(id==AlchemyLocationId)size=catalog.alchemy.preparationSize;
            if(id=="baishitang")size.y=System.Math.Max(size.y,commissionGridHeight);
            return size;
        }

        // Shared by destination rewards and isolated validation fixtures.
        public bool AddLocationItem(string definitionId)
        {
            if(!IsTravelling || CurrentLocationId==null)return Fail("请先进入地点。");
            var definition=catalog.Find(definitionId);
            var item=new GridItem {Id=nextId,Definition=definition,Owner=ItemOwner.Player,
                SpiritUnits=definition.spiritResource?.CapacityUnits??0};
            if(!StoragePlacementAllowed(item,ContainerId.Location,0,out var reason))return Fail(reason);
            var size=GridSize(ContainerId.Location);
            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
            {
                if(!Fits(item,ContainerId.Location,x,y,0,false))continue;
                Place(item,ContainerId.Location,x,y,0,false);item.LocationId=CurrentLocationId;
                items.Add(item);nextId++;
                return Success("物品已放入地点区域，请手动收入随身区域。");
            }
            return Fail("地点区域空间不足，未生成物品。");
        }

        string ValidateLocationItem(GridItem item)
        {
            if(item.Container!=ContainerId.Location)
                return string.IsNullOrEmpty(item.LocationId)?null:"Non-location item has location ID";
            var location=catalog.travelLocations.SingleOrDefault(l=>l.id==item.LocationId);
            if(location==null || (!location.preserveItemsBetweenVisits && item.LocationId!=CurrentLocationId))return "Invalid location item state";
            if(item.ForSale)return "Location item cannot belong to a shop customer";
            return null;
        }
    }
}
