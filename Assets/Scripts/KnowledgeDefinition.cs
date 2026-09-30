using System;

namespace XiuXianShop
{
    public enum KnowledgeType { Text, Technique }

    [Serializable]
    public sealed class KnowledgeDefinition : AuthoredRecord
    {
        public string title, text;
        public KnowledgeType type;
        public int studyStamina, progressPerStudy, masteryThreshold;
    }

    [Serializable]
    public sealed class JadeSlipMapping : AuthoredRecord
    {
        public string itemId, knowledgeId;
    }

    public sealed class KnowledgeProgress
    {
        public int Progress { get; internal set; }
        public bool Mastered { get; internal set; }
    }

    [Serializable]
    public sealed class SavedKnowledgeProgress
    {
        public string knowledgeId;
        public int progress;
        public bool mastered;
    }
}
