using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XiuXianShop.Editor
{
    public sealed class ContentSyncWindow : EditorWindow
    {
        DropdownField sources;
        Label latest,summary;
        ScrollView report;
        Button import;
        ContentSyncPlan plan;
        [MenuItem("XiuXianShop/内容同步 (Content Sync)")]
        public static void Open()=>GetWindow<ContentSyncWindow>("内容同步");
        public void CreateGUI()
        {
            minSize=new Vector2(650,430);
            var root=rootVisualElement;root.Clear();root.style.paddingLeft=12;root.style.paddingRight=12;root.style.paddingTop=10;
            root.Add(new Label("编辑 / 导出快照 → Validate → Preview Diff → Import → Play"));
            root.Add(new Label("源目录：ContentSources/；只导入明确待同步内容，草稿不进入运行时。"));
            sources=new DropdownField("内容快照");root.Add(sources);sources.RegisterValueChangedCallback(_=>Invalidate());
            root.Add(new Button(RefreshSources){text="刷新快照列表"});
            var actions=new VisualElement();actions.style.flexDirection=FlexDirection.Row;root.Add(actions);
            actions.Add(new Button(()=>Review(false)){text="Validate"});
            actions.Add(new Button(()=>Review(true)){text="Preview Diff"});
            import=new Button(ImportReviewed){text="Import"};actions.Add(import);import.SetEnabled(false);
            latest=new Label();root.Add(latest);summary=new Label();root.Add(summary);
            report=new ScrollView();report.style.flexGrow=1;root.Add(report);
            RefreshSources();
        }
        void RefreshSources()
        {
            sources.choices=ContentSync.Discover(ContentSync.SourceRoot).ToList();
            sources.value=sources.choices.FirstOrDefault()??"";Invalidate();ShowLatest();
        }
        void ShowLatest()
        {
            var content=AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath)?.authoredContent;
            latest.text=content==null?"缺少 Catalog 内容资产引用。":"最近成功导入："+(string.IsNullOrEmpty(content.lastSnapshotId)?"尚无":content.lastSnapshotId+" · "+content.lastImportedAtUtc);
        }
        void Invalidate(){plan?.Dispose();plan=null;import?.SetEnabled(false);report?.Clear();if(summary!=null)summary.text="请选择快照并校验。";}
        void Review(bool diff)
        {
            Invalidate();var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>(ContentSync.CatalogPath);
            plan=ContentSync.Validate(sources.value,catalog,catalog?.authoredContent);
            summary.text=plan.Summary;report.Clear();
            foreach(var issue in plan.issues)AddLine(issue.ToString(),issue.error);
            if(diff)foreach(var change in plan.changes)AddLine(change,false);
            if(plan.Valid && diff && plan.changes.Count==0)AddLine("无内容差异。",false);
            import.SetEnabled(plan.Valid && diff);
        }
        void AddLine(string text,bool error)
        {
            var label=new Label(text);label.style.whiteSpace=WhiteSpace.Normal;label.style.marginBottom=5;
            if(error)label.style.color=new Color(.95f,.4f,.35f);report.Add(label);
        }
        void ImportReviewed()
        {
            try {bool changed=ContentSync.Import(plan);summary.text=changed?"导入成功。运行时只读取本地资产。":"相同快照，无需重复写入。";ShowLatest();}
            catch(Exception e){summary.text=e.Message;import.SetEnabled(false);}
        }
        void OnDisable(){plan?.Dispose();plan=null;}
    }
}
