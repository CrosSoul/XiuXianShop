namespace XiuXianShop
{
    // Explicit isolated legacy recipe fixture; normal Play reads imported Catalog data.
    public static class AlchemyRecipeVerification
    {
        public static AlchemyRecipe[] Recipes() => new[]
        {
            new AlchemyRecipe {id="recipe_pill_basic",title="回气丹",productId="pill",targets=new[]{
                Ingredient("herb",0),Ingredient("dew",1),Collect(2)}},
            new AlchemyRecipe {id="recipe_pill_fire_yang",title="赤阳丹",productId="pill_fire_yang",allowGrinding=true,targets=new[]{
                Ingredient("mat_fire_herb",0),Ingredient("mat_fire_fruit",1,true),Collect(2)}},
            new AlchemyRecipe {id="recipe_pill_metal_water",title="金水凝元丹",productId="pill_metal_water",allowGrinding=true,allowHeatChange=true,targets=new[]{
                Ingredient("mat_metal_herb",0),Ingredient("mat_water_fruit",1,true),Heat(1,AlchemyHeat.High),
                Ingredient("mat_metal_fruit",2,true),Heat(3,AlchemyHeat.Low),Collect(4)}}
        };
        static AlchemyTarget Ingredient(string id,float breaths,bool ground=false) => new AlchemyTarget {kind=AlchemyEventKind.Ingredient,itemId=id,breaths=breaths,ground=ground,preloaded=breaths==0,residenceSeconds=(id.StartsWith("mat_metal") || id=="mat_water_fruit"?4-breaths:2-breaths)*8};
        static AlchemyTarget Heat(float breaths,AlchemyHeat heat) => new AlchemyTarget {kind=AlchemyEventKind.Heat,breaths=breaths,heat=heat};
        static AlchemyTarget Collect(float breaths) => new AlchemyTarget {kind=AlchemyEventKind.Collect,breaths=breaths};
    }
}
