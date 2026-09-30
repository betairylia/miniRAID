using System.Collections;
using System.Collections.Generic;
using System.Linq;
using miniRAID.Spells;
using UnityEngine;

namespace miniRAID
{
    public partial class MobData
    {
        #region Manipulating actions
        
        public bool actedThisTurn { get; protected set; }
        public Movement movement;
        
        public RuntimeAction AddAction(ActionSOEntry actSO)
        {
            if (actSO.data == null)
            {
                Debug.LogError("Action to be added has null data. Action ignored.");
                return null;
            }
            var ract = actSO.data.LeveledWrapAbstract(this, actSO.level);
            actions.Add(ract);
            AddListener(ract);

            return ract;
        }

        public RuntimeAction AddAction<T>(ActionSOEntry<T> actSO) where T : ActionDataSO
            => AddAction(actSO.ToBase());
        
        /// <summary>
        /// Get the first available action with action name.
        /// </summary>
        /// <param name="ActionName"></param>
        /// <returns></returns>
        public RuntimeAction GetAction(string ActionName)
        {
            foreach (var act in availableActions)
            {
                if(act.data.ActionName == ActionName)
                {
                    return act;
                }
            }

            return null;
        }

        public RuntimeAction GetAction(ActionDataSO action)
        {
            foreach (var act in availableActions)
            {
                if (act.data == action)
                {
                    return act;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the first action of class specified.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetAction<T>() where T : RuntimeAction
        {
            foreach (var act in availableActions)
            {
                if (act is T)
                {
                    return act as T;
                }
            }

            return null;
        }
        
        public RuntimeAction GetActionFromSO<T>() where T : ActionDataSO
        {
            foreach (var act in availableActions)
            {
                if (act.data is T)
                {
                    return act;
                }
            }

            return null;
        }

        #endregion
        
        #region Performing actions
        
        public IEnumerator ActionPrecheck(RuntimeAction raction, SpellTarget target)
        {
            yield return new JumpIn(OnActionChosen?.InvokeCoroutine(this, raction, target));
        }

        public IEnumerator ActionBegin(RuntimeAction raction, SpellTarget target)
        {
            yield return new JumpIn(OnActionPrecast?.InvokeCoroutine(this, raction, target));
        }

        public IEnumerator ActionDone(RuntimeAction raction, SpellTarget target)
        {
            yield return new JumpIn(OnActionPostcast?.InvokeCoroutine(this, raction, target));

            if (!raction.Flags.HasFlag(Consts.ActionFlags.Movement))
            {
                actedThisTurn = true;
            }
            
            RecalculateStats();
        }
        
        //public Cost GetDisplayCost(Cost cost, RuntimeAction<TSpellTarget> ract)
        //{
        //    // TODO: Dummy action
        //    OnCostQueryDisplay?.Invoke(cost, ract, this);
        //    return cost;
        //}
        
        public Cost GetModifiedCost(Cost cost, RuntimeAction ract)
        {
            // TODO: Dummy action
            OnModifyCost?.InvokeInstant(cost, ract, this);
            return cost;
        }
        
        public bool CheckCost(Cost cost, RuntimeAction ract)
        {
            // TODO: Dummy action
            var mCost = GetModifiedCost(cost, ract);
            bool pass = false;

            switch(mCost.type)
            {
                case Cost.Type.AP:
                    pass = cost.value <= actionPoints;
                    break;
                case Cost.Type.CD:
                    pass = ract.cooldownRemain <= 0;
                    break;
            }

            return OnCheckCost?.InvokeWhenNot(pass, cost, ract, this) ?? pass;
        }
        
        public IEnumerator ApplyCost(Cost cost, RuntimeAction ract)
        {
            yield return new JumpIn(OnApplyCost?.InvokeCoroutine(cost, ract, this));

            switch (cost.type)
            {
                case Cost.Type.AP:
                    UseActionPoint(cost.value);
                    break;
                case Cost.Type.Mana:
                    Debug.LogWarning("Please implement ApplyCost for Mana.");
                    break;
                case Cost.Type.CD:
                    ract.SetCoolDown(cost.value);
                    break;
                default:
                    // This should be handled in OnApplyCost() event.
                    break;
            }

            yield break;
        }

        public IEnumerator DoAction<TSpellTarget>(
            ActionSOEntry actionEntry,
            TSpellTarget target,
            List<Cost> costs = null) where TSpellTarget : SpellTarget
        {
            var ra = (GetAction(actionEntry.data) ?? AddAction(actionEntry)) as RuntimeAction<TSpellTarget>;
            if (ra == null)
            {
                Debug.LogError("RuntimeAction cannot be retrieved!");
                yield break;
            }
            
            int tempLevel = -99;
            if (ra.level != actionEntry.level)
            {
                tempLevel = ra.level;
                ra.level = actionEntry.level;
            }
            
            yield return new JumpIn(DoAction(ra, target, costs));
            
            if(tempLevel != -99)
            {
                ra.level = tempLevel;
            }
        }

        // Check costs, apply them and emit the action
        // Quite dirty due to type-specific / type-agnostic settings, any better workarounds?
        // Wanted to avoid casting back to type-agnostic RuntimeAction / SpellTarget pairs.
        public IEnumerator _DoActionWrapper (
            RuntimeAction raction,
            SpellTarget target,
            List<Cost> costs,
            IEnumerator action)
        {
            // Needs this?
            //RecalculateStats();

            yield return new JumpIn(ActionPrecheck(raction, target));
            
            // Do Cost Check Stuffs
            if(costs != null)
            {
                // Check all costs
                if(costs.Select(cost => CheckCost(cost, raction)).Any(x => !x))
                {
                    // If some cost cannot be satisfied
                    // Stop using the action
                    Globals.combatMgr.Instance.ReportActionFailure("insufficient_cost");
                    yield break;
                }

                foreach (Cost cost in costs)
                {
                    yield return new JumpIn(ApplyCost(cost, raction));
                }
            }
            
            Globals.logger?.Log($"[DoAction] {nickname} attempts casting {raction.data.name} towards {target.ToString()}");
            Globals.combatTracker.Record(new Consts.TrackerActionEvent()
            {
                action = raction,
                target = target
            });

            yield return new JumpIn(ActionBegin(raction, target));
            yield return new JumpIn(action);
            yield return new JumpIn(ActionDone(raction, target));
        }
        
        public IEnumerator _DoActionSpecific<TSpellTarget>(
            RuntimeAction<TSpellTarget> raction,
            TSpellTarget target) where TSpellTarget : SpellTarget
        {
            yield return new JumpIn(raction.Activate(this, target));
        }

        /// <summary>
        /// Perform an action.
        /// </summary>
        /// <param name="raction">Runtime action instance.</param>
        /// <param name="target">Target to perform the action.</param>
        /// <param name="costs">Costs that must be applied before performing the action. Default is null (no cost).</param>
        /// <typeparam name="TSpellTarget">Type of the SpellTarget of the action.</typeparam>
        /// <returns></returns>
        public IEnumerator DoAction<TSpellTarget>(
            RuntimeAction<TSpellTarget> raction,
            TSpellTarget target,
            List<Cost> costs = null) where TSpellTarget : SpellTarget
            => _DoActionWrapper(raction, target, costs, _DoActionSpecific(raction, target));

        // Type-agnostic version of above
        public IEnumerator _DoActionAbstract(
            RuntimeAction raction,
            SpellTarget target)
        {
            yield return new JumpIn(raction.ActivateAbstract(this, target));
        }

        /// <summary>
        /// Perform an action (without knowing its target type).
        /// </summary>
        /// <param name="raction">Runtime action instance.</param>
        /// <param name="target">Target to perform the action.</param>
        /// <param name="costs">Costs that must be applied before performing the action. Default is null (no cost).</param>
        /// <returns></returns>
        public IEnumerator DoAction(
            RuntimeAction raction,
            SpellTarget target,
            List<Cost> costs = null)
            => _DoActionWrapper(raction, target, costs, _DoActionAbstract(raction, target));
        
        public IEnumerator DoActionWithDefaultCosts<TSpellTarget>(
            RuntimeAction<TSpellTarget> raction,
            TSpellTarget target) where TSpellTarget : SpellTarget
        {
            List<Cost> cost = raction.costs.Select(pair =>
                new Cost(dNumber.CreateComposite(pair.Value.Eval((this, target))), pair.Key)).ToList();
            yield return new JumpIn(DoAction(raction, target, cost));
        }
        
        internal bool CheckIsActionPerformable(RuntimeAction action)
        {
            // CheckCost modifies costs for subsequent payment. UI queries must use copies.
            // dNumber is a value struct, so this also copies its numeric composition.
            return action.Valid && action.costBounds.Select(cost =>
                CheckCost(new Cost(cost.Item1.value, cost.Item1.type), action)).All(x => x);
        }
        
        // Play an animation on MobRenderer instance and wait for complete.
        // If mobRenderer == null, this coroutine will end immediately.
        public IEnumerator WaitForAnimation(string animationState)
        {
            if (mobRenderer != null)
                yield return new JumpIn(mobRenderer.WaitForAnimation(animationState));
        }
        
        #endregion
    }
}