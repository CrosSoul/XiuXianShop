using System;
using System.Linq;
using NUnit.Framework;

namespace XiuXianShop.Tests
{
    public sealed class MarketSecretTests
    {
        MarketCalendar Fixed(MarketEventDefinition[] definitions,params MarketEvent[] events) => new MarketCalendar(new MarketCalendarState
        {seed=48,fixedSchedule=true,definitions=definitions,events=events,generatedYears=Array.Empty<int>()});
        MarketEvent Event(MarketEventDefinition d,string id,int first,int last)=>new MarketEvent{id=id,title=d.title,description=d.description,startTurn=first,endTurn=last,effect=d.effect.Copy()};

        [Test] public void ExistingFutureEventIsRevealedWithFullDurationAcrossYearAndOnlyActsWhenActive()
        {
            var d=MarketCalendar.PrototypeDefinitions()[0];
            var calendar=Fixed(new[]{d},Event(d,"past",1,1),Event(d,"active",8,9),Event(d,"too-soon",9,9),Event(d,"future",12,13),Event(d,"far",22,22));
            var result=calendar.RevealSecret(8,2,12,new Random(48));Assert.That(result.id,Is.EqualTo("future"));
            Assert.That(result.Duration,Is.EqualTo(2));Assert.That(calendar.Capture().events.Length,Is.EqualTo(5));
            Assert.That(calendar.VisibleBetween(10,20,8).Single().id,Is.EqualTo("future"));
            Assert.That(calendar.ActiveTags(10),Is.Empty);Assert.That(calendar.ActiveTags(12).Single().id,Is.EqualTo("future"));
            Assert.That(calendar.Segments(13,8).Single().Event.id,Is.EqualTo("future"));
            var restored=new MarketCalendar(calendar.Capture());Assert.That(restored.DisclosedEvents.Single().id,Is.EqualTo(result.id));
        }
        [Test] public void EmptyWindowGeneratesLegalEventAndLaterScheduleGenerationRespectsIt()
        {
            var d=MarketCalendar.PrototypeDefinitions()[0];var calendar=Fixed(new[]{d},Event(d,"earlier",1,2));
            var result=calendar.RevealSecret(1,2,12,new Random(48));
            Assert.That(result.startTurn,Is.InRange(5,13));Assert.That(result.Duration,Is.InRange(1,MarketCalendar.TestMaximumDuration));
            Assert.That(calendar.DisclosedEvents.Single().id,Is.EqualTo(result.id));
            var state=calendar.Capture();state.fixedSchedule=false;var expanding=new MarketCalendar(state);var all=expanding.Between(1,60);
            foreach(var a in all)foreach(var b in all.Where(e=>e.startTurn>a.startTurn && e.effect.id==a.effect.id))
                Assert.That(b.startTurn,Is.GreaterThan(a.endTurn+MarketCalendar.TestCooldownTurns));
            Assert.That(all.Count(e=>e.id==result.id),Is.EqualTo(1));
        }
        [Test] public void NoLegalCombinationReturnsNothingAndLeavesScheduleUnchanged()
        {
            var d=MarketCalendar.PrototypeDefinitions()[0];var calendar=Fixed(new[]{d},Event(d,"blocks",1,2));
            string before=UnityEngine.JsonUtility.ToJson(calendar.Capture());
            Assert.That(calendar.RevealSecret(1,2,2,new Random(48)),Is.Null);
            Assert.That(calendar.Capture().events.Length,Is.EqualTo(1));Assert.That(calendar.DisclosedEvents,Is.Empty);
            Assert.That(calendar.Capture().events[0].id,Is.EqualTo("blocks"));
        }
    }
}
