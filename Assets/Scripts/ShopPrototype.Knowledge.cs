using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform knowledgeWindow;
        string readingKnowledgeId;
        UnityEngine.UI.Text knowledgeState;
        UnityEngine.UI.Button studyKnowledgeButton;
        public bool IsKnowledgeWindowOpen => knowledgeWindow != null;

        void OpenKnowledgeWindow(GridItem item)
        {
            if(item.Owner != ItemOwner.Player || Session.IsTravelling)return;
            var definition = Session.KnowledgeForItem(item);
            if(definition == null){localNotice = "这枚玉简尚未配置可读内容。";return;}
            CloseKnowledgeWindow();CancelDrag();readingKnowledgeId = definition.id;
            knowledgeWindow = Rect(content, "KnowledgeWindow", 960, 230, 600, 460);
            Image(knowledgeWindow, panel, true);
            var title = Rect(knowledgeWindow, "KnowledgeTitleBar", 0, 0, 600, 44);Image(title, line, true);
            title.gameObject.AddComponent<StorageWindowDrag>().window = knowledgeWindow;
            Label(title, "KnowledgeTitle", 12, 8, 510, 32, definition.title, 22, gold);
            CarryButton(title, "KnowledgeClose", 544, 4, 46, "×", CloseKnowledgeWindow);
            Label(knowledgeWindow, "KnowledgeType", 16, 52, 568, 30, definition.type == KnowledgeType.Text ? "普通文本" : "功法 · 可学习", 18, muted);
            var viewport = Rect(knowledgeWindow, "KnowledgeViewport", 16, 90, 568, 230);
            Image(viewport, new Color(.07f,.105f,.12f), true);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var body = Label(viewport, "KnowledgeBody", 8, 4, 540, 226, definition.text, 20, textColor);
            body.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;scroll.content = body.rectTransform;scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity = 25;
            knowledgeState = Label(knowledgeWindow, "KnowledgeState", 16, 334, 568, 68, "", 18, textColor);
            studyKnowledgeButton = CarryButton(knowledgeWindow, "KnowledgeStudy", 16, 408, 568, "学习", () => Run(() => Session.StudyKnowledge(readingKnowledgeId)));
            studyKnowledgeButton.gameObject.SetActive(definition.type == KnowledgeType.Technique);
            RefreshKnowledgeWindow();
        }

        void RefreshKnowledgeWindow()
        {
            if(knowledgeWindow == null)return;
            var definition = Session.FindKnowledge(readingKnowledgeId);
            if(definition.type == KnowledgeType.Text){knowledgeState.text = "阅读不消耗体力，也不消耗玉简。";return;}
            var state = Session.GetKnowledgeProgress(readingKnowledgeId);
            studyKnowledgeButton.interactable = Session.CanStudyKnowledge(readingKnowledgeId, out string reason);
            studyKnowledgeButton.GetComponentInChildren<UnityEngine.UI.Text>().text = $"学习 · 体力 {definition.studyStamina} · 进度 +{definition.progressPerStudy}";
            knowledgeState.text = $"进度 {state.Progress}/{definition.masteryThreshold} · {(state.Mastered ? "已掌握" : "未掌握")}\n" +
                (studyKnowledgeButton.interactable ? "闭店后可重复学习；关闭窗口不消耗资源。" : reason);
        }

        public void CloseKnowledgeWindow()
        {
            if(knowledgeWindow != null){knowledgeWindow.gameObject.SetActive(false);Destroy(knowledgeWindow.gameObject);}
            knowledgeWindow = null;readingKnowledgeId = null;
        }
    }
}
