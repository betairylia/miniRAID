using System.Collections;
using System.Linq;
using miniRAID.Spells;
using UnityEngine;
using UnityEngine.InputSystem;

namespace miniRAID.UI.TargetRequester
{
    // TODO: Implement this
    public class FourDirectionalRequestValidator : TargetRequestValidatorBase<FourDirectionalTarget>
    {
        public override GridCollider Shape => shape;
        
        public GridOverlay.Types type;

        public override RequestStage Next(Vector3Int coord, bool notFirst = true)
        {
            // Empty
            RequestStage stage = new RequestStage();
            return stage;
        }

        public override void Request(
            MobData mob, RuntimeAction<FourDirectionalTarget> ract, OnRequestFinish onFinish, System.Action onCancel)
        {
            IsAwaitingChoice = true;
            this.mob = mob;
            this.ract = ract;
            currentStageCompleted = -1;
            query_stack ??= new();
            query_stack.Clear();
            choice ??= new();
            choice.Clear();

            this.onFinish = onFinish;
            this.onCancel = onCancel;

            ui.EnterState(this, true);
            ui.cursor.ChangeCollider(shape);
        }

        public override System.Collections.Generic.IEnumerable<Vector3Int> Choices => new[] { mob.GridPosition + Vector3Int.forward, mob.GridPosition + Vector3Int.back, mob.GridPosition + Vector3Int.left, mob.GridPosition + Vector3Int.right };

        public override bool TrySubmitGrid(Vector3Int grid)
        {
            if (!enabled || !IsAwaitingChoice || !Choices.Contains(grid)) return false;
            UpdateCursor(grid);
            _Next(grid);
            
            var dirc = Globals.backend.GetDominantDirection(mob.GridPosition, choice.First());
            Finish(new FourDirectionalTarget(dirc));
            return true;
        }

        public override void Submit(InputValue input)
        {
            UpdateCursor(ui.cursor.GridPos);
            TrySubmitGrid(ui.cursor.GridPos);
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            UpdateCursor(mob.GridPosition);
        }

        public override void PointAtGrid(Vector3Int gridPos)
        {
            base.PointAtGrid(gridPos);

            UpdateCursor(gridPos);
        }

        public override void MoveCursor(Vector3Int movement)
        {
            UpdateCursor(mob.GridPosition + movement);
        }

        void UpdateCursor(Vector3Int gridPos, bool forced = false)
        {
            var dirc = Globals.backend.GetDominantDirection(mob.GridPosition, gridPos);

            switch (dirc)
            {
                case Consts.Direction.Up:
                    ui.cursor.Position = mob.Position + new Vector3Int(0, 0, 1);
                    break;
                case Consts.Direction.Left:
                    ui.cursor.Position = mob.Position + new Vector3Int(-1, 0, 0);
                    break;
                case Consts.Direction.Down:
                    ui.cursor.Position = mob.Position + new Vector3Int(0, 0, -1);
                    break;
                case Consts.Direction.Right:
                    ui.cursor.Position = mob.Position + new Vector3Int(1, 0, 0);
                    break;
            }
            
            if (forced || dirc != shape.Direction)
            {
                shape.Direction = dirc;
                ui.cursor.Update(shape, mob.SpellCastPivot);
            }
        }
    }
}