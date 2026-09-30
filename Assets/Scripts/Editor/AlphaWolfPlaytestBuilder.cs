#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using miniRAID;
using miniRAID.Spells;
using miniRAID.Weapon;
using miniRAID.ActionHelpers;
using miniRAID.TurnSchedule;
using miniRAID.AlphaWolfPlaytest;
public static class AlphaWolfPlaytestBuilder
{
    const string Dir="Assets/GameContent/Playtests/AlphaWolf";
    const string Scene="Assets/Scenes/OpenTest/AlphaWolfPlaytest.unity";
    static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
    static T Save<T>(T obj,string name) where T:UnityEngine.Object
    {var path=Dir+"/"+name+".asset";if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new Exception("Refusing to overwrite "+path);AssetDatabase.CreateAsset(obj,path);return obj;}
    static T Clone<T>(T source,string name) where T:UnityEngine.Object=>Save(UnityEngine.Object.Instantiate(source),name);
    static LocalizedString Text(string key,string value)
    {
        var collection=LocalizationEditorSettings.GetStringTableCollection("Actions");
        foreach(var table in collection.StringTables){table.AddEntry("alphawolf.lesson."+key,value);EditorUtility.SetDirty(table);}
        EditorUtility.SetDirty(collection.SharedData);
        return new LocalizedString("Actions","alphawolf.lesson."+key);
    }
    static T Step<T>(string name,string label) where T:TurnSliceSO
    {var o=ScriptableObject.CreateInstance<T>();o.label=label;o.mainColor=Color.cyan;return Save(o,name);}
    static void SetDamage(ActionDataSO action,Consts.Elements element)
    {
        var f=action.GetType().GetField("damageOrHeal",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(f==null)throw new Exception("Missing damage helper");
        ((SpellDamageHeal)f.GetValue(action)).type=element;EditorUtility.SetDirty(action);
    }
    static AlphaWolfLessonAction EnemyAction(string key,string label,float amount)
    {var a=ScriptableObject.CreateInstance<AlphaWolfLessonAction>();a.ActionNameKey=Text(key,label);a.amount=amount;a.power=new PowerGetter(1);a.auxPower=new PowerGetter(1);a.power.powerFactor=new LeveledStats<float>(1);a.auxPower.powerFactor=new LeveledStats<float>(1);return Save(a,key);}
    static MobRenderer Copy(MobRenderer source,string name,BaseMobDescriptorSO descriptor,Vector3Int pos,Consts.UnitGroup group)
    {
        var obj=UnityEngine.Object.Instantiate(source.gameObject);obj.name=name;obj.transform.position=pos+new Vector3(.5f,0,.5f);
        var r=obj.GetComponent<MobRenderer>();r.data.baseDescriptor=descriptor;r.data.unitGroup=group;r.handleDataInit=true;r.isBoss=false;
        return r;
    }
    [MenuItem("miniRAID/AlphaWolf/Open playable lesson")]
    public static void Open(){if(EditorApplication.isPlaying)throw new Exception("Stop Play first");EditorSceneManager.OpenScene(Scene);}
    [MenuItem("miniRAID/AlphaWolf/Build lesson (one time)")]
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene)!=null)throw new Exception("Lesson exists; edit through Editor instead of rebuilding");
        System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
        var services=EditorSceneManager.OpenScene("Assets/Scenes/CombatBase.unity");
        var arena=EditorSceneManager.OpenScene("Assets/Scenes/OpenTest/AlphaWolf.unity",OpenSceneMode.Additive);
        SceneManager.MergeScenes(arena,services);
        foreach(var loader in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(x=>x.GetType().Name=="CombatSceneLoader"))UnityEngine.Object.DestroyImmediate(loader);
        var renderers=UnityEngine.Object.FindObjectsByType<MobRenderer>(FindObjectsSortMode.None);
        var warrior=renderers.Single(x=>x.name=="1_Warrior");var wolf=renderers.Single(x=>x.name=="AlphaWolf");
        var wd=Clone((EnemyMobDescriptorSO)wolf.data.baseDescriptor,"LessonWolf");wd.rootAgent=null;wd.listenerSOs.Clear();wd.baseEnemyStats.MaxHP=480;wd.gridBody=new PointCollider();wolf.data.baseDescriptor=wd;wolf.transform.position=new Vector3(13.5f,1,15.5f);
        var melee=Clone((MobDescriptorSO)warrior.data.baseDescriptor,"LessonGuardian");melee.nickname="Guardian";warrior.data.baseDescriptor=melee;warrior.name="Guardian";warrior.transform.position=new Vector3(16.5f,1,15.5f);
        var mageData=Clone(Load<MobDescriptorSO>("Assets/GameContent/Allies/Characters/D4.asset"),"LessonMage");mageData.nickname="Fire Mage";mageData.race=null;mageData.job=null;mageData.actionSOs=Array.Empty<ActionSOEntry>();
        var staff=Clone((StaffSO)mageData.mainWeaponSO,"SparkStaff");
        var bolt=Clone((ActionDataSO)staff.regularAttack.data,"FireBolt");bolt.ActionNameKey=Text("firebolt","Fire Bolt");SetDamage(bolt,Consts.Elements.Fire);
        var blast=Clone((ActionDataSO)staff.specialAttack.data,"FireBlast");blast.ActionNameKey=Text("fireblast","Fire Blast");SetDamage(blast,Consts.Elements.Fire);
        staff.nameKey=Text("staff","Spark Staff");
        staff.ToolTipKey=Text("staff-description","Tutorial fire staff. Fire Blast causes resonance: both staff attacks become unavailable until two recovery stages have passed.");
        bolt.DescriptionKey=Text("bolt-description","Deals {HitPower} Fire damage to one legal target.");
        blast.DescriptionKey=Text("blast-description","Deals {HitPower} Fire damage to one legal target. Resonance: this staff becomes unavailable until two recovery stages have passed.");
        staff.regularAttack=new ActionSOEntry{data=bolt,level=0};staff.specialAttack=new ActionSOEntry{data=blast,level=0};mageData.mainWeaponSO=staff;mageData.listenerSOs.Add(Load<MobListenerSO>("Assets/GameContent/Allies/Passives/GeneralResourceListeners/Mana.asset"));
        var mage=Copy(warrior,"Fire Mage",mageData,new Vector3Int(18,1,17),Consts.UnitGroup.Player);
        var healerData=Clone(mageData,"LessonHealer");healerData.nickname="Healer";healerData.mainWeaponSO=null;healerData.baseStats.VIT=12;
        var heal=Load<ActionDataSO>("Assets/GameContent/Allies/Actions/Essentials/Test/BasicHealing.asset");
        if(heal==null)throw new Exception("BasicHeal asset not found");
        healerData.actionSOs=new[]{new ActionSOEntry{data=heal,level=0}};
        var healer=Copy(warrior,"Healer",healerData,new Vector3Int(18,1,13),Consts.UnitGroup.Player);
        var pillarData=Clone(wd,"LessonPillar");pillarData.nickname="Breakable pillar";pillarData.baseEnemyStats.MaxHP=40;pillarData.movable=false;pillarData.mainWeaponSO=null;pillarData.actionSOs=Array.Empty<ActionSOEntry>();
        var pillars=new List<MobRenderer>();
        foreach(var pos in new[]{new Vector3Int(15,1,14),new Vector3Int(15,1,16)})
        {
            var p=Copy(warrior,"Pillar "+(pillars.Count+1),pillarData,pos,Consts.UnitGroup.Enemy);
            foreach(var r in p.GetComponentsInChildren<SpriteRenderer>())r.enabled=false;
            var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="Stone pillar";stone.transform.SetParent(p.transform,false);stone.transform.localPosition=new Vector3(0,.8f,0);stone.transform.localScale=new Vector3(.7f,1.6f,.7f);UnityEngine.Object.DestroyImmediate(stone.GetComponent<Collider>());pillars.Add(p);
        }
        var lesson=new GameObject("AlphaWolf Lesson").AddComponent<AlphaWolfLesson>();lesson.wolfRenderer=wolf;lesson.partyRenderers=new[]{warrior,mage,healer};lesson.pillarRenderers=pillars.ToArray();
        lesson.bite=EnemyAction("bite","Wolf Bite",12);lesson.sweep=EnemyAction("sweep","Wolf Sweep",26);lesson.charge=EnemyAction("charge","Wolf Charge",32);lesson.roar=EnemyAction("roar","Wolf Roar",38);lesson.breakPillar=EnemyAction("pillar","Shatter Pillar",1000);
        var start=Step<StartTurnTurnSliceSO>("Start","Round");var prep=Step<AlphaWolfLessonStep>("Prepare","Wolf telegraph");prep.prepare=true;
        var player=Step<CommonPlayerTurnSliceSO>("Player","[ PLAYER ]");
        var resolve=Step<AlphaWolfLessonStep>("Resolve","Wolf resolves telegraph");resolve.prepare=false;
        var auto=Step<AutoAttackTurnSliceSO>("Auto","Auto Attack");auto.SkipUserInput=true;
        var recovery=Step<RecoveryTurnSliceSO>("Recovery","Recovery");
        var end=Step<EndTurnTurnSliceSO>("End","End round");
        var generator=ScriptableObject.CreateInstance<AlphaWolfTurnGenerator>();generator.turnSlices=new List<TurnSliceSO>{start,recovery,prep,player,player,player,auto,resolve,end};Save(generator,"Timeline");
        UnityEngine.Object.FindFirstObjectByType<TurnSchedulerComponent>().scheduler=generator;
        foreach(var asset in AssetDatabase.FindAssets("",new[]{Dir}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadMainAssetAtPath).Where(x=>x!=null))EditorUtility.SetDirty(asset);
        foreach(var m in new[]{wolf,warrior}){EditorUtility.SetDirty(m);PrefabUtility.RecordPrefabInstancePropertyModifications(m);}
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(services,Scene);
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(Scene,true)}).ToArray();
        return Scene;
    }
}
#endif
