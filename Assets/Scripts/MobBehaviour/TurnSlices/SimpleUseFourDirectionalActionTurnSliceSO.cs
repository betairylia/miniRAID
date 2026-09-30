using System.Collections;
using System.Linq;
using miniRAID.Agents;
using miniRAID.Backend;
using miniRAID.Spells;
using miniRAID.TurnSchedule;
using Sirenix.Serialization;
using UnityEngine;

namespace miniRAID.MobBehaviour.TurnSlices
{
    public class SimpleUseFourDirectionalActionTurnSliceSO : SimpleUseTargetedActionTurnSliceSO
    {
        public override MobActionTurnSlice Wrap(MobData mob, RuntimeAction action, TurnSliceMetadata metadata)
        {
            return new SimpleUseFourDirectionalActionTurnSlice(mob, action, this, metadata);
        }

        public override IEnumerator Turn(TurnSlice slice, CombatSchedulerCoroutine coroutine)
        {
            throw new System.NotImplementedException();
        }
    }

    public class SimpleUseFourDirectionalActionTurnSlice : SimpleUseTargetedActionTurnSlice<FourDirectionalTarget>, IRenderableState
    {
        private bool IgnoreWall => action.data.IgnoreWall;

        public SimpleUseFourDirectionalActionTurnSlice(
            MobData mob, RuntimeAction action, AbstractTurnSliceSO data, TurnSliceMetadata metadata)
            : base(mob, action, data, metadata) { }

        protected override FourDirectionalTarget GetTarget(MobData self)
        {
            var targetIndicator = self.FindListener<TargetIndicator>();
            
            if (targetIndicator == null || targetIndicator.CurrentTarget == null)
            {
                Globals.ui.Instance.combatView.debugText.text = $"{mob.nickname}: {action.ActionName} -> NO Target";
                return new FourDirectionalTarget(Consts.Direction.Up);
            }
            
            Globals.ui.Instance.combatView.debugText.text = $"{mob.nickname}: {action.ActionName} -> {(((SimpleUseFourDirectionalActionTurnSliceSO)data).UpdateTargetAfterInitialized ? "" : "(LOCK) ")}{targetIndicator.CurrentTarget.nickname}";
            
            return new FourDirectionalTarget(
                Globals.backend.GetDominantDirection(self.GridPosition, targetIndicator.CurrentTarget.GridPosition));
        }

        public override void ConstructRenderer()
        {
            GridCollider indicatorShape = (GridCollider)((RuntimeAction<FourDirectionalTarget>)action)?.Shape?.CloneWithNewGuid();
            indicatorShape.Position = mob.Position;
            indicatorShape.Direction = target.Target;
            
            if (indicatorShape != null)
            {
                renderer = new GridColliderIndicator(
                    indicatorShape, GridOverlay.Types.INCOMING_ATTACK);
                UpdateRenderer();
            }
        }

        public override void UpdateRenderer()
        {
            GridCollider indicatorShape = (renderer as GridColliderIndicator)?.collider;
            if (indicatorShape != null)
            {
                indicatorShape.Position = mob.Position;
                indicatorShape.Direction = target.Target;
                (renderer as GridColliderIndicator)?.Update(indicatorShape, IgnoreWall ? null : mob.SpellCastPivot);
            }
        }
    }
}