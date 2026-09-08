using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XiuXianShop
{
    [Serializable]
    public sealed class MarketEventDefinition
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public PriceTag effect;
        public MarketEventDefinition Copy() => new MarketEventDefinition { id=id, title=title, description=description, effect=effect.Copy() };
    }

    [Serializable]
    public sealed class MarketEvent
    {
        public string id;
        public string title;
        public string description;
        public int startDay;
        public int endDay;
        public PriceTag effect;
        public int Duration => endDay-startDay+1;
        public bool ActiveOn(int day) => startDay<=day && day<=endDay;
        public string StatusOn(int day) => day<startDay ? "未开始" : day>endDay ? "已结束" : "生效中";
        public MarketEvent Copy() => new MarketEvent {id=id,title=title,description=description,startDay=startDay,endDay=endDay,effect=effect.Copy()};
    }

    [Serializable]
    public sealed class MarketCalendarState
    {
        public int seed;
        public bool fixedSchedule;
        public MarketEventDefinition[] definitions;
        public int[] generatedWeeks;
        public MarketEvent[] events;
    }

    public sealed class CalendarEventSegment
    {
        public MarketEvent Event { get; internal set; }
        public int Week { get; internal set; }
        public int Column { get; internal set; }
        public int Days { get; internal set; }
        public int Lane { get; internal set; }
    }

    // A session snapshot, not a global clock. Reading a week never consumes customer randomness.
    public sealed class MarketCalendar
    {
        readonly int seed;
        readonly bool fixedSchedule;
        readonly MarketEventDefinition[] definitions;
        readonly HashSet<int> generatedWeeks = new HashSet<int>();
        readonly List<MarketEvent> events = new List<MarketEvent>();
        public static int WeekStart(int day) => (Math.Max(1,day)-1)/7*7+1;

        public MarketCalendar(MarketEventDefinition[] definitions, int seed)
        {
            this.seed=seed;
            this.definitions=(definitions??Array.Empty<MarketEventDefinition>()).Select(d=>d.Copy()).ToArray();
        }
        public MarketCalendar(MarketCalendarState state)
        {
            if(state==null || state.events==null || state.generatedWeeks==null || state.definitions==null)
                throw new ArgumentException("市场存档不完整。");
            seed=state.seed;fixedSchedule=state.fixedSchedule;
            definitions=state.definitions.Select(d=>d.Copy()).ToArray();
            foreach(var week in state.generatedWeeks) {if(week<1 || !generatedWeeks.Add(week))throw new ArgumentException("市场周记录重复或无效。");}
            foreach(var e in state.events)
            {
                if(e==null || string.IsNullOrWhiteSpace(e.id) || e.startDay<1 || e.Duration<1 || e.Duration>3 || e.effect==null ||
                    float.IsNaN(e.effect.percent) || float.IsInfinity(e.effect.percent) || Math.Abs(e.effect.percent)>100 || events.Any(x=>x.id==e.id))
                    throw new ArgumentException("市场事件记录无效或重复。");
                events.Add(e.Copy());
            }
        }
        public MarketCalendarState Capture() => new MarketCalendarState {seed=seed,fixedSchedule=fixedSchedule,
            definitions=definitions.Select(d=>d.Copy()).ToArray(),generatedWeeks=generatedWeeks.OrderBy(w=>w).ToArray(),events=events.Select(e=>e.Copy()).ToArray()};

        void EnsureWeek(int week)
        {
            // Generate missing weeks in order so browsing ahead cannot change the schedule.
            for(int w=1;w<=week;w++)
                if(generatedWeeks.Add(w) && !fixedSchedule && definitions.Length>0)GenerateWeek(w);
        }
        void GenerateWeek(int week)
        {
            var random=new System.Random(unchecked(seed ^ (week*73856093)));
            int count=random.Next(2,4);
            for(int i=0;i<count;i++)
            {
                var candidates=new List<MarketEvent>();
                foreach(var d in definitions)
                for(int offset=0;offset<7;offset++)
                for(int duration=1;duration<=3;duration++)
                {
                    int start=(week-1)*7+1+offset,end=start+duration-1;
                    // Stable effect IDs identify the market type, including saved events.
                    // Ending on day 4 blocks days 5 and 6; the earliest repeat is day 7.
                    if(events.Any(e=>e.effect.id==d.id && start<=e.endDay+2 && end+2>=e.startDay))continue;
                    var effect=d.effect.Copy();effect.id=d.id;
                    candidates.Add(new MarketEvent {id=$"market:{week}:{i}",title=d.title,description=d.description,
                        startDay=start,endDay=end,effect=effect});
                }
                // A small custom event pool may run out: never violate the cooldown to fill a quota.
                if(candidates.Count==0)break;
                events.Add(candidates[random.Next(candidates.Count)]);
            }
        }
        public MarketEvent[] Between(int first,int last)
        {
            // Include the previous week: a 3-day event can continue across the boundary.
            EnsureWeek((last-1)/7+1);
            return events.Where(e=>e.startDay<=last && e.endDay>=first).OrderBy(e=>e.startDay).ThenBy(e=>e.id,StringComparer.Ordinal).Select(e=>e.Copy()).ToArray();
        }
        public IEnumerable<PriceTag> ActiveTags(int day) => Between(day,day).Where(e=>e.ActiveOn(day)).Select(e=>
        {
            var tag=e.effect.Copy();tag.id=e.id;tag.title=e.title;return tag;
        });
        public MarketEvent[] VisibleBetween(int first,int last,int today)
            => Between(first,last).Where(e=>e.startDay<=today).ToArray();
        public CalendarEventSegment[] Segments(int firstDay,int today=int.MaxValue)
        {
            var visible=VisibleBetween(firstDay,firstDay+13,today);
            var result=new List<CalendarEventSegment>();
            for(int week=0;week<2;week++)
            {
                int start=firstDay+week*7,end=start+6;
                var laneEnds=new List<int>();
                foreach(var e in visible.Where(e=>e.startDay<=end && e.endDay>=start))
                {
                    int a=Math.Max(start,e.startDay),b=Math.Min(end,e.endDay),lane=laneEnds.FindIndex(x=>x<a);
                    if(lane<0) {lane=laneEnds.Count;laneEnds.Add(b);} else laneEnds[lane]=b;
                    result.Add(new CalendarEventSegment{Event=e,Week=week,Column=a-start,Days=b-a+1,Lane=lane});
                }
            }
            return result.ToArray();
        }

        public static MarketEventDefinition[] PrototypeDefinitions() => new[]
        {
            Definition("medicine-demand","丹药求购潮","药坊求购增多，丹药出售报价暂时上涨。",ItemCategory.Medicine,.2f,true,false),
            Definition("medicine-supply","丹药集中到货","商队集中供货，丹药买卖报价暂时下降。",ItemCategory.Medicine,-.1f,true,true),
            Definition("materials-supply","灵材丰收","灵材货源增加，收购材料报价暂时下降。",ItemCategory.Material,-.2f,false,true),
            Definition("equipment-demand","法器行情升温","装备交易需求增加，装备买卖报价暂时上涨。",ItemCategory.Equipment,.2f,true,true)
        };
        static MarketEventDefinition Definition(string id,string title,string description,ItemCategory category,float percent,bool sells,bool buys)
            => new MarketEventDefinition{id=id,title=title,description=description,effect=new PriceTag{id=id,title=title,category=category,percent=percent,playerSells=sells,playerBuys=buys}};

        // Fixed, explicit verification data: week crossing, overlap, and 1/2/3-day durations.
        public static MarketCalendar OverlapExample()
        {
            var defs=PrototypeDefinitions();
            var scheduled=new[]
            {
                new MarketEvent{id="example-rise",title="丹药求购潮",description="固定测试：出售丹药 +20%，第7–9天。",startDay=7,endDay=9,effect=defs[0].effect.Copy()},
                new MarketEvent{id="example-fall",title="丹药集中到货与集市临时让利",description="固定测试：丹药买卖 -10%，与求购潮在第8–9天重叠。",startDay=8,endDay=9,effect=defs[1].effect.Copy()},
                new MarketEvent{id="example-buy",title="药商收购报价上涨",description="固定测试：只对玩家买入丹药 +20%。",startDay=6,endDay=6,effect=Definition("buy","收购", "",ItemCategory.Medicine,.2f,false,true).effect},
                new MarketEvent{id="example-low",title="丹药低价日",description="固定测试：只对玩家出售丹药 -20%。",startDay=11,endDay=11,effect=Definition("low","低价","",ItemCategory.Medicine,-.2f,true,false).effect}
            };
            return new MarketCalendar(new MarketCalendarState {seed=17,fixedSchedule=true,definitions=Array.Empty<MarketEventDefinition>(),generatedWeeks=Array.Empty<int>(),events=scheduled});
        }
    }
}
