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
        public string[] itemIds;
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
        public CommissionTemplate[] templates =
        {
            new CommissionTemplate { id="BSH-001", title="药铺理库", weight=10, description="坊市药铺刚清过后库，剩下不少边角材料与旧包装需要重新分类，掌柜临时缺人手。", rewardPoolId="BSH-R01", rewardHint="低阶材料 × 若干" },
            new CommissionTemplate { id="BSH-002", title="商队点货", weight=10, description="一支短途商队刚进坊市，需要在开市前核对货箱数量和标签，避免卸货时拿错。", rewardPoolId="BSH-R02", rewardHint="普通商品 × 若干" },
            new CommissionTemplate { id="BSH-003", title="铺面帮工", weight=10, description="临街杂货铺换了一批货架，需要有人把散货重新归位，并把剩余包装材料清出来。", rewardPoolId="BSH-R03", rewardHint="材料与商品各若干" },
            new CommissionTemplate { id="BSH-004", title="送契跑腿", weight=8, description="两家商铺需要在闭市前互送一份盖印契书，路不远，但双方都抽不开身。", rewardPoolId="BSH-R04", rewardHint="少量经营货币" },
            new CommissionTemplate { id="BSH-005", title="库房清账", weight=8, description="一间公用货栈的账面与实物对不上，需要按货签重新清点一遍散落杂项。", rewardPoolId="BSH-R06", rewardHint="低阶杂项 × 若干" },
            new CommissionTemplate { id="BSH-006", title="临时看摊", weight=10, description="摊主临时要去办事，请人替他照看片刻摊位并按已经写好的价签交接货物。", rewardPoolId="BSH-R02", rewardHint="普通商品 × 若干" },
            new CommissionTemplate { id="BSH-007", title="货栈搬运", weight=10, description="货栈里有一批不值当雇专门力士的小箱子，需要从后门挪到分拣区。", rewardPoolId="BSH-R01", rewardHint="低阶材料 × 若干" },
            new CommissionTemplate { id="BSH-008", title="驿站送件", weight=8, description="坊市驿站积着几份短途包件，正好需要有人顺路送到附近几家商铺。", rewardPoolId="BSH-R05", rewardHint="少量经营货币＋一件实物" },
            new CommissionTemplate { id="BSH-009", title="修缮杂役", weight=9, description="一间旧铺子换了门板和货架，完工后还有钉件、木料和杂材要整理收拢。", rewardPoolId="BSH-R01", rewardHint="低阶材料 × 若干" },
            new CommissionTemplate { id="BSH-010", title="集市盘货", weight=9, description="集市准备收摊，几家摊位需要合并清点剩余货物，按各自名册重新装箱。", rewardPoolId="BSH-R02", rewardHint="普通商品 × 若干" },
            new CommissionTemplate { id="BSH-011", title="行商卸货", weight=9, description="一名行商带回零散货物，数量不多但种类杂，需要帮忙卸下并按用途分开。", rewardPoolId="BSH-R03", rewardHint="材料与商品各若干" },
            new CommissionTemplate { id="BSH-012", title="药材分拣", weight=10, description="药农送来一批品质普通的杂材，需要把可用部分、包装和废边分开，方便药铺入库。", rewardPoolId="BSH-R01", rewardHint="低阶材料 × 若干" },
            new CommissionTemplate { id="BSH-013", title="账房抄录", weight=7, description="百事堂代商户归档一批旧账，需要把几页已核对的流水誊写到统一格式中。", rewardPoolId="BSH-R04", rewardHint="少量经营货币" },
            new CommissionTemplate { id="BSH-014", title="器铺搬箱", weight=9, description="器物铺刚收到一批普通零件和杂货，需要把没有技术要求的箱件搬到对应货位。", rewardPoolId="BSH-R06", rewardHint="低阶杂项 × 若干" },
            new CommissionTemplate { id="BSH-015", title="客栈搬柴", weight=8, description="客栈后院新到一批日常耗材，需要在晚市前搬入库房并按用途堆放。", rewardPoolId="BSH-R05", rewardHint="少量经营货币＋一件实物" },
            new CommissionTemplate { id="BSH-016", title="灵舟泊位卸货", weight=8, description="小型灵舟临时靠泊，几箱普通货物急着进坊市，搬运与分拣都不需要专业资格。", rewardPoolId="BSH-R03", rewardHint="材料与商品各若干" }
        };
        // Initial pool membership uses approved existing goods only. Edit the catalog
        // to change members; generation uses IDs and never branches on a template ID.
        public CommissionRewardPool[] rewardPools =
        {
            new CommissionRewardPool { id="BSH-R01", items=new[]{Group(2,4,"herb","dew","cinnabar")} },
            new CommissionRewardPool { id="BSH-R02", items=new[]{Group(1,3,"sword")} },
            new CommissionRewardPool { id="BSH-R03", items=new[]{Group(1,1,"herb","dew","cinnabar"),Group(1,1,"sword")} },
            new CommissionRewardPool { id="BSH-R04", minimumMoney=6, maximumMoney=10 },
            new CommissionRewardPool { id="BSH-R05", minimumMoney=4, maximumMoney=6, items=new[]{Group(1,1,"herb","dew","cinnabar","sword")} },
            new CommissionRewardPool { id="BSH-R06", items=new[]{Group(1,3,"herb","dew","cinnabar","sword")} },
            new CommissionRewardPool { id="BSH-R07", items=new[]{Group(1,1,"pill")} }
        };
        static CommissionItemReward Group(int min,int max,params string[] ids) =>
            new CommissionItemReward { minimumCount=min,maximumCount=max,itemIds=ids };
    }
}
