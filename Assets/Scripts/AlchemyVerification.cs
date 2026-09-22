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
            AddDefinitions(catalog);
            catalog.Find("test-portable").storageSize=new Vector2Int(6,4);
            catalog.Find("test-portable").title="灰盒材料包";
            catalog.startingItems=new[]{"test-portable","herb","dew","mat_fire_herb","mat_fire_fruit","mat_metal_herb","mat_metal_fruit","mat_water_fruit","stone_mid","stone_low"};
            catalog.travelLocations.Single(l=>l.id==ShopSession.AlchemyLocationId).initiallyUnlocked=true;
        }
        public const string MaterialPackId="alchemy-test-pack";
        public static void AddShopFurnaceDefinition(ShopCatalog catalog)
        {
            AddDefinitions(catalog);
            if(catalog.items.Any(d=>d.id==ShopSession.ShopFurnaceDefinitionId))return;
            catalog.items=catalog.items.Concat(new[]{new ItemDefinition {
                id=ShopSession.ShopFurnaceDefinitionId,title="微缩炼丹炉（灰盒）",category=ItemCategory.ProductionEquipment,
                baseValue=240,cells=Enumerable.Range(0,9).Select(n=>new Vector2Int(n%3,n/3)).ToArray(),
                supplierAvailable=false,color=new Color(.55f,.47f,.38f),description="点击打开店内炼丹。3×3与价值240取自候选数据，仅作灰盒参考。"
            }}).ToArray();
        }
        public static void AddDefinitions(ShopCatalog catalog)
        {
            SpiritStoneVerification.AddDefinitions(catalog);
            Vector2Int[] herb={new Vector2Int(0,0),new Vector2Int(0,1),new Vector2Int(1,1)};
            Vector2Int[] fruit={new Vector2Int(0,0),new Vector2Int(1,0)};
            var additions=new[] {
                Material("mat_fire_herb","赤炎草",5,herb),Material("mat_fire_fruit","火灵果",9,fruit),
                Material("mat_metal_herb","金凝草",5,herb),Material("mat_metal_fruit","金灵果",9,fruit),Material("mat_water_fruit","水灵果",9,fruit),
                Pill("pill_fire_yang","赤阳丹",48),Pill("pill_metal_water","金水凝元丹",72)};
            catalog.items=catalog.items.Concat(additions.Where(d=>!catalog.items.Any(existing=>existing.id==d.id))).ToArray();
            if(!catalog.items.Any(d=>d.id==MaterialPackId))
                catalog.items=catalog.items.Concat(new[]{new ItemDefinition {
                    id=MaterialPackId,title="炼丹材料包（开发测试）",category=ItemCategory.PortableContainer,
                    cells=new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(1,1),new Vector2Int(0,2),new Vector2Int(1,2)},
                    storageSize=new Vector2Int(6,4),supplierAvailable=false,color=new Color(.5f,.6f,.4f)
                }}).ToArray();
            if(!catalog.travelLocations.Any(l=>l.id==ShopSession.AlchemyLocationId))
                catalog.travelLocations=catalog.travelLocations.Concat(new[]{new TravelLocation{id=ShopSession.AlchemyLocationId,title="炼丹房（开发灰盒）"}}).ToArray();
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
