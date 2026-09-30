using miniRAID.TurnSchedule;
namespace miniRAID.AlphaWolfPlaytest
{
    public class AlphaWolfTurnGenerator : DefaultTurnGenerator
    {
        public override TurnScheduleSequence GetNewTurn(ref Timestamp now)
        {
            var timestamp = now;
            var sequence = base.GetNewTurn(ref now);
            foreach (var slice in sequence) slice.metadata.timestamp = timestamp;
            return sequence;
        }
    }
}
