using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform commissionWindow;

        public void OpenCommissions()
        {
            CancelDrag();CloseCommissions();
            commissionWindow=Rect(content,"CommissionWindow",0,0,1600,1000);
            Image(commissionWindow,new Color(.035f,.06f,.07f),true);
            Label(commissionWindow,"Title",100,65,1400,60,"百事堂 · 本月委托",32,gold);
            Label(commissionWindow,"Hint",100,130,1400,80,
                Session.CommissionLimitReached?"本月委托已完成，请带走领取区实物。":
                "选择一项，即时完成。只支付进入地点的体力，实物报酬需自行带回。",24,textColor);
            var viewport=Rect(commissionWindow,"CommissionList",90,235,1420,540);
            Image(viewport,panel,true);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var candidates=System.Linq.Enumerable.ToArray(Session.CommissionCandidates);
            var rows=Rect(viewport,"Rows",0,0,1400,Mathf.Max(540,candidates.Length*175));
            var scroll=viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.scrollSensitivity=35;
            for(int i=0;i<candidates.Length;i++)
            {
                var template=candidates[i];float y=i*175;
                Label(rows,"CommissionTitle_"+template.id,25,y+5,970,35,template.title,26,gold);
                Label(rows,"CommissionDescription_"+template.id,25,y+45,975,70,template.description,21,textColor);
                Label(rows,"CommissionReward_"+template.id,25,y+116,960,42,template.rewardHint,22,gold);
                var button=CarryButton(rows,"CommissionComplete_"+template.id,1040,y+58,320,"选择并完成",()=>
                {
                    if(Session.CompleteCommission(template.id)){CloseCommissions();ShowTravelWindow();}
                    else { CloseCommissions();ShowTravelWindow();locationNotice.text=Session.Message; }
                });
                button.interactable=!Session.CommissionLimitReached;
            }
            CarryButton(commissionWindow,"CommissionClose",1100,825,390,"返回领取区",CloseCommissions);
        }

        void CloseCommissions()
        {
            if(commissionWindow!=null){commissionWindow.gameObject.SetActive(false);Destroy(commissionWindow.gameObject);}
            commissionWindow=null;
        }
    }
}
