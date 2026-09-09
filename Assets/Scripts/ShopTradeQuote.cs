using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    public sealed class ShopTradeLine
    {
        public GridItem Item { get; }
        public PriceQuote Price { get; }
        public bool Buying { get; }
        public long SignedAmount => Buying?-(long)Price.Amount:Price.Amount;
        public ShopTradeLine(GridItem item,PriceQuote price){Item=item;Price=price;Buying=item.ForSale;}
        public string Description => $"{(Buying?"买入 −":"卖出 +")}{Price.Amount}    {Item.Definition.title} × 1\n基础 {Price.BaseValue} · {Price.Modifiers}";
    }

    // A disposable preview; confirmation always builds a fresh quote and placement plan.
    public sealed class ShopTradeQuote
    {
        public IReadOnlyList<ShopTradeLine> Lines { get; internal set; }
        public long SaleTotal => Lines.Where(l=>!l.Buying).Sum(l=>(long)l.Price.Amount);
        public long PurchaseTotal => Lines.Where(l=>l.Buying).Sum(l=>(long)l.Price.Amount);
        public long Net => SaleTotal-PurchaseTotal;
        public bool CanConfirm { get; internal set; }
        public string Reason { get; internal set; }
        internal readonly Dictionary<int,Vector2Int> PurchasePositions=new Dictionary<int,Vector2Int>();
    }
}
