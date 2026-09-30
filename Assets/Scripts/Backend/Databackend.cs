using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using miniRAID.Spells;
using miniRAID.Buff;
using miniRAID.ActionHelpers;
using System;
using System.Linq;
using miniRAID.Backend;
using Backend.Map;
using Sirenix.Serialization;
using Sirenix.Utilities;
using UnityEngine.Serialization;

namespace miniRAID
{
    public class GridData
    {
        public enum TerrainType
        {
            Normal,
            Mountain
        }

        public bool solid = false, standable = false, passable = true;
        public MobData mob;

        public int intrusion;
        
        public TerrainType type;
    }

    public class GridPath
    {
        public List<Vector3Int> path = new List<Vector3Int>();

        public void Step()
        {
            path.RemoveAt(0);
        }

        public MovementTarget ToMovementTarget()
        {
            MovementTarget movementTarget = new MovementTarget();
            movementTarget.destinationGrid = path[^1];
            movementTarget.path = this;

            return movementTarget;
        }

        public GridPath Trim(int trimLength, bool removeTrimed = false)
        {
            trimLength = Mathf.Clamp(trimLength, 0, this.path.Count);
            
            GridPath path = new GridPath();
            path.path = this.path.GetRange(0, trimLength);

            if (removeTrimed)
            {
                this.path.RemoveRange(0, trimLength);
            }

            return path;
        }
    }

    public static class StructExts
    {
        public static T Clone<T>(this T val) where T : struct => val;
    }

    public static class Consts
    {
        public enum Direction
        {
            Up = 0,
            Left = 1,
            Down = 2,
            Right = 3
        };

        public static Vector3Int[] DirectionVectors = new Vector3Int[4]
        {
            Vector3Int.forward,
            Vector3Int.left,
            Vector3Int.back,
            Vector3Int.right,
        };

        public static Vector3 Rotate(Vector3 osVector, Direction direction)
        {
            switch (direction)
            {
                case Consts.Direction.Up:
                    return osVector;
                    break;
                case Consts.Direction.Down:
                    return new Vector3(-osVector.x, osVector.y, -osVector.z);
                    break;
                case Consts.Direction.Left:
                    return new Vector3(-osVector.z, osVector.y, osVector.x);
                    break;
                case Consts.Direction.Right:
                    return new Vector3(osVector.z, osVector.y, -osVector.x);
                    break;
            }

            return Vector3.zero;
        }

        public static Vector3Int Rotate(Vector3Int osVector, Direction direction)
            => Vector3Int.FloorToInt(Rotate((Vector3)osVector, direction));

        public static BoundsInt Rotate(BoundsInt osBounds, Direction direction)
        {
            var min = osBounds.min;
            var max = osBounds.max;
            min = Rotate(min, direction);
            max = Rotate(max, direction);
            
            osBounds.SetMinMax(Vector3Int.Min(min, max), Vector3Int.Max(min, max));
            return osBounds;
        }

        public enum AllElements
        {
            Physical = 16,
            Elemental = 17,
            Pure = 32,

            Slash = 0,
            Knock = 1,
            Pierce = 2,

            Fire = 3,
            Ice = 4,
            Water = 5,
            Nature = 6,
            Wind = 7,
            Thunder = 8,
            Light = 9,
            Dark = 10,

            Heal = 11
        }

        public enum Elements
        {
            Slash = 0,
            Knock = 1,
            Pierce = 2,

            Fire = 3,
            Ice = 4,
            Water = 5,
            Nature = 6,
            Wind = 7,
            Thunder = 8,
            Light = 9,
            Dark = 10,

            Heal = 11
        }

        public static Color MissColor = new Color(1.0f, 0.6f, 0.5f);
        public static Color HitColor = new Color(1.0f, 0.7f, 0.6f);
        public static Color CritColor = new Color(1.0f, 0.88f, 1.0f);
        public static Color HealColor = new Color(0.6f, 1.0f, 0.4f);
        public static Color BuffColor = new Color(0.7f, 0.9f, 1.0f);

        public enum RefType
        {
            Constant = 0,
            Variable = 1,
            Function = 2
        }

        [Flags]
        public enum DamageHealFlags
        {
            // 间接效果（否则为直接效果；直接效果通常可以触发各种特效）
            Indirect = 1 << 1,
            
            // 随时间发动的效果
            OvertimeEffect = 1 << 2,
            
            // 反击效果
            Counter = 1 << 3,
            
            // 追击效果
            FollowUp = 1 << 4,
        }

        public static bool IsDirect(DamageHealFlags flags) => (flags & DamageHealFlags.Indirect) == 0;

        [Flags]
        public enum ActionFlags
        {
            // 无法被打断
            CannotBeInterrupted = 1 << 1,
            
            // 武器的普通攻击
            RegularAction = 1 << 2,
            
            // 武器的特殊攻击
            SpecialAction = 1 << 3,
            
            // 团队技能
            TeamAction = 1 << 4,
            
            // 移动（不算作一般行动）
            Movement = 1 << 5,
        }
        
        [Flags]
        // TODO: Buff dispel types?
        public enum BuffFlags
        {
            // 无法被驱散
            CannotBeDispelled = 1 << 1,
        }

        public class DamageHeal_FrontEndInput
        {
            public MobData source;
            public float value;
            public Elements type;

            public float crit; // crit chance
            public float hit; // Hit chance

            public RuntimeAction sourceAction;
            public Buff.Buff sourceBuff;

            public DamageHealFlags flags;

            public bool IsAction => sourceAction != null;
            // public int Id => IsAction ? sourceAction.data.Id : sourceBuff.data.Id;
            public string Name => IsAction ? sourceAction.ActionName : sourceBuff.data.name;
            // public string Description => IsAction ? sourceAction.data.Description : "No description";

            public bool popup;
        }

        // public class DamageHeal_FrontEndInput_ByRef
        // {
        //     public DamageHeal_FrontEndInput value;
        //
        //     public DamageHeal_FrontEndInput_ByRef(DamageHeal_FrontEndInput val)
        //     {
        //         value = val;
        //     }
        // }

        public static bool IsHeal(DamageHeal_FrontEndInput input)
        {
            return input.type == Elements.Heal;
        }

        public class DamageHeal_ComputedRates
        {
            public int value;
            public float hit, crit;
        }
        
        // public class DamageHeal_ComputedRates_ByRef
        // {
        //     public DamageHeal_ComputedRates value;
        //
        //     public DamageHeal_ComputedRates_ByRef(DamageHeal_ComputedRates val)
        //     {
        //         value = val;
        //     }
        // }

        public class DamageHeal_Result
        {
            public MobData source;
            public MobData target;

            public int value;
            public int overdeal;
            public Elements type;

            public bool isCrit;
            public bool isAvoid;
            public bool isBlock;

            public RuntimeAction sourceAction;
            public Buff.Buff sourceBuff;
            
            public DamageHealFlags flags;

            public bool IsAction => sourceAction != null;
            public bool NoInfo => sourceAction == null && sourceBuff == null;
            // public int Id => NoInfo ? -1 : (IsAction ? sourceAction.data.Id : sourceBuff.data.Id);
            public string Name => NoInfo ? "<NULL ACTION>" : (IsAction ? sourceAction.data.ActionName : sourceBuff.data.name);
            // public string Description => NoInfo ? "<NULL ACTION>" : (IsAction ? sourceAction.GetTooltip(source) : "No description");

            public bool popup;
        }

        public static float HealAggroMul = 1.3f;
        
        // This is a multiplier that will be applied to aggro per turn (update).
        public static float AggroDecay = 0.75f;

        public enum BuffEventType
        {
            Attached,
            Stacked,
            Refreshed,
            Removed
        }

        public struct BuffEvents
        {
            public Buff.Buff buff;
            public BuffEventType eventType;
        }

        public struct KillEvent
        {
            public Consts.DamageHeal_Result info;
        }
        
        public struct TrackerActionEvent
        {
            public RuntimeAction action;
            public SpellTarget target;
        }

        // ......
        [Serializable]
        public struct AllTypes<T>
        {
            public T Physical;
            public T Elemental;
            public T Pure;

            public T Slash;
            public T Knock;
            public T Pierce;

            public T Fire;
            public T Ice;
            public T Water;
            public T Nature;
            public T Wind;
            public T Thunder;
            public T Light;
            public T Dark;

            public T Heal;

            // public T Get(AllElements e)
            // {
            //     switch(e)
            //     {
            //         case AllElements.Physical:
            //             return Physical;
            //         case AllElements.Elemental:
            //             return Elemental;
            //         case AllElements.Pure:
            //             return Pure;
            //
            //         case AllElements.Slash:
            //             return Slash;
            //         case AllElements.Knock:
            //             return Knock;
            //         case AllElements.Pierce:
            //             return Pierce;
            //
            //         case AllElements.Fire:
            //             return Fire;
            //         case AllElements.Ice:
            //             return Ice;
            //         case AllElements.Water:
            //             return Water;
            //         case AllElements.Nature:
            //             return Nature;
            //         case AllElements.Wind:
            //             return Wind;
            //         case AllElements.Thunder:
            //             return Thunder;
            //         case AllElements.Light:
            //             return Light;
            //         case AllElements.Dark:
            //             return Dark;
            //
            //         case AllElements.Heal:
            //             return Heal;
            //
            //         default:
            //             return default;
            //     }
            // }
            //
            // public T Get(Elements e) => Get((AllElements)e);
        }

        public static AllElements parentType(AllElements e)
        {
            switch(e)
            {
                case AllElements.Slash:
                case AllElements.Knock:
                case AllElements.Pierce:
                    return AllElements.Physical;
                    break;

                case AllElements.Fire:
                case AllElements.Ice:
                case AllElements.Water:
                case AllElements.Nature:
                case AllElements.Wind:
                case AllElements.Thunder:
                case AllElements.Light:
                case AllElements.Dark:
                    return AllElements.Elemental;
                    break;

                case AllElements.Physical:
                case AllElements.Elemental:
                case AllElements.Pure:
                default:
                    return AllElements.Pure;
                    break;
            }
        }

        public static AllElements parentType(Elements e)
            => parentType((AllElements)e);

        [Serializable]
        public struct BaseStatsInt
        {
            public int VIT, STR, MAG, INT;
            [FormerlySerializedAs("DEX")] public int AGI;
            public int TEC;
        }
        
        [Serializable]
        public struct BaseStatsGrowth
        {
            public float VIT, STR, MAG, INT;
            [FormerlySerializedAs("DEX")] public float AGI;
            public float TEC;
        }

        [Serializable]
        public struct BaseStats
        {
            public dNumber VIT, STR, MAG, INT;
            [FormerlySerializedAs("DEX")] public dNumber AGI;
            public dNumber TEC;
        }

        // TODO: Determine our values

        public static int maxPlayers = 9, basePlayerPerTurn = 4;

        // Extra level added to attacker level during damage calc.
        // For a smoother early-game experience.
        // However, this may not be a good solution and should be 0 for now.
        // Instead, characters will have significant non-zero stats at Lv1.
        public static int additionalAttackerLevels = 0;

        public static float GetIdenticalDefense(int referenceLevel)
        {
            return referenceLevel + additionalAttackerLevels;
        }
        
        // Incremental in the range of +50% hit rate per level.
        // Hit rate = BaseHit + (attacker.Hit - defender.Dodge) / (HitRangePerLevel * defender.Level)
        public static float HitRangePerLevel = 6;
        public static float BaseHit = 0.70f;
        public static float MaxHitAcc = 1000000.0f;
        
        // Same but for critical strikes.
        public static float CritRangePerLevel = 7;
        public static float BaseCrit = -0.1f;

        public static float APRegenPerDEX = 0.05f;
        public static float baseAPRegenTurn = 1.0f;
        public static int freeAP = 1, maximumNonFreeAP = 5;
        public static float baseAPRegenRecoveryStage = 0.0f;

        public static float GetHitRate(float spellHit, float defenderDodge, int defenderLevel)
        {
            return Mathf.Clamp(BaseHit + (spellHit - defenderDodge) / (HitRangePerLevel * defenderLevel), 0, 1);
        }

        public static float GetCriticalRate(float spellCrit, float defenderAntiCrit, int defenderLevel)
        {
            return Mathf.Clamp(BaseCrit + (spellCrit - defenderAntiCrit) / (CritRangePerLevel * defenderLevel), 0, 1);
        }

        public static int GetDamage(float spellPower, int attackerLevel, float defense, int defenderLevel)
        {
            return Mathf.CeilToInt(spellPower * GetIdenticalDefense(attackerLevel) / (defense + Consts.GetIdenticalDefense(defenderLevel)));
        }
        
        public static float GetDefenseRate(float defense, int defenderLevel)
        {
            return GetIdenticalDefense(defenderLevel) / (defense + GetIdenticalDefense(defenderLevel));
        }
        
        [Serializable]
        public struct BattleStats
        {
            public AllTypes<float> exResist;
            public AllTypes<float> exDamage;
        }
        
        // Maximum levels for skills, buffs, etc
        public static int MaxListenerLevels = 5;
        
        // Standard values at iLvl 10
        private static float SVbaseStats = 10;
        private static float SVpowers = 6.0f;
        private static float SVdefense = 0.3f;
        private static float SVhit = 1.0f;
        
        public static float ValueFromItemLevel(int iLvl, StatModifierSO.StatModTarget entryKey, float val)
        {
            float normalizedItemLV = (float)iLvl / 10.0f;

            switch (entryKey)
            {
                // Main stats
                case StatModifierSO.StatModTarget.VIT:
                case StatModifierSO.StatModTarget.STR:
                case StatModifierSO.StatModTarget.MAG:
                case StatModifierSO.StatModTarget.INT:
                case StatModifierSO.StatModTarget.DEX:
                case StatModifierSO.StatModTarget.TEC:
                    return SVbaseStats * normalizedItemLV * val;
                    break;

                // Sub stats
                // TODO: Add cases for sub stats

                // Battle stats
                case StatModifierSO.StatModTarget.AttackPower:
                case StatModifierSO.StatModTarget.SpellPower:
                case StatModifierSO.StatModTarget.HealPower:
                case StatModifierSO.StatModTarget.BuffPower:
                    return SVpowers * normalizedItemLV * val;
                    break;
                
                case StatModifierSO.StatModTarget.Defense:
                case StatModifierSO.StatModTarget.SpDefense:
                    return SVdefense * normalizedItemLV * val;
                    break;
                
                case StatModifierSO.StatModTarget.AggroMul:
                    break;
                case StatModifierSO.StatModTarget.Hit:
                    return SVhit * normalizedItemLV * val;
                    break;
                case StatModifierSO.StatModTarget.Dodge:
                    break;
                case StatModifierSO.StatModTarget.Crit:
                    break;
                case StatModifierSO.StatModTarget.CritRes:
                    break;
                case StatModifierSO.StatModTarget.ExRange:
                    break;
                case StatModifierSO.StatModTarget.APRegen:
                    break;

                default:
                    // Handle any other cases that are not explicitly listed
                    break;
            }

            return 0f;
        }

        public static float baseStatBaseLv1 = 5;
        public static float baseStatAverageGrowth = 1.0f;

        public static float BaseStatsFromLevel(int lvl, float growthRate)
        {
            return lvl * growthRate + baseStatBaseLv1;
        }

        public static float GetHealth(int lvl, float VIT)
        {
            return lvl * 2 + VIT * 6;
        }

        public static float HealerSelfFocusThresholdHPPercentage = 0.4f;
        public static float HealerSelfFocusPriorityBoost = 1.5f;
        
        public static float GetPrioritizedHealthRatio(MobData source, MobData mob)
        {
            float healP = mob.healPriority;
            if (healP == 0) healP = 1;
            
            if (mob == source && mob.health < mob.maxHealth * Consts.HealerSelfFocusThresholdHPPercentage)
            {
                healP *= Consts.HealerSelfFocusPriorityBoost;
            }

            return (
                (float)mob.health / ((float)mob.maxHealth * healP) // % of max HP, while focusing on self more when in danger
                - 0.01f * mob.healPriority); // Focus on high priority targets when at full HP (or rarely, same %)
        }

        public static float GetPrioritizedHealthRatio(MobData mob) => GetPrioritizedHealthRatio(null, mob);
        
        public enum UnitGroup
        {
            Player = 0, // Originally players, nothing else allowed
            Enemy = 1, // Enemies
            Ally = 2, // Player's summoned creatures, NPCs etc.
                      // May have agents, in that case they will move as their will instead (NPC)
            Others = 3,
        }

        public static int[] UnitGroupToMaskBit = new int[4] { 1, 2, 4, 8 };

        [Flags]
        enum UnitGroupMaskBit
        {
            Player = 1,
            Enemy = 2,
            Ally = 4,
            Others = 8,
        }

        public static int[] Enemies = new int[4]
        {
        (int)UnitGroupMaskBit.Enemy | (int)UnitGroupMaskBit.Others,
        (int)UnitGroupMaskBit.Player | (int)UnitGroupMaskBit.Ally | (int)UnitGroupMaskBit.Others,
        (int)UnitGroupMaskBit.Enemy | (int)UnitGroupMaskBit.Others,
        (int)UnitGroupMaskBit.Player | (int)UnitGroupMaskBit.Enemy | (int)UnitGroupMaskBit.Ally
        };

        public static int[] Allies = new int[4]
        {
        (int)UnitGroupMaskBit.Player | (int)UnitGroupMaskBit.Ally,
        (int)UnitGroupMaskBit.Enemy,
        (int)UnitGroupMaskBit.Ally | (int)UnitGroupMaskBit.Player,
        (int)UnitGroupMaskBit.Others
        };

        public static int EnemyMask(UnitGroup group)
        {
            return Enemies[(int)group];
        }

        public static int AllyMask(UnitGroup group)
        {
            return Allies[(int)group];
        }

        public static bool ApplyMask(int mask, UnitGroup group)
        {
            return (mask & UnitGroupToMaskBit[(int)group]) > 0;
        }

        public static int Distance(Vector3 _a, Vector3 _b)
        {
            var a = Vector3Int.FloorToInt(_a);
            var b = Vector3Int.FloorToInt(_b);
            return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z));
        }

        public static int RangedActionDistance(Vector3 _src, Vector3 _dst)
        {
            // Attacking lower places is easier
            float highlandBonus = _src.y - _dst.y;
            if (highlandBonus > 0)
            {
                _dst.y += Mathf.Min(6.0f, highlandBonus);
            }
            
            var rawDistance = Distance(_src, _dst);
            rawDistance -= Mathf.Min(2, Mathf.CeilToInt(highlandBonus));

            return rawDistance;
        }
        
        public static bool IsPointWithinCollider(Collider collider, Vector3 point)
        {
            return (collider.ClosestPoint(point) - point).sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon;
        }
    }
    
    public static class IEnumeratorExtensions
    {
        public static IEnumerable<T> ToIEnumerable<T>(this IEnumerator<T> enumerator)
        {
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
    }

    public partial class Databackend
    {
        static private Databackend instance;
        // Called by an isolated encounter before its scene units initialize.
        public static void ResetForNewCombat() { instance = new Databackend(); }
        static public Databackend GetSingleton()
        {
            if (instance == null)
            {
                instance = new Databackend();
            }
            return instance;
        }
        
        // New Map System
        private MapSystem mapSystem;
        
        [OdinSerialize]
        public HashSet<MobData> allMobs { get; private set; } = new HashSet<MobData>();

        [OdinSerialize]
        // public Dictionary<GridEffect, List<Vector3Int>> allGridEffects { get; private set; } = new(); // Fx -> Fx location
        public HashSet<GridEffect> allGridEffects = new();

        public MobEvent<MobData.MobArgumentDelegate> onMobAdded = new();
        public MobEvent<MobData.MobArgumentDelegate> onMobRemoved = new();
        public MobEvent<MobData.MobActionWithTargetCoroutineDelegate> onGlobalActionPostcast = new();

        private Databackend()
        {
            // Initialize new map system (but don't load chunks yet)
            mapSystem = new MapSystem();
            
            // Chunk loading now happens in CombatSchedulerCoroutine.Combat()
            // via InitializeMapAsync with addressables
        }

        public IEnumerator Initialize()
        {
            yield return new JumpIn(InitializeMap());
        }
        
        private IEnumerator InitializeMap()
        {
            Utils.SceneConfig config = UnityEngine.Object.FindFirstObjectByType<Utils.SceneConfig>();
            string mapName = config?.mapName ?? "default";
            Vector3 playerStartPos = config?.playerStartPosition ?? Vector3.zero;
            
            yield return new JumpIn(mapSystem.InitializeMapAsync(mapName, playerStartPos));
        }

        public GridData GetMap(Vector3 backendPos)
        {
            var gridPos = BackendToGridPos(backendPos);
            return GetMap(gridPos, true);
        }

        public GridData GetMap(int x, int y, int z, bool queryMob = true)
        {
            return GetMap(new Vector3Int(x, y, z), queryMob);
        }

        public GridData GetMap(Vector3Int pos, bool queryMob = true)
        {
            // Use new map system
            var backendPos = new Vector3(pos.x, pos.y, pos.z);
            var gridData = mapSystem.GetMap(backendPos);
            
            if (gridData == null) return null;

            if (queryMob)
            {
                PointCollider p = new();
                p.Position = backendPos;
                gridData.mob = allMobs.FirstOrDefault(m => p.Overlaps(m.Collider));
            }
            else
            {
                gridData.mob = null;
            }

            return gridData;
        }

        public GridData GetMap(Vector3Int pos) => GetMap(pos, true);
        
        private IEnumerator GlobalActionPostcast(MobData mob, RuntimeAction action, Spells.SpellTarget target)
        {
            yield return new JumpIn(onGlobalActionPostcast?.InvokeCoroutine(mob, action, target));
            foreach (var state in allStates)
            {
                (state as IRenderableState)?.UpdateRenderer();
            }
            
            Globals.ui.Instance.OnGlobalActionPostcast(mob, action, target);
        }

        private void AddMob(MobData mob)
        {
            allMobs.Add(mob);
            mob.AddedToWorld(this);
            mob.OnActionPostcast.AddListener(GlobalActionPostcast);
            onMobAdded?.InvokeInstant(mob);
            
            TryAddMobToPlayer(mob);
        }

        private void TryAddMobToPlayer(MobData mob)
        {
            // Register to indicator circle
            // TODO: Remove me as I am for early dev versions only
            if (mob.unitGroup == Consts.UnitGroup.Player &&
                char.IsDigit(mob.nickname[0]))
            {
                int n = mob.nickname[0] - '0'; 
                Globals.ui.Instance.circles.RegisterPlayer(n - 1, mob); 
            }
        }

        private void RemoveMob(MobData mob)
        {
            mob.OnActionPostcast.RemoveListener(GlobalActionPostcast);
            allMobs.Remove(mob);
            onMobRemoved?.InvokeInstant(mob);
            
            mob.RemovedFromWorld(this);
        }

        public void SetMob(MobData mob)
        {
            if (!allMobs.Contains(mob))
            {
                AddMob(mob);
            }
        }

        public void ClearMob(MobData mob, bool remove = true)
        {
            if (remove)
            {
                RemoveMob(mob);
            }
        }

        public void AddFx(GridEffect fx)
        {
            fx.Register();
            allGridEffects.Add(fx);

            allMobs
                .Where(m => m.Collider.Overlaps(fx.Collider))
                .ForEach(fx.OnEnterCollider);
        }

        public void RemoveFx(GridEffect fx)
        {
            allMobs
                .Where(m => m.Collider.Overlaps(fx.Collider))
                .ForEach(fx.OnExitCollider);
            
            allGridEffects.Remove(fx);
        }

        // Temp array for MoveMob
        Dictionary<GridEffect, bool> gridEffectChanges = new();
        public void MoveMob(Vector3 from, Vector3 to, MobData mob)
        {
            if (mob.initialized == false)
            {
                mob.Collider.Position = to;
                return;
            }
            
            gridEffectChanges.Clear();

            allGridEffects
                .Where(fx => fx.Collider.Overlaps(mob.Collider))
                .ForEach(fx => gridEffectChanges.TryAdd(fx, false));

            mob.Collider.Position = to;
            
            allGridEffects
                .Where(fx => fx.Collider.Overlaps(mob.Collider))
                .ForEach(fx =>
                {
                    if (!gridEffectChanges.TryAdd(fx, true))
                    {
                        gridEffectChanges.Remove(fx);
                    }
                });
            
            foreach (var fx in gridEffectChanges)
            {
                // Add
                if(fx.Value == true)
                {
                    fx.Key.OnEnterCollider(mob);
                    mob.OnEnterCollider(fx.Key);
                }
                // Remove
                else
                {
                    fx.Key.OnExitCollider(mob);
                    mob.OnExitCollider(fx.Key);
                }
            }
        }

        // TODO: Modify me when implementing new renderer !!
        public static Vector3Int BackendToGridPos(Vector3 backendPos)
        {
            // return new Vector3Int(Mathf.FloorToInt(pos.x), 0, Mathf.FloorToInt(pos.y));
            return new Vector3Int(Mathf.FloorToInt(backendPos.x), Mathf.FloorToInt(backendPos.y), Mathf.FloorToInt(backendPos.z));
        }

        public Vector3 RenderToBackendPos(Vector3 renderPos)
        {
            return renderPos;
        }

        public Vector3Int RenderToGridPos(Vector3 renderPos) => BackendToGridPos(RenderToBackendPos(renderPos));
        
        public Vector3 BackendPosReflooring(Vector3 backendPos)
            => GridToBackendFloorPos(BackendToGridPos(backendPos));
        
        public Vector3 WorldToBackendFlooredPos(Vector3 worldPos)
            => BackendPosReflooring(RenderToBackendPos(worldPos));

        public Vector3 GridToBackendFloorPos(Vector3Int gridPos)
        {
            // Check if this grid position has intrusion data that affects the floor height
            var mapSystem = GetMapSystem();
            if (mapSystem != null)
            {
                Vector3Int chunkCoord = MapSystem.WorldToChunkCoordinate(gridPos);
                Vector3Int localCoord = MapSystem.WorldToLocalChunkCoordinate(gridPos);
                
                var chunk = mapSystem.GetLoadedChunk(chunkCoord);
                if (chunk != null && chunk.GetIsStandable(localCoord.x, localCoord.y, localCoord.z))
                {
                    int intrusionData = chunk.GetBlockIntrude(localCoord.x, localCoord.y, localCoord.z);
                    if (intrusionData > 0)
                    {
                        // Get Y+ intrusion level to adjust floor height
                        int yPlusIntrusion = IntrusionBits.GetFaceIntrusion(intrusionData, Vector3Int.up);
                        
                        // Each intrusion level is 1/8th of a unit
                        float intrusionOffset = yPlusIntrusion * (1.0f / 8.0f);
                        
                        // Floor position is reduced by Y+ intrusion (block is shorter)
                        return new Vector3(gridPos.x, gridPos.y + 1.0f - intrusionOffset, gridPos.z);
                    }
                }
            }
            
            // Default: full block height
            return gridPos;
        }

        // TODO: Modify me when implementing new renderer !!
        public Vector3 BackendToRenderPos(Vector3 backendPos)
        {
            // return newVector3(gridPos.x, gridPos.z, 0) * 1.0f;
            return new Vector3(backendPos.x, backendPos.y, backendPos.z) * 1.0f;
        }

        public Vector3 BackendToRenderPosCentered(Vector3 backendPos)
        {
            return BackendToRenderPos(backendPos) + new Vector3(0.5f, 0.5f, 0.5f);
        }

        public Vector3 BackendToRenderPosCenteredGrounded(Vector3 backendPos)
        {
            return BackendToRenderPos(backendPos) + new Vector3(0.5f, 0.0f, 0.5f);
        }

        // TODO: Map border
        public Consts.Direction GetDominantDirection(Vector3Int from, Vector3Int to)
        {
            Vector3Int diff = to - from;

            if(Mathf.Abs(diff.z) >= Mathf.Abs(diff.x) && diff.z >= 0)
            {
                return Consts.Direction.Up;
            }

            if (Mathf.Abs(diff.z) >= Mathf.Abs(diff.x) && diff.z < 0)
            {
                return Consts.Direction.Down;
            }

            if (Mathf.Abs(diff.z) <= Mathf.Abs(diff.x) && diff.x >= 0)
            {
                return Consts.Direction.Right;
            }

            if (Mathf.Abs(diff.z) <= Mathf.Abs(diff.x) && diff.x < 0)
            {
                return Consts.Direction.Left;
            }

            return Consts.Direction.Up;
        }

        public IEnumerator DealDmgHeal(MobData target, Consts.DamageHeal_FrontEndInput input)
        {
            Consts.DamageHeal_Result result = new Consts.DamageHeal_Result();
            yield return new JumpIn(target.ReceiveDamage(input, result));

            //return true;
        }

        public bool InMap(Vector3Int pos)
        {
            return true;
        }

        public delegate bool IsGridValidFunc(Vector3Int pos, GridData data);
        public delegate bool IsMobValidFunc(MobData mob);

        // TODO: FIXME
        public HashSet<Vector3Int> GetGridsWithMob(IsMobValidFunc mobFilter, IsGridValidFunc gridFilter)
        {
            HashSet<Vector3Int> result = new HashSet<Vector3Int>();

            foreach (var mob in allMobs)
            {
                if (mobFilter == null || mobFilter(mob))
                {
                    foreach (var grid in mob.Collider)
                    {
                        GridData data = GetMap(grid, false);
                        data.mob = mob;

                        if (gridFilter == null || gridFilter(grid, data))
                        {
                            result.Add(grid);
                        }
                    }
                }
            }

            return result;
        }

        public List<Vector3> GetColliderMapIntersect(IEnumerable<Vector3Int> grids)
        {
            return grids
                .Where(x =>
                {
                    var g = GetMap(x, false);
                    var gbelow = GetMap(x + Vector3Int.down, false);
                    var showHere = g.standable && g.passable;
                    var showBelow = g.passable && (gbelow.standable || gbelow.solid) &&
                                    IntrusionBits.GetFaceIntrusion(gbelow.intrusion, Vector3Int.up) == 0;
                    
                    return showHere || showBelow;
                })
                .Select(GridToBackendFloorPos).ToList();
        }

        public Vector3 GetColliderMapIntersect(Vector3Int grid)
        {
            return GridToBackendFloorPos(grid);
        }

        public struct GridBFSKeys : IComparable
        {
            public Vector3Int position;
            public float distance;

            public int CompareTo(object obj)
            {
                return Math.Sign(distance - ((GridBFSKeys)obj).distance);
            }

            public GridBFSKeys(Vector3Int p, float d)
            {
                this.position = p;
                this.distance = d;
            }
        }

        private static Consts.Direction[] possibleDirections = new[]
        {
            Consts.Direction.Up,
            Consts.Direction.Left,
            Consts.Direction.Down,
            Consts.Direction.Right,
        };

        public GridPath FindPathTo(GridCollider origin, Vector3Int to, Movement movement, int maxDistance = -1)
        {
            if (GenericMovementBFS(
                    origin, movement, key => key.position == to, maxDistance,
                    out var gridInfo))
            {
                // Found a path
                return ReconstructPath(x => gridInfo[x].prevGrid, to);
            }

            return null;
        }

        public static GridPath ReconstructPath(Func<Vector3Int, Vector3Int> getPrevGrid, Vector3Int destination)
        {
            GridPath path = new GridPath();
            path.path = new List<Vector3Int>();

            Vector3Int pathCurr = destination;
            while(getPrevGrid(pathCurr) != pathCurr)
            {
                path.path.Add(pathCurr);
                pathCurr = getPrevGrid(pathCurr);
            }

            path.path.Reverse();
            return path;
        }

        public delegate bool MovementTerminationCondition(GridBFSKeys key);

        public bool GenericMovementBFS(
            GridCollider origin,
            Movement movement,
            MovementTerminationCondition termCond,
            int maxDistance,
            out Dictionary<Vector3Int, (Vector3Int prevGrid, float distance)> gridInfo)
        {
            Vector3Int from = BackendToGridPos(origin.Position);
            
            // TODO: Dont new them everytime
            C5.IntervalHeap<GridBFSKeys> searchedGrids = new C5.IntervalHeap<GridBFSKeys>();
            gridInfo = new();

            searchedGrids.Add(new GridBFSKeys(from, 0));
            gridInfo.Add(from, (from, 0));

            while(!searchedGrids.IsEmpty)
            {
                var curr = searchedGrids.DeleteMin();

                // Reconstruct a collider
                var coll = origin.ShallowClone();
                coll.Position = GridToBackendFloorPos(curr.position);
                
                // Ask the movement action where could we go
                var targetGrids = movement.ProposeMovementGrids(coll, curr);
                        
                foreach (var newKey in targetGrids)
                {
                    var newPos = newKey.position;
                    if(!gridInfo.ContainsKey(newPos))
                    {
                        // Get cost of grid
                        // TODO: IsMoveable might get stuck with >1x1 gridBodies

                        if (InMap(newPos))
                        {
                            if (termCond(newKey))
                            {
                                searchedGrids.Add(newKey);
                                gridInfo.Add(newPos, (curr.position, newKey.distance));
                                return true;
                            }
                            else if(newKey.distance <= maxDistance)
                            {
                                gridInfo.Add(newPos, (curr.position, newKey.distance));

                                if (newKey.distance < maxDistance)
                                {
                                    searchedGrids.Add(newKey);
                                }
                            }
                        } 
                    }
                }
            }

            return false;
        }

        public bool IsPathValid(MobData mob, GridPath path)
        {
            // TODO: detailed check

            return path.path.Count <= mob.actionPoints;
        }

        // TODO: Optimize this by caching results.
        // Use a non-serialized version number system on IGridColliders to cache valid results.
        public bool CanPositionPlaceMob(Vector3 position, GridCollider body)
        {
            var newbody = body.ShallowClone();
            newbody.Position = position;
            
            return CanPositionPlaceMob(newbody);
        }
        
        // TODO: Add support for maps
        public bool CanPositionPlaceMob(GridCollider body)
        {
            var result = allMobs.All(x => (x.Collider == body || !(x.Collider.Overlaps(body))));
            return result;
        }

        public Vector3Int? FindNearestEmptyGrid(Vector3Int center, Func<Vector3Int, bool> isGridValid = null) 
            => FindNearestEmptyGrid(center, new PointCollider(), isGridValid);

        // TODO: Convert this to 3D
        public Vector3Int? FindNearestEmptyGrid(Vector3Int center, GridCollider body, Func<Vector3Int, bool> isGridValid = null)
        {
            if (!InMap(center)) { return null; }
            
            if (CanPositionPlaceMob(GridToBackendFloorPos(center), body) && (isGridValid?.Invoke(center) ?? true))
            {
                return center;
            }

            int start = UnityEngine.Random.Range(0, 4);

            for (int distance = 1; distance < 5; distance++)
            {
                int x = 0;
                int y = distance - x;

                for(int dirc = 0; dirc < 4; dirc += 1)
                {
                    for(int p = 0; p < distance; p++)
                    {
                        // Compute x, y
                        switch((dirc + start) % 4)
                        {
                            case 0:
                                x = p;
                                y = distance - p;
                                break;
                            case 1:
                                x = distance - p;
                                y = -p;
                                break;
                            case 2:
                                x = -p;
                                y = p - distance;
                                break;
                            case 3:
                                x = p - distance;
                                y = p;
                                break;
                            default:
                                x = 0;
                                y = 0;
                                break;
                        }

                        Vector3Int pos = center + new Vector3Int(x, y);
                        if (InMap(pos) && CanPositionPlaceMob(GridToBackendFloorPos(pos), body) && (isGridValid?.Invoke(pos) ?? true))
                        {
                            return pos;
                        }
                    }
                }
            }

            return null;
        }

        public List<MobData> GetAllMobs()
        {
            return allMobs.ToList();
        }

        // Expose MapSystem for editor access
        public MapSystem GetMapSystem()
        {
            return mapSystem;
        }
        
        public bool HasLineOfSight(Vector3 from, Vector3 to, MapSystem.RaycastTarget target = MapSystem.RaycastTarget.Solid)
            => HasLineOfSight(from, to, out var _, target);
        
        public bool HasLineOfSight(
            Vector3 from, 
            Vector3 to, 
            out (Vector3Int hitPos, Vector3Int faceNormal)? hitInfo,
            MapSystem.RaycastTarget target = MapSystem.RaycastTarget.Solid)
        {
            Vector3 diff = (to - from);
            const float eps = 0.01f;
            
            Ray ray = new Ray();
            ray.origin = from;
            ray.direction = diff.normalized;
            float dist = diff.magnitude - eps;

            hitInfo = mapSystem.DDAGridRaycast(ray, target, dist);
            return !hitInfo.HasValue;
        }

        public void AimOnTarget(MobData targetMob)
        {
            // TODO: Enemy?
            var mobs = Globals.backend.allMobs.Where(x => x.unitGroup == Consts.UnitGroup.Player);
            foreach (MobData mob in mobs)
            {
                RuntimeAction<SingleMobTarget> ratk = mob.mainWeapon?.GetRegularAttackSpell() as RuntimeAction<SingleMobTarget>;
                if (ratk?.data != null)
                {
                    SingleMobTarget validTarget = ActionHelpers.ValidTargetFinder.FindValidSingleMobTarget(mob, targetMob, ratk);
                    if (validTarget != null)
                    {
                        ratk._SetLastTarget(validTarget);
                    }
                }
            }
        }
    }
}
