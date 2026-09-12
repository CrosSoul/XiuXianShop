using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    // DP-33 isolated fixture only: never called by normal startup or written to the catalog asset.
    public static class SpiritStoneVerification
    {
        public static void Configure(ShopCatalog catalog)
        {
            catalog.SetStorageVerificationDefaults();
            ItemDefinition Stone(string id,string title,ItemCategory category,int equivalents,int shell,bool reusable,int width,int height)
                => new ItemDefinition {id=id,title=title,category=category,
                    description="DP-33隔离测试：精度0.01、壳价为临时测试值；下品1格、中品1×2竖向、上品2×2为已确认形状。",
                    color=new Color(.65f,.76f,.83f),cells=Enumerable.Range(0,width*height).Select(n=>new Vector2Int(n%width,n/width)).ToArray(),
                    spiritResource=new SpiritResourceDefinition(equivalents*100,100,shell,reusable)};
            catalog.items=catalog.items.Concat(new[]{
                Stone("stone_low","下品灵石",ItemCategory.StoneLow,1,0,false,1,1),
                Stone("stone_mid","中品灵石",ItemCategory.StoneMid,100,7,true,1,2),
                Stone("stone_high","上品灵石",ItemCategory.StoneHigh,10000,23,true,2,2)}).ToArray();
            catalog.startingItems=new[]{"stone_low","stone_mid","stone_mid","stone_high","test-portable"};
            catalog.marketEvents=System.Array.Empty<MarketEventDefinition>();
        }
    }
}
