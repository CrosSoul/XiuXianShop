using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    public enum ContainerId { Storage, Display, Counter }
    public enum DayPhase { Preparation, Open, Closed }
    public enum ItemOwner { Player, Customer }
    public enum TradeDirection { CustomerBuys, CustomerSells }

    public sealed class GridItem
    {
        public int Id { get; internal set; }
        public ItemDefinition Definition { get; internal set; }
        public ContainerId Container { get; internal set; }
        public ItemOwner Owner { get; internal set; }
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int Rotation { get; internal set; }
        public bool Flipped { get; internal set; }
        public Vector2Int[] Cells => Definition.Shape(Rotation, Flipped);
    }

    public sealed class TradeOffer
    {
        public TradeDirection Direction { get; internal set; }
        public string CustomerName { get; internal set; }
        public int ItemId { get; internal set; }
        public int Price { get; internal set; }
        internal int originX, originY, originRotation;
        internal bool originFlipped;
    }

    // One small session owns the grid and economic mutations. UI never edits inventory directly.
    // Failed operations validate before committing: no money or items are consumed on failure.
    public sealed class ShopSession
    {
        readonly ShopCatalog catalog;
        readonly List<GridItem> items = new List<GridItem>();
        readonly Queue<(TradeDirection direction, int itemId, string definitionId)> queue = new Queue<(TradeDirection,int,string)>();
        int nextId = 1;
        public IReadOnlyList<GridItem> Items => items;
        public ShopCatalog Catalog => catalog;
        public int Money { get; private set; }
        public int Day { get; private set; } = 1;
        public int Rent { get; private set; }
        public int RentDebt { get; private set; }
        public int NextRentDay => ((Day - 1) / catalog.rentPeriod + 1) * catalog.rentPeriod;
        public DayPhase Phase { get; private set; } = DayPhase.Preparation;
        public TradeOffer Offer { get; private set; }
        public int RemainingCustomers => queue.Count;
        public int ServedToday { get; private set; }
        public int Crafted { get; private set; }
        public int Purchases { get; private set; }
        public int Sales { get; private set; }
        public bool HasFurnace => true;
        public string Message { get; private set; } = "先整理物品，再把收购牌和商品放入展示柜。丹炉已备好。";

        public ShopSession(ShopCatalog catalog, bool seed = true)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Money = catalog.startingMoney;
            Rent = catalog.firstRent;
            if (seed)
                foreach (string id in catalog.startingItems)
                {
                    var item = NewItem(catalog.Find(id), ItemOwner.Player);
                    if (!FindSpace(item, ContainerId.Storage, out int x, out int y)) throw new InvalidOperationException("Starter inventory does not fit.");
                    Place(item,ContainerId.Storage,x,y,0,false); items.Add(item);
                }
        }

        public static Vector2Int Size(ContainerId container)
            => container == ContainerId.Storage ? new Vector2Int(10,7) : container == ContainerId.Display ? new Vector2Int(6,4) : new Vector2Int(5,4);
        public GridItem Find(int id) => items.FirstOrDefault(i=>i.Id==id);
        public IEnumerable<GridItem> In(ContainerId container) => items.Where(i=>i.Container==container);
        public int Occupied(ContainerId container) => In(container).Sum(i=>i.Cells.Length);
        GridItem NewItem(ItemDefinition def, ItemOwner owner) => new GridItem{Id=nextId++, Definition=def, Owner=owner};
        bool Fail(string message) { Message=message; return false; }
        bool Success(string message) { Message=message; return true; }

        public bool Fits(GridItem item, ContainerId target, int x, int y, int rotation, bool flipped, ISet<int> ignored = null)
        {
            Vector2Int size=Size(target);
            var cells=item.Definition.Shape(rotation,flipped).Select(p=>p+new Vector2Int(x,y)).ToArray();
            if (cells.Any(p=>p.x<0 || p.y<0 || p.x>=size.x || p.y>=size.y)) return false;
            var occupied=new HashSet<Vector2Int>(In(target).Where(i=>i.Id!=item.Id && (ignored==null || !ignored.Contains(i.Id)))
                .SelectMany(i=>i.Cells.Select(p=>p+new Vector2Int(i.X,i.Y))));
            return !cells.Any(occupied.Contains);
        }

        public bool FindSpace(GridItem item, ContainerId target, out int x, out int y, ISet<int> ignored=null)
        {
            var size=Size(target);
            for (y=0;y<size.y;y++) for (x=0;x<size.x;x++) if (Fits(item,target,x,y,item.Rotation,item.Flipped,ignored)) return true;
            x=y=0; return false;
        }

        public bool CanMove(int id, ContainerId target, int x, int y, int rotation, bool flipped, out string reason)
        {
            var item=Find(id);
            reason="";
            if (item==null) { reason="物品已不在这里。"; return false; }
            if (item.Owner==ItemOwner.Customer && target!=ContainerId.Counter) { reason="这是顾客的货物；确认收购后才属于你。"; return false; }
            if (item.Owner==ItemOwner.Player)
            {
                bool tradeItem=Offer!=null && Offer.Direction==TradeDirection.CustomerBuys && Offer.ItemId==id;
                if (target==ContainerId.Counter && !tradeItem) { reason="柜台只接收当前顾客要买的那件商品。"; return false; }
                if (Phase==DayPhase.Open && (target==ContainerId.Display || item.Container==ContainerId.Display || item.Container==ContainerId.Counter))
                {
                    bool toCounter=tradeItem && target==ContainerId.Counter;
                    bool returning=tradeItem && item.Container==ContainerId.Counter && target==ContainerId.Display && x==Offer.originX && y==Offer.originY && rotation==Offer.originRotation && flipped==Offer.originFlipped;
                    if (!toCounter && !returning) { reason="营业中展示已锁定；只能将当前指定商品摆上柜台。结束营业后可重新配置。"; return false; }
                }
            }
            if (!Fits(item,target,x,y,rotation,flipped)) { reason="放不下：不能越界，也不能覆盖其他物品。"; return false; }
            return true;
        }

        public bool Move(int id, ContainerId target, int x, int y, int rotation, bool flipped)
        {
            if (!CanMove(id,target,x,y,rotation,flipped,out string reason)) return Fail(reason);
            var item=Find(id); Place(item,target,x,y,rotation,flipped);
            return Success($"已摆放 {item.Definition.title}。" );
        }
        static void Place(GridItem item, ContainerId target, int x, int y, int rotation, bool flipped)
        { item.Container=target; item.X=x; item.Y=y; item.Rotation=((rotation%4)+4)%4; item.Flipped=flipped; }

        public bool BeginBusiness()
        {
            if (Phase!=DayPhase.Preparation) return Fail("今天已经营业过；闭店后睡觉进入下一天。" );
            var display=In(ContainerId.Display).ToArray();
            if (display.Length==0) return Fail("展示柜是空的。先放入商品或收购牌。" );
            queue.Clear(); ServedToday=0;
            if (display.Any(i=>i.Definition.procurementSign))
                for(int n=0;n<2;n++)
                { queue.Enqueue((TradeDirection.CustomerSells,0,catalog.herbId)); queue.Enqueue((TradeDirection.CustomerSells,0,catalog.dewId)); }
            foreach(var item in display.Where(i=>!i.Definition.procurementSign)) queue.Enqueue((TradeDirection.CustomerBuys,item.Id,null));
            Phase=DayPhase.Open;
            return NextCustomer();
        }

        public bool NextCustomer()
        {
            if (Phase!=DayPhase.Open) return Fail("请先开始营业。" );
            if (Offer!=null) return Fail("先完成或拒绝当前交易。" );
            if (queue.Count==0) return Success("今日顾客已接待完。可以结束营业，炼丹整理，再睡觉。" );
            var request=queue.Peek();
            GridItem item;
            if (request.direction==TradeDirection.CustomerSells)
            {
                item=NewItem(catalog.Find(request.definitionId),ItemOwner.Customer);
                if (!FindSpace(item,ContainerId.Counter,out int x,out int y)) return Fail("柜台没有空间接待供货。" );
                Place(item,ContainerId.Counter,x,y,0,false); items.Add(item);
            }
            else
            {
                item=Find(request.itemId);
                if (item==null || item.Container!=ContainerId.Display) return Fail("展示商品缺失；请结束营业重新配置。" );
            }
            queue.Dequeue();
            Offer=new TradeOffer{Direction=request.direction,ItemId=item.Id,Price=request.direction==TradeDirection.CustomerSells?item.Definition.purchasePrice:item.Definition.salePrice,
                CustomerName=request.direction==TradeDirection.CustomerSells?"采药客 · 阿青":"散修 · 云生", originX=item.X,originY=item.Y,originRotation=item.Rotation,originFlipped=item.Flipped};
            return Success(request.direction==TradeDirection.CustomerSells ? "供货已上柜台。蓝色归属标记代表顾客物品，确认后收进背包。" : "顾客看中了展示商品。将指定商品拖到柜台，再确认出售。" );
        }

        public bool StageSale()
        {
            if (Offer==null || Offer.Direction!=TradeDirection.CustomerBuys) return Fail("当前没有待摆放的出售商品。" );
            var item=Find(Offer.ItemId);
            if (!FindSpace(item,ContainerId.Counter,out int x,out int y)) return Fail("柜台空间不足，请调整商品朝向。" );
            return Move(item.Id,ContainerId.Counter,x,y,item.Rotation,item.Flipped);
        }

        public bool AcceptTrade()
        {
            if (Phase!=DayPhase.Open || Offer==null) return Fail("当前没有可结算的交易。" );
            var item=Find(Offer.ItemId);
            if (item==null) return Fail("交易物品缺失。" );
            string title=item.Definition.title;
            int price=Offer.Price;
            if (Offer.Direction==TradeDirection.CustomerSells)
            {
                if (item.Owner!=ItemOwner.Customer || item.Container!=ContainerId.Counter) return Fail("供货归属不正确。" );
                if (Money<price) return Fail($"灵石不足：需要 {price}，当前 {Money}。" );
                if (!FindSpace(item,ContainerId.Storage,out int x,out int y)) return Fail("背包空间不足。先整理背包，或在柜台旋转货物后再收购；不会扣款。" );
                Money-=price; item.Owner=ItemOwner.Player; Place(item,ContainerId.Storage,x,y,item.Rotation,item.Flipped); Purchases++;
            }
            else
            {
                if (item.Owner!=ItemOwner.Player || item.Container!=ContainerId.Counter) return Fail("请先将顾客指定的展示商品摆上柜台。" );
                Money+=price; items.Remove(item); Sales++;
            }
            bool bought=Offer.Direction==TradeDirection.CustomerSells;
            Offer=null; ServedToday++;
            return Success($"交易完成：{(bought?"获得":"售出")} {title}，灵石 {(bought?"−":"+")}{price}。顾客已离开。" );
        }

        public bool RejectTrade()
        {
            if (Offer==null) return Fail("当前没有需要拒绝的交易。" );
            CancelOffer(); ServedToday++;
            return Success("已拒绝交易。玩家物品和灵石未减少，顾客已离开。" );
        }
        void CancelOffer()
        {
            if(Offer==null) return;
            var item=Find(Offer.ItemId);
            if(item!=null)
            {
                if(Offer.Direction==TradeDirection.CustomerSells) items.Remove(item);
                else Place(item,ContainerId.Display,Offer.originX,Offer.originY,Offer.originRotation,Offer.originFlipped);
            }
            Offer=null;
        }
        public bool EndBusiness()
        {
            if(Phase!=DayPhase.Open) return Fail("当前没有营业。" );
            CancelOffer(); queue.Clear(); Phase=DayPhase.Closed;
            return Success("今日已闭店，未成交商品已归位。现在可以整理、炼丹并睡觉。" );
        }
        public bool Craft()
        {
            var herb=In(ContainerId.Storage).FirstOrDefault(i=>i.Owner==ItemOwner.Player && i.Definition.id==catalog.herbId);
            var dew=In(ContainerId.Storage).FirstOrDefault(i=>i.Owner==ItemOwner.Player && i.Definition.id==catalog.dewId);
            if(herb==null || dew==null) return Fail("背包中需要 1 凝气草 + 1 灵露。可展示收购牌获得稳定供货。" );
            var product=NewItem(catalog.Find(catalog.productId),ItemOwner.Player);
            var consumed=new HashSet<int>{herb.Id,dew.Id};
            if(!FindSpace(product,ContainerId.Storage,out int x,out int y,consumed)) return Fail("没有连续空间容纳成品；请先整理。原料未消耗。" );
            items.Remove(herb); items.Remove(dew); Place(product,ContainerId.Storage,x,y,0,false); items.Add(product); Crafted++;
            int cost=herb.Definition.purchasePrice+dew.Definition.purchasePrice;
            int sale=product.Definition.salePrice;
            return Success($"炼丹完成：凝气草 + 灵露 → 回气丹，已放入背包。成本 {cost}，出售 {sale}，毛利 {sale-cost} 灵石。" );
        }
        public bool Sleep()
        {
            if(Phase!=DayPhase.Closed) return Fail("请先开始并结束当天营业，再睡觉。" );
            int due=RentDebt;
            if(Day%catalog.rentPeriod==0) { due+=Rent; Rent=(int)Math.Ceiling(Rent*1.05); }
            int paid=Math.Min(Money,due); Money-=paid; RentDebt=due-paid;
            Day++; Phase=DayPhase.Preparation; ServedToday=0; queue.Clear();
            return Success($"第 {Day} 天清晨。库存、灵石和丹炉已保留。"+(due>0?$" 支付房租 {paid}，待付 {RentDebt}。":"今天重新配置展示，再次经营。"));
        }
        public string ValidateState()
        {
            if(Money<0 || RentDebt<0) return "Negative money or debt";
            if(items.Select(i=>i.Id).Distinct().Count()!=items.Count) return "Duplicate IDs";
            foreach(var item in items)
            {
                if(!Fits(item,item.Container,item.X,item.Y,item.Rotation,item.Flipped)) return "Invalid grid placement: "+item.Id;
                if(item.Owner==ItemOwner.Customer && (Offer==null || Offer.ItemId!=item.Id || item.Container!=ContainerId.Counter)) return "Orphaned customer item";
            }
            return null;
        }
    }
}
