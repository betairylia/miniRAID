import sys,json,time,uuid,shutil
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from gameplay_harness import call,wait,unity
import argparse
parser=argparse.ArgumentParser(description='Explicit live tactical/provenance regression, includes isolated fault fixtures; stops Play.')
parser.add_argument('--run',action='store_true',required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args();D=args.output.parent;D.mkdir(parents=True,exist_ok=True);records=[]
def ev(c):
 r=unity('eval',c);assert r['success'],r;return r['result']
def ck(n,x):
 records.append({'test':n,'result':x});args.output.write_text(json.dumps(records,ensure_ascii=False,indent=2));print(n,flush=True)
def obs():return call({'op':'observe'})
def ready():
 end=time.monotonic()+35
 while time.monotonic()<end:
  s=obs()
  if s['state']['phase']=='await_command':return s
  time.sleep(.25)
 raise AssertionError('ready timeout')
def cmd(op,**kw):
 s=obs();q={'op':op,'id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],**kw};t=time.monotonic();r=wait(q,seconds=25);ck(op,{'request':q,'response':r,'seconds':time.monotonic()-t});return r
def menu(label):return cmd('menu',entry=next(e['id'] for e in obs()['state']['menu'] if e['label']==label))
def start(scene=None):
 ev('UnityEditor.EditorApplication.isPlaying=false; return true;');time.sleep(.8)
 ev('UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CombatBase.unity"); return true;')
 if scene:ev('UnityEditor.SceneManagement.EditorSceneManager.OpenScene('+json.dumps(scene)+',UnityEditor.SceneManagement.OpenSceneMode.Additive); return true;')
 unity('editor_play');return ready()
try:
 s=start(); p=next(u for u in s['state']['units'] if u['team']=='Player');e=next(u for u in s['state']['units'] if u['team']=='Enemy')
 cmd('select',unit=p['id']);move_request_result=menu('Move')
 before=obs();t=time.monotonic();detail=call({'op':'options_detail','limit':128});dt=time.monotonic()-t
 t=time.monotonic();tactical=call({'op':'tactical'});tt=time.monotonic()-t
 assert obs()==before
 ck('queries_readonly',{'options_bytes':len(json.dumps(detail,separators=(',',':')).encode()),'options_seconds':dt,'tactical_bytes':len(json.dumps(tactical,separators=(',',':')).encode()),'tactical_seconds':tt})
 dest=min((x for x in detail['choices'] if x['grid']!=p['grid']),key=lambda x:(sum(abs(a-b) for a,b in zip(x['grid'],e['grid'])),x['movement']['movementAP'],x['movement']['distance']))
 r=cmd('target',grid=dest['grid']);assert r['status']=='succeeded'
 after=next(u for u in obs()['state']['units'] if u['id']==p['id'])
 assert abs(p['ap']-after['ap']-dest['movement']['movementAP'])<.001,(p,after,dest)
 assert abs(after['move']-dest['movement']['moveRemaining'])<.001
 ck('movement_actual_matches_preview',{'preview':dest,'before':p,'after':after})
 assert call({'op':'result','id':move_request_result['id']})==move_request_result;ck('completed_menu_result_immutable',True)
 menu('Slash');opts=call({'op':'options_detail'});assert any(x['unit']==e['id'] for x in opts['choices']);ck('target_entity_mapping',opts)
 # Runtime cost race fixture: after valid target submission, drain AP before actual coroutine executes.
 ev('UnityEditor.EditorApplication.isPaused=true; return true;')
 s=obs();q={'op':'target','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'grid':opts['choices'][0]['grid']};r=call(q);assert r['accepted']
 ev('var m=miniRAID.Globals.backend.allMobs.First(x=>x.guid.ToString("N")=="'+p['id']+'"); m.UseActionPoint(m.actionPoints); UnityEditor.EditorApplication.isPaused=false; return true;')
 end=time.monotonic()+15
 while time.monotonic()<end:
  r=call({'op':'result','id':q['id']})
  if not r['pending']:break
  time.sleep(.2)
 assert r['status']=='rejected' and r['reason']=='insufficient_cost' and not r['effectsMayHaveOccurred'],r
 ck('real_player_cost_failure_after_acceptance',r)
 # Isolated provenance fixture, no damage/costs; directly exercises receipt matching and partial classification.
 proof=ev('''var scheduler=miniRAID.Globals.combatMgr.Instance;
var receipt=scheduler.LastPlayerAction;
var field=typeof(miniRAID.CombatSchedulerCoroutine).GetField("executingPlayerAction",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
var source=miniRAID.Globals.backend.allMobs.First(m=>m.unitGroup==miniRAID.Consts.UnitGroup.Player);
var other=miniRAID.Globals.backend.allMobs.First(m=>m.unitGroup==miniRAID.Consts.UnitGroup.Enemy);
var action=source.availableActions.First();
var previous=field.GetValue(scheduler);
try { field.SetValue(scheduler,receipt); scheduler.BindPlayerAction(source,action); receipt.failure=null;
scheduler.ReportActionFailure("enemy_failure",other,action); bool isolated=receipt.failure==null;
scheduler.ReportActionEffects(source,action);scheduler.ReportActionFailure("invalid_target",source,action);
return new {isolated,receipt.failure,receipt.effectsStarted,receipt.Outcome};
} finally {field.SetValue(scheduler,previous);}''')
 assert proof['isolated'] and proof['effectsStarted'] and proof['failure']=='invalid_target' and proof['Outcome']=='partial_failure';ck('receipt_source_and_effect_tracking_fixture',proof)
 # Isolated post-cost failure fixture: real legal movement still executes; status must disclose possible effects.
 s=start();p=next(u for u in s['state']['units'] if u['team']=='Player')
 cmd('select',unit=p['id']);menu('Move');opts=call({'op':'options_detail','limit':128}); enemy=next(u for u in s['state']['units'] if u['team']=='Enemy')
 destination=min((x for x in opts['choices'] if x['grid']!=p['grid']),key=lambda x:(sum(abs(a-b) for a,b in zip(x['grid'],enemy['grid'])),x['movement']['movementAP'],x['movement']['distance']))
 assert cmd('target',grid=destination['grid'])['status']=='succeeded';menu('Slash');opts=call({'op':'options_detail'})
 ev('''var m=miniRAID.Globals.backend.allMobs.First(x=>x.unitGroup==miniRAID.Consts.UnitGroup.Player);
 miniRAID.MobData.MobOnApplyCostCoroutineDelegate hook=null;
 System.Collections.IEnumerator Inject(miniRAID.Cost cost,miniRAID.RuntimeAction a,miniRAID.MobData mob){
 mob.OnApplyCost.RemoveListener(hook);miniRAID.Globals.combatMgr.Instance.ReportActionFailure("test_post_cost_failure",mob,a);yield break;}
 hook=Inject;m.OnApplyCost.AddListener(hook);return true;''')
 target=next(x['grid'] for x in opts['choices'] if x['grid']!=p['grid'])
 partial=cmd('target',grid=target)
 assert partial['status']=='partial_failure' and partial['effectsMayHaveOccurred'],partial
 assert next(u for u in obs()['state']['units'] if u['id']==p['id'])['ap']<p['ap']
 ck('partial_failure_after_real_skill_fixture',partial)
 # Paid path cost and resource-only version invalidation.
 s=start();p=next(u for u in s['state']['units'] if u['team']=='Player');cmd('select',unit=p['id']);menu('Move')
 choices=call({'op':'options_detail','limit':128})['choices'];dest=next(x for x in choices if x['movement']['movementAP']==1)
 original=obs()
 for _ in range(3):call({'op':'options_detail'});call({'op':'tactical'})
 assert obs()==original
 r=cmd('target',grid=dest['grid']);after=next(u for u in obs()['state']['units'] if u['id']==p['id'])
 assert r['status']=='succeeded' and abs(p['ap']-after['ap']-1)<.001 and abs(after['move']-dest['movement']['moveRemaining'])<.001
 ck('paid_movement_and_repeated_readonly_queries',{'preview':dest,'before':p,'after':after})
 old=obs();mana=ev('var m=miniRAID.Globals.backend.allMobs.First(x=>x.unitGroup==miniRAID.Consts.UnitGroup.Player).FindListener<miniRAID.GeneralManaListener>();var value=m.current;m.current--;return value;')
 try:
  assert obs()['version']>old['version']
  rejected=call({'op':'cancel','id':uuid.uuid4().hex,'session':old['session'],'version':old['version']});assert rejected['reason']=='stale_state';ck('mana_only_change_invalidates_version_fixture',rejected)
 finally:ev('miniRAID.Globals.backend.allMobs.First(x=>x.unitGroup==miniRAID.Consts.UnitGroup.Player).FindListener<miniRAID.GeneralManaListener>().current='+str(mana)+';return true;')
 cmd('cancel');cmd('general');r=menu('EndTurn');assert r['status']=='succeeded' and r['effectsMayHaveOccurred'];ck('completed_endturn_discloses_effects',True)
 s=start('Assets/Scenes/SlimeKing/SlimeKing-All.unity')
 before=obs();tactical=call({'op':'tactical'});after=obs();assert before==after
 live=ev('''return miniRAID.Globals.backend.allMobs.Select(m=>new {id=m.guid.ToString("N"),mana=m.FindListener<miniRAID.GeneralManaListener>()==null?null:new[]{m.FindListener<miniRAID.GeneralManaListener>().current,m.FindListener<miniRAID.GeneralManaListener>().max},buffs=m.listeners.Where(f=>f.type is miniRAID.MobListenerSO.ListenerType.Buff or miniRAID.MobListenerSO.ListenerType.Passive).Select(f=>f is miniRAID.Buff.Buff b?b.detailedName:f.name).ToArray()}).ToArray();''')
 assert {u['id']:(u['mana'],u['buffs']) for u in tactical['units']}=={u['id']:(u['mana'],u['buffs']) for u in live}
 ck('slime_ui_resource_and_buff_fields',tactical)
 # Inject an enemy-attributed diagnostic during real EndTurn resolution; not a natural AI cost failure.
 ev('''UnityEditor.EditorApplication.CallbackFunction hook=null;
 hook=()=> {var c=miniRAID.Globals.combatMgr.Instance;
 if(!UnityEditor.EditorApplication.isPlaying){UnityEditor.EditorApplication.update-=hook;return;}
 if(c!=null&&!c.WaitingForPlayer){var e=miniRAID.Globals.backend.allMobs.First(m=>m.unitGroup==miniRAID.Consts.UnitGroup.Enemy);
 c.ReportActionFailure("test_enemy_insufficient_cost",e,e.availableActions.FirstOrDefault());UnityEditor.EditorApplication.update-=hook;}};
 UnityEditor.EditorApplication.update+=hook;return true;''')
 seen=False
 for i in range(6):
  cmd('general');r=menu('EndTurn');assert r['status']=='succeeded',r
  diag=ev('return new {failure=miniRAID.Globals.combatMgr.Instance.ActionFailure,playerFailure=miniRAID.Globals.combatMgr.Instance.LastPlayerAction.failure,turn=miniRAID.Globals.combatMgr.Instance.now.currentTurnID};')
  if diag['failure']:
   assert diag['playerFailure'] is None;ck('real_endturn_injected_enemy_diagnostic_isolated',{'diagnostic':diag,'response':r});seen=True;break
 assert seen
 ck('PASS',{'checks':len(records)})
finally:
 root=Path(__file__).resolve().parents[1]
 for n in ['miniRAID.combat.log','miniRAID.log']:shutil.copyfile(root/n,D/('targeted-'+n))
 ev('UnityEditor.EditorApplication.isPlaying=false; return true;')
