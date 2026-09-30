using System;
using System.Linq;

namespace XiuXianShop.Editor
{
    public sealed class StartProfileContentAdapter : ContentDomainAdapter
    {
        readonly string file;
        StartProfileContentAdapter(string file){this.file=file;}
        public override string FileName=>file+".csv";
        public static ContentDomainAdapter[] Create()=>new[]{new StartProfileContentAdapter("start-profiles"),new StartProfileContentAdapter("start-items"),new StartProfileContentAdapter("start-states")};
        public override void Stage(ContentSyncPlan p,string csv)
        {
            var c=p.staged;
            if(file=="start-profiles")c.startProfiles=Merge(p,csv,"ProfileID","配置名称|ProfileID|开始年份|开始月份|初始卡内余额|初始体力|数据状态",c.startProfiles,(r,old)=>new StartProfile{
                title=r.Required("配置名称"),year=r.Number("开始年份",1,true).value,month=r.Number("开始月份",1,true).value,money=r.Number("初始卡内余额",0,true).value,stamina=r.Number("初始体力",0,true).value});
            else if(file=="start-items")c.startItems=Merge(p,csv,"条目ID","条目ID|ProfileID|物品ID|数量|目标区域|X|Y|旋转|数据状态",c.startItems,(r,old)=>{
                string area=r.OneOf("目标区域","Storage","Display","Counter");
                return new StartItem{profileId=r.Required("ProfileID"),itemId=r.Required("物品ID"),quantity=r.Number("数量",1,true).value,area=area=="Display"?ContainerId.Display:area=="Counter"?ContainerId.Counter:ContainerId.Storage,
                    x=r.Number("X",0),y=r.Number("Y",0),rotation=r.Number("旋转",0)};});
            else c.startStates=Merge(p,csv,"条目ID","条目ID|ProfileID|状态类型|状态ID|状态值|数据状态",c.startStates,(r,old)=>new StartState{
                profileId=r.Required("ProfileID"),type=r.OneOf("状态类型","Location","Profession","Flag","Knowledge","Recipe"),stateId=r.Required("状态ID"),value=r.OneOf("状态值","true","false")=="true"});
        }
        public override void Validate(ContentSyncPlan p)
        {
            if(file!="start-states")return;
            var c=p.staged;var catalog=p.stagedCatalog;
            var ids=c.startProfiles.Where(r=>r.enabled).Select(r=>r.id).ToArray();
            foreach(var row in c.startItems.Where(r=>r.enabled))
            {
                if(!ids.Contains(row.profileId))p.Error("start-items.csv",row.id,"ProfileID","未知或停用开局配置。");
                if(!catalog.items.Any(i=>i.id==row.itemId))p.Error("start-items.csv",row.id,"物品ID","未知物品："+row.itemId);
            }
            foreach(var row in c.startStates.Where(r=>r.enabled))
            {
                if(!ids.Contains(row.profileId))p.Error(FileName,row.id,"ProfileID","未知或停用开局配置。");
                bool known=row.type=="Flag" || row.type=="Location" && catalog.travelLocations.Any(l=>l.id==row.stateId) || row.type=="Profession" && row.stateId=="alchemy" || row.type=="Recipe" && catalog.alchemy.recipes.Any(r=>r.id==row.stateId);
                if(!known)p.Error(FileName,row.id,"状态ID","未知引用或当前尚未实现的状态（Knowledge尚无正式系统）："+row.stateId);
            }
            foreach(var group in c.startStates.Where(r=>r.enabled).GroupBy(r=>(r.profileId,r.type,r.stateId)).Where(g=>g.Count()>1))
                p.Error(FileName,group.First().id,"状态ID","同一Profile的同一状态重复。");
            if(!ids.Contains(catalog.startProfileId))p.Error("start-profiles.csv",catalog.startProfileId,"ProfileID","默认开局Profile必须有效。");
            if(!p.Valid)return;
            // Reuse the real constructor to validate grids; no separate placement simulator.
            var previous=catalog.authoredContent;catalog.authoredContent=c;
            try
            {
                foreach(string id in ids)
                    try{ShopSession.NewGame(catalog,id,68);}
                    catch(Exception e){p.Error("start-profiles.csv",id,"开局构建",e.Message);}
            }
            finally{catalog.authoredContent=previous;}
        }
    }
}
