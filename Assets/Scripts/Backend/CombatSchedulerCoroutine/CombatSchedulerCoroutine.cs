using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using miniRAID.TurnSchedule;
using UnityEngine;

using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Utils;

namespace miniRAID
{
    [RequireComponent(typeof(SerialCoroutine))]
    public partial class CombatSchedulerCoroutine : MonoBehaviour
    {
        static CombatSchedulerCoroutine mInstance;

        public static CombatSchedulerCoroutine Instance
        {
            get
            {
                return mInstance ? mInstance : (mInstance = (new GameObject("CombatSchedulerCoroutine")).AddComponent<CombatSchedulerCoroutine>());
            }
        }


        [Title("Combat")] 
        public Timestamp now, appendedTurns;

        SerialCoroutine sc;
        ICombatCompletion completion;
        public bool CombatFinished => completion?.Finished ?? false;
        public bool CombatStopped { get; private set; }
        public bool SupportsRewind => completion == null;
        [OdinSerialize] private TurnSlice currentTurnSlice;

        [NonSerialized] public IEnumerator OnBeforeNextTurnSlice;

        [NonSerialized] private float turnWaitTime = 0.0f;

        private void Awake()
        {
            sc = GetComponent<SerialCoroutine>();
        }

        private IEnumerator Start()
        {
            var loader=FindFirstObjectByType<CombatSceneLoader>();
            if(loader!=null)yield return loader.EnsureLoaded();
            completion=FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ICombatCompletion>().FirstOrDefault();
            // Try to read global config
            SceneConfig config = FindFirstObjectByType<SceneConfig>();
            turnWaitTime = config?.turnWaitTimeSec ?? 0.0f;
            
            // This starts the whole combat SerialCoroutine.
            // Changes on default context should not be made here as this will last for entire combat.
            sc.StartSerialCoroutine(Combat(), new SerialCoroutineContext()
            {
                animation = config?.enableAnimation ?? true,
                forceNoWait = config?.forceNoWait ?? false,
                rng = new RNG((uint)(DateTime.Now.GetHashCode() - int.MinValue))
            });
        }

        public void OnNextSnapshot(IEnumerator act)
        {
            if (OnBeforeNextTurnSlice != null)
            {
                Debug.LogError("CombatSchedulerCoroutine is already full of OnBeforeNextTurnSlice! Action Ignored!");
                return;
            }
            OnBeforeNextTurnSlice = act;
        }
        
        public TurnSchedule.LockedPlayerTurnSlice GetCurrentLockedPlayerTurnSlice()
        {
            return currentTurnSlice as TurnSchedule.LockedPlayerTurnSlice;
        }
        
        public IEnumerator Combat()
        {
            // TODO: Move me to the game's beginning scene
            yield return new JumpIn(Globals.localizer.Initialization());
            
            // Initialize map system with addressables
            yield return new JumpIn(Globals.backend.Initialize());
            
            // For debug
            //yield return new JumpIn(Test());

            // Preparing
            yield return new JumpIn(Preparation());

            // Before entering Turn 1
            yield return new JumpIn(StartCombat());
            
            // TODO: FIXME: Remove me from here!
            FindObjectOfType<BGMLoopWithIntro>().Play();

            // Battle main loop
            while (!IsCombatFinished())
            {
                // Save-Load happens only here, perhaps
                // Try load first
                if (OnBeforeNextTurnSlice != null)
                {
                    yield return new JumpIn(OnBeforeNextTurnSlice);
                    OnBeforeNextTurnSlice = null;
                }
                // Save state before turn slice
                if (turnSchedule.First.Value != null &&
                    turnSchedule.First.Value.data.GetType() == typeof(CommonPlayerTurnSliceSO))
                {
                    SaveDataSerializer.saveSlotBackup = SaveDataSerializer.SerializeEverything();
                }
                
                UpdateSchedulerUI();
                
                currentTurnSlice = turnSchedule.Dequeue();
                if (currentTurnSlice.muted == false)
                    yield return new JumpIn(currentTurnSlice.Turn());

                if (turnWaitTime > 0)
                {
                    yield return new JumpIn(Chill());
                }
                
                currentTurnSlice.OnRemove(this);

                KeepTurnScheduleLength();
            }
            CombatStopped = true;
        }

        // TODO: Move me to another place specific for UI
        public void UpdateSchedulerUI(bool includeCurrent = false)
        {
            int length = 12;

            var visible = (includeCurrent && currentTurnSlice != null
                ? new[] { currentTurnSlice }.Concat(turnSchedule) : turnSchedule).Where(x => x.ShowInUI);
            string message = String.Join("\n",
                visible
                    // .Skip(1)
                    .Take(length)
                    .Select(x =>
                        $"<color=#{ColorUtility.ToHtmlStringRGB(x.MainColor)}> {x.Label} </color>")
                    .ToArray());
            
            Globals.ui.Instance.combatView.schedulerPlaceholder.text = message;

            var t = visible.FirstOrDefault();
            Globals.ui.Instance.combatView.currentTurnPlaceholder.text = t == null ? "" : $"<color=#{ColorUtility.ToHtmlStringRGB(t.MainColor)}> {t.Label} </color>";
        }

        private IEnumerator Chill()
        {
            var ctx = Globals.cc;
            var ctxBackup = Globals.cc;
            ctx.forceNoWait = false;
            Globals.ccNewContext(ctx);
            
            yield return new WaitForSeconds(turnWaitTime);
            
            Globals.ccNewContext(ctxBackup);
        }

        private bool HasAlly()
        {
            return false;
        }

        public bool ShouldSkipPlayerPhase()
        {
            // TODO: Controllable allies?
            var mobs = Globals.backend.allMobs.Where(x => x.unitGroup == Consts.UnitGroup.Player);
            return !mobs.Any(m => m.isControllable);
        }

        private bool IsCombatFinished()
        {
            return CombatFinished;
        }

        private IEnumerator Preparation()
        {
            // TODO
            yield break;
        }

        private IEnumerator StartCombat()
        {
            InitializeTurnSchedule();
            
            // TODO
            now.currentTurnID = 0;
            
            yield break;
        }

        // Update is called once per frame
        void Update()
        {
        }

        #region test

        private IEnumerator Test()
        {
            // Test
            Debug.Log("Test1");
            for (int i = 0; i < 1000; i++) { var a = Test2(); yield return new JumpIn(a); }
            Debug.Log("Test2");
            yield return new WaitForSeconds(2.0f);
            Debug.Log("Test3");
            yield return Test3();
            Debug.Log("Test4");
        }

        private IEnumerator Test2()
        {
            if (false)
            {
                yield return null;
            }
        }

        private IEnumerator Test3()
        {
            // Test
            Debug.Log("Test3.1");
            yield return new WaitForSeconds(2.0f);
            Debug.Log("Test3.2");
        }

        #endregion
    }
}
