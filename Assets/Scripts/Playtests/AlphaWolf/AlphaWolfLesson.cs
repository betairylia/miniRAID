using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using miniRAID.Spells;
namespace miniRAID.AlphaWolfPlaytest
{
    [DefaultExecutionOrder(-1000)]
    public class AlphaWolfLesson : MonoBehaviour, ICombatCompletion
    {
        public MobRenderer wolfRenderer;
        public MobRenderer[] partyRenderers, pillarRenderers;
        public AlphaWolfLessonAction bite, sweep, charge, roar, breakPillar;
        public int roarThreshold=70;
        public string entryScene="AlphaWolfPlaytest";
        public string Outcome {get;private set;}="playing";
        public bool Finished=>Outcome!="playing";
        public string Telegraph
        {
            get
            {
                if(!LocalizationSettings.InitializationOperation.IsDone)return string.Empty;
                var locale=LocalizationSettings.SelectedLocale;
                if(telegraphText==null || telegraphLocale!=locale)
                {telegraphLocale=locale;telegraphText=Text(telegraphKey,telegraphArgs);}
                return telegraphText;
            }
        }
        string telegraphKey="preparing",telegraphText;
        object[] telegraphArgs=Array.Empty<object>();
        Locale telegraphLocale,displayLocale;
        bool presentationDirty=true;
        int displayedSegments=-1;
        static string Text(string key,params object[] args)=>
            Globals.localizer.L(new LocalizedString("Actions","alphawolf.lesson.ui-"+key),args);
        void SetTelegraph(string key,params object[] args)
        {telegraphKey=key;telegraphArgs=args;telegraphText=null;presentationDirty=true;}
        public int PillarsBroken {get;private set;}
        public int Intercepts {get;private set;}
        public int RoarsInterrupted {get;private set;}
        public int RoarsReleased {get;private set;}
        public int SweepHits {get;private set;}
        public int ChargeHits {get;private set;}
        public int SegmentsUsed {get;private set;}
        public int DamageToInterrupt=>Mathf.Max(0,roarThreshold-roarDamage);
        public string Mechanic=>stunned?"stunned":phase==0?"sweep":phase==1?"charge":"roar";
        public Vector3Int[] Danger=>danger.ToArray();
        public string LockedTarget=>chargeTarget?.nickname;
        MobData wolf;
        MobData[] party,pillars;
        int phase=-1,roarDamage,stunTurns;
        bool resolved;
        bool initialized,stunned,chargingRoar,interrupted,restarting,chargePending;
        Vector3Int previewOrigin, previewTarget;
        bool previewValid;
        MobData chargeTarget;
        List<Vector3Int> chargePath=new();
        HashSet<Vector3Int> danger=new();
        GridColliderIndicator indicator;
        VisualElement lessonPanel;
        Label lessonText,lessonTitle;
        Button restartButton;
        readonly List<RuntimeAction> runtimeActions=new();
        public readonly List<string> Events=new();
        void Record(string text)
        {
            Events.Add($"R{Globals.combatMgr.Instance.now.currentTurnID}: {text}");
            Debug.Log("[AlphaWolf lesson] "+text);
        }
        void Awake()
        {
            // Legacy merged scenes still initialize here; CombatBase owns additive startup.
            if(FindFirstObjectByType<CombatSceneLoader>()==null)
            {Databackend.ResetForNewCombat();Globals.combatTracker = new CombatTracker();}
        }
        void Initialize()
        {
            if(initialized)return;
            wolf=wolfRenderer.data;party=partyRenderers.Select(x=>x.data).ToArray();
            pillars=pillarRenderers.Select(x=>x.data).ToArray();
            wolf.OnDamageReceived.AddListener(OnWolfDamage);
            foreach(var mob in party.Append(wolf))mob.OnRealDeath.AddListener(OnCombatantDeath);
            foreach(var action in new[]{bite,sweep,charge,roar,breakPillar})
                runtimeActions.Add(wolf.AddAction(new ActionSOEntry {data=action,level=0}));
            initialized=true;
        }
        public void Prepare()
        {
            Initialize();CheckOutcome();if(Finished)return;
            SegmentsUsed=0;resolved=false;interrupted=false;chargingRoar=false;
            chargePending=false;previewValid=false;ClearIndicator();danger.Clear();stunned=stunTurns>0;
            if(stunned) {stunTurns--;SetTelegraph("stunned");Record(Telegraph);return;}
            // Stunned phases consume time, not an ability in the boss sequence.
            phase=(phase+1)%3;
            var living=party.Where(x=>!x.isDead).ToArray();
            if(phase==0)
            {
                var target=living.OrderBy(x=>(x.GridPosition-wolf.GridPosition).sqrMagnitude).First();
                danger=AlphaWolfGeometry.Sweep(wolf.GridPosition,target.GridPosition);
                SetTelegraph("sweep");
            }
            else if(phase==1)
            {
                // Deterministic interpretation of the draft's low-defense / low-HP preference.
                chargeTarget=living.OrderBy(x=>(float)x.defense).ThenBy(x=>(float)x.health).ThenBy(x=>x.nickname).First();
                chargePending=true;
                RefreshChargePreview();
            }
            else
            {
                chargingRoar=true;roarDamage=0;
                SetTelegraph("roar",2-SegmentsUsed,DamageToInterrupt);
            }
            if(danger.Count>0 && indicator==null)indicator=new GridColliderIndicator(new EnumerateGridCollider(new GridShape{shape=danger}),GridOverlay.Types.INCOMING_ATTACK);
            Record(Telegraph);
        }
        public IEnumerator Resolve()
        {
            // Future phases are queued ahead of time, so choose the boundary at execution.
            if(!initialized || stunned || resolved || SegmentsUsed!=(phase==2?2:1))yield break;
            resolved=true;
            CheckOutcome();if(Finished)yield break;
            if(chargePending) RefreshChargePreview();
            chargePending=false;
            ClearIndicator();
            if(phase==0)
            {
                foreach(var target in party.Where(x=>!x.isDead && danger.Contains(x.GridPosition)).ToArray())
                {SweepHits++;yield return new JumpIn(Hit(sweep,target));}
                Record("Sweep resolved using its locked preview cells");
            }
            else if(phase==1)
            {
                if(!ChargeTargetAvailable)
                { Record("Charge cancelled: marked target unavailable; no retarget"); }
                foreach(var cell in ChargeTargetAvailable ? chargePath : new List<Vector3Int>())
                {
                    var grid=Globals.backend.GetMap(cell);
                    if(grid==null || !grid.passable || grid.solid)
                    {stunTurns=1;Record("Charge stopped by terrain; wolf stunned");break;}
                    var victim=grid.mob;
                    if(victim!=null && victim!=wolf && !victim.isDead)
                    {
                        bool pillar=pillars.Contains(victim);
                        yield return new JumpIn(Hit(pillar?breakPillar:charge,victim));
                        if(pillar){PillarsBroken++;stunTurns=1;Record("Pillar shattered; wolf stunned");}
                        else if(victim!=chargeTarget){Intercepts++;stunTurns=1;Record("Ally intercepted charge; wolf stunned");}
                        else {ChargeHits++;Record("Charge hit its marked target");}
                        break;
                    }
                    yield return new JumpIn(wolf.SetPosition(cell));
                }
            }
            else if(phase==2)
            {
                chargingRoar=false;
                if(!interrupted)
                {
                    RoarsReleased++;Record("Roar released: all living party members");
                    foreach(var target in party.Where(x=>!x.isDead).ToArray())yield return new JumpIn(Hit(roar,target));
                }
                else Record("Interrupted roar did not fire");
            }
            danger.Clear();CheckOutcome();
            if(!Finished)SetTelegraph(interrupted?"interrupted":stunTurns>0?"blocked":"resolved");
        }
        public void CompletePlayerSegment()
        {
            SegmentsUsed++;
            if(chargingRoar && !interrupted)
                SetTelegraph("roar",Mathf.Max(0,2-SegmentsUsed),DamageToInterrupt);
        }
        public IEnumerator FinishPhase()
        {
            CheckOutcome();if(Finished)yield break;
            if(stunned || stunTurns>0 || interrupted)
            {Record("Stun suppresses phase-end bite");yield break;}
            var nearby=party.Where(x=>!x.isDead && (x.GridPosition-wolf.GridPosition).sqrMagnitude<=4)
                .OrderBy(x=>(x.GridPosition-wolf.GridPosition).sqrMagnitude).FirstOrDefault();
            if(nearby!=null){Record("Phase-end bite");yield return new JumpIn(Hit(bite,nearby));}
            else Record("Phase-end bite: nobody within 2 cells; no extra movement");
            CheckOutcome();
        }
        bool ChargeTargetAvailable => chargeTarget!=null && !chargeTarget.isDead &&
            Globals.backend.allMobs.Contains(chargeTarget) && chargeTarget.renderer!=null;
        void RefreshChargePreview()
        {
            if(!chargePending)return;
            if(!ChargeTargetAvailable)
            {
                ClearIndicator();danger.Clear();chargePath.Clear();previewValid=false;
                if(telegraphKey!="cancelled")SetTelegraph("cancelled");
                return;
            }
            var origin=wolf.GridPosition;var target=chargeTarget.GridPosition;
            if(previewValid && previewOrigin==origin && previewTarget==target)return;
            previewOrigin=origin;previewTarget=target;previewValid=true;
            chargePath=AlphaWolfGeometry.ChargeLine(origin,target);danger=chargePath.ToHashSet();
            ClearIndicator();
            if(danger.Count>0)indicator=new GridColliderIndicator(new EnumerateGridCollider(new GridShape{shape=danger}),GridOverlay.Types.INCOMING_ATTACK);
            SetTelegraph("charge",chargeTarget.nickname);
        }
        IEnumerator Hit(AlphaWolfLessonAction action,MobData target)
        {
            if(wolf.isDead || target.isDead)yield break;
            var runtime=(RuntimeAction<SingleMobTarget>)runtimeActions.First(x=>x.data==action);
            yield return new JumpIn(wolf.DoAction(runtime,new SingleMobTarget(target,target.GridPosition)));
        }
        IEnumerator OnCombatantDeath(MobData mob,Consts.DamageHeal_Result result)
        {
            CheckOutcome();
            yield break;
        }
        IEnumerator OnWolfDamage(MobData mob,Consts.DamageHeal_Result result)
        {
            if(chargingRoar && !interrupted && result.source!=mob && result.type!=Consts.Elements.Heal && !result.isAvoid)
            {
                roarDamage+=result.value;
                if(roarDamage>=roarThreshold)
                {interrupted=true;stunTurns=1;RoarsInterrupted++;SetTelegraph("interrupted");Record(Telegraph);}
                else SetTelegraph("roar",Mathf.Max(0,2-SegmentsUsed),DamageToInterrupt);
            }
            yield break;
        }
        void Update()
        {
            if(initialized){CheckOutcome();if(!Finished)RefreshChargePreview();}
            if(!initialized || !LocalizationSettings.InitializationOperation.IsDone)return;
            if(lessonPanel==null)AttachPanel();
            if(lessonText!=null && (presentationDirty || displayedSegments!=SegmentsUsed || displayLocale!=LocalizationSettings.SelectedLocale))
            {
                if(displayLocale!=null && displayLocale!=LocalizationSettings.SelectedLocale && !Globals.combatMgr.Instance.CombatStopped)
                    Globals.combatMgr.Instance.UpdateSchedulerUI(includeCurrent:true);
                displayLocale=LocalizationSettings.SelectedLocale;displayedSegments=SegmentsUsed;presentationDirty=false;
                lessonTitle.text=Text("title");
                lessonText.text=Telegraph+(Finished?"":"\n"+Text("segments",SegmentsUsed));
                restartButton.text=Text("restart");
            }
            restartButton?.SetEnabled(Finished && Globals.combatMgr.Instance.CombatStopped);
        }
        void AttachPanel()
        {
            var view=FindFirstObjectByType<miniRAID.UIElements.CombatView>();
            if(view==null)return;
            var root=view.GetComponent<UIDocument>().rootVisualElement;
            if(root==null)return;
            lessonPanel=new VisualElement {name="AlphaWolfLessonPanel",pickingMode=PickingMode.Ignore};
            lessonPanel.AddToClassList("alpha-lesson");
            lessonPanel.styleSheets.Add(Resources.Load<StyleSheet>("UI/AlphaWolfLesson"));
            lessonTitle=new Label();lessonTitle.AddToClassList("alpha-title");lessonPanel.Add(lessonTitle);
            lessonText=new Label {name="EncounterTelegraph"};lessonText.AddToClassList("alpha-text");lessonText.AddToClassList("tactical-text");lessonPanel.Add(lessonText);
            restartButton=new Button(Restart);restartButton.AddToClassList("alpha-restart");lessonPanel.Add(restartButton);
            root.Add(lessonPanel);
        }
        void CheckOutcome()
        {
            if(Finished)return;
            if(wolf.isDead)Outcome="victory";
            else if(party.All(x=>x.isDead))Outcome="defeat";
            if(Finished){chargingRoar=false;ClearIndicator();SetTelegraph(Outcome);Record(Telegraph);}
        }
        void ClearIndicator(){indicator?.Destroy();indicator=null;}
        void OnDestroy(){lessonPanel?.RemoveFromHierarchy();ClearIndicator();if(initialized){wolf.OnDamageReceived.RemoveListener(OnWolfDamage);foreach(var mob in party.Append(wolf))mob.OnRealDeath.RemoveListener(OnCombatantDeath);}}
        public void Restart()
        {
            if(restarting || !Finished || !Globals.combatMgr.Instance.CombatStopped)return;
            restarting=true;
            CombatSceneLoader.NextEncounter=gameObject.scene.path;
            SceneManager.LoadScene("CombatBase",LoadSceneMode.Single);
        }
    }
}
