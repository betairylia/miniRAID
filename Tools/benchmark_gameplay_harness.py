"""Live AlphaWolf benchmark; output sizes are UTF-8 compact JSON, timings are monotonic.
Text token figures are estimates (bytes/3, with bytes/4..bytes/2 range), not model billing.
"""
import sys,time,json,uuid,subprocess,shutil,argparse
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import gameplay_harness as h
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--run',action='store_true',required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args();out=args.output;out.parent.mkdir(parents=True,exist_ok=True)
raw_unity,raw_call=h.unity,h.call
processes=[];calls=[];actions=[]
def size(value):
 b=len(json.dumps(value,ensure_ascii=False,separators=(',',':')).encode());return {'bytes':b,'estimated_tokens':round(b/3),'token_range':[round(b/4),round(b/2)]}
def timed_unity(command,*args):
 start=time.perf_counter()
 try:return raw_unity(command,*args)
 finally:processes.append({'command':command,'seconds':time.perf_counter()-start})
def timed_call(request):
 start=time.perf_counter();n=len(processes);result=raw_call(request)
 calls.append({'request':request,'response':result,'request_size':size(request),'response_size':size(result),'seconds':time.perf_counter()-start,'cli_calls':len(processes)-n})
 return result
h.unity=timed_unity;h.call=timed_call
record={'token_method':'No installed tokenizer found. UTF-8 bytes / 3 central estimate, /4 to /2 range. UUID-heavy JSON; actual model unknown. No PNG-size token estimate.','calls':calls,'actions':actions,'processes':processes}
def save():out.write_text(json.dumps(record,ensure_ascii=False,indent=2))
def ready():
 end=time.monotonic()+30
 while time.monotonic()<end:
  s=h.call({'op':'observe'})
  if s['state']['phase']=='await_command':return s
  time.sleep(.3)
 raise AssertionError('not ready')
def execute(op,**fields):
 global state
 req={'op':op,'id':uuid.uuid4().hex,'session':state['session'],'version':state['version'],**fields}
 start=time.perf_counter();n=len(processes);c=len(calls)
 result=h.wait(req);elapsed=time.perf_counter()-start
 assert result['status']=='succeeded',result
 item={'op':op,'request':req,'completion_compact':result,'wall_seconds':elapsed,'cli_calls':len(processes)-n,'polls':sum(x['request']['op']=='result' for x in calls[c:])}
 full=h.call({'op':'result','id':req['id'],'detail':'full'})
 compact=h.call({'op':'result','id':req['id']})
 assert compact==result
 item.update(completion_full=full,compact_size=size(compact),full_size=size(full))
 actions.append(item);state=result['result'];save();return result
try:
 # Actual CLI process startup baseline only; it excludes Pipeline and game work.
 t=time.perf_counter();p=subprocess.run(['unity','--version'],capture_output=True,text=True);record['cli_version_baseline']={'seconds':time.perf_counter()-t,'output':p.stdout.strip()}
 h.unity('editor_play');state=ready()
 full=h.call({'op':'observe','detail':'full'});compact=h.call({'op':'observe'});record['initial_observe']={'full':size(full),'compact':size(compact)}
 for malformed in ({},{'detail':{}},{'detail':[]},{'detail':'unexpected'}):
  if not malformed:continue
  r=h.call({'op':'observe',**malformed});assert r['accepted'] is False and r['reason']=='invalid_request'
 assert h.call({'op':'observe'})==compact
 player=next(u for u in full['state']['units'] if u['team']=='Player');enemy=next(u for u in full['state']['units'] if u['team']=='Enemy')
 execute('select',unit=player['id'])
 screenshot=out.parent/'benchmark-game.png';screenshot.unlink(missing_ok=True)
 shot_start=time.perf_counter();shot=h.unity('eval','UnityEngine.ScreenCapture.CaptureScreenshot("'+str(screenshot)+'"); return new {width=UnityEngine.Screen.width,height=UnityEngine.Screen.height};')
 shot_deadline=time.monotonic()+5
 while not screenshot.exists() and time.monotonic()<shot_deadline:time.sleep(.02)
 import struct
 image_bytes=screenshot.read_bytes();width,height=struct.unpack('>II',image_bytes[16:24])
 record['game_screenshot']={'path':str(screenshot),'width':width,'height':height,'capture_to_file_seconds':time.perf_counter()-shot_start,'screen':shot,'bytes':len(image_bytes)}
 execute('menu',entry=next(e['id'] for e in state['state']['menu'] if e['label']=='Move'))
 options=h.call({'op':'options','limit':64});optionsfull=h.call({'op':'options','limit':64,'detail':'full'});assert options==optionsfull
 choices=list(options['choices'])
 while len(choices)<options['count']:
  choices.extend(h.call({'op':'options','offset':len(choices),'limit':128})['choices'])
 dest=min((g for g in choices if g!=player['grid']),key=lambda g:(sum(abs(a-b) for a,b in zip(g,enemy['grid'])),sum(abs(a-b) for a,b in zip(g,player['grid']))))
 move=execute('target',grid=dest);record['move_operation']=move['id']
 # detail is display-only: same accepted ID with full display does not execute again.
 same=h.call({**actions[-1]['request'],'detail':'full'});assert same==actions[-1]['completion_full']
 execute('menu',entry=next(e['id'] for e in state['state']['menu'] if e['label']=='Slash'))
 assert state['state']['target']['choices']
 slash=execute('target',grid=state['state']['target']['choices'][0]);record['slash_operation']=slash['id']
 # One model/tool call; local sequence still uses the same server validators at every step.
 seq=[{'op':'cancel'},{'op':'general'},{'op':'menu','label':'EndTurn'},{'op':'select','unit':player['id']},{'op':'menu','label':'Move'},{'op':'target','grid':[player['grid'][0]-1,player['grid'][1],player['grid'][2]]},{'op':'cancel'}]
 t=time.perf_counter();n=len(processes);result=h.sequence(seq);record['sequence']={'actions':seq,'result':result,'wall_seconds':time.perf_counter()-t,'cli_calls':len(processes)-n,'output_size':size(result),'model_tool_calls':1};assert result['status']=='succeeded'
 assert any(step.get('unitsChanged') for step in result['steps'])
 shutil.copyfile(Path(h.PROJECT)/'miniRAID.combat.log',out.parent/'efficiency-combat.log')
 record['PASS']=True;save()
finally:
 h.call({'op':'observe'});h.unity('editor_stop');save();print(out,flush=True)
