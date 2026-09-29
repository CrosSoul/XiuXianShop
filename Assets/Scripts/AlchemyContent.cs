using System;
namespace XiuXianShop
{
    [Serializable] public sealed class RecipeMasterRow : AuthoredRecord { public string title,description; }
    [Serializable] public sealed class RecipeLineRow : AuthoredRecord { public string recipeId,itemId,purpose;public int quantity; }
    [Serializable] public sealed class AlchemyGlobalRow : AuthoredRecord { public float value; }
    [Serializable] public sealed class AlchemyMaterialRow : AuthoredRecord
    {
        public string recipeId,itemId;
        public int quantity,order;
        public bool preloaded,ground;
        public float residenceSeconds;
    }
    [Serializable] public sealed class AlchemyStepRow : AuthoredRecord
    {
        public string recipeId,action,itemId,state,hint;
        public int order;
        public AlchemyHeat heat;
        public float furnaceSeconds;
    }
}
