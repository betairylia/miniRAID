#!/usr/bin/env python3
"""Live integration acceptance on CombatBase -> AlphaWolf. Stops Play in finally.
Run explicitly: python3 Tools/test_gameplay_harness.py --run --output /tmp/harness-tests.json
The facade/client are scenario-independent; these acceptance expectations are not.
"""
import argparse
import shutil
import json
from pathlib import Path
import time
import uuid
from gameplay_harness import call as raw_call, wait as raw_wait, unity

# Acceptance assertions inspect full state. The default compact surface is benchmarked separately.
def call(request): return raw_call({**request,'detail':'full'})
def wait(request): return raw_wait({**request,'detail':'full'})


# Isolated query regression: real WindBuff callback, temporarily subscribed, always removed.
# This is not a gameplay action or an end-to-end buff acquisition test.
COST_QUERY_REGRESSION = r"""var mob=miniRAID.Globals.backend.allMobs.First(m=>m.unitGroup==miniRAID.Consts.UnitGroup.Player);
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<miniRAID.Buff.BuffSO>("Assets/GameContent/Allies/Actions/Essentials/Test/WindBuffNew.asset");
var buff=asset.Wrap(mob);
var method=buff.GetType().GetMethod("MobOnModifyCost",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
var callback=(miniRAID.MobData.CostQueryDelegate)System.Delegate.CreateDelegate(typeof(miniRAID.MobData.CostQueryDelegate),buff,method);
var before=mob.availableActions.SelectMany(a=>a.costBounds).SelectMany(c=>new[]{c.Item1.value.Value,c.Item2.value.Value}).ToArray();
mob.OnModifyCost.AddListener(callback);
try {
 var probe=new miniRAID.Cost(miniRAID.dNumber.CreateComposite(3),miniRAID.Cost.Type.AP); callback(probe,null,mob);
 var snapshot=miniRAID.EditorTools.GameplayHarness.Call("{\"op\":\"observe\"}");
 for(int i=0;i<6;i++) {
  if(miniRAID.EditorTools.GameplayHarness.Call("{\"op\":\"observe\"}")!=snapshot)throw new System.Exception("unstable observation");
  miniRAID.EditorTools.GameplayHarness.Call("{\"op\":\"options\"}");
  miniRAID.EditorTools.GameplayHarness.Call("{\"op\":\"cancel\",\"id\":\"cost-stale\",\"session\":\"old\",\"version\":0}");
 }
 var after=mob.availableActions.SelectMany(a=>a.costBounds).SelectMany(c=>new[]{c.Item1.value.Value,c.Item2.value.Value}).ToArray();
 return new {success=before.SequenceEqual(after)&&probe.value.Value==2,before,after,probe=probe.value.Value};
} finally { mob.OnModifyCost.RemoveListener(callback); }"""

def run(output):
    records=[]
    def check(name, value):
        records.append({'test':name,'result':value})
        output.write_text(json.dumps(records,ensure_ascii=False,indent=2))
        print(name, value.get('status','ok'),flush=True)
    def observe():
        return call({'op':'observe'})
    def command(op, **kw):
        s=observe()
        request={'op':op,'id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],**kw}
        response=wait(request)
        check(op,response)
        assert response['status']=='succeeded',response
        return response,request
    def menu(label):
        entry=next(e for e in observe()['state']['menu'] if e['label']==label)
        return command('menu',entry=entry['id'])
    def ready():
        deadline=time.monotonic()+30
        while time.monotonic()<deadline:
            s=observe()
            if s['state']['phase']=='await_command':return s
            time.sleep(.3)
        raise AssertionError('No player decision point after 30 seconds')
    def stop():
        # Wait for a responsive Editor after a domain reload before issuing cleanup.
        call({'op':'observe'})
        unity('eval','UnityEditor.EditorApplication.isPaused=false; return true;')
        unity('editor_stop')
        deadline=time.monotonic()+20
        while time.monotonic()<deadline:
            if observe()['state']['phase']=='not_ready':return
            time.sleep(.2)
        raise AssertionError('Play did not stop')
    try:
        stop()
        unity('editor_play')
        start=ready()
        player=next(u for u in start['state']['units'] if u['team']=='Player' and u['active'])
        enemy=next(u for u in start['state']['units'] if u['team']=='Enemy')
        command('select',unit=enemy['id'])
        s=observe(); assert not any(e['label'] in ('Move','Pass','Slash') and e['enabled'] for e in s['state']['menu'])
        response=call({'op':'menu','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'entry':999})
        check('enemy_menu_permissions',response);assert response['reason']=='invalid_entry' and observe()==s
        command('cancel')
        command('select',unit=player['id'])
        cost=unity('eval', COST_QUERY_REGRESSION)
        check('windbuff_query_isolation',cost);assert cost['success'] and cost['result']['success']
        s=observe(); move=next(e for e in s['state']['menu'] if e['label']=='Move')
        for fields in ({'timeoutMs':'oops'},{'entry':1.5},{'version':'bad'},{'grid':[1.5,1,2]},{'session':{}},{'id':[]}):
            request={'op':'menu','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'entry':move['id'],**fields}
            response=call(request);check('invalid_parameter_'+next(iter(fields)),response)
            assert response['reason']=='invalid_request' and response['accepted'] is False and observe()==s
        for _ in range(4):
            assert observe()==s
            call({'op':'options'})
        check('repeated_reads_unchanged',{'status':'succeeded'})
        menu('Move')
        s=observe()
        invalid={'op':'target','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'grid':[99999,99999,99999]}
        response=call(invalid);check('illegal_target',response);assert response['reason']=='not_allowed'
        assert observe()==s,'Rejected target changed snapshot'
        response=call({**invalid,'id':uuid.uuid4().hex,'version':s['version']-1});check('stale',response);assert response['reason']=='stale_state'
        command('cancel');assert observe()['state']['ui']=='UnitMenu'
        # Pausing advances neither game logic nor frames: verify exclusivity and timeout without a duplicate command.
        unity('eval','UnityEditor.EditorApplication.isPaused=true; return true;')
        s=observe();entry=next(e for e in s['state']['menu'] if e['label']=='Move')
        req={'op':'menu','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'entry':entry['id'],'timeoutMs':0}
        response=call(req);check('timeout_pending',response);assert response['status']=='timed_out' and response['pending']
        s=observe();response=call({'op':'cancel','id':uuid.uuid4().hex,'session':s['session'],'version':s['version']});check('busy',response);assert response['reason']=='busy'
        unity('eval','UnityEditor.EditorApplication.isPaused=false; return true;')
        deadline=time.monotonic()+10
        while time.monotonic()<deadline:
            response=call({'op':'result','id':req['id']})
            if not response['pending']:break
            time.sleep(.2)
        check('timeout_later_result',response);assert response['status']=='succeeded'
        s=observe();options=call({'op':'options','offset':0,'limit':128});check('target_options',options)
        assert len(options['choices'])<=128
        enemy=next(u for u in s['state']['units'] if u['team']=='Enemy')
        choices=[]
        while len(choices)<options['count']:
            page=call({'op':'options','offset':len(choices),'limit':128})
            assert page['session']==options['session'] and page['version']==options['version']
            assert page['choices'];choices.extend(page['choices'])
        dest=min((g for g in choices if g!=player['grid']),key=lambda g:(sum(abs(a-b) for a,b in zip(g,enemy['grid'])),sum(abs(a-b) for a,b in zip(g,player['grid']))))
        response,request=command('target',grid=dest)
        moved=next(u for u in response['result']['state']['units'] if u['id']==player['id'])
        assert moved['grid']==dest and moved['pos']!=player['pos']
        duplicate=call(request);check('duplicate_no_execution',duplicate);assert duplicate==response
        conflict=call({**request,'grid':player['grid']});check('duplicate_conflict',conflict);assert conflict['reason']=='duplicate_id_conflict'
        response,_=menu('Slash');targets=response['result']['state']['target']['choices'];assert targets
        response,_=command('target',grid=targets[0])
        after=next(u for u in response['result']['state']['units'] if u['id']==player['id'])
        assert after['ap']<moved['ap'],'Actual skill cost not applied'
        # Insufficient AP must be rejected by the shared menu gate, without consuming more AP.
        s=observe();slash=next(e for e in s['state']['menu'] if e['label']=='Slash');assert not slash['enabled']
        response=call({'op':'menu','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'entry':slash['id']});check('insufficient_ap',response);assert response['reason']=='not_allowed' and observe()==s
        command('cancel');command('general')
        s=observe();debug=next(e for e in s['state']['menu'] if e['label']=='Cheat');assert not debug['enabled']
        response=call({'op':'menu','id':uuid.uuid4().hex,'session':s['session'],'version':s['version'],'entry':debug['id']});check('debug_disabled',response);assert response['reason']=='debug_action_disabled'
        before_turn=observe()
        menu('EndTurn');assert observe()['state']['phase']=='await_command'
        after_turn=observe();assert after_turn['state']['lockedUnit'] is None
        # A schedule may contain several player slices at the same numbered turn.
        for _ in range(8):
            combat=(Path(__file__).resolve().parents[1]/'miniRAID.combat.log').read_text()
            if 'AlphaWolf 施放了' in combat:break
            command('general');menu('EndTurn')
        assert 'AlphaWolf 施放了' in combat,'No enemy cast observed within eight player slices'
        after_turn=observe()
        check('endturn_advanced',{'status':'succeeded','before':before_turn['state'],'after':after_turn['state']})
        # Preserve the original engine logs BEFORE restart truncates them; no generated contents.
        for filename in ('miniRAID.combat.log','miniRAID.log'):
            shutil.copyfile(Path(__file__).resolve().parents[1]/filename,output.parent/('review-'+filename))
        old=observe();stop();unity('editor_play');fresh=ready()
        assert fresh['session']!=old['session']
        response=call(request);check('old_session_rejected',response);assert response['reason']=='stale_state'
        response=call({'op':'result','id':request['id']});check('old_operation_cleared',response);assert response['reason']=='unknown_request'
        fresh_player=next(u for u in fresh['state']['units'] if u['team']=='Player')
        assert fresh_player['grid']==player['grid'] and fresh_player['ap']==player['ap']
        command('select',unit=fresh_player['id']);menu('Pass');assert observe()['state']['phase']=='await_command'
        check('PASS',{'status':'succeeded','checks':len(records)})
    finally:
        stop()

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run',action='store_true',required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();run(args.output)
