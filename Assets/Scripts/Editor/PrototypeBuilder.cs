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
            verificationCatalog.marketEvents=System.Array.Empty<MarketEventDefinition>();
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
        [MenuItem("XiuXianShop/Validation/Start DP18 Mixed Trading Day (resets Play session)")]
        public static void StartMixedTradingDay()
        {
            if(!CanStartVerification())return;
            StartVerificationDay();
            verificationCatalog.name="Temporary DP18 mixed trading catalog";
            verificationCatalog.startingItems=new[]{"pill","pill","pill","pill","pill","jade"};
            verificationCatalog.Find("pill").baseValue=10;
            verificationCatalog.Find("herb").baseValue=15;verificationCatalog.Find("dew").baseValue=20;
            foreach(var item in verificationCatalog.items)item.supplierAvailable=item.id=="herb" || item.id=="dew";
            verificationCatalog.retailMarkup=0;verificationCatalog.priceTags=System.Array.Empty<PriceTag>();
            verificationCatalog.baseSupplierChance=1;
            verificationCatalog.buyerBudgetTiers=new[]{new BuyerBudgetTier{baseBudget=60}};
            var shop=Object.FindFirstObjectByType<ShopPrototype>();
            // Use an isolated save path so this repeatable fixture cannot overwrite a player's save.
            shop.StartVerificationSession(verificationCatalog,17);
            shop.SavePath=System.IO.Path.Combine(Application.dataPath,"../Temp/DP18VerificationSave.json");
        }
        [MenuItem("XiuXianShop/Validation/Start DP18 Mixed Trading Day (resets Play session)",true)]
        static bool CanStartMixedTradingDay()=>CanStartVerification();
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
        [MenuItem("XiuXianShop/Validation/Start Calendar Overlap Example (resets Play session)")]
        public static void StartCalendarExample()
        {
            if(!CanStartVerification())return;
            StartVerificationDay();
            verificationCatalog.priceTags=System.Array.Empty<PriceTag>();
            var shop=Object.FindFirstObjectByType<ShopPrototype>();
            shop.StartVerificationSession(verificationCatalog,0,MarketCalendar.OverlapExample());
            shop.SavePath=System.IO.Path.Combine(Application.dataPath,"../Temp/DP17VerificationSave.json");
            while(shop.Session.Day<6){shop.Session.BeginBusiness();shop.Session.EndBusiness();shop.Session.Sleep();}
            shop.Refresh();shop.CalendarView.Open();
        }
        [MenuItem("XiuXianShop/Validation/Start Calendar Overlap Example (resets Play session)",true)]
        static bool CanStartCalendarExample()=>CanStartVerification();
        [MenuItem("XiuXianShop/Validation/Advance Calendar Example One Day (Play session)")]
        public static void AdvanceCalendarExample()
        {
            if(!CanStartVerification() || verificationCatalog==null)return;
            var shop=Object.FindFirstObjectByType<ShopPrototype>();
            if(shop.Session.Phase==DayPhase.Preparation)shop.Session.BeginBusiness();
            if(shop.Session.Phase==DayPhase.Open)shop.Session.EndBusiness();
            shop.Session.Sleep();shop.Refresh();
        }
        [MenuItem("XiuXianShop/Validation/Advance Calendar Example One Day (Play session)",true)]
        static bool CanAdvanceCalendarExample()=>CanStartVerification() && verificationCatalog!=null;
        public static string InstallCalendarDefaults()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
            if(catalog.marketEvents!=null && catalog.marketEvents.Length>0)return "Existing market configuration retained.";
            catalog.marketEvents=MarketCalendar.PrototypeDefinitions();
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            return "Added four adjustable prototype market event definitions; existing catalog data retained.";
        }
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
