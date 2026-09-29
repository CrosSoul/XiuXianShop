using System;
using System.Collections.Generic;
using System.Linq;

namespace XiuXianShop
{
    public sealed partial class MarketCalendar
    {
        public MarketEvent[] DisclosedEvents => events.Where(e=>e.knownInAdvance).OrderBy(e=>e.startTurn).Select(e=>e.Copy()).ToArray();
        bool CanSchedule(MarketEventDefinition definition,int start,int end) => !events.Any(e=>e.effect.id==definition.id &&
            (long)start<=e.endTurn+(long)e.cooldownTurns && end+(long)definition.cooldownTurns>=e.startTurn);

        MarketEvent ChooseCandidate(List<MarketEvent> candidates,Random random)
        {
            var groups=candidates.GroupBy(e=>e.effect.id).ToArray();
            double roll=random.NextDouble()*groups.Sum(g=>(double)definitions.Single(d=>d.id==g.Key).weight);
            var chosen=groups[groups.Length-1];
            foreach(var group in groups){roll-=definitions.Single(d=>d.id==group.Key).weight;if(roll<0){chosen=group;break;}}
            var options=chosen.ToArray();return options[random.Next(options.Length)];
        }

        public MarketEvent RevealSecret(int today,int minimumAhead,int maximumAhead,Random random)
        {
            if(minimumAhead<1 || maximumAhead<minimumAhead)throw new ArgumentException("秘闻时间窗口无效。");
            int first=today+minimumAhead,last=today+maximumAhead;
            EnsureYear((last-1)/12+1);
            var scheduled=events.Where(e=>e.startTurn>=first && e.startTurn<=last).ToArray();
            MarketEvent chosen;
            if(scheduled.Length>0)chosen=scheduled[random.Next(scheduled.Length)];
            else
            {
                var candidates=new List<MarketEvent>();
                foreach(var definition in definitions)
                for(int start=first;start<=last;start++)
                for(int duration=definition.minimumDuration;duration<=definition.maximumDuration;duration++)
                {
                    int end=start+duration-1;
                    if(!CanSchedule(definition,start,end))continue;
                    var effect=definition.effect.Copy();effect.id=definition.id;
                    candidates.Add(new MarketEvent{id=$"secret:{today}:{events.Count}",title=definition.title,
                        description=definition.description,startTurn=start,endTurn=end,effect=effect,cooldownTurns=definition.cooldownTurns});
                }
                if(candidates.Count==0)return null;
                // Select a legal type, then a legal start/duration for that type.
                chosen=ChooseCandidate(candidates,random);events.Add(chosen);
            }
            chosen.knownInAdvance=true;
            return chosen.Copy();
        }
    }
}
