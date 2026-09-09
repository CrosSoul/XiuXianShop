using UnityEngine;
using UnityEngine.UI;

namespace XiuXianShop
{
    public sealed partial class ShopPrototype
    {
        RectTransform storageWindow;
        int openStorageId;
        public int OpenStorageId => openStorageId;

        public void OpenStorage(int id)
        {
            var box=Session.Find(id);
            if(!box.Definition.IsStorage || box.ForSale || box.Container!=ContainerId.Storage)return;
            var size=box.Definition.storageSize;
            if(size.x<1 || size.y<1){localNotice="该储存物品的内部尺寸尚未配置，不能打开。";return;}
            CloseStorage();openStorageId=id;
            float cell=Mathf.Min(32,400f/Mathf.Max(size.x,size.y));
            float width=Mathf.Max(350,size.x*cell+24),height=size.y*cell+100;
            storageWindow=Rect(content,"StorageWindow",990,300,width,height);
            Image(storageWindow,panel,true);
            var title=Rect(storageWindow,"StorageTitleBar",0,0,width,40);Image(title,line,true);
            title.gameObject.AddComponent<StorageWindowDrag>().window=storageWindow;
            Label(title,"Title",10,8,width-64,28,box.Definition.title,19,gold);
            var close=Rect(title,"StorageClose",width-42,4,36,32);Image(close,line,true);
            close.gameObject.AddComponent<Button>().onClick.AddListener(()=>{CloseStorage();Refresh();});
            Label(close,"Label",10,3,25,26,"×",22,textColor);
            var grid=Rect(storageWindow,"InteriorGrid",12,46,size.x*cell,size.y*cell);
            grids[ContainerId.Interior]=grid;cellSizes[ContainerId.Interior]=cell;
            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
                Image(Rect(grid,$"Slot_{x}_{y}",x*cell,y*cell,cell-2,cell-2),new Color(.12f,.18f,.21f));
            Label(storageWindow,"StorageHint",12,height-47,width-24,42,"拖动标题移动窗口；拖入/取出物品。\n当前仅打开一个储存窗口；关闭保留内容。",15,muted);
            Refresh();
        }

        public void CloseStorage()
        {
            CancelDrag();
            if(storageWindow!=null){storageWindow.gameObject.SetActive(false);Destroy(storageWindow.gameObject);}
            storageWindow=null;openStorageId=0;
            grids.Remove(ContainerId.Interior);cellSizes.Remove(ContainerId.Interior);
        }
    }
}
