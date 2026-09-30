#!/usr/bin/env python3
"""Thin local client: JSON over existing Unity Pipeline eval, no server or screen input."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid

PROJECT = str(Path(__file__).resolve().parents[1])

def unity(command, *args):
    result = subprocess.run(['unity', 'command', command, '--caller', 'plugin', '--skill', 'unity-cli',
                             '--project-path', PROJECT, '--format', 'json', *args],
                            capture_output=True, text=True, timeout=15,
                            env={**os.environ, 'UNITY_NO_UPDATE_CHECK':'1'})
    envelope = json.loads(result.stdout)
    if not envelope.get('success'):
        raise RuntimeError(envelope.get('errors'))
    return envelope['data']['result']

def call(request):
    literal = json.dumps(json.dumps(request, ensure_ascii=False), ensure_ascii=False)
    # Only read requests are retried across Unity domain reloads. Never resubmit gameplay mutations.
    attempts = 15 if request.get('op') in ('observe', 'options', 'options_detail', 'tactical', 'result') else 1
    for attempt in range(attempts):
        try:
            result = unity('eval', 'return miniRAID.EditorTools.GameplayHarness.Call(' + literal + ');')
            if not result.get('success'):
                raise RuntimeError(result)
            return json.loads(result['result'])
        except (RuntimeError, json.JSONDecodeError, subprocess.TimeoutExpired):
            if attempt + 1 == attempts:
                raise
            time.sleep(0.3)

def wait(request, seconds=15):
    """Never retries submission. A client timeout leaves the accepted operation queryable."""
    result = call(request)
    deadline = time.monotonic() + seconds
    interval = 0.15
    while result.get('pending') and result.get('status') != 'timed_out':
        if time.monotonic() >= deadline:
            return {**result, 'status':'timed_out', 'pending':True, 'reason':'client_deadline'}
        time.sleep(interval)
        interval = min(0.6, interval * 1.5)
        result = call({'op':'result', 'id':request['id'], 'detail':request.get('detail','compact')})
    return result

def sequence(actions, detail='compact'):
    """Client-side straight-line UI steps. Stops on any failure; never plans or bypasses rules."""
    if not isinstance(actions,list) or not actions or any(not isinstance(a,dict) or a.get('op') not in ('select','general','menu','target','cancel') for a in actions):
        raise ValueError('sequence requires a nonempty array of semantic action objects')
    observation=call({'op':'observe','detail':detail})
    steps=[]
    for action in actions:
        action=dict(action)
        if action['op']=='menu' and 'label' in action:
            matches=[e for e in (observation['state'].get('menu') or []) if e['label']==action['label']]
            if len(matches)!=1:
                return {'status':'rejected','accepted':False,'reason':'menu_label_not_unique','steps':steps,'observation':observation}
            action['entry']=matches[0]['id'];del action['label']
        request={**action,'id':uuid.uuid4().hex,'session':observation['session'],'version':observation['version'],'detail':detail}
        try:
            result=wait(request)
        except (RuntimeError,ValueError,subprocess.TimeoutExpired) as error:
            # A transport failure is not a rejection. Preserve the generated ID for recovery.
            return {'id':request['id'],'status':'unknown','accepted':None,'pending':None,
                    'reason':'transport_error','detail':str(error),'request':request,'steps':steps}
        step={k:result[k] for k in ('id','status','accepted','pending','reason') if k in result}
        state=(result.get('result') or {}).get('state',{})
        for key in ('unitsChanged','unitsRemoved'):
            if state.get(key):step[key]=state[key]
        steps.append(step)
        if result['status']!='succeeded':return {**result,'steps':steps}
        observation=result['result']
    return {**result,'steps':steps}

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('request', nargs='?', help='JSON request; omitted reads stdin')
    parser.add_argument('--wait', action='store_true')
    parser.add_argument('--sequence', action='store_true', help='Execute a JSON array of UI steps locally; stop on failure')
    parser.add_argument('--full', action='store_true', help='Return full debug snapshots')
    args=parser.parse_args()
    request=json.loads(args.request if args.request is not None else sys.stdin.read())
    if args.sequence:
        result=sequence(request, 'full' if args.full else 'compact')
    else:
        if args.full:request={**request,'detail':'full'}
        result=wait(request) if args.wait else call(request)
    print(json.dumps(result, ensure_ascii=False, separators=(',', ':')))
