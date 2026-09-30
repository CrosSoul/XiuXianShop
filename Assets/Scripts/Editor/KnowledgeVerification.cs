using UnityEditor;
using UnityEngine;

namespace XiuXianShop.Editor
{
    public static class KnowledgeVerification
    {
        [MenuItem("XiuXianShop/Current Play Session/Grant Jade Slip/普通文本（测试）")]
        public static void GrantText() => Grant("test-jade-text");
        [MenuItem("XiuXianShop/Current Play Session/Grant Jade Slip/基础功法（测试）")]
        public static void GrantTechnique() => Grant("test-jade-technique");

        static void Grant(string id)
        {
            if(!EditorApplication.isPlaying){Debug.LogWarning("请先 Play，再领取测试玉简。");return;}
            var shop = Object.FindAnyObjectByType<ShopPrototype>();
            shop.Run(() => shop.Session.GrantJadeSlipForVerification(id));
        }
    }
}
