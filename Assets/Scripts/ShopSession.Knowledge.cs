using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        readonly Dictionary<string, KnowledgeProgress> knowledgeProgress = new Dictionary<string, KnowledgeProgress>();

        public KnowledgeDefinition FindKnowledge(string id) => catalog.authoredContent?.knowledge.FirstOrDefault(k => k.enabled && k.id == id);

        public KnowledgeDefinition KnowledgeForItem(GridItem item)
        {
            if(item.Definition.category != ItemCategory.JadeSlip)return null;
            var mapping = catalog.authoredContent?.jadeSlips.SingleOrDefault(m => m.enabled && m.itemId == item.Definition.id);
            return mapping == null ? null : FindKnowledge(mapping.knowledgeId);
        }

        public KnowledgeProgress GetKnowledgeProgress(string id) => knowledgeProgress.TryGetValue(id, out var state) ?
            new KnowledgeProgress { Progress = state.Progress, Mastered = state.Mastered } : new KnowledgeProgress();

        public bool HasKnowledgeSource(string id) => items.Any(i => i.Owner == ItemOwner.Player && KnowledgeForItem(i)?.id == id);

        public bool CanStudyKnowledge(string id, out string reason)
        {
            var definition = FindKnowledge(id);
            if(definition == null || definition.type != KnowledgeType.Technique){reason = "该内容不可学习。";return false;}
            if(Phase != TurnPhase.Closed){reason = "请先结束本回合营业，再学习。";return false;}
            if(IsTravelling || PendingVisitScene != null){reason = "请回到店铺并结束当前剧情后学习。";return false;}
            if(GetKnowledgeProgress(id).Mastered){reason = "已经掌握，无需重复学习。";return false;}
            if(!HasKnowledgeSource(id)){reason = "当前没有该内容的自有玉简；已有进度保留。";return false;}
            return CanSpendStamina(definition.studyStamina, out reason);
        }

        public bool StudyKnowledge(string id)
        {
            if(!CanStudyKnowledge(id, out var reason))return Fail(reason);
            var definition = FindKnowledge(id);
            var state = GetKnowledgeProgress(id);
            // One operation per KnowledgeID, regardless of the number of physical copies.
            state.Progress = (int)Math.Min(definition.masteryThreshold, (long)state.Progress + definition.progressPerStudy);
            state.Mastered = state.Progress >= definition.masteryThreshold;
            Stamina -= definition.studyStamina;
            knowledgeProgress[id] = state;
            return Success($"{definition.title}：进度 {state.Progress}/{definition.masteryThreshold}" + (state.Mastered ? " · 已掌握。" : "。"));
        }

        void RestoreKnowledgeProgress(SavedKnowledgeProgress[] saved)
        {
            // Schema 1 saves written before DP-41 contain no Knowledge progress yet.
            foreach(var row in saved ?? Array.Empty<SavedKnowledgeProgress>())
            {
                if(row == null || string.IsNullOrWhiteSpace(row.knowledgeId) || row.progress < 0 || knowledgeProgress.ContainsKey(row.knowledgeId) ||
                    catalog.authoredContent == null || !catalog.authoredContent.knowledge.Any(k => k.id == row.knowledgeId && k.type == KnowledgeType.Technique))
                    throw new ArgumentException("存档知识进度缺失引用、重复或无效。");
                // Authored thresholds can change; loading preserves earned progress/mastery.
                knowledgeProgress.Add(row.knowledgeId, new KnowledgeProgress { Progress = row.progress, Mastered = row.mastered });
            }
        }

        public bool GrantJadeSlipForVerification(string itemId)
        {
            if(IsTravelling || IsCarrying || IsUsingAlchemy)return Fail("请回到店铺并结束当前活动，再领取测试玉简。");
            var definition = catalog.Find(itemId);
            var item = new GridItem { Id = nextId, Definition = definition, Owner = ItemOwner.Player };
            if(KnowledgeForItem(item) == null)return Fail("测试物品缺少有效玉简内容映射。");
            if(!FindSpace(item, ContainerId.Storage, out int x, out int y))return Fail("仓库放不下；未发放玉简。");
            nextId++;Place(item, ContainerId.Storage, x, y, 0, false);items.Add(item);
            return Success("测试玉简已放入仓库；点击阅读，闭店后可学习功法。");
        }
    }
}
