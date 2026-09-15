using System;
using UnityEngine;

namespace XiuXianShop
{
    public enum CustomerBehavior { Buying, Selling, Trading }
    public enum CustomerBudgetTier { Ordinary, Wealthy, Lavish }

    [Serializable]
    public sealed class CategoryCustomerBudget
    {
        public ItemCategory category;
        [Min(1)] public int ordinary, wealthy, lavish;
        [Tooltip("除材料普通20、丹药普通45外，当前金额均为原型配置。")]
        public string balanceNote = "原型金额，非最终平衡";
        public int Amount(CustomerBudgetTier tier) =>
            tier==CustomerBudgetTier.Ordinary?ordinary:tier==CustomerBudgetTier.Wealthy?wealthy:lavish;
    }

    [Serializable]
    public sealed class CustomerSupplyPool
    {
        public ItemCategory category;
        [Min(0)] public float weight = 10;
        public string[] itemIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CustomerGenerationSettings
    {
        [Min(1)] public int baseCustomerCount = 5;
        [Min(0)] public int staminaOverflowCustomers = 1;
        [Min(0)] public float buyingWeight = 40, sellingWeight = 25, tradingWeight = 35;
        [Min(0)] public float baseCategoryWeight = 10, displayedItemWeight = 5;
        [Min(0)] public float ordinaryWeight = 75, wealthyWeight = 23, lavishWeight = 2;
        [Min(0)] public float oneSupplyWeight = 75, twoSuppliesWeight = 25;
        public CategoryCustomerBudget[] budgets =
        {
            new CategoryCustomerBudget {category=ItemCategory.Material,ordinary=20,wealthy=60,lavish=150,balanceNote="普通20已确认；富裕/阔绰为原型"},
            new CategoryCustomerBudget {category=ItemCategory.Medicine,ordinary=45,wealthy=100,lavish=220,balanceNote="普通45已确认；富裕/阔绰为原型"},
            new CategoryCustomerBudget {category=ItemCategory.Equipment,ordinary=30,wealthy=80,lavish=180},
            new CategoryCustomerBudget {category=ItemCategory.Container,ordinary=25,wealthy=70,lavish=160},
            new CategoryCustomerBudget {category=ItemCategory.StoneLow,ordinary=20,wealthy=60,lavish=150},
            new CategoryCustomerBudget {category=ItemCategory.StoneMid,ordinary=120,wealthy=250,lavish=500},
            new CategoryCustomerBudget {category=ItemCategory.StoneHigh,ordinary=11000,wealthy=15000,lavish=20000}
        };
        public CustomerSupplyPool[] supplyPools =
        {
            new CustomerSupplyPool {category=ItemCategory.Material,itemIds=new[]{"herb","dew","cinnabar"}},
            new CustomerSupplyPool {category=ItemCategory.Medicine,itemIds=new[]{"pill"}},
            new CustomerSupplyPool {category=ItemCategory.Equipment,itemIds=new[]{"sword"}}
        };
    }
}
