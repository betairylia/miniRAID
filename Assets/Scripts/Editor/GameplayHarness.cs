using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using miniRAID.UI;
using miniRAID.UI.TargetRequester;

namespace miniRAID.EditorTools
{
    /// <summary>Editor-only adapter to the same menu and requester paths used by the player.</summary>
    [InitializeOnLoad]
    public static class GameplayHarness
    {
        sealed class Operation
        {
            public string id, payload, status = "pending", reason;
            public double deadline;
            public int frame;
            public string verb, initialTarget;
            public object result;
            public JToken before;
        }
        static readonly Dictionary<string, Operation> operations = new();
        static readonly Queue<string> history = new();
        static Operation active;
        static string session = Guid.NewGuid().ToString("N"), fingerprint;
        static long version;
        static string fault;
        static GridUI UI => UnityEngine.Object.FindFirstObjectByType<GridUI>();
        static CombatSchedulerCoroutine Scheduler => UnityEngine.Object.FindFirstObjectByType<CombatSchedulerCoroutine>();

        static GameplayHarness()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += _ => Reset();
            Application.logMessageReceived += (_, trace, type) =>
            {
                if (active != null && type == LogType.Exception) fault = trace;
            };
        }
        static void Reset()
        {
            session = Guid.NewGuid().ToString("N"); version = 0; fingerprint = null;
            operations.Clear(); history.Clear(); active = null; fault = null;
        }
        static bool Live => EditorApplication.isPlaying && UI != null && Scheduler != null;
        static string Phase
        {
            get
            {
                if (!Live) return "not_ready";
                if (fault != null) return "faulted";
                if (!Scheduler.WaitingForPlayer) return "resolving";
                if (UI.currentState is TargetRequesterUIState t && t.IsAwaitingChoice) return "await_target";
                if (Scheduler.ActionPending || UI.isInAnimation) return "resolving";
                return "await_command";
            }
        }
        static int[] Grid(Vector3Int p) => new[] { p.x, p.y, p.z };
        static string TargetSignature() => UI?.currentState is TargetRequesterUIState t
            ? t.GetType().Name + ":" + string.Join(";", t.Choices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z)) : null;
        static object Snapshot()
        {
            if (!Live) return new { phase = "not_ready" };
            var ui = UI; var scheduler = Scheduler;
            var menu = ui.combatView.menu;
            var entries = ui.currentState is UnitMenu && menu.IsMenuShown && !ui.isInAnimation
                ? menu.Entries.Select((e,i) => new { id = i, label = e.text,
                    action = e.runtimeAction?.guid.ToString("N"),
                    enabled = e.IsPerformable && e.action != null && e.gameplayAction,
                    cooldown = e.runtimeAction?.cooldownRemain, targetKind=e.runtimeAction?.SpellTargetType.Name }).ToArray() : null;
            var target = ui.currentState as TargetRequesterUIState;
            if (target != null && !target.IsAwaitingChoice) target = null;
            var choices = target?.Choices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).Select(Grid).ToArray();
            return new {
                phase = Phase, ui = ui.currentState?.GetType().Name,
                turn = scheduler.now.currentTurnID,
                lockedUnit = scheduler.GetCurrentLockedPlayerTurnSlice()?.LockedPlayer?.guid.ToString("N"),
                queue = scheduler.turnSchedule?.Where(t=>t.ShowInUI).Take(8).Select(t=>new { id=t.guid.ToString("N"),label=t.Label,turn=t.metadata.timestamp.currentTurnID }).ToArray(),
                units = Globals.backend.allMobs.OrderBy(m=>m.guid).Select(m => new {
                    id=m.guid.ToString("N"), name=m.nickname, team=m.unitGroup.ToString(),
                    pos=new[]{m.Position.x,m.Position.y,m.Position.z}, grid=Grid(m.GridPosition),
                    hp=new[]{m.health,m.maxHealth}, ap=m.actionPoints, move=m.MoveRangeLeft,
                    active=m.isControllable, dead=m.isDead }).ToArray(),
                menu=entries, target = choices == null ? null : new { kind=target.GetType().Name, count=choices.Length, choices=choices.Take(64).ToArray(), more=choices.Length>64 },
                failure=scheduler.ActionFailure
            };
        }
        static object Observe()
        {
            var snapshot = Snapshot();
            var encoded = JsonConvert.SerializeObject(snapshot);
            if (encoded != fingerprint) { version++; fingerprint=encoded; }
            return new { session, version, state=snapshot };
        }
        static void Tick()
        {
            if (active == null) return;
            if (!Live) { Finish("faulted", "play_stopped"); return; }
            if (fault != null) { Finish("faulted", "game_exception"); return; }
            if (Time.frameCount <= active.frame + 1) return;
            var phase = Phase;
            if (phase == "await_command" || (phase == "await_target" &&
                (active.verb == "menu" || TargetSignature() != active.initialTarget)))
                {
                var reason=(active.verb == "menu" || active.verb == "target") ? Scheduler.ActionFailure : null;
                Finish(reason == null ? "succeeded" : "rejected", reason);
            }
        }
        static void Finish(string status, string reason)
        {
            active.status=status; active.reason=reason; active.result=Observe(); active=null;
        }
        static object Result(Operation op) => new {
            id=op.id, status=op.status == "pending" && EditorApplication.timeSinceStartup >= op.deadline ? "timed_out" : op.status,
            pending=op.status == "pending", accepted=true, reason=op.reason,
            result=op.result, fault=fault == null ? null : fault.Split('\n').FirstOrDefault()
        };
        static object Reject(string reason) => new { status="rejected", accepted=false, reason, observation=Observe() };

        /// <summary>Call through Pipeline eval. Never waits on Unity's main thread.</summary>
        public static string Call(string json)
        {
            JObject request;
            try { request=JObject.Parse(json); }
            catch (JsonException e) { return JsonConvert.SerializeObject(new {status="rejected",accepted=false,reason="invalid_json",detail=e.Message}); }
            try
            {
                var output=JToken.FromObject(Dispatch(request));
                return (request["detail"]?.Type == JTokenType.String && (string)request["detail"] == "full" ? output : Compact(output)).ToString(Formatting.None);
            }
            catch (Exception e)
            {
                // A callback may have changed state before throwing. Never promise rejection/rollback.
                fault=e.ToString();
                if (active != null) Finish("faulted", "command_exception");
                return JsonConvert.SerializeObject(new {status="faulted",accepted=(bool?)null,reason="command_exception",detail=e.Message});
            }
        }
        static JObject Pick(JToken value, params string[] keys)
        {
            var result=new JObject();
            foreach(var key in keys) if(value?[key] != null) result[key]=value[key].DeepClone();
            return result;
        }
        static JObject CompactState(JToken state, JToken before = null)
        {
            var output=Pick(state,"phase","ui","turn","lockedUnit","failure");
            if(state["units"] is JArray units)
            {
                if(before?["units"] is JArray previous)
                {
                    var changes=new JArray();
                    foreach(var unit in units)
                    {
                        var old=previous.FirstOrDefault(u=>(string)u["id"]==(string)unit["id"]);
                        var delta=Pick(unit,"id","name");
                        foreach(var key in new[]{"team","grid","hp","ap","move","active","dead"})
                            if(old == null || !JToken.DeepEquals(unit[key],old[key])) delta[key]=unit[key].DeepClone();
                        if(delta.Count>2) changes.Add(delta);
                    }
                    output["unitsChanged"]=changes;
                    var removed=previous.Where(u=>!units.Any(n=>(string)n["id"]==(string)u["id"])).Select(u=>(string)u["id"]).ToArray();
                    if(removed.Length>0) output["unitsRemoved"]=JArray.FromObject(removed);
                }
                else output["units"]=new JArray(units.Select(u=>Pick(u,"id","name","team","grid","hp","ap","move","active","dead")));
            }
            if(state["menu"] is JArray menu)
                output["menu"]=new JArray(menu.Select(e=> {
                    var entry=Pick(e,"id","label","enabled","targetKind");
                    if(entry["targetKind"]?.Type==JTokenType.Null) entry.Remove("targetKind");
                    if(e["cooldown"]?.Type==JTokenType.Integer && (int)e["cooldown"]!=0) entry["cooldown"]=e["cooldown"].DeepClone();
                    return entry;
                }));
            if(state["queue"] is JArray queue && (before == null || !JToken.DeepEquals(state["queue"],before["queue"])))
                output["queue"]=new JArray(queue.Select(e=>Pick(e,"label","turn")));
            if(state["target"] is JObject target)
            {
                output["target"]=Pick(target,"kind","count");
                // Small target sets are useful directly; larger sets use the existing options pagination.
                if((int)target["count"]<=8) output["target"]["choices"]=target["choices"].DeepClone();
            }
            if(output["failure"]?.Type==JTokenType.Null) output.Remove("failure");
            return output;
        }
        static JToken Compact(JToken output)
        {
            if(output["state"] is JObject state) output["state"]=CompactState(state);
            if(output["observation"] is JObject observed && observed["state"] is JObject observation)
                output["observation"]["state"]=CompactState(observation);
            if(output["result"] is JObject completed && completed["state"] is JObject result)
            {
                operations.TryGetValue((string)output["id"] ?? "",out var operation);
                output["result"]["state"]=CompactState(result,operation?.before);
            }
            if(output is JObject obj)
                foreach(var key in new[]{"reason","fault","result"})
                    if(obj[key]?.Type==JTokenType.Null) obj.Remove(key);
            return output;
        }
        static string Validate(JObject r)
        {
            bool Text(string key) => r[key]?.Type == JTokenType.String;
            bool Integer(string key, long min, long max) => r[key]?.Type == JTokenType.Integer &&
                long.TryParse(r[key].ToString(), out var n) && n >= min && n <= max;
            if (!Text("op")) return "op must be a string";
            if (r["detail"] != null && (!Text("detail") || ((string)r["detail"] != "full" && (string)r["detail"] != "compact")))
                return "detail must be compact or full";
            foreach (var key in new[]{"offset","limit","timeoutMs","entry"})
                if (r[key] != null && !Integer(key,int.MinValue,int.MaxValue)) return key + " must be an integer";
            foreach (var key in new[]{"id","session","unit"})
                if (r[key] != null && !Text(key)) return key + " must be a string";
            if (r["version"] != null && !Integer("version",0,long.MaxValue)) return "version must be a nonnegative integer";
            if (r["grid"] != null && (!(r["grid"] is JArray a) || a.Count != 3 ||
                a.Any(v=>v.Type != JTokenType.Integer || !int.TryParse(v.ToString(),out _))))
                return "grid must contain three integers";
            return null;
        }
        static object Dispatch(JObject r)
        {
            // Validate all fields before Tick/Observe or any game callback. Input errors are not game faults.
            var invalid = Validate(r);
            if (invalid != null) return new {status="rejected",accepted=false,reason="invalid_request",detail=invalid};
            Tick();
            var verb=(string)r["op"];
            if (verb == "observe") return Observe();
            if (verb == "options")
            {
                Observe();
                var target=UI?.currentState as TargetRequesterUIState;
                var offset=Math.Max(0,(int?)r["offset"] ?? 0);
                var limit=Math.Max(1,Math.Min(128,(int?)r["limit"] ?? 64));
                var choices=target != null && target.IsAwaitingChoice
                    ? target.Choices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray() : Array.Empty<Vector3Int>();
                return new { session, version, count=choices.Length, offset, choices=choices.Skip(offset).Take(limit).Select(Grid).ToArray() };
            }
            if (verb == "result") return operations.TryGetValue((string)r["id"] ?? "",out var found) ? Result(found) : Reject("unknown_request");
            var id=(string)r["id"];
            if (string.IsNullOrWhiteSpace(id) || id.Length>128) return Reject("request_id_required");
            var identity=(JObject)r.DeepClone(); identity.Remove("detail");
            var payload=identity.ToString(Formatting.None);
            if (operations.TryGetValue(id,out var previous)) return previous.payload == payload ? Result(previous) : Reject("duplicate_id_conflict");
            Observe();
            if ((string)r["session"] != session || (long?)r["version"] != version) return Reject("stale_state");
            if (!Live || fault != null) return Reject("not_ready");
            if (active != null) return Reject("busy");
            var ui=UI;
            var before=JToken.Parse(fingerprint);
            string initial=TargetSignature();
            bool accepted=false;
            if (verb == "select" && Phase == "await_command" && ui.currentState is FreeView)
            {
                var unit=Globals.backend.allMobs.FirstOrDefault(m=>m.guid.ToString("N") == (string)r["unit"]);
                if (unit == null || unit.mobRenderer == null) return Reject("unknown_unit");
                ui.cursor.Position=Globals.backend.GridToBackendFloorPos(unit.GridPosition);
                ui.currentState.Submit(null); accepted=true;
            }
            else if (verb == "general" && Phase == "await_command" && ui.currentState is FreeView)
            {
                // Same general menu used by clicking an empty grid; no invented action permissions.
                ui.EnterState(new UnitMenu(null),true); accepted=true;
            }
            else if (verb == "menu" && Phase == "await_command" && ui.currentState is UnitMenu)
            {
                int index=(int?)r["entry"] ?? -1;
                var menu=ui.combatView.menu;
                if(index<0 || index>=menu.Entries.Count) return Reject("invalid_entry");
                var entry=menu.Entries[index];
                if(!entry.gameplayAction) return Reject("debug_action_disabled");
                accepted=menu.TryExecuteEntry(index);
            }
            else if (verb == "target" && Phase == "await_target" && ui.currentState is TargetRequesterUIState t)
            {
                var grid=r["grid"]?.ToObject<int[]>();
                if(grid == null || grid.Length != 3) return Reject("invalid_grid");
                accepted=t.TrySubmitGrid(new Vector3Int(grid[0],grid[1],grid[2]));
            }
            else if (verb == "cancel" && (Phase == "await_target" || Phase == "await_command"))
            { ui.currentState.Cancel(null); accepted=true; }
            if(!accepted) return Reject("not_allowed");
            var operation=new Operation { id=id,payload=payload,frame=Time.frameCount,verb=verb,initialTarget=initial,before=before,
                deadline=EditorApplication.timeSinceStartup + Math.Max(0,Math.Min(60000,(int?)r["timeoutMs"] ?? 10000))/1000.0 };
            operations.Add(id,operation);history.Enqueue(id);
            while(history.Count>256) operations.Remove(history.Dequeue());
            active=operation; version++; fingerprint=null;
            return Result(operation);
        }
    }
}
