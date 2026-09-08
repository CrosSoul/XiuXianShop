#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace XiuXianShop.Tests
{
    public sealed partial class ShopPlayableTests
    {
        [UnityTest,Category("DP19")] public IEnumerator NegotiationEntryCancelsAndConfirmsSaleWithoutChangingCustomer()
        {
            yield return CalendarFixture(1);
            var session=shop.Session;
            yield return Drag(session.Items[0],ContainerId.Display,0,0);
            yield return Click("BeginBusiness");var customer=session.Offer;
            var sold=session.Items[1];yield return Drag(sold,ContainerId.Counter,0,0);
            int money=session.Money;
            yield return Click("NegotiationOpen");
            Assert.That(shop.NegotiationView.IsOpen);
            Assert.That(CalendarText("NegotiationLines"),Does.Contain("卖出 +23"));
            Assert.That(CalendarText("NegotiationTotal"),Does.Contain("+23"));
            yield return Click("NextCustomer");Assert.That(session.Offer,Is.SameAs(customer),"Modal blocks underlying buttons.");
            yield return Click("NegotiationClose");
            Assert.That(session.Money,Is.EqualTo(money));Assert.That(sold.Owner,Is.EqualTo(ItemOwner.Player));
            yield return Click("NegotiationOpen");yield return Click("NegotiationConfirm");
            Assert.That(shop.NegotiationView.IsOpen,Is.False);
            Assert.That(session.Money,Is.EqualTo(money+23));Assert.That(session.Find(sold.Id),Is.Null);
            Assert.That(session.Offer,Is.SameAs(customer));Assert.That(customer.RemainingBudget,Is.EqualTo(27));
            yield return Click("NegotiationOpen");Assert.That(shop.FindButton("NegotiationConfirm").interactable,Is.False);
            yield return Click("NegotiationClose");
            Assert.That(session.Money,Is.EqualTo(money+23));Assert.That(session.ValidateState(),Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
