using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace miniRAID
{
	public partial class CombatSchedulerCoroutine : MonoBehaviour
    {
        // #region Cut-In animations
        // public IEnumerator UIPlayerPhase()
        // {
        //     // notify UI
        //     Globals.debugMessage.AddMessage($"玩家回合");
        //
        //     yield break;
        // }
        //
        // public IEnumerator UIEnemyPhase()
        // {
        //     // notify UI
        //     Globals.debugMessage.AddMessage($"敌方回合");
        //
        //     yield break;
        // }
        //
        // public IEnumerator UIAllyPhase()
        // {
        //     // notify UI
        //     Globals.debugMessage.AddMessage($"友方回合");
        //
        //     yield break;
        // }
        // #endregion

        MobRenderer _chosenMobRenderer = null;

        System.Func<IEnumerator> actionToDo = null;
        public bool WaitingForPlayer { get; private set; }
        public bool ActionPending => actionToDo != null;
        public string ActionFailure { get; private set; }
        public void ReportActionFailure(string reason) => ActionFailure = reason;

        public IEnumerator UIWaitPlayerInput()
        {
            WaitingForPlayer = true;
            // Refresh UI state
            Globals.ui.Instance.EnterState();

            // bool shouldEnd = false;

            while(!playerPhaseEnd)
            {
                if(actionToDo == null) { yield return null; }
                else
                {
                    yield return new JumpIn(actionToDo.Invoke());
                    // shouldEnd = CheckPhaseEnd(awaitForActions);
                }
            }

            WaitingForPlayer = false;
            playerPhaseEnd = false;
        }

        // Uses lazy evaluation of IEnumerator.
        public bool UIPickedAction(IEnumerator action, IEnumerator onActionFinished)
        {
            if(!WaitingForPlayer || actionToDo != null)
            {
                Debug.LogError("CombatSchedulerCoroutine: UIPickedAction before previous action finished! Request overrided but nobody knows what will happen ...");
                return false;
            }

            IEnumerator OnFinishWrapper()
            {
                yield return new JumpIn(action);

                // Maybe some fading animation?
                yield return new JumpIn(onActionFinished);

                Globals.logger?.Log($"[csc-UI] Set actionToDo to NULL value");
                actionToDo = null;
            };

            Globals.logger?.Log($"[csc-UI] Set actionToDo to non-null value");
            ActionFailure = null;
            actionToDo = OnFinishWrapper;

            return true;
        }
    }
}
