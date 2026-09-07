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
        // ItemId/Price are only used by suppliers. Buyers request a category, never an instance ID.
        public int ItemId { get; internal set; }
        public int Price { get; internal set; }
        public ItemCategory RequestedCategory { get; internal set; }
        public int RemainingBudget { get; internal set; }
    }

    // The same snapshot feeds the opening preview and the day's queue. No random draws in previews.
    public sealed class DisplayAttraction
    {
        public ItemCategory BuyerCategory { get; internal set; }
        public int DisplayValue { get; internal set; }
        public int BudgetTierMinimum { get; internal set; }
        public int MinimumBuyerBudget { get; internal set; }
        public int MaximumBuyerBudget { get; internal set; }
        public float SupplierChance { get; internal set; }
        public bool HasAdvertisement { get; internal set; }
        internal ItemDefinition[] Supplies;
        public string SupplierDescription => HasAdvertisement?string.Join("、",Supplies.Select(d=>d.category).Distinct().Select(ShopCatalog.CategoryName)):"随机商品";
    }

    // One small session owns the grid and economic mutations. UI never edits inventory directly.
    // Failed operations validate before committing: no money or items are consumed on failure.
    public sealed class ShopSession
    {
        public const int DailyCustomerCount = 5;
        readonly ShopCatalog catalog;
        readonly System.Random customerRandom;
        readonly List<GridItem> items = new List<GridItem>();
        readonly Queue<(TradeDirection direction, string definitionId, ItemCategory category, int budget)> queue
            = new Queue<(TradeDirection,string,ItemCategory,int)>();
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
        public DisplayAttraction TodayAttraction { get; private set; }
        public int BuyersToday { get; private set; }
        public int SuppliersToday { get; private set; }
        public bool SupplyAdvertisedToday => TodayAttraction?.HasAdvertisement??false;
        public bool HasFurnace => true;
        public string Message { get; private set; } = "先整理物品，再把收购牌和商品放入展示柜。丹炉已备好。";

        public ShopSession(ShopCatalog catalog, bool seed = true, int? customerSeed = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            customerRandom = customerSeed.HasValue ? new System.Random(customerSeed.Value) : new System.Random();
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

        public DisplayAttraction PreviewAttraction()
        {
            var display=In(ContainerId.Display).Where(i=>i.Owner==ItemOwner.Player).ToArray();
            var dominant=display.Where(i=>IsSaleItem(i.Definition)).GroupBy(i=>i.Definition.category)
                .Select(g=>new {Category=g.Key,Value=g.Sum(i=>i.Definition.salePrice)})
                .OrderByDescending(g=>g.Value).ThenBy(g=>(int)g.Category).FirstOrDefault();
            var advertised=display.Where(i=>i.Definition.procurementSign).Select(i=>i.Definition.advertisedCategory).Distinct().ToArray();
            // Select definitions by the sign's category, not a hard-coded herb/dew schedule.
            var allSupplies=catalog.items.Where(d=>IsSaleItem(d) && d.purchasePrice>0).ToArray();
            var supplies=allSupplies.Where(d=>advertised.Contains(d.category)).ToArray();
            bool hasAdvertisement=supplies.Length>0;
            float supplierChance=Mathf.Clamp01(catalog.baseSupplierChance+(hasAdvertisement?catalog.advertisementSupplierBonus:0)-(dominant!=null?catalog.displayedGoodsBuyerBonus:0));
            if(allSupplies.Length==0) supplierChance=0;
            int value=dominant?.Value??0;
            var tier=(catalog.buyerBudgetTiers??Array.Empty<BuyerBudgetTier>()).Where(t=>t!=null && t.minimumDisplayValue<=value)
                .OrderByDescending(t=>t.minimumDisplayValue).FirstOrDefault();
            int baseBudget=Math.Max(1,tier?.baseBudget??20);
            float variation=Mathf.Clamp(catalog.buyerBudgetVariation,0,.5f);
            return new DisplayAttraction
            {
                BuyerCategory=dominant?.Category??ItemCategory.Unclassified,
                DisplayValue=value,
                BudgetTierMinimum=tier?.minimumDisplayValue??0,
                MinimumBuyerBudget=Math.Max(1,Mathf.CeilToInt(baseBudget*(1-variation))),
                MaximumBuyerBudget=Math.Max(1,Mathf.FloorToInt(baseBudget*(1+variation))),
                SupplierChance=supplierChance,
                HasAdvertisement=hasAdvertisement,
                Supplies=hasAdvertisement?supplies:allSupplies
            };
        }
        static bool IsSaleItem(ItemDefinition definition) => !definition.procurementSign && definition.salePrice>0
            && definition.category!=ItemCategory.Unclassified && definition.category!=ItemCategory.BusinessSign;

        public bool BeginBusiness()
        {
            if (Phase!=DayPhase.Preparation) return Fail("今天已经营业过；闭店后睡觉进入下一天。" );
            var attraction=PreviewAttraction();
            var categories=catalog.items.Where(IsSaleItem).Select(d=>d.category).Distinct().OrderBy(c=>(int)c).ToArray();
            if(categories.Length==0) return Fail("商品配置缺少可售类别，无法生成顾客。请检查 ShopCatalog。" );
            queue.Clear(); ServedToday=0;BuyersToday=0;SuppliersToday=0;
            TodayAttraction=attraction;
            for(int n=0;n<DailyCustomerCount;n++)
            {
                if(customerRandom.NextDouble()<attraction.SupplierChance)
                {
                    var supply=attraction.Supplies[customerRandom.Next(attraction.Supplies.Length)];
                    queue.Enqueue((TradeDirection.CustomerSells,supply.id,ItemCategory.Unclassified,0));
                    SuppliersToday++;
                }
                else
                {
                    var category=attraction.BuyerCategory==ItemCategory.Unclassified?categories[customerRandom.Next(categories.Length)]:attraction.BuyerCategory;
                    int budget=customerRandom.Next(attraction.MinimumBuyerBudget,attraction.MaximumBuyerBudget+1);
                    queue.Enqueue((TradeDirection.CustomerBuys,null,category,budget));
                    BuyersToday++;
                }
            }
            Phase=DayPhase.Open;
            return NextCustomer();
        }

        public bool NextCustomer()
        {
            if (Phase!=DayPhase.Open) return Fail("请先开始营业。" );
            // Departure is explicit, even after a successful sale or with an unfinished basket.
            if (Offer!=null) { CancelOffer(); ServedToday++; }
            if (queue.Count==0) return Success("今日顾客已接待完。可以结束营业，炼丹整理，再睡觉。" );
            var request=queue.Peek();
            GridItem item=null;
            if (request.direction==TradeDirection.CustomerSells)
            {
                item=NewItem(catalog.Find(request.definitionId),ItemOwner.Customer);
                if (!FindSpace(item,ContainerId.Counter,out int x,out int y)) return Fail("柜台没有空间接待供货。" );
                Place(item,ContainerId.Counter,x,y,0,false); items.Add(item);
            }
            queue.Dequeue();
            Offer=new TradeOffer{Direction=request.direction,ItemId=item?.Id??0,Price=item?.Definition.purchasePrice??0,
                CustomerName=request.direction==TradeDirection.CustomerSells?"采药客 · 阿青":"散修 · 云生",
                RequestedCategory=request.category,RemainingBudget=request.budget};
            return Success(request.direction==TradeDirection.CustomerSells ? "供货已上柜台。顾客物品确认收购后才属于你。" : $"顾客求购{ShopCatalog.CategoryName(request.category)}，预算 {request.budget}。可多件或分次出售，随时接待下一位。" );
        }

        public bool StageSale()
        {
            if (Offer==null || Offer.Direction!=TradeDirection.CustomerBuys) return Fail("当前没有待摆放的出售商品。" );
            foreach(var item in items.Where(i=>i.Owner==ItemOwner.Player && i.Container!=ContainerId.Counter && !i.Definition.procurementSign && i.Definition.category==Offer.RequestedCategory))
                if(FindSpace(item,ContainerId.Counter,out int x,out int y)) return Move(item.Id,ContainerId.Counter,x,y,item.Rotation,item.Flipped);
            return Fail("没有可摆放的同类商品，或柜台空间不足。也可以手动拖入任意物品。" );
        }

        public string CounterSaleSummary => string.Join(" + ", In(ContainerId.Counter).Where(i=>i.Owner==ItemOwner.Player)
            .GroupBy(i=>i.Definition).Select(g=>$"{g.Key.title} {g.Key.salePrice}×{g.Count()}={g.Key.salePrice*g.Count()}"));

        public bool CanAcceptTrade(out int total, out string reason)
        {
            total=0; reason="";
            if(Phase!=DayPhase.Open || Offer==null) {reason="当前没有可结算的交易。";return false;}
            if(Offer.Direction==TradeDirection.CustomerSells)
            {
                var item=Find(Offer.ItemId);total=Offer.Price;
                if(item==null || item.Owner!=ItemOwner.Customer || item.Container!=ContainerId.Counter) {reason="顾客货物缺失或归属不正确。";return false;}
                if(Money<total) {reason=$"灵石不足：需要 {total}，当前 {Money}。";return false;}
                if(!FindSpace(item,ContainerId.Storage,out _,out _)) {reason="背包空间不足；先整理或旋转货物，不会扣款。";return false;}
                reason="可收购：付款后货物进入背包。";return true;
            }
            var basket=In(ContainerId.Counter).Where(i=>i.Owner==ItemOwner.Player).ToArray();
            total=basket.Sum(i=>i.Definition.salePrice);
            if(basket.Length==0) {reason="柜台为空，请摆入要出售的物品。";return false;}
            if(basket.Any(i=>i.Definition.procurementSign || i.Definition.salePrice<=0)) {reason="柜台含不可出售的物品，请移出。";return false;}
            var wrong=basket.FirstOrDefault(i=>i.Definition.category!=Offer.RequestedCategory);
            if(wrong!=null) {reason=$"类别不符：{wrong.Definition.title}不是{ShopCatalog.CategoryName(Offer.RequestedCategory)}。";return false;}
            if(total>Offer.RemainingBudget) {reason=$"顾客资金不足：总价 {total}，剩余预算 {Offer.RemainingBudget}。";return false;}
            reason="交易成立，可确认出售柜台上的全部物品。";return true;
        }

        public bool AcceptTrade()
        {
            if(!CanAcceptTrade(out int total,out string reason)) return Fail(reason);
            if(Offer.Direction==TradeDirection.CustomerBuys)
            {
                var basket=In(ContainerId.Counter).Where(i=>i.Owner==ItemOwner.Player).ToArray();
                // Validate the entire basket above, then commit all items and money together.
                foreach(var sold in basket) items.Remove(sold);
                Money+=total;Offer.RemainingBudget-=total;Sales+=basket.Length;
                return Success($"已出售 {basket.Length} 件，收入 {total}。顾客剩余预算 {Offer.RemainingBudget}，可继续交易或点击下一位。" );
            }
            var item=Find(Offer.ItemId);
            string title=item.Definition.title;
            FindSpace(item,ContainerId.Storage,out int x,out int y);
            Money-=total;item.Owner=ItemOwner.Player;Place(item,ContainerId.Storage,x,y,item.Rotation,item.Flipped);Purchases++;
            Offer=null; ServedToday++;
            return Success($"已收购 {title}，支付 {total} 灵石。供货顾客已离开。" );
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
            if(item!=null && item.Owner==ItemOwner.Customer) items.Remove(item);
            Offer=null;
        }
        public bool EndBusiness()
        {
            if(Phase!=DayPhase.Open) return Fail("当前没有营业。" );
            CancelOffer(); queue.Clear(); Phase=DayPhase.Closed;
            return Success("今日已闭店。你的物品保持当前位置，可以继续整理、炼丹并睡觉。" );
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
            Day++; Phase=DayPhase.Preparation; ServedToday=0;TodayAttraction=null;BuyersToday=0;SuppliersToday=0; queue.Clear();
            return Success($"第 {Day} 天清晨。库存、灵石和丹炉已保留。"+(due>0?$" 支付房租 {paid}，待付 {RentDebt}。":"今天重新配置展示，再次经营。"));
        }
        public string ValidateState()
        {
            if(Money<0 || RentDebt<0) return "Negative money or debt";
            if(Offer!=null && Offer.RemainingBudget<0) return "Negative customer budget";
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
