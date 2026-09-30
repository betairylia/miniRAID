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
        // A receipt belongs to one UI submission, including its target request, never to later AI turns.
        public sealed class PlayerActionReceipt
        {
            public string failure;
            public bool completed, effectsStarted;
            public string Outcome => failure == null ? "succeeded" : effectsStarted ? "partial_failure" : "rejected";
            internal MobData source;
            internal RuntimeAction action;
        }
        public PlayerActionReceipt LastPlayerAction { get; private set; }
        PlayerActionReceipt executingPlayerAction;
        public void BindPlayerAction(MobData source, RuntimeAction action)
        {
            if (executingPlayerAction == null) return;
            executingPlayerAction.source = source;
            executingPlayerAction.action = action;
        }
        bool IsSubmittedAction(MobData source, RuntimeAction action) => executingPlayerAction != null &&
            source != null && executingPlayerAction.source == source && executingPlayerAction.action == action;
        public void ReportActionEffects(MobData source, RuntimeAction action)
        {
            if (IsSubmittedAction(source, action)) executingPlayerAction.effectsStarted = true;
        }
        public void ReportActionFailure(string reason, MobData source = null, RuntimeAction action = null)
        {
            ActionFailure = reason; // Legacy diagnostic only; not a player command result.
            if (IsSubmittedAction(source, action)) executingPlayerAction.failure ??= reason;
        }

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

            var receipt = new PlayerActionReceipt();
            LastPlayerAction = receipt;
            IEnumerator OnFinishWrapper()
            {
                executingPlayerAction = receipt;
                yield return new JumpIn(action);
                receipt.completed = true;
                executingPlayerAction = null;

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
