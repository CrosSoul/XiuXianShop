using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    [Serializable]
    public sealed class ItemDefinition
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public Color color = Color.white;
        public Vector2Int[] cells;
        [Min(0)] public int purchasePrice;
        [Min(0)] public int salePrice;
        public bool procurementSign;

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
        public ItemDefinition[] items;
        public string[] startingItems = { "sign", "herb", "dew", "pill", "cinnabar", "jade", "sword" };
        public string herbId = "herb";
        public string dewId = "dew";
        public string productId = "pill";
        [Min(0)] public int startingMoney = 120;
        [Min(1)] public int rentPeriod = 7;
        [Min(1)] public int firstRent = 20;
        public ItemDefinition Find(string id) => items.First(d => d.id == id);

        // Used once by the scene builder. The resulting asset is editable in Inspector.
        public void SetPrototypeDefaults()
        {
            items = new[]
            {
                Def("herb", "凝气草", "炼丹原料 · L 形。与灵露炼成回气丹。", new Color(.36f,.72f,.51f), 4, 3, new[]{P(0,0),P(0,1),P(1,1)}),
                Def("dew", "灵露", "炼丹原料 · 单格。收购招牌带来稳定供货。", new Color(.36f,.67f,.88f), 3, 2, new[]{P(0,0)}),
                Def("pill", "回气丹", "炼丹成品 · 2×2 丹盒。原料成本 7，出售收入 18。", new Color(.93f,.68f,.31f), 20, 18, new[]{P(0,0),P(1,0),P(0,1),P(1,1)}),
                Def("cinnabar", "朱砂", "不规则试摆物 · T 形，旋转会改变占格。", new Color(.83f,.42f,.39f), 8, 6, new[]{P(0,0),P(1,0),P(2,0),P(1,1)}),
                Def("jade", "玉匣", "大件试摆物 · 2×2，占用四格。", new Color(.56f,.71f,.69f), 18, 14, new[]{P(0,0),P(1,0),P(0,1),P(1,1)}),
                Def("sword", "木剑", "长条试摆物 · 四格，竖放或横放。", new Color(.69f,.55f,.37f), 14, 10, new[]{P(0,0),P(0,1),P(0,2),P(0,3)}),
                Def("sign", "收购牌", "业务招牌 · 放入展示柜，每天带来两组凝气草与灵露。不可出售。", new Color(.65f,.56f,.83f), 0, 0, new[]{P(0,0),P(1,0),P(1,1)}, true)
            };
        }
        static Vector2Int P(int x, int y) => new Vector2Int(x,y);
        static ItemDefinition Def(string id, string title, string description, Color color, int buy, int sell, Vector2Int[] cells, bool sign = false)
            => new ItemDefinition { id=id, title=title, description=description, color=color, purchasePrice=buy, salePrice=sell, cells=cells, procurementSign=sign };
    }
}
