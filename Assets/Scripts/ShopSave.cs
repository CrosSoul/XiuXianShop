using System;

namespace XiuXianShop
{
    // Safe-point snapshot. No half-finished customer transaction or active production is saved.
    [Serializable]
    public sealed class ShopSave
    {
        public const int CurrentSchemaVersion=1;
        public int schemaVersion;
        public string savedAtUtc;
        public TurnPhase phase;
        public bool hasAlchemyDefinitions,hasSpiritDefinitions,hasFurnaceDefinition;
        public int turn, money, rent, debt, nextId, customerSeed, customerDraws;
        public int crafted, purchases, sales;
        public int openingMoney,incomeToday,expensesToday,servedToday,buyersToday,suppliersToday,tradingCustomersToday;
        public string lastCustomerResult;
        public bool hasStaminaState,staminaOverflowCustomer;
        public int stamina;
        public bool hasTravelledThisTurn;
        public string[] unlockedLocationIds;
        public string[] visitedLocationIds,progressFlags;
        public VisitProgress[] visits;
        public int specialVisitsThisTurn;
        public TeaVisitResult latestTeaVisit,activeTeaEffect;
        public bool hasLatestTeaVisit,hasActiveTeaEffect;
        public int commissionTurn, commissionsCompleted;
        public string[] commissionCandidates;
        public string completedCommissionId,commissionResult;
        public SavedShopItem[] items;
        public PriceTag[] tags;
        public MarketCalendarState calendar;
    }
    [Serializable]
    public sealed class SavedShopItem
    {
        public int id;
        public string definitionId;
        public string locationId;
        public ContainerId container;
        public int x,y,rotation,storageItemId;
        public bool flipped,hasPurchaseValue;
        public int purchaseValue;
        public bool hasSpiritResource;
        public int spiritUnits;
        public int spiritCapacityUnits;
        public PillQuality quality;
        // Unity JsonUtility does not serialize decimal; round-trip the exact value in invariant text.
        public string qualityValueMultiplier;
    }
}
