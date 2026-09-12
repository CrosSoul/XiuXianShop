using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    [Serializable]
    public sealed class PriceTag
    {
        public string id;
        public string title;
        [Tooltip("百分比修正：-0.3 表示 -30%；同 ID 更新而非叠加。")]
        public float percent;
        [Tooltip("未分类表示作用于所有类别。")]
        public ItemCategory category;
        public bool playerSells = true;
        public bool playerBuys = true;

        public PriceTag Copy() => (PriceTag)MemberwiseClone();
    }

    // One immutable calculation is shared by UI, validation and settlement.
    public sealed class PriceQuote
    {
        public decimal BaseValue { get; }
        public decimal RawValue { get; }
        public int Amount { get; }
        public string Modifiers { get; }
        public PriceQuote(ItemDefinition definition, IEnumerable<PriceTag> tags) : this(definition, tags, definition.FullBaseValue) { }
        public PriceQuote(ItemDefinition definition, IEnumerable<PriceTag> tags, decimal baseValue)
        {
            BaseValue = baseValue;
            var effective = tags.ToArray();
            decimal percent = effective.Sum(t => (decimal)t.percent);
            RawValue = BaseValue * (1 + percent);
            Amount = definition.procurementSign || BaseValue <= 0 ? 0 :
                (int)Math.Min(int.MaxValue, Math.Max(1, Math.Round(RawValue, 0, MidpointRounding.AwayFromZero)));
            Modifiers = effective.Length == 0 ? "无价格修正" :
                string.Join("、", effective.Select(t => $"{t.title} ({t.percent * 100:+0.##;-0.##;0}%)"));
        }
    }
}
