using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace XiuXianShop.Editor
{
    public static class PrototypeBuilder
    {
        public const string ScenePath="Assets/Scenes/ShopPrototype.unity";
        public const string CatalogPath="Assets/Data/ShopCatalog.asset";
        static ShopCatalog verificationCatalog;
        static bool discountEnabled;
        [MenuItem("XiuXianShop/Validation/Start Repeatable Trading Day (resets Play session)")]
        public static void StartVerificationDay()
        {
            var shop=Object.FindFirstObjectByType<ShopPrototype>();
            if(!EditorApplication.isPlaying || shop==null) return;
            if(verificationCatalog!=null) Object.DestroyImmediate(verificationCatalog);
            verificationCatalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath));
            verificationCatalog.name="Temporary trading verification catalog";
            verificationCatalog.Find("pill").baseValue=20;
            verificationCatalog.startingItems=new[]{"pill","pill","pill"};
            foreach(var item in verificationCatalog.items)item.supplierAvailable=item.id=="pill";
            verificationCatalog.retailMarkup=.15f;
            verificationCatalog.baseSupplierChance=.5f;
            verificationCatalog.advertisementSupplierBonus=0;verificationCatalog.displayedGoodsBuyerBonus=0;
            verificationCatalog.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=60}};
            verificationCatalog.buyerBudgetVariation=0;
            verificationCatalog.priceTags=new[]{new PriceTag{id="buy-test",title="测试收购修正",percent=-.2f,playerSells=false}};
            shop.StartVerificationSession(verificationCatalog,0);discountEnabled=false;
            EditorApplication.playModeStateChanged-=CleanupVerification;
            EditorApplication.playModeStateChanged+=CleanupVerification;
        }
        [MenuItem("XiuXianShop/Validation/Start Repeatable Trading Day (resets Play session)",true)]
        static bool CanStartVerification()=>EditorApplication.isPlaying && Object.FindFirstObjectByType<ShopPrototype>()!=null;
        [MenuItem("XiuXianShop/Validation/Toggle Sale Discount -30% (Play session)")]
        public static void ToggleQuoteTest()
        {
            var shop=Object.FindFirstObjectByType<ShopPrototype>();if(!EditorApplication.isPlaying || shop==null)return;
            discountEnabled=!discountEnabled;
            if(discountEnabled)shop.Session.SetPriceTag(new PriceTag{id="sale-test",title="测试降价",percent=-.3f,playerBuys=false});
            else shop.Session.RemovePriceTag("sale-test");
        }
        [MenuItem("XiuXianShop/Validation/Toggle Sale Discount -30% (Play session)",true)]
        static bool CanToggleQuoteTest()=>CanStartVerification();
        static void CleanupVerification(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            if(verificationCatalog!=null)Object.DestroyImmediate(verificationCatalog);
            verificationCatalog=null;discountEnabled=false;
            EditorApplication.playModeStateChanged-=CleanupVerification;
        }
        public static string MigratePrices()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if(catalog.priceModelVersion>=1) return "Price model already migrated.";
            foreach(var item in catalog.items)
            {
                item.baseValue=item.salePrice;
                item.supplierAvailable=item.purchasePrice>0;
                if(item.id=="pill") item.description="炼丹成品 · 2×2 丹盒。以凝气草和灵露炼制，成交时按有效标签报价。";
            }
            catalog.priceModelVersion=1;
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            return "Migrated existing catalog in place; base values preserve previous display weights.";
        }
        [MenuItem("XiuXianShop/Open Playable Prototype")]
        public static void OpenPrototype()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
        public static string Build()
        {
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null) return "Prototype scene already exists; no changes made.";
            var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if(catalog==null)
            {
                catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();AssetDatabase.CreateAsset(catalog,CatalogPath);
            }
            // Finish native asset import before serializing a scene reference to the new catalog.
            AssetDatabase.SaveAssetIfDirty(catalog);
            AssetDatabase.ImportAsset(CatalogPath,ImportAssetOptions.ForceSynchronousImport);
            catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(UniversalAdditionalCameraData));
            cameraGo.tag="MainCamera";cameraGo.transform.position=new Vector3(0,0,-10);
            var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.075f,.095f);
            cameraGo.GetComponent<UniversalAdditionalCameraData>().SetRenderer(-1);
            var shop=new GameObject("Shop Prototype").AddComponent<ShopPrototype>();shop.Catalog=catalog;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            return ScenePath;
        }
    }
}
