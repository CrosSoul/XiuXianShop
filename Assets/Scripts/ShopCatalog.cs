using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    public enum ItemCategory { Unclassified, Medicine, Material, Equipment, Container, BusinessSign, StorageContainer, EquipmentContainer, PortableContainer, ProductionEquipment }

    [Serializable]
    public sealed class BuyerBudgetTier
    {
        [Min(0), Tooltip("此档展示基础价值总和下限，包含该数值；下一档下限不包含在此档。")]
        public int minimumDisplayValue;
        [Min(1)] public int baseBudget;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string id;
        public string title;
        public ItemCategory category;
        [TextArea] public string description;
        public Color color = Color.white;
        public Vector2Int[] cells;
        [Min(0), Tooltip("商品固有价值；成交报价在此基础上按标签计算。")]
        public int baseValue;
        public bool supplierAvailable = true;
        [Tooltip("储存物品内部格子尺寸；0 表示尚未配置，不能打开。外部形状仍由 cells 决定。")]
        public Vector2Int storageSize;
        [Tooltip("设备储存可接纳的生产设备稳定 ID；只配置已确认或隔离测试的兼容项。")]
        public string[] compatibleEquipmentIds = Array.Empty<string>();
        public bool IsStorage => category==ItemCategory.StorageContainer || category==ItemCategory.EquipmentContainer || category==ItemCategory.PortableContainer;
        // Retained only for one-time migration of existing assets, never read by trading.
        [HideInInspector] public int purchasePrice;
        [HideInInspector] public int salePrice;
        public bool procurementSign;
        [Tooltip("仅广告牌使用：吸引出售此类别商品的顾客。")]
        public ItemCategory advertisedCategory = ItemCategory.Material;

        // Transform coordinates, then normalize the shape to its top-left bounding box.
        public Vector2Int[] Shape(int rotation, bool flipped)
        {
            var result = cells.Select(p => new Vector2Int(flipped ? -p.x : p.x, p.y)).ToArray();
            for (int r = 0; r < ((rotation % 4) + 4) % 4; r++)
                for (int i = 0; i < result.Length; i++) result[i] = new Vector2Int(-result[i].y, result[i].x);
            int minX = result.Min(p => p.x), minY = result.Min(p => p.y);
            for (int i = 0; i < result.Length; i++) result[i] -= new Vector2Int(minX, minY);
            return result;
        }
    }

    [CreateAssetMenu(menuName = "XiuXianShop/Prototype Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [HideInInspector] public int priceModelVersion;
        public ItemDefinition[] items;
        public string[] startingItems = { "sign", "herb", "dew", "pill", "cinnabar", "jade", "sword" };
        public string herbId = "herb";
        public string dewId = "dew";
        public string productId = "pill";
        [Min(0)] public int startingMoney = 120;
        [Tooltip("玩家出售时默认零售加价；0.15 表示 +15%。")]
        public float retailMarkup = .15f;
        [Tooltip("明确配置的常驻价格标签；有日期的市场标签由 Market Events 生成。")]
        public PriceTag[] priceTags = Array.Empty<PriceTag>();
        [Tooltip("原型行情效果；月度频率、持续与冷却使用 MarketCalendar 的隔离测试值，尚非正式平衡。")]
        public MarketEventDefinition[] marketEvents = Array.Empty<MarketEventDefinition>();
        [Min(1)] public int rentPeriod = 6;
        [Min(1)] public int firstRent = 20;
        [Tooltip("按生效类别展示基础价值总和选档，同档价值变化不改变资金范围。空展示柜按价值 0。")]
        public BuyerBudgetTier[] buyerBudgetTiers =
        {
            new BuyerBudgetTier {minimumDisplayValue=0,baseBudget=20},
            new BuyerBudgetTier {minimumDisplayValue=30,baseBudget=60},
            new BuyerBudgetTier {minimumDisplayValue=100,baseBudget=150},
            new BuyerBudgetTier {minimumDisplayValue=300,baseBudget=400}
        };
        [Range(0,.5f), Tooltip("每位买家的资金在当前档基准值上下浮动的比例；0.1 表示 ±10%。")]
        public float buyerBudgetVariation = .1f;
        [Range(0,1), Tooltip("无展示影响时，每位顾客成为卖家的概率。")]
        public float baseSupplierChance = .4f;
        [Range(0,1), Tooltip("有有效广告牌时增加的卖家概率；重复广告牌不叠加。")]
        public float advertisementSupplierBonus = .4f;
        [Range(0,1), Tooltip("有可售商品展示时增加的买家概率（从卖家概率中扣除）。")]
        public float displayedGoodsBuyerBonus = .2f;
        public ItemDefinition Find(string id) => items.First(d => d.id == id);
        // Explicit isolated fixture; never writes the catalog asset or imports draft goods.
        public void SetStorageVerificationDefaults()
        {
            SetPrototypeDefaults();
            Vector2Int[] Rectangle(int w,int h) => Enumerable.Range(0,w*h).Select(i=>new Vector2Int(i%w,i/w)).ToArray();
            items=items.Concat(new[]{
                new ItemDefinition{id="test-storage-case",title="储物匣（隔离验证）",category=ItemCategory.StorageContainer,cells=Rectangle(3,3),storageSize=new Vector2Int(10,10),supplierAvailable=false},
                new ItemDefinition{id="test-equipment-case",title="设备匣（暂定6×6）",category=ItemCategory.EquipmentContainer,cells=Rectangle(3,3),storageSize=new Vector2Int(6,6),compatibleEquipmentIds=new[]{"test-production"},supplierAvailable=false},
                new ItemDefinition{id="test-production",title="生产设备（分类测试）",category=ItemCategory.ProductionEquipment,cells=Rectangle(2,2),supplierAvailable=false},
                new ItemDefinition{id="test-portable",title="便携储存（分类测试）",category=ItemCategory.PortableContainer,cells=Rectangle(2,3),storageSize=new Vector2Int(3,3),supplierAvailable=false}
            }).ToArray();
            startingItems=new[]{"test-storage-case","herb","sword","pill","test-equipment-case","test-production","test-portable"};
        }
        public static string CategoryName(ItemCategory category)
        {
            switch(category)
            {
                case ItemCategory.Medicine: return "丹药";
                case ItemCategory.Material: return "材料";
                case ItemCategory.Equipment: return "装备";
                case ItemCategory.Container: return "容器";
                case ItemCategory.BusinessSign: return "业务招牌";
                case ItemCategory.StorageContainer: return "物品储存";
                case ItemCategory.EquipmentContainer: return "设备储存";
                case ItemCategory.PortableContainer: return "便携储存";
                case ItemCategory.ProductionEquipment: return "生产设备";
                default: return "未分类";
            }
        }

        public static ItemCategory PrototypeCategory(string id)
        {
            switch(id)
            {
                case "pill": return ItemCategory.Medicine;
                case "herb": case "dew": case "cinnabar": return ItemCategory.Material;
                case "sword": return ItemCategory.Equipment;
                case "jade": return ItemCategory.Container;
                case "sign": return ItemCategory.BusinessSign;
                default: return ItemCategory.Unclassified;
            }
        }

        // Used once by the scene builder. The resulting asset is editable in Inspector.
        public void SetPrototypeDefaults()
        {
            priceModelVersion=1;
            items = new[]
            {
                Def("herb", "凝气草", "炼丹原料 · L 形。与灵露炼成回气丹。", new Color(.36f,.72f,.51f), 4, 3, new[]{P(0,0),P(0,1),P(1,1)}),
                Def("dew", "灵露", "炼丹原料 · 单格。收购招牌带来稳定供货。", new Color(.36f,.67f,.88f), 3, 2, new[]{P(0,0)}),
                Def("pill", "回气丹", "炼丹成品 · 2×2 丹盒。以凝气草和灵露炼制，成交时按有效标签报价。", new Color(.93f,.68f,.31f), 20, 18, new[]{P(0,0),P(1,0),P(0,1),P(1,1)}),
                Def("cinnabar", "朱砂", "不规则试摆物 · T 形，旋转会改变占格。", new Color(.83f,.42f,.39f), 8, 6, new[]{P(0,0),P(1,0),P(2,0),P(1,1)}),
                Def("jade", "玉匣", "大件试摆物 · 2×2，占用四格。", new Color(.56f,.71f,.69f), 18, 14, new[]{P(0,0),P(1,0),P(0,1),P(1,1)}),
                Def("sword", "木剑", "长条试摆物 · 四格，竖放或横放。", new Color(.69f,.55f,.37f), 14, 10, new[]{P(0,0),P(0,1),P(0,2),P(0,3)}),
                Def("sign", "收购牌", "材料广告牌 · 开门时提高卖家概率，并吸引出售材料的卖家；每日总客流仍为 5 位。不可出售。", new Color(.65f,.56f,.83f), 0, 0, new[]{P(0,0),P(1,0),P(1,1)}, true)
            };
        }
        static Vector2Int P(int x, int y) => new Vector2Int(x,y);
        static ItemDefinition Def(string id, string title, string description, Color color, int buy, int sell, Vector2Int[] cells, bool sign = false)
            => new ItemDefinition { id=id, title=title, category=PrototypeCategory(id), description=description, color=color, baseValue=sell, supplierAvailable=buy>0, purchasePrice=buy, salePrice=sell, cells=cells, procurementSign=sign };
    }
}
