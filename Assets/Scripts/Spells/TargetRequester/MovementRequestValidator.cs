using System.Collections;
using System.Collections.Generic;
using System.Linq;
using miniRAID.Spells;
using UnityEngine;

namespace miniRAID.UI.TargetRequester
{
    public class MovementRequestValidator : TargetRequestValidatorBase<MovementTarget>
    {
        public bool overrideMobMovement = false;
        public int moveRange = 3;
        public int extraRange = 0;

        private Dictionary<Vector3Int, (Vector3Int prevGrid, float distance)> gridInfo;

        protected virtual int GridPathCost(GridData data)
        {
            if(data.solid || data.mob != null) { return 1; }
            return 0;
        }

        public override RequestStage Next(Vector3Int coord, bool notFirst = true)
        {
            if(currentStageCompleted >= 1)
            {
                Decided();
                return null;
            }

            RequestStage stage = new RequestStage();

            float resolvedMoveRange = mob.actedThisTurn ? 0 : mob.MoveRangeLeft;
            int resolvedExtraRange = Mathf.FloorToInt(mob.actionPoints);
            if (overrideMobMovement)
            {
                resolvedMoveRange = this.moveRange;
                resolvedExtraRange = extraRange;
            }

            Globals.backend.GenericMovementBFS(
                mob.Collider,
                (Movement)ract,
                x => false,
                Mathf.CeilToInt(resolvedMoveRange) + resolvedExtraRange,
                out gridInfo
            );
            
            stage.type = RequestType.Ground;

            foreach (var gridPos in gridInfo)
            {
                stage.map.Add(gridPos.Key, gridPos.Value.distance > resolvedMoveRange ? GridOverlay.Types.BUFF : GridOverlay.Types.MOVE);
            }

            return stage;
        }

        // Read the exact cached player BFS path; never rerun the requester or mutate costs.
        public object PreviewCost(Vector3Int grid)
        {
            if (!IsAwaitingChoice || gridInfo == null || !Choices.Contains(grid)) return null;
            var movement = (Movement)ract;
            var path = Databackend.ReconstructPath(x => gridInfo[x].prevGrid, grid);
            float moved = mob.movedGrids, ap = 0, distance = 0;
            var previous = mob.GridPosition;
            foreach (var step in path.path)
            {
                var d = movement.ComputeDistance(previous, step);
                distance += d;
                if (!movement.movementData.ignoreCostByDistance)
                {
                    ap += Mathf.Floor(Mathf.Max(0, d - (mob.actedThisTurn ? 0 : mob.MoveRange - moved)) * 100) / 100;
                    if (!mob.actedThisTurn) moved = Mathf.Min(mob.MoveRange, moved + d);
                }
                previous = step;
            }
            return new { distance, movementAP = ap, moveRemaining = mob.MoveRange - moved };
        }

        public object PreviewRules() => new {
            actionCostBounds = ract.costBounds.Select(c=>new { resource=c.Item1.type.ToString(), min=c.Item1.value.Value, max=c.Item2.value.Value }).ToArray(),
            basis = "cached_ui_path", excludes = "dynamic effects; base costs listed separately" };

        void Decided()
        {
            // Reconstruct path
            var result = new MovementTarget();
            result.path = Databackend.ReconstructPath(x => gridInfo[x].prevGrid, choice.First());
            result.destinationGrid = choice.First();
            
            Finish(result);
        }
    }
}