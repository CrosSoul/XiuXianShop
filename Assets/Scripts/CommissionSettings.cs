using System;
using UnityEngine;

namespace XiuXianShop
{
    [Serializable]
    public sealed class CommissionTemplate
    {
        public string id, title;
        public bool enabled = true;
        [Min(0)] public float weight = 1;
        [TextArea] public string description;
        public string rewardPoolId, rewardHint;
    }

    [Serializable]
    public sealed class CommissionItemReward
    {
        public CommissionMemberRow[] members = Array.Empty<CommissionMemberRow>();
        [Min(1)] public int minimumCount = 1, maximumCount = 1;
    }

    [Serializable]
    public sealed class CommissionRewardPool
    {
        public string id;
        public bool enabled = true;
        [Min(0)] public int minimumMoney, maximumMoney;
        public CommissionItemReward[] items = Array.Empty<CommissionItemReward>();
    }

    [Serializable]
    public sealed class CommissionSettings
    {
        [Min(1)] public int candidateCount = 3;
        [Min(1)] public int completionLimitPerTurn = 1;
        [Range(0,1)] public float extraGiftChance = .08f;
        [Min(1)] public int extraGiftCount = 1;
        public string extraGiftPoolId = "BSH-R07";
        public CommissionTemplate[] templates = Array.Empty<CommissionTemplate>();
        public CommissionRewardPool[] rewardPools = Array.Empty<CommissionRewardPool>();
    }
}
