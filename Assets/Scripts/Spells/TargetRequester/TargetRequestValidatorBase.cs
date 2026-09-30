using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using miniRAID.Spells;
using UnityEngine.InputSystem;

using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace miniRAID.UI.TargetRequester
{
    public enum RequestType
    {
        Target,
        Ground,
        TargetRange,
        Directional
    }

    public class RequestStage
    {
        public Dictionary<Vector3Int, GridOverlay.Types> map;
        public Dictionary<Vector3Int, GridOverlay.Types> cursor;
        public RequestType type;

        public RequestStage()
        {
            map = new Dictionary<Vector3Int, GridOverlay.Types>();
            cursor = new Dictionary<Vector3Int, GridOverlay.Types>();
            type = RequestType.Target;
        }
    }

    [System.Serializable]
    public abstract class TargetRequesterUIState : UIState
    {
        public bool IsAwaitingChoice { get; protected set; }
        public abstract IEnumerable<Vector3Int> Choices { get; }
        public abstract bool TrySubmitGrid(Vector3Int grid);
    }

    /// <summary>
    /// 1 UIState start a request, given a mob;
    /// 2 TargetRequester will get control of current UI
    /// 3 After finished, TargetRequester invokes onFinish callback and give back the control of current UI
    ///
    /// TODO: The requester is unique upto ActionDataSOs, i.e., it is shared among RuntimeActions. Is it okay?
    /// </summary>
    [System.Serializable]
    public abstract class TargetRequestValidatorBase<T> : TargetRequesterUIState where T : SpellTarget
    {
        protected MobData mob;
        protected RuntimeAction<T> ract;

        public virtual GridCollider Shape
        {
            get
            {
                if (shape == null)
                {
                    shape = new PointCollider();
                }

                return shape;
            }
        }

        [SerializeField] protected GridCollider shape;
        
        public virtual bool IgnoreWall { get => ignoreWall; }
        [SerializeField] protected bool ignoreWall = false;

        [FoldoutGroup("Debug info")]
        [LabelText("Current Stage")]
        [ReadOnly]
        public int currentStageCompleted = -1;

        [FoldoutGroup("Debug info")]
        [ReadOnly]
        public Stack<RequestStage> query_stack = new Stack<RequestStage>();

        [FoldoutGroup("Debug info")]
        [ReadOnly]
        public Stack<Vector3Int> choice = new Stack<Vector3Int>();

        public delegate void OnRequestFinish(T target);

        protected System.Action onCancel;
        protected OnRequestFinish onFinish;

        protected GridOverlay overlay;

        RequestStage currentQuery
        {
            get
            {
                if(query_stack != null && query_stack.Count > 0)
                {
                    return query_stack.Peek();
                }
                return null;
            }
        }

        /// <summary>
        /// This will modify current UI
        /// </summary>
        /// <param name="mob"></param>
        public virtual void Request(MobData mob, RuntimeAction<T> ract, OnRequestFinish onFinish, System.Action onCancel)
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
            _Next(mob.GridPosition, false);
        }

        protected void _Next(Vector3Int coord, bool notFirst = true)
        {
            if (notFirst)
            {
                choice.Push(coord);
            }

            currentStageCompleted += 1;
            query_stack.Push(Next(coord, notFirst));
            ShowQuery();
        }

        /// <summary>
        /// Progress to next step
        /// </summary>
        /// <param name="coord">coord selected in this step by UI</param>
        public abstract RequestStage Next(Vector3Int coord, bool notFirst = true);

        public virtual void Back()
        {
            if(query_stack.Count > 0)
            {
                query_stack.Pop();
            }

            if(choice.Count > 0)
            {
                choice.Pop();
            }

            currentStageCompleted -= 1;

            ShowQuery();
        }

        public virtual bool IsChoiceValid(Vector3Int coord)
        {
            return currentQuery != null && currentQuery.map.ContainsKey(coord);
        }

        public override IEnumerable<Vector3Int> Choices => currentQuery?.map.Keys ?? Enumerable.Empty<Vector3Int>();

        public override bool TrySubmitGrid(Vector3Int grid)
        {
            if (!enabled || !IsAwaitingChoice || !IsChoiceValid(grid)) return false;
            _Next(grid);
            return true;
        }

        public override void Submit(InputValue input) => TrySubmitGrid(ui.cursor.GridPos);

        public override void Cancel(InputValue input)
        {
            if(currentStageCompleted > 0)
            {
                Back();
            }
            else
            {
                IsAwaitingChoice = false;
                SafeKillOverlay();
                onCancel();
                base.Cancel(input);
            }
        }

        public virtual void Finish(T result)
        {
            IsAwaitingChoice = false;
            SafeKillOverlay();

            onFinish?.Invoke(result);
        }

        public void EndState()
        {
            ui.BackState();
        }

        public void SafeKillOverlay()
        {
            if (overlay != null)
            {
                GameObject.Destroy(overlay.gameObject);
            }
        }

        public override void OnStateExit()
        {
            base.OnStateExit();
            ui.cursor.ChangeCollider(new PointCollider());
        }

        public void ShowQuery()
        {
            SafeKillOverlay();

            if (currentQuery != null)
            {
                overlay = Globals.overlayMgr.Instance.FromDictionary(
                    currentQuery.map.ToDictionary(
                        x => Globals.backend.GetColliderMapIntersect(x.Key),
                        x => x.Value
                    )
                );
            }
            // TODO: change cursor
        }

        public virtual bool ValidateTargets(MobData mob, T target)
        {
            return true;
        }
    }
}
