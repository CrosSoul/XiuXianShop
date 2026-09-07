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
