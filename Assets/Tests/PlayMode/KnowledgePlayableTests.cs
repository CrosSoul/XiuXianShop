#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        IEnumerator ClickJade(GridItem item)
        {
            yield return MouseAt(ItemPoint(item), false);yield return MouseAt(ItemPoint(item), true);yield return MouseAt(ItemPoint(item), false);
            Assert.That(shop.IsKnowledgeWindowOpen);
        }
        string KnowledgeLabel(string name) => shop.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t => t.name == name).text;
        IEnumerator CloseBusinessForKnowledge()
        {
            yield return Click("BeginBusiness");while(shop.Session.PendingVisitScene != null)yield return Click("VisitSceneContinue");
            yield return Click("EndBusiness");
        }

        [UnityTest, Category("DP41")] public IEnumerator TextClickReadCloseAndGridTooltipRemainUsable()
        {
            IsolateAlchemyTestMouse();var s = shop.Session;
            Assert.That(s.GrantJadeSlipForVerification("test-jade-text"));shop.Refresh();yield return null;
            var item = s.Items.Last();int stamina = s.Stamina, id = item.Id;
            shop.TooltipHoverDelaySeconds = .5f;
            yield return MouseAt(ItemPoint(item), false);yield return new WaitForSecondsRealtime(.65f);
            Assert.That(TooltipText(), Is.Not.Null);
            Assert.That(TooltipText().text, Does.Contain("类别：玉简"));
            Assert.That(TooltipText().text, Does.Not.Contain("text_test_market_notice"));
            yield return ClickJade(item);Assert.That(KnowledgeLabel("KnowledgeBody"), Is.EqualTo(s.KnowledgeForItem(item).text));
            Assert.That(KnowledgeLabel("KnowledgeType"), Is.EqualTo("普通文本"));Assert.That(shop.FindButton("KnowledgeStudy").gameObject.activeSelf, Is.False);
            Assert.That(shop.GetComponentsInChildren<RectTransform>().Single(r => r.name == "KnowledgeWindow").rect.size, Is.EqualTo(new Vector2(600,460)));
            yield return Click("KnowledgeClose");Assert.That(s.Find(id), Is.SameAs(item));Assert.That(s.Stamina, Is.EqualTo(stamina));
            yield return Drag(item, ContainerId.Display, 0, 0);Assert.That(item.Container, Is.EqualTo(ContainerId.Display));
            yield return MouseAt(Vector2.zero, false);Assert.That(TooltipText(), Is.Null);Assert.That(s.ValidateState(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("DP41")] public IEnumerator TechniqueGateFourActualStudyClicksAndSaveLoad()
        {
            IsolateAlchemyTestMouse();var s = shop.Session;
            Assert.That(s.GrantJadeSlipForVerification("test-jade-technique"));shop.Refresh();yield return null;
            var item = s.Items.Last();yield return ClickJade(item);
            Assert.That(shop.FindButton("KnowledgeStudy").interactable, Is.False);Assert.That(KnowledgeLabel("KnowledgeState"), Does.Contain("结束本回合营业"));
            yield return Click("KnowledgeClose");yield return Click("BeginBusiness");while(s.PendingVisitScene != null)yield return Click("VisitSceneContinue");
            yield return ClickJade(item);Assert.That(shop.FindButton("KnowledgeStudy").interactable, Is.False);
            yield return Click("KnowledgeClose");yield return Click("EndBusiness");yield return ClickJade(item);
            for(int n = 0; n < 4; n++)
            {
                yield return Click("KnowledgeStudy");Assert.That(s.Stamina, Is.EqualTo(80-20*n));
                Assert.That(s.GetKnowledgeProgress("knowledge_test_basic").Progress, Is.EqualTo(25+25*n));
            }
            Assert.That(KnowledgeLabel("KnowledgeState"), Does.Contain("已掌握"));Assert.That(shop.FindButton("KnowledgeStudy").interactable, Is.False);
            yield return Click("KnowledgeClose");shop.OpenSystemMenu();yield return Click("SaveSlot_1");
            yield return Click("SystemNewGame");yield return Click("SaveConfirm");Assert.That(shop.IsKnowledgeWindowOpen, Is.False);
            Assert.That(shop.Session.GetKnowledgeProgress("knowledge_test_basic").Progress, Is.Zero);
            shop.OpenSystemMenu();yield return Click("LoadSlot_1");yield return Click("SaveConfirm");yield return Click("SystemResume");
            var loaded = shop.Session;Assert.That(loaded.GetKnowledgeProgress("knowledge_test_basic").Mastered);Assert.That(loaded.Stamina, Is.EqualTo(20));
            yield return ClickJade(loaded.Items.Single(i => i.Definition.id == "test-jade-technique"));Assert.That(KnowledgeLabel("KnowledgeState"), Does.Contain("100/100"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Category("DP41")] public IEnumerator DuplicateCopiesReopenShareProgressAndCloseDoesNotSpend()
        {
            IsolateAlchemyTestMouse();var s = shop.Session;
            Assert.That(s.GrantJadeSlipForVerification("test-jade-technique"));Assert.That(s.GrantJadeSlipForVerification("test-jade-technique"));
            shop.Refresh();yield return null;yield return CloseBusinessForKnowledge();
            var copies = s.Items.Where(i => i.Definition.id == "test-jade-technique").ToArray();
            yield return ClickJade(copies[0]);yield return Click("KnowledgeStudy");yield return Click("KnowledgeClose");
            yield return ClickJade(copies[1]);Assert.That(KnowledgeLabel("KnowledgeState"), Does.Contain("25/100"));
            yield return Click("KnowledgeClose");Assert.That(s.Stamina, Is.EqualTo(80));Assert.That(s.GetKnowledgeProgress("knowledge_test_basic").Progress, Is.EqualTo(25));
            Assert.That(s.TrySpendStamina(61));yield return ClickJade(copies[1]);Assert.That(shop.FindButton("KnowledgeStudy").interactable, Is.False);
            Assert.That(KnowledgeLabel("KnowledgeState"), Does.Contain("体力不足"));yield return Click("KnowledgeClose");Assert.That(s.Stamina, Is.EqualTo(19));
            Assert.That(s.ValidateState(), Is.Null);LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
