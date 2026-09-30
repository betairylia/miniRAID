using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sirenix.OdinInspector;
using System.Linq;
using miniRAID.Spells;
using miniRAID.Weapon;
using Sirenix.Serialization;
using UnityEngine.Localization;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace miniRAID
{
    public class GeneralCombatData
    {
        public dNumber power, auxPower;
        public GridCollider shape;
        public RuntimeAction ract;

        public GameObject[] gameObjects;
        public Sprite[] sprites;

        public GeneralCombatData() { }

        public GeneralCombatData(float p)
        {
            power = (dNumber)p;
            auxPower = (dNumber)0;
            shape = null;
        }

        public GeneralCombatData(dNumber p)
        {
            power = p;
            auxPower = (dNumber)0;
            shape = null;
        }
    }

    public class PowerGetter
    {
        public enum PowerGetterType
        {
            STATIC,
            AttackPower,
            SpellPower,
            HealPower,
            BuffPower,
            DYNAMIC,
        }

        public PowerGetterType powerType;
        public LeveledStats<float> powerFactor;
        public System.Func<MobData, float> dynamicGetter;

        public PowerGetter(float val)
        {
            powerType = PowerGetterType.AttackPower;
            powerFactor = new LeveledStats<float>();
        }

        public float Eval(int level, MobData param)
        {
            switch (powerType)
            {
                case PowerGetterType.STATIC:
                    return powerFactor.Eval(level);
                    break;
                    
                case PowerGetterType.DYNAMIC:
                    Debug.LogError("PowerGetterType.DYNAMIC will cause problems during serialization, please be careful!");
                    throw new NotImplementedException();
                    return dynamicGetter(param);
                    break;
                
                case PowerGetterType.AttackPower:
                    return param.attackPower * powerFactor.Eval(level);
                    break;
                
                case PowerGetterType.SpellPower:
                    return param.spellPower * powerFactor.Eval(level);
                    break;
                
                case PowerGetterType.HealPower:
                    return param.healPower * powerFactor.Eval(level);
                    break;
                
                case PowerGetterType.BuffPower:
                    return param.buffPower * powerFactor.Eval(level);
                    break;
            }

            return 0;
        }
    }

    public class ColoredBoxAttribute : Attribute
    {
        public Color color;

        public ColoredBoxAttribute(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out color);
        }
    }

    /* TODO: Update doc from ActionDataOnPerformSO to ActionDataSO
     * The class for actions (data).
     * To provide a custom implementation (so you can use it in your actions), please
     * implement `OnPerform()`.
     *
     * Your implementation must be pure function, otherwise the behaviour is undefined.
     * Those ScriptableObjects won't be copied / instantiated during runtime, and only 1 instance (per created assets) will be kept in memory.
     *
     * Pure function means that your function does not have any side effects.
     * i.e., you cannot modify some variables that is outside of this function's scope.
     * To keep track of some external state, consider apply buffs to the source mob and query for that buff each time.
     */
    // [LuaCallCSharp]
    // [CreateAssetMenu(fileName = "ActionData.asset", menuName = "ActionDataSO", order = 0)]
    public abstract class ActionDataSO : CustomIconScriptableObject 
    {
        [Title("Basic info")]
        public LocalizedString ActionNameKey;

        public string ActionName => Globals.localizer.L(ActionNameKey) ?? "BAD_STRING";

        [FormerlySerializedAs("MaxLevel")] public int maxLevel;

        // [TextArea(1, 25)]
        public LocalizedString DescriptionKey;

        [Title("Flags")] public Consts.ActionFlags flags;
        public ValueGetter<MobData, bool> isActivelyUsed = true;

        // TODO: Boolean arrays
        // public List<string> Tags;
        [Title("Power stats")]
        public PowerGetter power;
        public PowerGetter auxPower;
        // public LeveledStats<float> test;
        
        // Mainshape of the action, typically effective range
        public virtual GridCollider MainShape { get; }
        public virtual bool IgnoreWall { get; }

        public abstract Dictionary<Cost.Type, (double, double)> GetCostBounds(MobData mob);
        public abstract bool CheckWithAbstractTargets(MobData mob, SpellTarget target);

        public abstract RuntimeAction LeveledWrapAbstract(MobData source, int level);

        public virtual void RecalculateStats(RuntimeAction ract, MobData mob) { }

        /// <summary>
        /// ract.RecalculateStats should be called before LazyPrepareTooltipVariables to get correct output.
        /// </summary>
        /// <returns>A dictionary(K: string, V: object) that represents all raw values can be used in tooltip.</returns>
        public virtual Dictionary<string, object> LazyPrepareTooltipVariables(RuntimeAction ract)
        {
            return new Dictionary<string, object> { {"Power", Mathf.CeilToInt(ract.power)} };
        }
    }

    public class ActionDataSO<TSpellTarget> : ActionDataSO where TSpellTarget : SpellTarget
    {
        // Cost related
        [Title("Costs", horizontalLine: true, bold: true)]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine)]
        public Dictionary<
            Cost.Type,
            ValueBoundedGetter<(MobData, TSpellTarget), MobData, double>> costs = new();

        public override Dictionary<Cost.Type, (double, double)> GetCostBounds(MobData mob)
        {
            return costs
                .Select(kvp => (kvp.Key, kvp.Value.PrecalculatedBounds(mob)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Item2);
        }

        //public LuaGetter<(Mob, T), GCDGroup> gcdGroup = GCDGroup.Common;

        [Title("Requester & Validation", horizontalLine: true, bold: true)]
        [Sirenix.OdinInspector.TypeFilter("GetRequesterTypes")]
        public UI.TargetRequester.TargetRequestValidatorBase<TSpellTarget> RequestValidator;
        public ActionTargetPickerBase<TSpellTarget> targetPicker;
        
        public override GridCollider MainShape { get => RequestValidator?.Shape; }
        public override bool IgnoreWall { get => RequestValidator?.IgnoreWall ?? true; }

        public virtual bool CheckCosts(MobRenderer mobRenderer, RuntimeAction<TSpellTarget> ract)
        {
            // TODO
            return true;
            //mob.data.availableActions.Contains(this) &&
            //!mob.data.IsInGCD(this.GCDgroup) &&
            //CooldownRemain <= 0;
        }

        public void DoCosts(MobRenderer mobRenderer)
        {
            // TODO
        }

        // Validation related (ActionValidatorSO) + Target Requester
        //[InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        //public ActionValidatorSO validator;

        public virtual bool Equipable(MobData mobdata) { return true; }
        public virtual bool Check(MobData mob) { return true; }

        public virtual bool CheckWithTargets(MobData mob, TSpellTarget target)
        {
            // TODO
            if (RequestValidator == null)
            {
                return true;
            }
            return RequestValidator.ValidateTargets(mob, target);
        }
        
        public override bool CheckWithAbstractTargets(MobData mob, SpellTarget target)
        {
            if ((target as TSpellTarget) is null)
            {
                return false;
            }

            return CheckWithTargets(mob, (TSpellTarget)target);
        }

        public IEnumerable<System.Type> GetRequesterTypes()
        {
            var q = typeof(UI.TargetRequester.TargetRequestValidatorBase<TSpellTarget>).Assembly.GetTypes()
            .Where(x => !x.IsAbstract)
            .Where(x => !x.IsGenericTypeDefinition)
            .Where(x => typeof(UI.TargetRequester.TargetRequestValidatorBase<TSpellTarget>).IsAssignableFrom(x));

            return q;
        }

        // Functions etc.
        [Title("Behavioural Parameters")] [SerializeField] private None _;
        // [Sirenix.Serialization.OdinSerialize]
        // [EventSlot]
        // public LuaFunc<(GeneralCombatData, MobRenderer, T), IEnumerator> onPerform = new();

        /// <summary>
        /// The coroutine that handles the action when performed.
        /// It should be override by the derived class if desired.
        ///
        /// Use `Globals.cc` to access the CoroutineContext, for e.g., animation settings & random number generators.
        /// </summary>
        /// <param name="ract">The RuntimeAction<TSpellTarget> instance wrapped from this ActionDataSO<TSpellTarget> that will be performed.</param>
        /// <param name="mob">MobData performing the action.</param>
        /// <param name="target">Target (List of Vector3Ints) of this action.</param>
        /// <returns>No return values.</returns>
        public virtual IEnumerator OnPerform(RuntimeAction<TSpellTarget> ract, MobData mob,
            TSpellTarget target)
        {
            yield return -1;
        }

        [Obsolete("Use RuntimeAction.Do instead.")]
        public virtual IEnumerator OnPerform(GeneralCombatData combatData, MobRenderer mobRenderer, TSpellTarget target)
        {
            yield break;
            // yield return new JumpIn(onPerform.Eval((combatData, mobRenderer, target)));
        }

        public virtual RuntimeAction<TSpellTarget> LeveledWrap(MobData source, int level)
        {
            var ract = new RuntimeAction<TSpellTarget>(source, this, level);
            ract.SetData(this);

            return ract;
        }
        
        public override RuntimeAction LeveledWrapAbstract(MobData source, int level)
        {
            return LeveledWrap(source, level);
        }
        
        //public override bool Equals(object other)
        //{
        //    return Guid == ((ActionDataSO)other).Guid;
        //}

        //public override int GetHashCode()
        //{
        //    return Guid.GetHashCode();
    }

    [System.Serializable]
    public struct ActionSOEntry
    {
        public ActionDataSO data;
        public int level;
    }
    
    // ActionSOEntry<TSpellTarget>
    
    [System.Serializable]
    public struct ActionSOEntry<TAction> where TAction : ActionDataSO
    {
        public TAction data;
        public int level;
        
        public ActionSOEntry ToBase()
        {
            return new ActionSOEntry()
            {
                data = data,
                level = level
            };
        }
    }

    public abstract class RuntimeAction : MobListener
    {
        public int Level => level;
        public int MaxLevel => data.maxLevel;

        public new ActionDataSO data; // TODO: FIXME: Overrides MobListener's MobListenerSO data. Should it?
        public ScriptableObject DataSO => data;
        public abstract System.Type SpellTargetType { get; }

        public string ActionName => data.ActionName;
        
        public virtual Consts.ActionFlags Flags => data.flags;

        public override MobListenerSO.ListenerType type => MobListenerSO.ListenerType.RuntimeAction;

        [OdinSerialize] private bool isValid = true;

        public bool Valid
        {
            get => isValid;
            set => isValid = value;
        }

        [NonSerialized]
        public List<(Cost, Cost)> costBounds = new();
        public int cooldownRemain;

        [NonSerialized] public dNumber power;
        [NonSerialized] public dNumber auxPower;
        [NonSerialized] public dNumber hit;
        [NonSerialized] public dNumber crit;

        public virtual GridCollider Shape => data.MainShape; 

        public delegate IEnumerator ActionOnPerform(MobRenderer mobRenderer, Spells.SpellTarget target);

        public delegate void Test(MobRenderer mobRenderer);

        protected RuntimeAction(MobData source, int level) : base(source, null)
        {
            this.level = level;
        }

        /// <summary>
        /// RecalculateStats should be called before LazyPrepareTooltipVariables to get correct output.
        /// </summary>
        /// <returns>A dictionary(K: string, V: object) that represents all raw values can be used in tooltip.</returns>
        public override Dictionary<string, object> LazyPrepareTooltipVariables() 
            => data.LazyPrepareTooltipVariables(this);

        public override string Tooltip => Globals.localizer.L(data.DescriptionKey, new object[]{LazyPrepareTooltipVariables()});
        
        public string GetFullTooltip(MobData mob)
        {
            string costString = "";
            foreach (var costBound in costBounds)
            {
                var lb = costBound.Item1;
                var ub = costBound.Item2;

                if(lb.type != ub.type) { Debug.LogError("Cost bound pair type mismatch, ignored."); continue; }

                if(lb.value == ub.value)
                {
                    costString += $"{lb.type.ToString()} {(int)lb.value.Value}\n";
                }
                else
                {
                    costString += $"{lb.type.ToString()} {(int)lb.value.Value} - {(int)ub.value.Value}\n";
                }
            }

            return $"{costString}\n" +
                   $"------------\n" +
                   $"{Tooltip}\n";/* +
                $"Power: {power}\n" +
                $"TODO - rAct.GetTooltip().";*/
        }

        public virtual void ShowInUI(VisualElement masterElem)
        {
            string costString = "";
            foreach (var costBound in costBounds)
            {
                var lb = costBound.Item1;
                var ub = costBound.Item2;

                if(lb.type != ub.type) { Debug.LogError("Cost bound pair type mismatch, ignored."); continue; }

                if(lb.value == ub.value)
                {
                    costString += $"{lb.type.ToString()} {(int)lb.value.Value}\n";
                }
                else
                {
                    costString += $"{lb.type.ToString()} {(int)lb.value.Value} - {(int)ub.value.Value}\n";
                }
            }
            
            masterElem.Q<Label>("name").text = data.ActionName;
            masterElem.Q<Label>("costs").text = costString.TrimEnd('\n');
            masterElem.Q<Label>("tooltip").text = Tooltip;
        }

        public abstract SpellTarget QueryAbstractTarget(MobData source);
        
        public void RecalculateStats(MobData mob)
        {
            if (costBounds == null)
            {
                costBounds = new();
            }
            
            // 1. Reset stats
            costBounds.Clear();
            var preBound = data.GetCostBounds(mob);
            
            foreach (var bound in preBound)
            {
                Cost lb = new Cost(dNumber.CreateComposite(bound.Value.Item1), bound.Key);
                Cost ub = new Cost(dNumber.CreateComposite(bound.Value.Item2), bound.Key);

                // Trigger events and re-calc.
                costBounds.Add((
                    mob.GetModifiedCost(lb, this),
                    mob.GetModifiedCost(ub, this)
                ));
            }

            // 2. Compute power etc.
            power = dNumber.CreateComposite(data.power.Eval(level, mob), "actionBase");
            auxPower = dNumber.CreateComposite(data.auxPower.Eval(level, mob), "actionBase");
            
            // TODO: assignable in inspector?
            hit = dNumber.CreateComposite(mob.hitAcc, "actionBase");
            crit = dNumber.CreateComposite(mob.crit, "actionBase");
            
            // TODO: FIXME: Assign gridShape here
            // shape = null;

            // After this, we will return to Mob.RecalculateStats();
            // Then Mob.OnActionStatCalculation event will be triggered,
            // which may modify our power values etc.
            // After the event, we go to OnRecalculateStatsFinish().

            data.RecalculateStats(this, mob);
        }

        public virtual void SetCoolDown(int cd)
        {
            cooldownRemain = cd;
        }

        public virtual IEnumerator OnNextTurn(MobData mob)
        {
            // CD moved to recover stage
            yield break;
        }

        public virtual IEnumerator OnRecoveryStage(MobData mob)
        {
            cooldownRemain -= 1;
            if (cooldownRemain < 0) { cooldownRemain = 0; }
            yield break;
        }

        public override void OnAttach(MobData mob)
        {
            base.OnAttach(mob);

            mob.OnNextTurn.AddListener(OnNextTurn);
            mob.OnRecoveryStage.AddListener(OnRecoveryStage);
        }

        public override void OnRemove(MobData mob)
        {
            mob.OnNextTurn.RemoveListener(OnNextTurn);
            mob.OnRecoveryStage.RemoveListener(OnRecoveryStage);
            
            base.OnRemove(mob);
        }

        public abstract IEnumerator ActivateAbstract(MobData mob, SpellTarget target);
        public abstract IEnumerator RequestInUI(MobData mob);
    }
    
    public class RuntimeAction<TSpellTarget> : RuntimeAction where TSpellTarget : SpellTarget
    {
        public ActionDataSO<TSpellTarget> actionData => (ActionDataSO<TSpellTarget>)data;
        public override System.Type SpellTargetType => typeof(TSpellTarget);
        
        public virtual Dictionary<
            Cost.Type,
            ValueBoundedGetter<(MobData, TSpellTarget), MobData, double>> costs => actionData.costs;
        
        public RuntimeAction(MobData source, ActionDataSO<TSpellTarget> data, int level) : base(source, level)
        {
            this.data = data;
        }

        public void SetData(ActionDataSO<TSpellTarget> data)
        {
            this.data = data;
        }

        public TSpellTarget LastTarget { get; private set; }

        public void _SetLastTarget(TSpellTarget target)
        {
            LastTarget = target;
        }

        /* Action perform routine:
         * ActivateInUI
         * > RecalcActionStats() -> Cost bounds
         * > Check if usable by mob (mob can comsume minimum cost)
         * > Pickable in UI; Picked in UI
         * > Target requester (Only legal positions pickable)
         * > Picked target
         * ~ Stat recalc?
         * > mob.ActionPrecheck (OnActionChosen)
         * > DoCost, etc...
         * > Activate()
         *
         * Activate(src, target, ...)
         * > CheckTarget()
         * > Do()
         */
        // TODO: Move Get delegate to Ctor
        public virtual IEnumerator Do(MobData mob, TSpellTarget target/*, bool cd = true*//*, bool host = false*/)
        {
            //            string postfix = data.Id;
            //            paddedLuaExpr = @$"function routine_{postfix}(mob, target)

            //{data.onPerform.LuaExpr}

            //end

            //function getCsRoutine_{postfix}(mob, target)
            //    return util.cs_generator(routine_{postfix}, mob, target)
            //end";

            //            Debug.Log(paddedLuaExpr);
            //            Globals.xLuaInstance.Instance.luaEnv.DoString(paddedLuaExpr, "Action onPerform chunk");

            //            var testIE = Globals.xLuaInstance.Instance.luaEnv.Global.Get<ActionOnPerform>($"getCsRoutine_{postfix}");
            //            Debug.Log(testIE);

            LastTarget = target;

            // Globals.ccNewContext(new SerialCoroutineContext() { animation = true, rng = Globals.cc.rng });
            yield return new JumpIn(actionData.OnPerform(this, mob, target));

            // Wait a bit for animation
            // yield return new WaitForSeconds(.5f);
        }

        public IEnumerator Activate(MobData mob, TSpellTarget target)
        {
            if(actionData.CheckWithTargets(mob, target))
            {
                Globals.combatMgr.Instance.ReportActionEffects(mob, this);
                yield return new JumpIn(Do(mob, target));
            }
            else Globals.combatMgr.Instance.ReportActionFailure("invalid_target", mob, this);
        }

        public override IEnumerator ActivateAbstract(MobData mob, SpellTarget target)
        {
            if ((target as TSpellTarget) != null)
            {
                yield return new JumpIn(Activate(mob, (TSpellTarget)target));
            }

            yield break;
        }

        public override IEnumerator RequestInUI(MobData mob)
        {
            Globals.combatMgr.Instance.BindPlayerAction(mob, this);
            Debug.LogWarning("Refactor UI to Coroutine based as well as Requesters !!!!!");

            if(cooldownRemain > 0)
            {
                Globals.combatMgr.Instance.ReportActionFailure("cooldown", mob, this);
                Globals.debugMessage.AddMessage($"{mob.nickname} 的 {data.name} 还没有准备好！");
                yield break;
            }
            
            TSpellTarget catchedTarget;

            if(actionData.Check(mob))
            {
                catchedTarget = null;
                bool canceled = false;

                // Assume max 1 request at once.
                actionData.RequestValidator.Request(mob, this, (TSpellTarget target) =>
                {
                    catchedTarget = target;
                }, () => { canceled = true; });

                while(catchedTarget == null && canceled == false)
                {
                    yield return null;
                }
                if (canceled) { yield break; }

                List<Cost> cost = costs.Select(pair =>
                new Cost(dNumber.CreateComposite(pair.Value.Eval((mob, catchedTarget))), pair.Key)).ToList();

                yield return new JumpIn(mob.DoAction(this, catchedTarget, cost));

                actionData.RequestValidator.EndState();
            }
            else Globals.combatMgr.Instance.ReportActionFailure("action_check_failed", mob, this);
        }

        public override SpellTarget QueryAbstractTarget(MobData source)
            => QueryTarget(source);

        public TSpellTarget QueryTarget(MobData source)
        {
            if (actionData?.targetPicker == null)
            {
                Debug.LogError($"{actionData?.ActionName} has no target picker!");
                return null;
            }

            return actionData.targetPicker.Pick(source, this);
        }
    }
}
