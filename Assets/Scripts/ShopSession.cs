using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    public enum ContainerId { Storage, Display, Counter, CustomerCounter }
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
        public int? PurchaseValue { get; internal set; }
        public bool ForSale => Owner==ItemOwner.Customer;
        public Vector2Int[] Cells => Definition.Shape(Rotation, Flipped);
    }

    public sealed class TradeOffer
    {
        public TradeDirection Direction { get; internal set; }
        public string CustomerName { get; internal set; }
        // Compatibility accessors for the first remaining supply; direction is an arrival hint,
        // never a restriction on which side of a mixed transaction the customer can use.
        public IReadOnlyList<GridItem> SupplierItems { get; internal set; } = Array.Empty<GridItem>();
        public GridItem SupplierItem => SupplierItems.FirstOrDefault(i=>i.ForSale);
        public int ItemId => SupplierItem?.Id??0;
        internal Func<int> CurrentPrice;
        public int Price => CurrentPrice?.Invoke() ?? 0;
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
        readonly int customerSeedValue;
        int customerDraws;
        readonly List<GridItem> items = new List<GridItem>();
        readonly Dictionary<string, PriceTag> priceTags = new Dictionary<string, PriceTag>();
        readonly Queue<(TradeDirection direction, string[] definitionIds, ItemCategory category, int budget)> queue
            = new Queue<(TradeDirection,string[],ItemCategory,int)>();
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
        public int PricingRevision { get; private set; }
        public MarketCalendar Calendar { get; private set; }
        public int OpeningMoney { get; private set; }
        public int IncomeToday { get; private set; }
        public int ExpensesToday { get; private set; }
        public int BalanceChange => Money - OpeningMoney;
        public string LastCustomerResult { get; private set; } = "尚未接待顾客。";
        public DisplayAttraction TodayAttraction { get; private set; }
        public int BuyersToday { get; private set; }
        public int SuppliersToday { get; private set; }
        public bool SupplyAdvertisedToday => TodayAttraction?.HasAdvertisement??false;
        public bool HasFurnace => true;
        public string Message { get; private set; } = "先整理物品，再把收购牌和商品放入展示柜。丹炉已备好。";

        public ShopSession(ShopCatalog catalog, bool seed = true, int? customerSeed = null, MarketCalendar calendar = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            customerSeedValue=customerSeed??Guid.NewGuid().GetHashCode();
            customerRandom = new System.Random(customerSeedValue);
            Calendar=calendar??new MarketCalendar(catalog.marketEvents,customerSeed??Guid.NewGuid().GetHashCode());
            Calendar.Between(1,14);
            Money = catalog.startingMoney;
            OpeningMoney = Money;
            foreach(var tag in catalog.priceTags ?? Array.Empty<PriceTag>()) SetPriceTag(tag);
            Rent = catalog.firstRent;
            if (seed)
                foreach (string id in catalog.startingItems)
                {
                    var item = NewItem(catalog.Find(id), ItemOwner.Player);
                    if (!FindSpace(item, ContainerId.Storage, out int x, out int y)) throw new InvalidOperationException("Starter inventory does not fit.");
                    Place(item,ContainerId.Storage,x,y,0,false); items.Add(item);
                }
        }

        double DrawCustomerChance() {customerDraws++;return customerRandom.NextDouble();}
        int DrawCustomerNumber(int minimum,int maximum) {customerDraws++;return customerRandom.Next(minimum,maximum);}

        public int? ProjectedRent(int day)
        {
            if(day<Day || day%catalog.rentPeriod!=0)return null;
            int amount=Rent;
            for(int date=NextRentDay;date<day;date+=catalog.rentPeriod) amount=(int)Math.Ceiling(amount*1.05);
            return amount;
        }
        public string CaptureSave()
        {
            if(Phase!=DayPhase.Preparation)throw new InvalidOperationException("仅营业准备阶段可存档；请先闭店并睡觉。");
            return JsonUtility.ToJson(new ShopSave {catalogSignature=Hash128.Compute(JsonUtility.ToJson(catalog)).ToString(),
                day=Day,money=Money,rent=Rent,debt=RentDebt,nextId=nextId,customerSeed=customerSeedValue,customerDraws=customerDraws,
                crafted=Crafted,purchases=Purchases,sales=Sales,calendar=Calendar.Capture(),tags=priceTags.Values.Select(t=>t.Copy()).ToArray(),
                items=items.Select(i=>new SavedShopItem{id=i.Id,definitionId=i.Definition.id,container=i.Container,x=i.X,y=i.Y,
                    rotation=i.Rotation,flipped=i.Flipped,hasPurchaseValue=i.PurchaseValue.HasValue,purchaseValue=i.PurchaseValue??0}).ToArray()},true);
        }
        public static ShopSession RestoreSave(ShopCatalog catalog,string json)
        {
            var save=JsonUtility.FromJson<ShopSave>(json);
            if(save==null || save.version!=1 || save.catalogSignature!=Hash128.Compute(JsonUtility.ToJson(catalog)).ToString())
                throw new ArgumentException("存档版本或商品配置不匹配，当前会话未改变。");
            if(save.day<1 || save.money<0 || save.rent<1 || save.debt<0 || save.nextId<1 || save.items==null || save.tags==null ||
                save.customerDraws<0 || save.customerDraws>10000000)throw new ArgumentException("存档数据不完整或无效。");
            var session=new ShopSession(catalog,false,save.customerSeed,new MarketCalendar(save.calendar));
            session.Day=save.day;session.Money=save.money;session.OpeningMoney=save.money;session.Rent=save.rent;session.RentDebt=save.debt;
            session.nextId=save.nextId;session.Crafted=save.crafted;session.Purchases=save.purchases;session.Sales=save.sales;
            for(int i=0;i<save.customerDraws;i++)session.customerRandom.NextDouble();
            session.customerDraws=save.customerDraws;
            session.priceTags.Clear();
            foreach(var tag in save.tags)if(!session.SetPriceTag(tag))throw new ArgumentException("存档价格标签无效。");
            foreach(var i in save.items)
            {
                if(i==null || i.id<1 || i.id>=save.nextId || !Enum.IsDefined(typeof(ContainerId),i.container) ||
                    i.rotation<0 || i.rotation>3 || (i.hasPurchaseValue && i.purchaseValue<1))throw new ArgumentException("存档物品数据无效。");
                session.items.Add(new GridItem {Id=i.id,Definition=catalog.Find(i.definitionId),Owner=ItemOwner.Player,Container=i.container,
                    X=i.x,Y=i.y,Rotation=i.rotation,Flipped=i.flipped,PurchaseValue=i.hasPurchaseValue?(int?)i.purchaseValue:null});
            }
            string error=session.ValidateState();if(error!=null)throw new ArgumentException("存档库存无效："+error);
            session.Calendar.Between(MarketCalendar.WeekStart(session.Day),MarketCalendar.WeekStart(session.Day)+13);
            session.Message=$"已恢复第 {session.Day} 天营业准备；市场安排与历史购买价值已保留。";
            return session;
        }

        public static Vector2Int Size(ContainerId container)
            => container == ContainerId.Storage ? new Vector2Int(10,7) : container == ContainerId.Display ? new Vector2Int(6,4) : new Vector2Int(5,4);
        public GridItem Find(int id) => items.FirstOrDefault(i=>i.Id==id);
        public IEnumerable<GridItem> In(ContainerId container) => items.Where(i=>i.Container==container);
        public int Occupied(ContainerId container) => In(container).Sum(i=>i.Cells.Length);
        GridItem NewItem(ItemDefinition def, ItemOwner owner) => new GridItem{Id=nextId++, Definition=def, Owner=owner};
        bool Fail(string message) { Message=message; return false; }
        bool Success(string message) { Message=message; return true; }

        public bool SetPriceTag(PriceTag tag)
        {
            if(tag == null || string.IsNullOrWhiteSpace(tag.id) || tag.id == "retail" ||
                float.IsNaN(tag.percent) || float.IsInfinity(tag.percent) || Math.Abs(tag.percent)>100) return false;
            priceTags[tag.id] = tag.Copy(); PricingRevision++; return true;
        }
        public bool RemovePriceTag(string id)
        { if(!priceTags.Remove(id)) return false; PricingRevision++; return true; }
        public PriceQuote Quote(GridItem item)
        {
            var direction = item.Owner == ItemOwner.Player ? TradeDirection.CustomerBuys : TradeDirection.CustomerSells;
            return Quote(item.Definition, direction);
        }
        public PriceQuote Quote(ItemDefinition definition, TradeDirection direction)
        {
            var effective = priceTags.Values.Concat(Calendar.ActiveTags(Day)).Where(t => (t.category == ItemCategory.Unclassified || t.category == definition.category) &&
                (direction == TradeDirection.CustomerBuys ? t.playerSells : t.playerBuys)).Select(t=>t.Copy()).ToList();
            if(direction == TradeDirection.CustomerBuys)
                effective.Insert(0, new PriceTag {id="retail",title="零售加价",percent=catalog.retailMarkup});
            return new PriceQuote(definition, effective);
        }
        public PriceQuote Estimate(GridItem item) => item.Container == ContainerId.Counter || item.ForSale ? Quote(item) :
            new PriceQuote(item.Definition, Array.Empty<PriceTag>());

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
            if (item.ForSale && target!=ContainerId.Counter && target!=ContainerId.CustomerCounter) { reason="这是顾客的货物；确认收购后才属于你。"; return false; }
            if (!item.ForSale && target==ContainerId.CustomerCounter) { reason="顾客柜台仅展示顾客来货；自有物品请放在仓库、展示柜或谈判柜台。"; return false; }
            if (!Fits(item,target,x,y,rotation,flipped)) { reason="放不下：不能越界，也不能覆盖其他物品。"; return false; }
            return true;
        }

        public bool Move(int id, ContainerId target, int x, int y, int rotation, bool flipped)
        {
            if (!CanMove(id,target,x,y,rotation,flipped,out string reason)) return Fail(reason);
            var item=Find(id); Place(item,target,x,y,rotation,flipped);
            TryPlaceSupplierItems();
            return Success($"已摆放 {item.Definition.title}。" );
        }
        static void Place(GridItem item, ContainerId target, int x, int y, int rotation, bool flipped)
        { item.Container=target; item.X=x; item.Y=y; item.Rotation=((rotation%4)+4)%4; item.Flipped=flipped; }

        public DisplayAttraction PreviewAttraction()
        {
            var display=In(ContainerId.Display).Where(i=>i.Owner==ItemOwner.Player).ToArray();
            var dominant=display.Where(i=>IsSaleItem(i.Definition)).GroupBy(i=>i.Definition.category)
                .Select(g=>new {Category=g.Key,Value=g.Sum(i=>i.Definition.baseValue)})
                .OrderByDescending(g=>g.Value).ThenBy(g=>(int)g.Category).FirstOrDefault();
            var advertised=display.Where(i=>i.Definition.procurementSign).Select(i=>i.Definition.advertisedCategory).Distinct().ToArray();
            // Select definitions by the sign's category, not a hard-coded herb/dew schedule.
            var allSupplies=catalog.items.Where(d=>IsSaleItem(d) && d.supplierAvailable).ToArray();
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
        static bool IsSaleItem(ItemDefinition definition) => !definition.procurementSign && definition.baseValue>0
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
                var category=attraction.BuyerCategory==ItemCategory.Unclassified?categories[DrawCustomerNumber(0,categories.Length)]:attraction.BuyerCategory;
                int budget=DrawCustomerNumber(attraction.MinimumBuyerBudget,attraction.MaximumBuyerBudget+1);
                if(DrawCustomerChance()<attraction.SupplierChance)
                {
                    // Two goods per supplying visitor is a temporary demo convention, not a balance rule.
                    var supplies=Enumerable.Range(0,2).Select(_=>attraction.Supplies[DrawCustomerNumber(0,attraction.Supplies.Length)].id).ToArray();
                    queue.Enqueue((TradeDirection.CustomerSells,supplies,category,budget));
                    SuppliersToday++;
                }
                else
                {
                    queue.Enqueue((TradeDirection.CustomerBuys,Array.Empty<string>(),category,budget));
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
            if (Offer!=null) { LastCustomerResult="已主动送别上一位顾客。"; CancelOffer(); ServedToday++; }
            if (queue.Count==0) return Success("今日顾客已接待完。可以结束营业，炼丹整理，再睡觉。" );
            var request=queue.Dequeue();
            Offer=new TradeOffer{Direction=request.direction, SupplierItems=request.definitionIds.Select(id=>NewItem(catalog.Find(id),ItemOwner.Customer)).ToArray(),
                CustomerName=request.direction==TradeDirection.CustomerSells?"采药客 · 阿青":"散修 · 云生",
                RequestedCategory=request.category,RemainingBudget=request.budget};
            var current=Offer;current.CurrentPrice=()=>current.SupplierItem==null?0:Quote(current.SupplierItem).Amount;
            TryPlaceSupplierItems();
            return Success($"顾客求购{ShopCatalog.CategoryName(request.category)}，预算 {request.budget}。来货在顾客柜台，确认前仍属顾客；可继续交易或随时下一位。" );
        }

        void TryPlaceSupplierItems()
        {
            if(Offer==null)return;
            foreach(var item in Offer.SupplierItems.Where(i=>i.ForSale && Find(i.Id)==null))
                if(FindSpace(item,ContainerId.CustomerCounter,out int x,out int y))
                { Place(item,ContainerId.CustomerCounter,x,y,0,false);items.Add(item); }
        }

        public bool StageSale()
        {
            if (Offer==null) return Fail("当前没有待摆放的出售商品。" );
            foreach(var item in items.Where(i=>i.Owner==ItemOwner.Player && i.Container!=ContainerId.Counter && !i.Definition.procurementSign && i.Definition.category==Offer.RequestedCategory))
                if(FindSpace(item,ContainerId.Counter,out int x,out int y)) return Move(item.Id,ContainerId.Counter,x,y,item.Rotation,item.Flipped);
            return Fail("没有可摆放的同类商品，或柜台空间不足。也可以手动拖入任意物品。" );
        }

        public string CounterSaleSummary => string.Join(" + ", In(ContainerId.Counter).Where(i=>i.Owner==ItemOwner.Player)
            .GroupBy(i=>new {i.Definition.title, Price=Quote(i).Amount}).Select(g=>$"{g.Key.title} {g.Key.Price}×{g.Count()}={g.Key.Price*g.Count()}"));

        public ShopTradeQuote PreviewTrade(bool requestAll=false)
        {
            var basket=In(ContainerId.Counter).ToList();
            if(requestAll && !basket.Any(i=>i.ForSale))basket.AddRange(In(ContainerId.CustomerCounter).Where(i=>i.ForSale));
            var quote=new ShopTradeQuote {Lines=basket.Select(i=>new ShopTradeLine(i,Quote(i))).ToArray()};
            quote.CanConfirm=ValidateTrade(quote,out string reason);quote.Reason=reason;return quote;
        }

        bool ValidateTrade(ShopTradeQuote quote,out string reason)
        {
            reason="当前没有可结算的交易。";
            if(Phase!=DayPhase.Open || Offer==null)return false;
            if(quote.Lines.Count==0){reason="柜台为空，请摆入商品或从谈判入口请求买入来货。";return false;}
            if(quote.Lines.Select(l=>l.Item.Id).Distinct().Count()!=quote.Lines.Count || ValidateState()!=null)
            {reason="物品位置或所有权无效，本次未成交。";return false;}
            foreach(var line in quote.Lines)
            {
                var item=line.Item;
                if(!IsSaleItem(item.Definition)){reason="柜台含不可出售的物品，请移出。";return false;}
                if(line.Buying && !Offer.SupplierItems.Contains(item)){reason="这件来货不属于当前顾客。";return false;}
                if(!line.Buying && item.Definition.category!=Offer.RequestedCategory)
                {reason=$"类别不符：{item.Definition.title}不是{ShopCatalog.CategoryName(Offer.RequestedCategory)}。";return false;}
            }
            if(quote.SaleTotal>Offer.RemainingBudget)
            {reason=$"顾客资金不足：卖出总价 {quote.SaleTotal}，剩余预算 {Offer.RemainingBudget}。";return false;}
            if(Money+quote.Net<0){reason=$"灵石不足：净支付 {-quote.Net}，当前 {Money}。";return false;}
            if(Money+quote.Net>int.MaxValue || quote.SaleTotal>int.MaxValue-IncomeToday || quote.PurchaseTotal>int.MaxValue-ExpensesToday)
            {reason="本次金额超出可结算范围。";return false;}
            // Reserve every destination before changing ownership, inventory, or money.
            var occupied=new HashSet<Vector2Int>(In(ContainerId.Storage).SelectMany(i=>i.Cells.Select(p=>p+new Vector2Int(i.X,i.Y))));
            var size=Size(ContainerId.Storage);
            foreach(var line in quote.Lines.Where(l=>l.Buying).OrderByDescending(l=>l.Item.Cells.Length).ThenBy(l=>l.Item.Id))
            {
                var item=line.Item;bool found=false;
                for(int y=0;y<size.y && !found;y++)for(int x=0;x<size.x && !found;x++)
                {
                    var cells=item.Cells.Select(p=>p+new Vector2Int(x,y)).ToArray();
                    if(cells.Any(p=>p.x<0 || p.y<0 || p.x>=size.x || p.y>=size.y || occupied.Contains(p)))continue;
                    quote.PurchasePositions[item.Id]=new Vector2Int(x,y);occupied.UnionWith(cells);found=true;
                }
                if(!found){reason=$"背包空间不足：无法放下 {item.Definition.title}。请关闭后整理或旋转来货，整笔未成交。";return false;}
            }
            reason="交易成立；确认后统一收付，买入商品进入背包。";return true;
        }

        public bool CanAcceptTrade(out int total,out string reason)
        {
            var quote=PreviewTrade();total=(int)Math.Max(int.MinValue,Math.Min(int.MaxValue,quote.Net));reason=quote.Reason;return quote.CanConfirm;
        }
        public bool AcceptTrade(bool requestAll=false)
        {
            var quote=PreviewTrade(requestAll);
            if(!quote.CanConfirm)return Fail(quote.Reason);
            foreach(var line in quote.Lines)
            {
                var item=line.Item;
                if(line.Buying)
                {
                    var position=quote.PurchasePositions[item.Id];
                    item.Owner=ItemOwner.Player;item.PurchaseValue=line.Price.Amount;
                    Place(item,ContainerId.Storage,position.x,position.y,item.Rotation,item.Flipped);Purchases++;
                }
                else {item.Owner=ItemOwner.Customer;items.Remove(item);Sales++;}
            }
            Money+=(int)quote.Net;IncomeToday+=(int)quote.SaleTotal;ExpensesToday+=(int)quote.PurchaseTotal;
            Offer.RemainingBudget-=(int)quote.SaleTotal;
            Offer.SupplierItems=Offer.SupplierItems.Where(i=>i.ForSale).ToArray();
            LastCustomerResult=$"成交 {quote.Lines.Count} 件，净额 {quote.Net:+0;-0;0}。";
            return Success($"{LastCustomerResult} 顾客剩余预算 {Offer.RemainingBudget}，可继续交易或主动下一位。");
        }

        public bool RejectTrade()
        {
            if (Offer==null) return Fail("当前没有需要拒绝的交易。" );
            CancelOffer(); ServedToday++;
            LastCustomerResult="已拒绝本次交易，顾客离开。";
            return Success("已拒绝交易。玩家物品和灵石未减少，顾客已离开。" );
        }
        void CancelOffer()
        {
            if(Offer==null) return;
            items.RemoveAll(i=>i.ForSale);
            Offer=null;
        }
        public bool EndBusiness()
        {
            if(Phase!=DayPhase.Open) return Fail("当前没有营业。" );
            CancelOffer(); queue.Clear(); Phase=DayPhase.Closed;
            return Success($"今日已闭店。收入 {IncomeToday}，支出 {ExpensesToday}，余额变化 {BalanceChange:+0;-0;0}。查看结算后可进入下一天。" );
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
            return Success("炼丹完成：凝气草 + 灵露 → 回气丹，已放入背包。直接制作的物品无购买价值。" );
        }
        public bool Sleep()
        {
            if(Phase!=DayPhase.Closed) return Fail("请先开始并结束当天营业，再睡觉。" );
            int due=RentDebt;
            if(Day%catalog.rentPeriod==0) { due+=Rent; Rent=(int)Math.Ceiling(Rent*1.05); }
            int paid=Math.Min(Money,due); Money-=paid; RentDebt=due-paid;
            Day++; Phase=DayPhase.Preparation; ServedToday=0;TodayAttraction=null;BuyersToday=0;SuppliersToday=0; queue.Clear();
            OpeningMoney=Money;IncomeToday=0;ExpensesToday=0;LastCustomerResult="新的一天，尚未接待顾客。";
            Calendar.Between(MarketCalendar.WeekStart(Day),MarketCalendar.WeekStart(Day)+13);PricingRevision++;
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
                if(item.ForSale && (Offer==null || !Offer.SupplierItems.Contains(item) ||
                    (item.Container!=ContainerId.Counter && item.Container!=ContainerId.CustomerCounter))) return "Orphaned customer item";
                if(!item.ForSale && item.Container==ContainerId.CustomerCounter)return "Player item on customer counter";
            }
            return null;
        }
    }
}
