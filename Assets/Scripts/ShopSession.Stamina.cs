namespace XiuXianShop
{
    public sealed partial class ShopSession
    {
        // Activity owners pass their configured cost after checking other prerequisites.
        // A destination must call this only on entry, never again for its interior actions.
        public bool CanSpendStamina(int cost,out string reason)
        {
            if(cost<0){reason="活动体力成本配置无效。";return false;}
            if(Phase==TurnPhase.Open){reason="请结束营业后再进行营业外活动。";return false;}
            if(Stamina<cost){reason=$"体力不足：需要 {cost}，当前 {Stamina}。";return false;}
            reason="";return true;
        }
        public bool TrySpendStamina(int cost)
        {
            if(!CanSpendStamina(cost,out var reason))return Fail(reason);
            Stamina-=cost;
            return Success($"消耗体力 {cost}，剩余 {Stamina}/{MaximumStamina}。");
        }
    }
}
