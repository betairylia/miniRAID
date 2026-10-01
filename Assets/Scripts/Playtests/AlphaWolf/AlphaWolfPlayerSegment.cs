using System.Collections;
using miniRAID.TurnSchedule;
namespace miniRAID.AlphaWolfPlaytest
{
    // Exactly one character per segment. Generic EndTurn must also consume an actor
    // who already moved/cast, otherwise that actor could take both slots this round.
    public class AlphaWolfPlayerSegment : CommonPlayerTurnSliceSO
    {
        public override IEnumerator Turn(TurnSlice slice, CombatSchedulerCoroutine scheduler)
        {
            yield return new JumpIn(base.Turn(slice,scheduler));
            var actor=(slice as LockedPlayerTurnSlice)?.LockedPlayer;
            if(actor!=null && actor.isActive && !actor.isDead)
                yield return new JumpIn(actor.SetActive(false));
            UnityEngine.Object.FindFirstObjectByType<AlphaWolfLesson>()?.CompletePlayerSegment();
        }
    }
}
