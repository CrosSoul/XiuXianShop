using System.Linq;

namespace XiuXianShop
{
    public static class VisitVerification
    {
        public const string TrainingLocationId="location_alchemy_training_visit";
        public static void Configure(ShopCatalog catalog,AuthoredContent content)
        {
            catalog.authoredContent=content;
            if(!catalog.travelLocations.Any(l=>l.id==TrainingLocationId))
                catalog.travelLocations=catalog.travelLocations.Concat(new[]{new TravelLocation {
                    id=TrainingLocationId,title="炼丹房求学（灰盒入口）",initiallyUnlocked=false,singleVisit=true}}).ToArray();
        }
    }
}
