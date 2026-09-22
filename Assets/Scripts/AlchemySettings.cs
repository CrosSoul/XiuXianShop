using System;
using UnityEngine;

namespace XiuXianShop
{
    public enum AlchemyHeat { Low, Medium, High }
    public enum PillQuality { Ordinary, Good, Superior, Ruined }
    public enum AlchemyPhase { Preparing, Running, Finished, Aborted }
    public enum AlchemyEventKind { Ingredient, Heat, Collect }

    [Serializable]
    public sealed class AlchemyTarget
    {
        public AlchemyEventKind kind;
        public float breaths;
        public string itemId;
        public bool ground;
        public AlchemyHeat heat = AlchemyHeat.Medium;
    }

    [Serializable]
    public sealed class AlchemyRecipe
    {
        public string id, title, productId;
        public bool allowGrinding, allowHeatChange;
        public AlchemyTarget[] targets;
    }

    [Serializable]
    public sealed class AlchemySettings
    {
        [Min(0), Tooltip("店内微缩炉每次成功开炉的体力；20仅为灰盒测试值。")]
        public int shopStaminaCost=20;
        [Tooltip("DP58灰盒时序，不是最终平衡。")]
        public float breathSeconds=8, grindSeconds=8, perfectWindow=.5f, acceptableWindow=2;
        [Tooltip("每秒消耗的下品灵石等价量；转换为原灵石实例的整数精度扣除。")]
        public float spiritEquivalentsPerSecond=.02f;
        public float lowHeatMultiplier=1, mediumHeatMultiplier=1.5f, highHeatMultiplier=2;
        public float perfectScore=1, acceptableScore=.6f, severeScore=0;
        public float superiorThreshold=.9f, goodThreshold=.7f, ordinaryThreshold=.45f;
        public float ordinaryValue=1, goodValue=1.2f, superiorValue=1.5f;
        public Vector2Int preparationSize=new Vector2Int(6,4), fuelSize=new Vector2Int(2,2), outputSize=new Vector2Int(2,2);
        public bool showDebug=true;
        [Min(.1f)] public float debugTimeScale=1;
        public AlchemyRecipe[] recipes =
        {
            new AlchemyRecipe {id="recipe_pill_basic",title="回气丹",productId="pill",targets=new[]{
                Ingredient("herb",0),Ingredient("dew",1),Collect(2)}},
            new AlchemyRecipe {id="recipe_pill_fire_yang",title="赤阳丹",productId="pill_fire_yang",allowGrinding=true,targets=new[]{
                Ingredient("mat_fire_herb",0),Ingredient("mat_fire_fruit",1,true),Collect(2)}},
            new AlchemyRecipe {id="recipe_pill_metal_water",title="金水凝元丹",productId="pill_metal_water",allowGrinding=true,allowHeatChange=true,targets=new[]{
                Ingredient("mat_metal_herb",0),Ingredient("mat_water_fruit",1,true),Heat(1,AlchemyHeat.High),
                Ingredient("mat_metal_fruit",2,true),Heat(3,AlchemyHeat.Low),Collect(4)}}
        };
        static AlchemyTarget Ingredient(string id,float breaths,bool ground=false) => new AlchemyTarget {kind=AlchemyEventKind.Ingredient,itemId=id,breaths=breaths,ground=ground};
        static AlchemyTarget Heat(float breaths,AlchemyHeat heat) => new AlchemyTarget {kind=AlchemyEventKind.Heat,breaths=breaths,heat=heat};
        static AlchemyTarget Collect(float breaths) => new AlchemyTarget {kind=AlchemyEventKind.Collect,breaths=breaths};
        public float HeatMultiplier(AlchemyHeat heat) => heat==AlchemyHeat.Low?lowHeatMultiplier:heat==AlchemyHeat.High?highHeatMultiplier:mediumHeatMultiplier;
        public float ValueMultiplier(PillQuality quality) => quality==PillQuality.Superior?superiorValue:quality==PillQuality.Good?goodValue:ordinaryValue;
        public static string QualityName(PillQuality quality) => quality==PillQuality.Superior?"上品":quality==PillQuality.Good?"良品":quality==PillQuality.Ordinary?"普通":"废丹";
        public static string HeatName(AlchemyHeat heat) => heat==AlchemyHeat.Low?"低火":heat==AlchemyHeat.Medium?"中火":"高火";
        public static string PhaseName(AlchemyPhase phase) => phase==AlchemyPhase.Preparing?"备料":phase==AlchemyPhase.Running?"炼制中":phase==AlchemyPhase.Finished?"已收丹":"已中止";
    }
}
