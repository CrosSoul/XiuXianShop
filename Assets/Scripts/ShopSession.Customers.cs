using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    public sealed class CustomerCategoryWeight
    {
        public ItemCategory Category { get; internal set; }
        public int DisplayedCount { get; internal set; }
        public double Weight { get; internal set; }
        internal string[] SupplyIds;
    }

    public sealed partial class ShopSession
    {
        static bool InvalidWeight(double weight) => weight<0 || double.IsNaN(weight) || double.IsInfinity(weight);

        void ValidateCustomerSettings()
        {
            var s=catalog.customers;
            var weights=new[]{s.buyingWeight,s.sellingWeight,s.tradingWeight,s.baseCategoryWeight,s.displayedItemWeight,
                s.ordinaryWeight,s.wealthyWeight,s.lavishWeight,s.oneSupplyWeight,s.twoSuppliesWeight};
            if(s.baseCustomerCount<1 || s.staminaOverflowCustomers<0 || weights.Any(w=>InvalidWeight(w)) ||
                s.buyingWeight+s.sellingWeight+s.tradingWeight<=0 || s.baseCategoryWeight<=0 ||
                s.ordinaryWeight+s.wealthyWeight+s.lavishWeight<=0 || s.oneSupplyWeight+s.twoSuppliesWeight<=0 ||
                s.budgets.Select(b=>b.category).Distinct().Count()!=s.budgets.Length ||
                s.budgets.Any(b=>b.ordinary<1 || b.wealthy<1 || b.lavish<1) ||
                s.supplyPools.Select(p=>p.category).Distinct().Count()!=s.supplyPools.Length ||
                s.supplyPools.Any(p=>InvalidWeight(p.weight)))
                throw new ArgumentException("普通顾客配置无效，请检查人数、权重、预算表及供货池。");
        }

        DisplayAttraction BuildCustomerAttraction()
        {
            ValidateCustomerSettings();
            var settings=catalog.customers;
            var display=In(ContainerId.Display).Where(i=>i.Owner==ItemOwner.Player && IsSaleItem(i.Definition)).ToArray();
            var buying=catalog.items.Where(IsSaleItem).Select(d=>d.category).Distinct().OrderBy(c=>(int)c)
                .Select(category=>new CustomerCategoryWeight {
                    Category=category,DisplayedCount=display.Count(i=>i.Definition.category==category),
                    Weight=(settings.baseCategoryWeight+settings.displayedItemWeight*display.Count(i=>i.Definition.category==category))*
                        (HasTeaEffect(TeaEffect.BuyerTrend) && ActiveTeaEffect.category==category?catalog.teaHouse.buyerCategoryMultiplier:1)
                }).ToArray();
            foreach(var category in buying)
                if(!settings.budgets.Any(b=>b.category==category.Category))
                    throw new ArgumentException("求购类别缺少三档预算："+category.Category);
            var selling=new List<CustomerCategoryWeight>();
            foreach(var pool in settings.supplyPools)
            {
                var members=pool.itemIds.Select(id=>catalog.Find(id)).Where(d=>IsSaleItem(d) && d.supplierAvailable && d.category==pool.category)
                    .Select(d=>d.id).Distinct().ToArray();
                if(pool.weight>0 && members.Length>0)selling.Add(new CustomerCategoryWeight {
                    Category=pool.category,SupplyIds=members,
                    Weight=pool.weight*(HasTeaEffect(TeaEffect.SupplierTrend) && ActiveTeaEffect.category==pool.category?catalog.teaHouse.supplierCategoryMultiplier:1)
                });
            }
            double total=settings.buyingWeight+settings.sellingWeight+settings.tradingWeight;
            double both=settings.tradingWeight/total;
            if(HasTeaEffect(TeaEffect.Travellers))
                both=Math.Min(catalog.teaHouse.travellerChanceCap,both+catalog.teaHouse.travellerChanceBonus);
            double singleTotal=settings.buyingWeight+settings.sellingWeight;
            if(singleTotal==0 && both<1)throw new ArgumentException("商旅修正后需要配置至少一种单向顾客权重。");
            return new DisplayAttraction {
                BuyingCategories=buying,SellingCategories=selling.ToArray(),
                BuyingChance=singleTotal==0?0:(1-both)*settings.buyingWeight/singleTotal,
                SellingChance=singleTotal==0?0:(1-both)*settings.sellingWeight/singleTotal,
                TradingChance=both
            };
        }

        int DrawCustomerWeight(double[] weights)
        {
            double roll=DrawCustomerChance()*weights.Sum();
            for(int i=0;i<weights.Length;i++){roll-=weights[i];if(roll<0)return i;}
            return weights.Length-1;
        }

        bool GenerateOrdinaryCustomers(DisplayAttraction snapshot)
        {
            if(snapshot.BuyingCategories.Length==0)return Fail("缺少普通交易类别，无法营业。");
            if(snapshot.SellingCategories.Length==0 && snapshot.SellingChance+snapshot.TradingChance>0)
                return Fail("出售顾客已启用，但没有合法非空的普通来货池。请检查顾客配置。");
            var s=catalog.customers;
            int wealthyIndex=HasTeaEffect(TeaEffect.WealthyVisitor)?DrawCustomerNumber(0,CustomerCountThisTurn):-1;
            for(int n=0;n<CustomerCountThisTurn;n++)
            {
                var behavior=(CustomerBehavior)DrawCustomerWeight(new[]{snapshot.BuyingChance,snapshot.SellingChance,snapshot.TradingChance});
                int tier=DrawCustomerWeight(new double[]{s.ordinaryWeight,s.wealthyWeight,s.lavishWeight});
                bool promoted=n==wealthyIndex;
                if(promoted)tier=Math.Min(2,tier+catalog.teaHouse.wealthyBudgetTierIncrease);
                ItemCategory category=ItemCategory.Unclassified;int budget=0;
                if(behavior!=CustomerBehavior.Selling)
                {
                    category=snapshot.BuyingCategories[DrawCustomerWeight(snapshot.BuyingCategories.Select(c=>c.Weight).ToArray())].Category;
                    budget=s.budgets.Single(b=>b.category==category).Amount((CustomerBudgetTier)tier);
                }
                string[] supplies=Array.Empty<string>();
                if(behavior!=CustomerBehavior.Buying)
                {
                    var pool=snapshot.SellingCategories[DrawCustomerWeight(snapshot.SellingCategories.Select(c=>c.Weight).ToArray())];
                    int count=1+DrawCustomerWeight(new double[]{s.oneSupplyWeight,s.twoSuppliesWeight});
                    supplies=Enumerable.Range(0,count).Select(_=>pool.SupplyIds[DrawCustomerNumber(0,pool.SupplyIds.Length)]).ToArray();
                }
                queue.Enqueue((behavior==CustomerBehavior.Buying?TradeDirection.CustomerBuys:TradeDirection.CustomerSells,
                    supplies,category,budget,behavior,(CustomerBudgetTier)tier,promoted));
                if(behavior==CustomerBehavior.Buying)BuyersToday++;
                else if(behavior==CustomerBehavior.Selling)SuppliersToday++;
                else TradingCustomersToday++;
            }
            return true;
        }
    }
}
