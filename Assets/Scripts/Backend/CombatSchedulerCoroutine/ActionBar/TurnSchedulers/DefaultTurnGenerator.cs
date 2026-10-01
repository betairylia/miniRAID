using System.Collections.Generic;
using System.Linq;

namespace miniRAID.TurnSchedule
{
    public class DefaultTurnGenerator : TurnSchedulerGeneratorBase
    {
        public List<TurnSliceSO> turnSlices;
        
        public override TurnScheduleSequence GetNewTurn(ref Timestamp now)
        {
            var timestamp=now;
            now.currentTurnID += 1;
            return new TurnScheduleSequence(
                turnSlices
                    .Select(x => x.Wrap(new TurnSliceMetadata(null) { timestamp=timestamp }))
                    .Where(x => x != null));
        }
    }
}