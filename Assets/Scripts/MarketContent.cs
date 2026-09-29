using System;
namespace XiuXianShop
{
    [Serializable] public sealed class MarketContent : AuthoredRecord
    {
        public string title,description,note;
        public ItemCategory category;
        public string direction;
        public float percent,weight;
        public int minimumDuration,maximumDuration,cooldownTurns;
    }
}
