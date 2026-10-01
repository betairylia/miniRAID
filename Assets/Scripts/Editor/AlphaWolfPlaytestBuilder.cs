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
    static readonly Dictionary<string,(string en,string zh)> PresentationText=new()
    {
        ["firebolt"]=("Fire Bolt","火焰箭"),
        ["fireblast"]=("Fire Blast","烈焰冲击"),
        ["staff"]=("Spark Staff","火花法杖"),
        ["staff-description"]=("A staff attuned to fire.","寄宿着火焰之力的法杖。"),
        ["bolt-description"]=("Deals {HitPower} Fire damage.","造成 {HitPower} 点火焰伤害。"),
        ["blast-description"]=("Deals {HitPower} Fire damage.","造成 {HitPower} 点火焰伤害。"),
        ["bite"]=("Wolf Bite","撕咬"),
        ["sweep"]=("Wolf Sweep","横扫"),
        ["charge"]=("Wolf Charge","冲锋"),
        ["roar"]=("Wolf Roar","咆哮"),
        ["pillar"]=("Shatter Pillar","击碎石柱"),
        ["ui-title"]=("AlphaWolf","AlphaWolf"),
        ["ui-preparing"]=("Preparing encounter…","准备战斗…"),
        ["ui-stunned"]=("Stunned · resumes its next ability after this phase.","眩晕 · 本阶段停手，随后继续原本的下一招。"),
        ["ui-sweep"]=("Sweep · purple cells are hit when this segment ends.","横扫 · 本段结束时命中紫色区域。"),
        ["ui-charge"]=("Charge · follows {0} when this segment ends. Block with a pillar or ally.","冲锋 · 本段结束时追向 {0}。可用石柱或同伴拦截。"),
        ["ui-roar"]=("Roar · {0} segments left; {1} more damage interrupts it.","咆哮 · 剩余 {0} 段；再造成 {1} 伤害可打断。"),
        ["ui-interrupted"]=("Roar interrupted · stunned through the next phase.","咆哮打断 · 眩晕至下阶段结束。"),
        ["ui-blocked"]=("Charge blocked · stunned through the next phase.","冲锋受阻 · 眩晕至下阶段结束。"),
        ["ui-resolved"]=("Ability resolved.","本次技能已结算。"),
        ["ui-cancelled"]=("Charge cancelled · marked target unavailable.","冲锋取消 · 标记目标已不可用。"),
        ["ui-segments"]=("Segments {0}/2 · choose distinct allies","行动段 {0}/2 · 选择不同队员"),
        ["ui-victory"]=("Victory","胜利"),
        ["ui-defeat"]=("Defeat","战败"),
        ["ui-restart"]=("Try again","重新挑战"),
        ["ui-round"]=("Round","回合"),
        ["ui-player"]=("Player","玩家行动"),
        ["ui-auto"]=("Auto Attack","自动攻击"),
        ["ui-recovery"]=("Recovery","恢复"),
        ["ui-end"]=("End round","回合结束")
    };
    static LocalizedString Text(string key)
    {
        var collection=LocalizationEditorSettings.GetStringTableCollection("Actions");
        var values=PresentationText[key];
        foreach(var table in collection.StringTables)
        {
            string value=table.LocaleIdentifier.Code switch
            {
                "en-US"=>values.en,"zh-CN"=>values.zh,
                _=>throw new InvalidOperationException("No lesson translation supplied for "+table.LocaleIdentifier.Code)
            };
            table.AddEntry("alphawolf.lesson."+key,value).IsSmart=value.Contains("{HitPower}");
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection.SharedData);
        return new LocalizedString("Actions","alphawolf.lesson."+key);
    }
    public static string UpdatePresentation()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before updating lesson assets");
        var collection=LocalizationEditorSettings.GetStringTableCollection("Actions");
        if(collection==null || !collection.StringTables.Any())throw new InvalidOperationException("Actions tables unavailable");
        if(collection.StringTables.Any(t=>t.LocaleIdentifier.Code!="en-US" && t.LocaleIdentifier.Code!="zh-CN"))
            throw new InvalidOperationException("Unexpected locale: supply its translation before updating");
        var labels=new Dictionary<string,string>{{"Start","round"},{"PlayerSegment","player"},{"Auto","auto"},{"Recovery","recovery"},{"End","end"},{"Prepare","title"},{"Resolve","title"},{"Finish","title"}};
        var slices=labels.Keys.ToDictionary(name=>name,name=>Load<TurnSliceSO>(Dir+"/"+name+".asset"));
        if(slices.Values.Any(x=>x==null))throw new InvalidOperationException("Lesson timeline assets unavailable");
        foreach(var key in PresentationText.Keys)Text(key);
        foreach(var pair in labels)
        {
            var slice=slices[pair.Key];
            slice.labelKey=new LocalizedString("Actions","alphawolf.lesson.ui-"+pair.Value);
            if(slice is AlphaWolfLessonStep)slice.showInUI=false;
            EditorUtility.SetDirty(slice);AssetDatabase.SaveAssetIfDirty(slice);
        }
        EditorUtility.SetDirty(collection);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
        foreach(var table in collection.StringTables)AssetDatabase.SaveAssetIfDirty(table);
        AssetDatabase.SaveAssetIfDirty(collection.SharedData);AssetDatabase.SaveAssetIfDirty(collection);
        return $"Updated {PresentationText.Count} bilingual entries and {slices.Count} lesson timeline labels";
    }
    static T Step<T>(string name,string label) where T:TurnSliceSO
    {var o=ScriptableObject.CreateInstance<T>();o.label=label;o.mainColor=Color.cyan;return Save(o,name);}
    static void SetDamage(ActionDataSO action,Consts.Elements element)
    {
        var f=action.GetType().GetField("damageOrHeal",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(f==null)throw new Exception("Missing damage helper");
        ((SpellDamageHeal)f.GetValue(action)).type=element;EditorUtility.SetDirty(action);
    }
    static AlphaWolfLessonAction EnemyAction(string key,float amount)
    {var a=ScriptableObject.CreateInstance<AlphaWolfLessonAction>();a.ActionNameKey=Text(key);a.amount=amount;a.power=new PowerGetter(1);a.auxPower=new PowerGetter(1);a.power.powerFactor=new LeveledStats<float>(1);a.auxPower.powerFactor=new LeveledStats<float>(1);return Save(a,key);}
    static MobRenderer Copy(MobRenderer source,string name,BaseMobDescriptorSO descriptor,Vector3Int pos,Consts.UnitGroup group)
    {
        var obj=UnityEngine.Object.Instantiate(source.gameObject);obj.name=name;obj.transform.position=pos+new Vector3(.5f,0,.5f);
        var r=obj.GetComponent<MobRenderer>();r.data.baseDescriptor=descriptor;r.data.unitGroup=group;r.handleDataInit=true;r.isBoss=false;
        return r;
    }
    static void ConfigureArena()
    {
        var chunk=new Backend.Map.MapChunk(Vector3Int.zero);
        for(int x=10;x<=22;x++)for(int z=10;z<=22;z++)
        {chunk.SetIsStandable(x,0,z,true);chunk.SetIsSolid(x,0,z,true);chunk.SetIsPassable(x,0,z,false);}
        var path=Dir+"/Arena.bytes";
        if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new Exception("Refusing to overwrite "+path);
        System.IO.File.WriteAllBytes(path,chunk.SerializeToBytes());AssetDatabase.ImportAsset(path);
        var settings=UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),settings.DefaultGroup);
        entry.address="mapchunk_alphawolf-playtest_0_0_0";
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);AssetDatabase.SaveAssetIfDirty(settings.DefaultGroup);
        UnityEngine.Object.FindFirstObjectByType<Utils.SceneConfig>().mapName="alphawolf-playtest";
        var renderer=UnityEngine.Object.FindFirstObjectByType<Backend.Map.MapRenderer>();
        renderer.solidStandableBlockColor=renderer.standableBlockColor=new Color(.48f,.58f,.46f,1);
        foreach(var stale in renderer.GetComponentsInChildren<MeshRenderer>().Where(x=>x.name.StartsWith("Chunk_")))UnityEngine.Object.DestroyImmediate(stale.gameObject);
    }
    [MenuItem("miniRAID/AlphaWolf/Open playable lesson")]
    public static void Open(){if(EditorApplication.isPlaying)throw new Exception("Stop Play first");EditorSceneManager.OpenScene("Assets/Scenes/CombatBase.unity");EditorSceneManager.OpenScene(Scene,OpenSceneMode.Additive);}
    [MenuItem("miniRAID/AlphaWolf/Build lesson (one time)")]
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene)!=null)throw new Exception("Lesson exists; edit through Editor instead of rebuilding");
        System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
        var services=EditorSceneManager.OpenScene("Assets/Scenes/CombatBase.unity");
        var arena=EditorSceneManager.OpenScene("Assets/Scenes/OpenTest/AlphaWolf.unity",OpenSceneMode.Additive);
        SceneManager.SetActiveScene(arena);
        var renderers=UnityEngine.Object.FindObjectsByType<MobRenderer>(FindObjectsSortMode.None);
        var warrior=renderers.Single(x=>x.name=="1_Warrior");var wolf=renderers.Single(x=>x.name=="AlphaWolf");
        var wd=Clone((EnemyMobDescriptorSO)wolf.data.baseDescriptor,"LessonWolf");wd.rootAgent=null;wd.listenerSOs.Clear();wd.baseEnemyStats.MaxHP=900;wd.gridBody=new PointCollider();wolf.data.baseDescriptor=wd;wolf.transform.position=new Vector3(13.5f,1,15.5f);
        var melee=Clone((MobDescriptorSO)warrior.data.baseDescriptor,"LessonGuardian");melee.nickname="Guardian";warrior.data.baseDescriptor=melee;warrior.name="Guardian";warrior.transform.position=new Vector3(16.5f,1,15.5f);
        var mageData=Clone(Load<MobDescriptorSO>("Assets/GameContent/Allies/Characters/D4.asset"),"LessonMage");mageData.nickname="Fire Mage";mageData.race=null;mageData.job=null;mageData.actionSOs=Array.Empty<ActionSOEntry>();
        var staff=Clone((StaffSO)mageData.mainWeaponSO,"SparkStaff");
        var bolt=Clone((ActionDataSO)staff.regularAttack.data,"FireBolt");bolt.ActionNameKey=Text("firebolt");SetDamage(bolt,Consts.Elements.Fire);
        var blast=Clone((ActionDataSO)staff.specialAttack.data,"FireBlast");blast.ActionNameKey=Text("fireblast");SetDamage(blast,Consts.Elements.Fire);
        staff.nameKey=Text("staff");
        staff.ToolTipKey=Text("staff-description");
        bolt.DescriptionKey=Text("bolt-description");
        blast.DescriptionKey=Text("blast-description");
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
            foreach(var r in p.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
            var stone=GameObject.CreatePrimitive(PrimitiveType.Cube);stone.name="Stone pillar";stone.transform.SetParent(p.transform,false);stone.transform.localPosition=new Vector3(0,.8f,0);stone.transform.localScale=new Vector3(.7f,1.6f,.7f);UnityEngine.Object.DestroyImmediate(stone.GetComponent<Collider>());pillars.Add(p);
        }
        var lesson=new GameObject("AlphaWolf Lesson").AddComponent<AlphaWolfLesson>();lesson.wolfRenderer=wolf;lesson.partyRenderers=new[]{warrior,mage,healer};lesson.pillarRenderers=pillars.ToArray();
        lesson.bite=EnemyAction("bite",12);lesson.sweep=EnemyAction("sweep",26);lesson.charge=EnemyAction("charge",32);lesson.roar=EnemyAction("roar",38);lesson.breakPillar=EnemyAction("pillar",1000);
        var start=Step<StartTurnTurnSliceSO>("Start","Round");var prep=Step<AlphaWolfLessonStep>("Prepare","Wolf telegraph");prep.prepare=true;
        var player=Step<AlphaWolfPlayerSegment>("PlayerSegment","[ PLAYER ]");
        var resolve=Step<AlphaWolfLessonStep>("Resolve","Wolf resolves telegraph");resolve.prepare=false;
        var finish=Step<AlphaWolfLessonStep>("Finish","Wolf phase-end bite");finish.finishPhase=true;
        var auto=Step<AutoAttackTurnSliceSO>("Auto","Auto Attack");auto.SkipUserInput=true;
        var recovery=Step<RecoveryTurnSliceSO>("Recovery","Recovery");
        var end=Step<EndTurnTurnSliceSO>("End","End round");
        var generator=ScriptableObject.CreateInstance<AlphaWolfTurnGenerator>();generator.turnSlices=new List<TurnSliceSO>{start,recovery,prep,player,resolve,finish,auto,end};Save(generator,"Timeline");
        UnityEngine.Object.FindFirstObjectByType<TurnSchedulerComponent>().scheduler=generator;
        foreach(var asset in AssetDatabase.FindAssets("",new[]{Dir}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadMainAssetAtPath).Where(x=>x!=null))EditorUtility.SetDirty(asset);
        foreach(var m in new[]{wolf,warrior}){EditorUtility.SetDirty(m);PrefabUtility.RecordPrefabInstancePropertyModifications(m);}
        ConfigureArena();
        UpdatePresentation();
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(arena,Scene);
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(Scene,true)}).ToArray();
        return Scene;
    }
}
#endif
