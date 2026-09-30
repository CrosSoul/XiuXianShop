using System;
using System.Linq;

namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        public static ShopSession NewGame(ShopCatalog catalog,string profileId,int? customerSeed=null)
        {
            var content=catalog.authoredContent;
            var profile=content.startProfiles.Single(p=>p.enabled && p.id==profileId);
            if(profile.year<1 || profile.month<1 || profile.month>12 || profile.money<0 || profile.stamina<0 || profile.stamina>catalog.maximumStamina)
                throw new ArgumentException("开局配置年月、余额或体力无效："+profileId);
            // Build privately; callers replace their active Session only after all placement succeeds.
            var session=new ShopSession(catalog,false,customerSeed,initializeDefaults:false);
            session.Turn=checked((profile.year-1)*12+profile.month);
            session.Money=session.OpeningMoney=profile.money;session.Stamina=profile.stamina;
            session.Calendar.Between((profile.year-1)*12+1,profile.year*12);
            foreach(var row in content.startItems.Where(i=>i.enabled && i.profileId==profileId))
            {
                if(row.quantity<1 || (row.area!=ContainerId.Storage && row.area!=ContainerId.Display && row.area!=ContainerId.Counter))
                    throw new ArgumentException("初始物品数量或区域无效："+row.id);
                bool positioned=row.x.hasValue || row.y.hasValue || row.rotation.hasValue;
                if(positioned && !(row.x.hasValue && row.y.hasValue && row.rotation.hasValue))throw new ArgumentException("初始摆位需要完整X/Y/旋转："+row.id);
                for(int n=0;n<row.quantity;n++)
                {
                    var item=session.NewItem(catalog.Find(row.itemId),ItemOwner.Player);
                    if(!session.StoragePlacementAllowed(item,row.area,0,out var reason))throw new ArgumentException("初始物品区域不合法："+row.id+" · "+reason);
                    int x=row.x.value,y=row.y.value,rotation=positioned?row.rotation.value:0;
                    if(positioned)
                    {
                        if(rotation<0 || rotation>3 || !session.Fits(item,row.area,x,y,rotation,false))throw new ArgumentException("初始强制摆位越界或重叠："+row.id);
                    }
                    else if(!session.FindSpace(item,row.area,out x,out y))throw new ArgumentException("初始物品放不下："+row.id);
                    Place(item,row.area,x,y,rotation,false);session.items.Add(item);
                }
            }
            foreach(var row in content.startStates.Where(s=>s.enabled && s.profileId==profileId))
            {
                switch(row.type)
                {
                    case "Location":
                        if(!catalog.travelLocations.Any(l=>l.id==row.stateId))throw new ArgumentException("未知开局地点："+row.stateId);
                        if(row.value)session.unlockedLocations.Add(row.stateId);break;
                    case "Profession":
                        if(row.stateId!="alchemy")throw new ArgumentException("未知职业："+row.stateId);
                        if(row.value)session.SetProgressFlag("profession:"+row.stateId);break;
                    case "Recipe":
                        if(!catalog.alchemy.recipes.Any(r=>r.id==row.stateId))throw new ArgumentException("未知配方："+row.stateId);
                        if(row.value)session.SetProgressFlag("recipe:"+row.stateId);break;
                    case "Flag":
                        if(string.IsNullOrWhiteSpace(row.stateId))throw new ArgumentException("初始Flag为空。");
                        if(row.value)session.SetProgressFlag(row.stateId);break;
                    default:throw new ArgumentException("当前未实现此开局状态类型："+row.type);
                }
            }
            return session;
        }
    }
}
