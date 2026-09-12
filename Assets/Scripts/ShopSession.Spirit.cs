namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        // Amount is measured in the definition's integer units, not item count or wallet balance.
        public bool ConsumeSpirit(int itemId,int units) => ChangeSpirit(itemId,units,false);
        public bool RefillSpirit(int itemId,int units) => ChangeSpirit(itemId,units,true);

        bool ChangeSpirit(int itemId,int units,bool refill)
        {
            var item=Find(itemId);
            if(item==null || item.Owner!=ItemOwner.Player || item.Definition.spiritResource==null)
                return Fail("只能调整自有灵石的灵气。");
            var resource=item.Definition.spiritResource;
            if(units<=0)return Fail("灵气变动量必须大于零。");
            if(refill && !resource.Reusable)return Fail("下品灵石不能重新充能。");
            if(refill ? units>resource.CapacityUnits-item.SpiritUnits : units>item.SpiritUnits)
                return Fail(refill?"补充量超过容量。":"剩余灵气不足。");
            item.SpiritUnits+=refill?units:-units;
            bool shattered=item.SpiritUnits==0 && !resource.Reusable;
            if(shattered)items.Remove(item);
            PricingRevision++;
            return Success(shattered?"下品灵石灵气耗尽，已粉碎。":$"{item.Definition.title}剩余灵气已更新，历史购买价值不变。");
        }
    }
}
