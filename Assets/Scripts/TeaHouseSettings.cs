using System;
using UnityEngine;

namespace XiuXianShop
{
    public enum TeaEffect { BuyerTrend, SupplierTrend, Travellers, Promotion, WealthyVisitor, MarketSecret }
    [Serializable]
    public sealed class TeaEffectWeight
    {
        public TeaEffect effect;
        [Min(0)] public float weight;
    }
    [Serializable]
    public sealed class TeaHouseSettings
    {
        public TeaEffectWeight[] effects =
        {
            new TeaEffectWeight{effect=TeaEffect.BuyerTrend,weight=28},
            new TeaEffectWeight{effect=TeaEffect.SupplierTrend,weight=28},
            new TeaEffectWeight{effect=TeaEffect.Travellers,weight=18},
            new TeaEffectWeight{effect=TeaEffect.Promotion,weight=10},
            new TeaEffectWeight{effect=TeaEffect.WealthyVisitor,weight=10},
            new TeaEffectWeight{effect=TeaEffect.MarketSecret,weight=6}
        };
        [Min(1)] public float buyerCategoryMultiplier=2;
        [Min(1)] public float supplierCategoryMultiplier=2;
        [Range(0,1)] public float travellerChanceBonus=.25f;
        [Range(0,1)] public float travellerChanceCap=.85f;
        [Min(0)] public int extraCustomers=1;
        [Min(0)] public int wealthyBudgetTierIncrease=1;
        [Min(1)] public int secretMinimumTurnsAhead=2;
        [Min(1)] public int secretMaximumTurnsAhead=12;
    }
    [Serializable]
    public sealed class TeaVisitResult
    {
        public int visitTurn;
        public TeaEffect effect;
        public ItemCategory category;
        public string secretEventId;
        public int ApplyTurn => visitTurn+1;
    }
}
