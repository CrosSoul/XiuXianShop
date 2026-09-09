using System;

namespace XiuXianShop
{
    // Versioned preparation-phase snapshot. No half-finished customer transaction is saved.
    [Serializable]
    public sealed class ShopSave
    {
        public int version=2;
        public string catalogSignature;
        public int turn, money, rent, debt, nextId, customerSeed, customerDraws;
        public int crafted, purchases, sales;
        public SavedShopItem[] items;
        public PriceTag[] tags;
        public MarketCalendarState calendar;
    }
    [Serializable]
    public sealed class SavedShopItem
    {
        public int id;
        public string definitionId;
        public ContainerId container;
        public int x,y,rotation;
        public bool flipped,hasPurchaseValue;
        public int purchaseValue;
    }
}
