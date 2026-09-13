using UnityEngine;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform teaWindow;
        public void OpenTeaNews()
        {
            CancelDrag();CloseTeaNews();
            teaWindow=Rect(content,"TeaNewsWindow",0,0,1600,1000);
            Image(teaWindow,new Color(.02f,.04f,.05f,.97f),true);
            Label(teaWindow,"TeaNewsTitle",160,100,1250,60,"听风茶肆 · 坊市消息",30,gold);
            Label(teaWindow,"TeaNewsDetails",160,200,1250,590,Session.TeaNewsText,23,textColor);
            CarryButton(teaWindow,"TeaNewsClose",1150,830,240,"关闭",CloseTeaNews);
        }
        void CloseTeaNews()
        {
            if(teaWindow!=null){teaWindow.gameObject.SetActive(false);Destroy(teaWindow.gameObject);}
            teaWindow=null;
        }
    }
}
