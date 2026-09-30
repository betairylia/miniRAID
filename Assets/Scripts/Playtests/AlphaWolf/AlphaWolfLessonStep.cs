using System.Collections;
using miniRAID.TurnSchedule;
using UnityEngine;
namespace miniRAID.AlphaWolfPlaytest
{
    public class AlphaWolfLessonStep : TurnSliceSO
    {
        public bool prepare;
        public override IEnumerator Turn(TurnSlice slice, CombatSchedulerCoroutine scheduler)
        {
            var encounter=Object.FindFirstObjectByType<AlphaWolfLesson>();
            if(encounter==null || encounter.Finished) yield break;
            if(prepare) encounter.Prepare();
            else yield return new JumpIn(encounter.Resolve());
        }
    }
}
