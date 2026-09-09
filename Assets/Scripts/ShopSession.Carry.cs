using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        public bool IsCarrying { get; private set; }
        public int CarriedPackId { get; private set; }
        public static bool IsHand(ContainerId area)=>area==ContainerId.LeftHand || area==ContainerId.RightHand;
        public IEnumerable<GridItem> PortableStorage => items.Where(i=>i.Owner==ItemOwner.Player && i.Definition.category==ItemCategory.PortableContainer);

        public bool BeginCarrying(int packId)
        {
            if(IsCarrying || Phase==TurnPhase.Open)return Fail("请在营业外选择携带物；携带期间不能更换背包。" );
            var pack=packId==0?null:Find(packId);
            if(packId!=0 && (pack==null || pack.Owner!=ItemOwner.Player || pack.Container!=ContainerId.Storage || pack.Definition.category!=ItemCategory.PortableContainer))
                return Fail("请选择仓库中实际拥有的一件便携储存。" );
            if(pack!=null && (pack.Definition.storageSize.x<1 || pack.Definition.storageSize.y<1))return Fail("这件便携储存的内部尺寸尚未配置。" );
            IsCarrying=true;CarriedPackId=packId;
            if(pack!=null)pack.Container=ContainerId.CarriedPack;
            return Success(pack==null?"不带背包：左右手各可携带一件物品。":"已携带原背包，预装内容保持不变。" );
        }

        public bool EndCarrying()
        {
            if(!IsCarrying)return Fail("当前没有携带中的物品。" );
            var carried=items.Where(i=>IsHand(i.Container) || i.Container==ContainerId.CarriedPack).ToArray();
            var occupied=new HashSet<Vector2Int>(In(ContainerId.Storage).SelectMany(i=>i.Cells.Select(p=>p+new Vector2Int(i.X,i.Y))));
            var positions=new Dictionary<int,Vector2Int>();var size=Size(ContainerId.Storage);
            foreach(var item in carried)
            {
                bool found=false;
                for(int y=0;y<size.y && !found;y++)for(int x=0;x<size.x && !found;x++)
                {
                    var cells=item.Cells.Select(p=>p+new Vector2Int(x,y)).ToArray();
                    if(cells.Any(p=>p.x>=size.x || p.y>=size.y || occupied.Contains(p)))continue;
                    positions.Add(item.Id,new Vector2Int(x,y));occupied.UnionWith(cells);found=true;
                }
                if(!found)return Fail("仓库没有足够合法空间，暂不能返回；携带物及内容全部保留，请先整理仓库。" );
            }
            foreach(var item in carried){var p=positions[item.Id];Place(item,ContainerId.Storage,p.x,p.y,item.Rotation,item.Flipped);}
            IsCarrying=false;CarriedPackId=0;
            return Success("携带物已返回仓库，实例与内容保持不变。" );
        }
    }
}
