using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using miniRAID;
using Utils;

[DefaultExecutionOrder(-2000)]
public class CombatSceneLoader : MonoBehaviour
{
    public string sceneName;
    // One-shot selection for restarting the current encounter through CombatBase.
    public static string NextEncounter;
    public bool Ready { get; private set; }
    string requested;
    void Awake()
    {
        Databackend.ResetForNewCombat();
        Globals.combatTracker=new CombatTracker();
        requested=NextEncounter;
        NextEncounter=null;
    }
    public IEnumerator EnsureLoaded()
    {
        if(Ready)yield break;
        var existing=FindObjectsByType<SceneConfig>(FindObjectsSortMode.None)
            .FirstOrDefault(x=>x.gameObject.scene!=gameObject.scene);
        if(existing!=null && !string.IsNullOrEmpty(requested) && existing.gameObject.scene.path!=requested && existing.gameObject.scene.name!=requested)
            throw new System.InvalidOperationException("Requested encounter conflicts with the already loaded encounter: "+requested);
        if(existing==null)
        {
            var target=string.IsNullOrEmpty(requested)?sceneName:requested;
            var scene=SceneManager.GetSceneByPath(target);
            if(!scene.IsValid())scene=SceneManager.GetSceneByName(target);
            if(!scene.isLoaded)yield return SceneManager.LoadSceneAsync(target,LoadSceneMode.Additive);
        }
        // Let newly loaded OnEnable run, then explicitly initialize mobs before Combat.
        foreach(var mob in FindObjectsByType<MobRenderer>(FindObjectsSortMode.None))mob.Init();
        Ready=true;
    }
}
