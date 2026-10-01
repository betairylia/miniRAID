using System.Linq;
using System.Collections.Generic;
using miniRAID.TurnSchedule;
namespace miniRAID.AlphaWolfPlaytest
{
    public class AlphaWolfTurnGenerator : DefaultTurnGenerator
    {
        public override TurnScheduleSequence GetNewTurn(ref Timestamp now)
        {
            var timestamp=now;
            var start=turnSlices.OfType<StartTurnTurnSliceSO>().Single();
            var recovery=turnSlices.OfType<RecoveryTurnSliceSO>().Single();
            var prepare=turnSlices.OfType<AlphaWolfLessonStep>().Single(x=>x.prepare);
            var resolve=turnSlices.OfType<AlphaWolfLessonStep>().Single(x=>!x.prepare && !x.finishPhase);
            var finish=turnSlices.OfType<AlphaWolfLessonStep>().Single(x=>x.finishPhase);
            var player=turnSlices.OfType<AlphaWolfPlayerSegment>().First();
            var auto=turnSlices.OfType<AutoAttackTurnSliceSO>().Single();
            var end=turnSlices.OfType<EndTurnTurnSliceSO>().Single();
            var steps=new List<TurnSliceSO>{start};
            // Initial wake-up only. Subsequent recovery belongs to the END of each phase.
            if(timestamp.currentTurnID==1)steps.Add(recovery);
            steps.Add(prepare);steps.Add(player);
            bool roar=(timestamp.currentTurnID-1)%3==2;
            if(!roar)steps.Add(resolve);
            steps.Add(player);
            if(roar)steps.Add(resolve);
            steps.Add(finish);steps.Add(auto);steps.Add(recovery);steps.Add(end);
            now.currentTurnID++;
            return new TurnScheduleSequence(steps.Select(x=>x.Wrap(new TurnSliceMetadata(null){timestamp=timestamp})));
        }
    }
}
