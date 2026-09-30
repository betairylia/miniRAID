using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
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
        public string Telegraph {get;private set;}="Preparing lesson";
        public int PillarsBroken {get;private set;}
        public int Intercepts {get;private set;}
        public int RoarsInterrupted {get;private set;}
        public int RoarsReleased {get;private set;}
        public int SweepHits {get;private set;}
        public int ChargeHits {get;private set;}
        public int DamageToInterrupt=>Mathf.Max(0,roarThreshold-roarDamage);
        public string Mechanic=>stunned?"stunned":phase==0?"sweep":phase==1?"charge":"roar";
        public Vector3Int[] Danger=>danger.ToArray();
        public string LockedTarget=>chargeTarget?.nickname;
        MobData wolf;
        MobData[] party,pillars;
        int phase,roarDamage,stunTurns;
        bool initialized,stunned,chargingRoar,interrupted,restarting;
        MobData chargeTarget;
        List<Vector3Int> chargePath=new();
        HashSet<Vector3Int> danger=new();
        GridColliderIndicator indicator;
        VisualElement lessonPanel;
        Label lessonText;
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
            Databackend.ResetForNewCombat();
            Globals.combatTracker = new CombatTracker();
        }
        void Initialize()
        {
            if(initialized)return;
            wolf=wolfRenderer.data;party=partyRenderers.Select(x=>x.data).ToArray();
            pillars=pillarRenderers.Select(x=>x.data).ToArray();
            wolf.OnDamageReceived.AddListener(OnWolfDamage);
            foreach(var action in new[]{bite,sweep,charge,roar,breakPillar})
                runtimeActions.Add(wolf.AddAction(new ActionSOEntry {data=action,level=0}));
            initialized=true;
        }
        public void Prepare()
        {
            Initialize();CheckOutcome();if(Finished)return;
            ClearIndicator();danger.Clear();stunned=stunTurns>0;
            if(stunned) {stunTurns--;Telegraph="STUNNED — one round to attack or recover";Record(Telegraph);return;}
            var living=party.Where(x=>!x.isDead).ToArray();
            if(phase==0)
            {
                var target=living.OrderBy(x=>(x.GridPosition-wolf.GridPosition).sqrMagnitude).First();
                danger=AlphaWolfGeometry.Sweep(wolf.GridPosition,target.GridPosition);
                Telegraph="SWEEP — fixed purple cells after three player opportunities. Then BITE the nearest ally within 2 cells (any direction)";
            }
            else if(phase==1)
            {
                // Deterministic interpretation of the draft's low-defense / low-HP preference.
                chargeTarget=living.OrderBy(x=>(float)x.defense).ThenBy(x=>(float)x.health).ThenBy(x=>x.nickname).First();
                chargePath=AlphaWolfGeometry.ChargeLine(wolf.GridPosition,chargeTarget.GridPosition);
                danger=chargePath.ToHashSet();
                Telegraph="CHARGE — "+chargeTarget.nickname+"; locked path. A pillar or another ally stops and stuns the wolf";
            }
            else
            {
                if(phase==2){chargingRoar=true;interrupted=false;roarDamage=0;}
                Telegraph=interrupted?"ROAR interrupted — recovery window":$"ROAR — {4-phase} round(s) left; deal {DamageToInterrupt} more damage to interrupt";
            }
            if(danger.Count>0)indicator=new GridColliderIndicator(new EnumerateGridCollider(new GridShape{shape=danger}),GridOverlay.Types.INCOMING_ATTACK);
            Record(Telegraph);
        }
        public IEnumerator Resolve()
        {
            if(!initialized)yield break;
            CheckOutcome();if(Finished)yield break;
            ClearIndicator();
            if(stunned){Record("Stun consumed; no enemy attack");yield break;}
            if(phase==0)
            {
                foreach(var target in party.Where(x=>!x.isDead && danger.Contains(x.GridPosition)).ToArray())
                {SweepHits++;yield return new JumpIn(Hit(sweep,target));}
                Record("Sweep resolved using its locked preview cells");
            }
            else if(phase==1)
            {
                foreach(var cell in chargePath)
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
            else if(phase==3)
            {
                chargingRoar=false;
                if(!interrupted)
                {
                    RoarsReleased++;Record("Roar released: all living party members");
                    foreach(var target in party.Where(x=>!x.isDead).ToArray())yield return new JumpIn(Hit(roar,target));
                }
                else Record("Interrupted roar did not fire");
            }
            // Bite is a separate, visibly documented nearby basic attack on sweep rounds only.
            if(phase==0 && !wolf.isDead)
            {
                var nearby=party.Where(x=>!x.isDead && (x.GridPosition-wolf.GridPosition).sqrMagnitude<=4).OrderBy(x=>(x.GridPosition-wolf.GridPosition).sqrMagnitude).FirstOrDefault();
                if(nearby!=null)yield return new JumpIn(Hit(bite,nearby));
            }
            phase=(phase+1)%4;CheckOutcome();
        }
        IEnumerator Hit(AlphaWolfLessonAction action,MobData target)
        {
            if(wolf.isDead || target.isDead)yield break;
            var runtime=(RuntimeAction<SingleMobTarget>)runtimeActions.First(x=>x.data==action);
            yield return new JumpIn(wolf.DoAction(runtime,new SingleMobTarget(target,target.GridPosition)));
        }
        IEnumerator OnWolfDamage(MobData mob,Consts.DamageHeal_Result result)
        {
            if(chargingRoar && !interrupted && result.source!=mob && result.type!=Consts.Elements.Heal && !result.isAvoid)
            {
                roarDamage+=result.value;
                if(roarDamage>=roarThreshold)
                {interrupted=true;RoarsInterrupted++;Telegraph="ROAR INTERRUPTED — saved the party";Record(Telegraph);}
                else Telegraph=$"ROAR — deal {DamageToInterrupt} more damage to interrupt";
            }
            yield break;
        }
        void Update()
        {
            if(initialized)CheckOutcome();
            if(lessonPanel==null)AttachPanel();
            if(lessonText!=null)lessonText.text=Telegraph+"\nPillars broken "+PillarsBroken+" | Ally blocks "+Intercepts+" | Roars interrupted "+RoarsInterrupted;
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
            var title=new Label("ALPHA WOLF / THREE-PERSON LESSON");title.AddToClassList("alpha-title");lessonPanel.Add(title);
            lessonText=new Label();lessonText.AddToClassList("alpha-text");lessonPanel.Add(lessonText);
            var hint=new Label("Purple: locked attack cells. Pass each actor; if nobody can act, click empty ground then EndTurn. Roar: burst within two rounds. Defeat the wolf to win.");hint.AddToClassList("alpha-hint");lessonPanel.Add(hint);
            restartButton=new Button(Restart){text="Restart lesson"};restartButton.AddToClassList("alpha-restart");lessonPanel.Add(restartButton);
            root.Add(lessonPanel);
        }
        void CheckOutcome()
        {
            if(Finished)return;
            if(wolf.isDead)Outcome="victory";
            else if(party.All(x=>x.isDead))Outcome="defeat";
            if(Finished){chargingRoar=false;ClearIndicator();Telegraph=Outcome.ToUpperInvariant();Record(Telegraph);}
        }
        void ClearIndicator(){indicator?.Destroy();indicator=null;}
        void OnDestroy(){lessonPanel?.RemoveFromHierarchy();ClearIndicator();if(initialized)wolf.OnDamageReceived.RemoveListener(OnWolfDamage);}
        public void Restart()
        {
            if(restarting || !Finished || !Globals.combatMgr.Instance.CombatStopped)return;
            restarting=true;
            SceneManager.LoadScene(entryScene,LoadSceneMode.Single);
        }
    }
}
