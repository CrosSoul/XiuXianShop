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
        public int startTurn;
        public int endTurn;
        public PriceTag effect;
        public int Duration => endTurn-startTurn+1;
        public bool ActiveOn(int turn) => startTurn<=turn && turn<=endTurn;
        public string StatusOn(int turn) => turn<startTurn ? "未开始" : turn>endTurn ? "已结束" : "生效中";
        public MarketEvent Copy() => new MarketEvent {id=id,title=title,description=description,startTurn=startTurn,endTurn=endTurn,effect=effect.Copy()};
    }

    [Serializable]
    public sealed class MarketCalendarState
    {
        public int seed;
        public bool fixedSchedule;
        public MarketEventDefinition[] definitions;
        public int[] generatedYears;
        public MarketEvent[] events;
    }

    public sealed class CalendarEventSegment
    {
        public MarketEvent Event { get; internal set; }
        public int Row { get; internal set; }
        public int Column { get; internal set; }
        public int Turns { get; internal set; }
        public int Lane { get; internal set; }
    }

    // A session snapshot, not a global clock. Reading a year never consumes customer randomness.
    public sealed class MarketCalendar
    {
        readonly int seed;
        readonly bool fixedSchedule;
        readonly MarketEventDefinition[] definitions;
        readonly HashSet<int> generatedYears = new HashSet<int>();
        readonly List<MarketEvent> events = new List<MarketEvent>();
        public static int YearStart(int turn) => (Math.Max(1,turn)-1)/12*12+1;
        // Isolated monthly prototype values, not approved economy balance.
        public const int TestEventsPerYear=3;
        public const int TestMaximumDuration=2;
        public const int TestCooldownTurns=2;

        public MarketCalendar(MarketEventDefinition[] definitions, int seed)
        {
            this.seed=seed;
            this.definitions=(definitions??Array.Empty<MarketEventDefinition>()).Select(d=>d.Copy()).ToArray();
        }
        public MarketCalendar(MarketCalendarState state)
        {
            if(state==null || state.events==null || state.generatedYears==null || state.definitions==null)
                throw new ArgumentException("市场存档不完整。");
            seed=state.seed;fixedSchedule=state.fixedSchedule;
            definitions=state.definitions.Select(d=>d.Copy()).ToArray();
            foreach(var year in state.generatedYears) {if(year<1 || !generatedYears.Add(year))throw new ArgumentException("市场年度记录重复或无效。");}
            foreach(var e in state.events)
            {
                if(e==null || string.IsNullOrWhiteSpace(e.id) || e.startTurn<1 || e.Duration<1 || e.Duration>3 || e.effect==null ||
                    float.IsNaN(e.effect.percent) || float.IsInfinity(e.effect.percent) || Math.Abs(e.effect.percent)>100 || events.Any(x=>x.id==e.id))
                    throw new ArgumentException("市场事件记录无效或重复。");
                events.Add(e.Copy());
            }
        }
        public MarketCalendarState Capture() => new MarketCalendarState {seed=seed,fixedSchedule=fixedSchedule,
            definitions=definitions.Select(d=>d.Copy()).ToArray(),generatedYears=generatedYears.OrderBy(w=>w).ToArray(),events=events.Select(e=>e.Copy()).ToArray()};

        void EnsureYear(int year)
        {
            // Generate missing years in order so browsing ahead cannot change the schedule.
            for(int w=1;w<=year;w++)
                if(generatedYears.Add(w) && !fixedSchedule && definitions.Length>0)GenerateYear(w);
        }
        void GenerateYear(int year)
        {
            var random=new System.Random(unchecked(seed ^ (year*73856093)));
            int count=TestEventsPerYear;
            for(int i=0;i<count;i++)
            {
                var candidates=new List<MarketEvent>();
                foreach(var d in definitions)
                for(int offset=0;offset<12;offset++)
                for(int duration=1;duration<=TestMaximumDuration;duration++)
                {
                    int start=(year-1)*12+1+offset,end=start+duration-1;
                    // Stable effect IDs identify the market type, including saved events.
                    // Ending on turn 4 blocks turns 5 and 6; the earliest repeat is turn 7.
                    if(events.Any(e=>e.effect.id==d.id && start<=e.endTurn+TestCooldownTurns && end+TestCooldownTurns>=e.startTurn))continue;
                    var effect=d.effect.Copy();effect.id=d.id;
                    candidates.Add(new MarketEvent {id=$"market:{year}:{i}",title=d.title,description=d.description,
                        startTurn=start,endTurn=end,effect=effect});
                }
                // A small custom event pool may run out: never violate the cooldown to fill a quota.
                if(candidates.Count==0)break;
                events.Add(candidates[random.Next(candidates.Count)]);
            }
        }
        public MarketEvent[] Between(int first,int last)
        {
            // Generate earlier years too, so cross-year events remain available.
            EnsureYear((last-1)/12+1);
            return events.Where(e=>e.startTurn<=last && e.endTurn>=first).OrderBy(e=>e.startTurn).ThenBy(e=>e.id,StringComparer.Ordinal).Select(e=>e.Copy()).ToArray();
        }
        public IEnumerable<PriceTag> ActiveTags(int turn) => Between(turn,turn).Where(e=>e.ActiveOn(turn)).Select(e=>
        {
            var tag=e.effect.Copy();tag.id=e.id;tag.title=e.title;return tag;
        });
        public MarketEvent[] VisibleBetween(int first,int last,int today)
            => Between(first,last).Where(e=>e.startTurn<=today).ToArray();
        public CalendarEventSegment[] Segments(int firstTurn,int today=int.MaxValue)
        {
            var visible=VisibleBetween(firstTurn,firstTurn+11,today);
            var result=new List<CalendarEventSegment>();
            for(int row=0;row<2;row++)
            {
                int start=firstTurn+row*6,end=start+5;
                var laneEnds=new List<int>();
                foreach(var e in visible.Where(e=>e.startTurn<=end && e.endTurn>=start))
                {
                    int a=Math.Max(start,e.startTurn),b=Math.Min(end,e.endTurn),lane=laneEnds.FindIndex(x=>x<a);
                    if(lane<0) {lane=laneEnds.Count;laneEnds.Add(b);} else laneEnds[lane]=b;
                    result.Add(new CalendarEventSegment{Event=e,Row=row,Column=a-start,Turns=b-a+1,Lane=lane});
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

        // Fixed, explicit verification data: overlap, and 1/2/3-turn durations.
        public static MarketCalendar OverlapExample()
        {
            var defs=PrototypeDefinitions();
            var scheduled=new[]
            {
                new MarketEvent{id="example-rise",title="丹药求购潮",description="固定测试：出售丹药 +20%，第7–9回合。",startTurn=7,endTurn=9,effect=defs[0].effect.Copy()},
                new MarketEvent{id="example-fall",title="丹药集中到货与集市临时让利",description="固定测试：丹药买卖 -10%，与求购潮在第8–9回合重叠。",startTurn=8,endTurn=9,effect=defs[1].effect.Copy()},
                new MarketEvent{id="example-buy",title="药商收购报价上涨",description="固定测试：只对玩家买入丹药 +20%。",startTurn=6,endTurn=6,effect=Definition("buy","收购", "",ItemCategory.Medicine,.2f,false,true).effect},
                new MarketEvent{id="example-low",title="丹药低价月",description="固定测试：只对玩家出售丹药 -20%。",startTurn=11,endTurn=11,effect=Definition("low","低价","",ItemCategory.Medicine,-.2f,true,false).effect}
            };
            return new MarketCalendar(new MarketCalendarState {seed=17,fixedSchedule=true,definitions=Array.Empty<MarketEventDefinition>(),generatedYears=Array.Empty<int>(),events=scheduled});
        }
    }
}
