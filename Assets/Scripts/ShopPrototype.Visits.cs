using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform visitWindow;
        StoryRunner visitPreview;

        void TickVisitScene()
        {
            var request=Session?.PendingVisitScene;
            if(request==visitPreview?.Request)return;
            CloseVisitPreview();
            if(request==null)return;
            CancelDrag();NegotiationView.Close();
            visitPreview=new StoryRunner(Session,request);
            DrawVisitPreview();
        }
        void DrawVisitPreview()
        {
            if(visitWindow!=null){visitWindow.gameObject.SetActive(false);Destroy(visitWindow.gameObject);}
            visitWindow=Rect(content,"VisitSceneGraybox",0,0,1600,1000);
            var background=Image(visitWindow,new Color(.02f,.04f,.05f,.98f),true);
            if(visitPreview.BackgroundSprite!=null){background.sprite=visitPreview.BackgroundSprite;background.color=Color.white;}
            Label(visitWindow,"VisitSceneTitle",100,35,1400,60,visitPreview.Background??"剧情场景 · 灰盒",30,gold);
            if(visitPreview.LeftPortrait!=null || visitPreview.LeftSprite!=null)
            {
                var left=Image(Rect(visitWindow,"StoryLeftPortrait",120,130,350,350),panel);
                if(visitPreview.LeftSprite!=null){left.sprite=visitPreview.LeftSprite;left.color=Color.white;left.preserveAspect=true;}
                Label(visitWindow,"StoryLeftName",140,220,300,160,visitPreview.LeftPortrait+"\n"+visitPreview.LeftExpression,28,textColor);
            }
            if(visitPreview.RightPortrait!=null || visitPreview.RightSprite!=null)
            {
                var right=Image(Rect(visitWindow,"StoryRightPortrait",1130,130,350,350),panel);
                if(visitPreview.RightSprite!=null){right.sprite=visitPreview.RightSprite;right.color=Color.white;right.preserveAspect=true;}
                Label(visitWindow,"StoryRightName",1150,220,300,160,visitPreview.RightPortrait+"\n"+visitPreview.RightExpression,28,textColor);
            }
            if(visitPreview.Comic!=null || visitPreview.ComicSprite!=null)
            {
                var comic=Image(Rect(visitWindow,"StoryComic",500,130,600,350),line);
                if(visitPreview.ComicSprite!=null){comic.sprite=visitPreview.ComicSprite;comic.color=Color.white;comic.preserveAspect=true;}
                Label(visitWindow,"StoryComicCaption",540,170,520,260,visitPreview.Comic,27,textColor);
            }
            var node=visitPreview.Current;
            string text=node.type=="End"?"对话结束。":(string.IsNullOrEmpty(node.speaker)?"":node.speaker+"\n")+node.text;
            Label(visitWindow,"VisitSceneText",160,510,1250,230,text,25,textColor);
            CarryButton(visitWindow,"VisitSceneCancel",170,830,270,"暂不继续",()=>{
                visitPreview.Cancel();CloseVisitPreview();Refresh();});
            if(node.type=="Choice")
            {
                var viewport=Rect(visitWindow,"StoryChoices",500,750,950,210);
                viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                var rows=Rect(viewport,"Rows",0,0,930,Mathf.Max(210,node.choices.Length*65));
                var scroll=viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.scrollSensitivity=30;
                for(int i=0;i<node.choices.Length;i++)
                {
                    int choice=i;
                    var button=CarryButton(rows,"StoryChoice_"+i,0,i*65,920,node.choices[i],()=>AdvanceStory(()=>visitPreview.Choose(choice)));
                    button.interactable=visitPreview.ChoiceAvailable(i);
                }
            }
            else CarryButton(visitWindow,"VisitSceneContinue",1090,830,300,node.type=="End"?"结束对话":"继续",()=>AdvanceStory(visitPreview.Continue));
        }
        void AdvanceStory(System.Func<bool> advance)
        {
            if(!advance()){Label(visitWindow,"VisitSceneResult",460,700,800,65,Session.Message,20,gold);return;}
            if(visitPreview.Finished){CloseVisitPreview();Refresh();}
            else DrawVisitPreview();
        }
        void CloseVisitPreview()
        {
            if(visitWindow!=null){visitWindow.gameObject.SetActive(false);Destroy(visitWindow.gameObject);}
            visitWindow=null;visitPreview=null;
        }
    }
}
