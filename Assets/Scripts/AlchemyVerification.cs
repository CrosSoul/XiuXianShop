using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    // DP58 explicitly authorizes these candidates for graybox use, not a formal data import.
    public static class AlchemyVerification
    {
        public static void Configure(ShopCatalog catalog)
        {
            SpiritStoneVerification.Configure(catalog);
            Vector2Int[] herb={new Vector2Int(0,0),new Vector2Int(0,1),new Vector2Int(1,1)};
            Vector2Int[] fruit={new Vector2Int(0,0),new Vector2Int(1,0)};
            var additions=new[] {
                Material("mat_fire_herb","赤炎草",5,herb),Material("mat_fire_fruit","火灵果",9,fruit),
                Material("mat_metal_herb","金凝草",5,herb),Material("mat_metal_fruit","金灵果",9,fruit),Material("mat_water_fruit","水灵果",9,fruit),
                Pill("pill_fire_yang","赤阳丹",48),Pill("pill_metal_water","金水凝元丹",72)};
            catalog.items=catalog.items.Concat(additions).ToArray();
            catalog.Find("test-portable").storageSize=new Vector2Int(6,4);
            catalog.Find("test-portable").title="灰盒材料包";
            catalog.startingItems=new[]{"test-portable","herb","dew","mat_fire_herb","mat_fire_fruit","mat_metal_herb","mat_metal_fruit","mat_water_fruit","stone_mid","stone_low"};
            catalog.travelLocations=catalog.travelLocations.Concat(new[]{new TravelLocation{id=ShopSession.AlchemyLocationId,title="炼丹房（灰盒）",initiallyUnlocked=true}}).ToArray();
        }
        static ItemDefinition Material(string id,string title,int value,Vector2Int[] cells) => new ItemDefinition {
            id=id,title=title,baseValue=value,cells=cells,category=ItemCategory.Material,supplierAvailable=false,
            description="DP58灰盒候选，形状与价格为参考值，不是正式数据同步。",color=new Color(.65f,.65f,.35f)};
        static ItemDefinition Pill(string id,string title,int value) => new ItemDefinition {
            id=id,title=title,baseValue=value,category=ItemCategory.Medicine,supplierAvailable=false,
            cells=new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(1,1)},
            description="DP58灰盒产物，品相保存在实例上。",color=new Color(.9f,.65f,.3f)};
    }
}
