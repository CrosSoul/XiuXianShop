using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace XiuXianShop.Tests
{
    public sealed class StaminaTests
    {
        ShopCatalog catalog;
        [SetUp] public void Setup(){catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.SetPrototypeDefaults();}
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(catalog);
        static void NextTurn(ShopSession s){Assert.That(s.BeginBusiness());Assert.That(s.EndBusiness());Assert.That(s.AdvanceTurn());}

        [TestCase(0,30,false)] [TestCase(70,100,false)] [TestCase(71,100,true)] [TestCase(90,100,true)] [TestCase(100,100,true)]
        public void MonthlyRecoveryStrictOverflowAndOneExtraCustomer(int before,int expected,bool bonus)
        {
            var s=new ShopSession(catalog,customerSeed:45);Assert.That(s.HasStaminaOverflowCustomer,Is.False);
            Assert.That(s.TrySpendStamina(100-before));NextTurn(s);
            Assert.That(s.Stamina,Is.EqualTo(expected));Assert.That(s.HasStaminaOverflowCustomer,Is.EqualTo(bonus));
            Assert.That(s.AdvanceTurn(),Is.False);Assert.That(s.Stamina,Is.EqualTo(expected));
            for(int i=0;i<3;i++)s.PreviewAttraction();
            Assert.That(s.BeginBusiness());Assert.That(s.BeginBusiness(),Is.False);
            Assert.That(s.BuyersToday+s.SuppliersToday,Is.EqualTo(bonus?6:5));
            int served=0;while(s.Offer!=null){served++;Assert.That(s.NextCustomer());}
            Assert.That(served,Is.EqualTo(bonus?6:5));Assert.That(s.Stamina,Is.EqualTo(expected));
        }
        [Test] public void ExactCostReachesZeroInsufficientFailsAndBusinessRemainsAvailable()
        {
            var s=new ShopSession(catalog);Assert.That(s.TrySpendStamina(80));
            Assert.That(s.CanSpendStamina(21,out var reason),Is.False);Assert.That(reason,Does.Contain("体力不足"));
            Assert.That(s.TrySpendStamina(21),Is.False);Assert.That(s.Stamina,Is.EqualTo(20));
            Assert.That(s.TrySpendStamina(-1),Is.False);Assert.That(s.TrySpendStamina(20));Assert.That(s.Stamina,Is.Zero);
            Assert.That(s.BeginBusiness());Assert.That(s.Stamina,Is.Zero);Assert.That(s.EndBusiness());Assert.That(s.Stamina,Is.Zero);
            Assert.That(s.AdvanceTurn());Assert.That(s.Stamina,Is.EqualTo(30));
        }
        [Test] public void ConfigurableLimitsAndRecoveryDoNotStackMonthlyBonus()
        {
            catalog.maximumStamina=150;catalog.staminaRecoveryPerTurn=50;
            var s=new ShopSession(catalog);Assert.That(s.Stamina,Is.EqualTo(150));NextTurn(s);
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(6));Assert.That(s.Stamina,Is.EqualTo(150));
            Assert.That(s.TrySpendStamina(100));NextTurn(s);Assert.That(s.Stamina,Is.EqualTo(100));
            Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
            catalog.staminaRecoveryPerTurn=0;NextTurn(s);Assert.That(s.Stamina,Is.EqualTo(100));Assert.That(s.CustomerCountThisTurn,Is.EqualTo(5));
        }
        [Test] public void SaveRestoreRetainsRecoveryAndOneBonusWithoutRerollingQueue()
        {
            var s=new ShopSession(catalog,customerSeed:45);Assert.That(s.TrySpendStamina(10));NextTurn(s);
            Assert.That(s.TrySpendStamina(20));string json=s.CaptureSave();
            var restored=ShopSession.RestoreSave(catalog,json);
            for(int n=0;n<3;n++)restored=ShopSession.RestoreSave(catalog,restored.CaptureSave());
            Assert.That(restored.Stamina,Is.EqualTo(80));Assert.That(restored.CustomerCountThisTurn,Is.EqualTo(6));
            Assert.That(s.BeginBusiness());Assert.That(restored.BeginBusiness());
            while(s.Offer!=null)
            {
                Assert.That(restored.Offer.Direction,Is.EqualTo(s.Offer.Direction));
                Assert.That(restored.Offer.RequestedCategory,Is.EqualTo(s.Offer.RequestedCategory));
                Assert.That(restored.Offer.RemainingBudget,Is.EqualTo(s.Offer.RemainingBudget));
                Assert.That(restored.Offer.SupplierItems.Select(i=>i.Definition.id),Is.EqualTo(s.Offer.SupplierItems.Select(i=>i.Definition.id)));
                s.NextCustomer();restored.NextCustomer();
            }
            Assert.That(restored.Offer,Is.Null);Assert.That(restored.ServedToday,Is.EqualTo(6));
            var invalid=JsonUtility.FromJson<ShopSave>(json);invalid.stamina=101;
            Assert.Throws<ArgumentException>(()=>ShopSession.RestoreSave(catalog,JsonUtility.ToJson(invalid)));
        }
    }
}
