using System.Linq;

namespace XiuXianShop.Editor
{
    public sealed class KnowledgeContentAdapter : ContentDomainAdapter
    {
        readonly bool mappings;
        KnowledgeContentAdapter(bool mappings){this.mappings = mappings;}
        public override string FileName => mappings ? "jade-slips.csv" : "knowledge.csv";
        public static ContentDomainAdapter[] Create() => new ContentDomainAdapter[] { new KnowledgeContentAdapter(false), new KnowledgeContentAdapter(true) };

        public override void Stage(ContentSyncPlan p, string csv)
        {
            if(mappings)p.staged.jadeSlips = Merge(p, csv, "条目ID", "条目ID|物品ID|KnowledgeID|数据状态", p.staged.jadeSlips,
                (r, old) => new JadeSlipMapping { itemId = r.Required("物品ID"), knowledgeId = r.Required("KnowledgeID") });
            else p.staged.knowledge = Merge(p, csv, "KnowledgeID", "知识名称|KnowledgeID|类型|正文|单次学习体力|单次进度|掌握阈值|数据状态", p.staged.knowledge, (r, old) =>
            {
                var type = r.OneOf("类型", "Text", "Technique") == "Technique" ? KnowledgeType.Technique : KnowledgeType.Text;
                var k = new KnowledgeDefinition { title = r.Required("知识名称"), text = r.Required("正文"), type = type };
                if(type == KnowledgeType.Technique)
                {
                    k.studyStamina = r.Number("单次学习体力", 0, true).value;
                    k.progressPerStudy = r.Number("单次进度", 1, true).value;
                    k.masteryThreshold = r.Number("掌握阈值", 1, true).value;
                }
                else if(new[] { "单次学习体力", "单次进度", "掌握阈值" }.Any(f => r.Get(f) != ""))r.Error("类型", "Text 不配置学习参数。");
                return k;
            });
        }

        public override void Validate(ContentSyncPlan p)
        {
            if(!mappings)return;
            foreach(var m in p.staged.jadeSlips.Where(m => m.enabled))
            {
                Reference(p, m.id, "物品ID", m.itemId, p.stagedCatalog.items.Where(i => i.category == ItemCategory.JadeSlip).Select(i => i.id));
                Reference(p, m.id, "KnowledgeID", m.knowledgeId, p.staged.knowledge.Where(k => k.enabled).Select(k => k.id));
            }
            foreach(var group in p.staged.jadeSlips.Where(m => m.enabled).GroupBy(m => m.itemId).Where(g => g.Count() > 1))
                p.Error(FileName, group.First().id, "物品ID", "同一玉简物品只能映射一条知识。");
        }
    }
}
