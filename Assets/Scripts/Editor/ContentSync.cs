using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XiuXianShop.Editor
{
    [Serializable] public sealed class ContentSnapshotInfo
    {
        public int formatVersion;
        public string snapshotId, description;
        public string[] domains;
    }
    public sealed class ContentSyncPlan : IDisposable
    {
        public readonly List<ContentIssue> issues=new List<ContentIssue>();
        public readonly List<string> changes=new List<string>();
        internal readonly Dictionary<string,ContentRow> rows=new Dictionary<string,ContentRow>();
        internal readonly Dictionary<string,string> inputs=new Dictionary<string,string>();
        internal ShopCatalog catalog;
        internal ShopCatalog stagedCatalog;
        internal AuthoredContent target,staged;
        internal string previousJson,catalogJson;
        public ContentSnapshotInfo Snapshot {get;internal set;}
        public bool Valid=>!issues.Any(i=>i.error);
        public string Summary=>$"{issues.Count(i=>i.error)} 个错误 · {changes.Count} 条差异 / 提示";
        public void Error(string table,string id,string field,string reason)
        {
            rows.TryGetValue(table+"/"+id,out var row);
            issues.Add(new ContentIssue{table=table,id=id,field=field,reason=reason,line=row?.line??0,error=true});
        }
        public void Dispose(){if(staged!=null)UnityEngine.Object.DestroyImmediate(staged);if(stagedCatalog!=null)UnityEngine.Object.DestroyImmediate(stagedCatalog);}
    }
    public static class ContentSync
    {
        public const string SourceRoot="ContentSources";
        public const string CatalogPath="Assets/Data/ShopCatalog.asset";
        public static string[] Discover(string root)=>Directory.Exists(root)?Directory.GetFiles(root,"snapshot.json",SearchOption.AllDirectories).Select(Path.GetDirectoryName).OrderBy(p=>p,StringComparer.Ordinal).ToArray():Array.Empty<string>();
        public static ContentSyncPlan Validate(string directory,ShopCatalog catalog,AuthoredContent target)
        {
            var plan=new ContentSyncPlan{catalog=catalog,target=target};
            if(catalog==null || target==null){plan.Error("配置","","Catalog","需要现有 ShopCatalog 与其内容资产引用。");return plan;}
            plan.previousJson=EditorJsonUtility.ToJson(target);plan.catalogJson=EditorJsonUtility.ToJson(catalog);
            plan.staged=UnityEngine.Object.Instantiate(target);plan.staged.name=target.name;
            plan.stagedCatalog=UnityEngine.Object.Instantiate(catalog);plan.stagedCatalog.name=catalog.name;
            string Read(string name)
            {
                string path=Path.Combine(directory,name);
                try {string text=File.ReadAllText(path);plan.inputs[path]=text;return text;}
                catch(Exception e){plan.Error(name,"","文件",e.Message);return null;}
            }
            string metadata=Read("snapshot.json");
            if(metadata!=null)
            {
                try {plan.Snapshot=JsonUtility.FromJson<ContentSnapshotInfo>(metadata);}
                catch(Exception e){plan.Error("snapshot.json","","JSON",e.Message);}
                if(plan.Snapshot==null || plan.Snapshot.formatVersion!=1 || string.IsNullOrWhiteSpace(plan.Snapshot.snapshotId))
                    plan.Error("snapshot.json","","snapshotId / formatVersion","需要非空快照标识与 formatVersion=1。");
            }
            var adapters=ContentDomainAdapter.CreateFor(plan.Snapshot?.domains,plan);
            foreach(var adapter in adapters)
            {
                string csv=Read(adapter.FileName);if(csv!=null)adapter.Stage(plan,csv);
            }
            // Duplicate IDs and parse failures must not reach relationship lookup or asset mutation.
            if(plan.Valid)foreach(var adapter in adapters)adapter.Validate(plan);
            return plan;
        }
        public static bool Import(ContentSyncPlan plan)
        {
            if(!plan.Valid)throw new InvalidOperationException("校验失败，运行时内容未修改。");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请退出 Play 后导入静态内容。");
            foreach(var input in plan.inputs)
                if(!File.Exists(input.Key) || File.ReadAllText(input.Key)!=input.Value)throw new InvalidOperationException("源快照已变更，请重新 Validate / Preview 后导入。");
            if(EditorJsonUtility.ToJson(plan.target)!=plan.previousJson || EditorJsonUtility.ToJson(plan.catalog)!=plan.catalogJson)
                throw new InvalidOperationException("运行时资产或引用目录已变更，请重新 Validate / Preview。");
            if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(plan.target)))throw new InvalidOperationException("目标须为已保存的内容资产。");
            // One target asset is committed only after every domain passed. Never apply rows piecemeal.
            plan.staged.lastSnapshotId=plan.Snapshot.snapshotId;
            plan.staged.lastImportedAtUtc=plan.target.lastImportedAtUtc;
            bool catalogChanged=EditorJsonUtility.ToJson(plan.stagedCatalog)!=plan.catalogJson;
            if(catalogChanged && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(plan.catalog)))throw new InvalidOperationException("配置导入目标须为已保存的 Catalog 资产。");
            if(EditorJsonUtility.ToJson(plan.staged)==plan.previousJson && !catalogChanged)return false;
            plan.staged.lastImportedAtUtc=DateTime.UtcNow.ToString("O");
            Undo.RecordObject(plan.target,"Import authored content");
            if(catalogChanged)Undo.RecordObject(plan.catalog,"Import gameplay configuration");
            try
            {
                EditorUtility.CopySerialized(plan.staged,plan.target);EditorUtility.SetDirty(plan.target);AssetDatabase.SaveAssetIfDirty(plan.target);
                if(catalogChanged){EditorUtility.CopySerialized(plan.stagedCatalog,plan.catalog);EditorUtility.SetDirty(plan.catalog);AssetDatabase.SaveAssetIfDirty(plan.catalog);}
            }
            catch
            {
                EditorJsonUtility.FromJsonOverwrite(plan.previousJson,plan.target);EditorUtility.SetDirty(plan.target);AssetDatabase.SaveAssetIfDirty(plan.target);
                if(catalogChanged){EditorJsonUtility.FromJsonOverwrite(plan.catalogJson,plan.catalog);EditorUtility.SetDirty(plan.catalog);AssetDatabase.SaveAssetIfDirty(plan.catalog);}throw;
            }
            // Repeated Import of this exact reviewed plan is also a no-op.
            plan.previousJson=EditorJsonUtility.ToJson(plan.target);
            plan.catalogJson=EditorJsonUtility.ToJson(plan.catalog);
            return true;
        }
    }
}
