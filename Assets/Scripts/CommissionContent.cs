using System;

namespace XiuXianShop
{
    [Serializable] public sealed class CommissionConfigRow : AuthoredRecord
    {
        public string value;
    }
    [Serializable] public sealed class CommissionPoolRow : AuthoredRecord
    {
        public string title, description, hint, groups;
        public int minimumMoney, maximumMoney;
    }
    [Serializable] public sealed class CommissionMemberRow : AuthoredRecord
    {
        public string poolId, groupId, itemId, note;
        public float weight=1;
        public int minimumCount=1, maximumCount=1;
    }
    [Serializable] public sealed class CommissionTemplateRow : AuthoredRecord
    {
        public string title, description, rewardPoolId, rewardHint;
        public float weight;
    }
}
