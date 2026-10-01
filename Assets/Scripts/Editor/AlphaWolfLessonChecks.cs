#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using miniRAID.AlphaWolfPlaytest;
public static class AlphaWolfLessonChecks
{
    public static string Run()
    {
        int checks=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        var origin=new Vector3Int(13,1,15);
        foreach(var delta in new[]{new Vector3Int(5,0,2),new Vector3Int(-5,0,2),new Vector3Int(2,0,-5),new Vector3Int(0,0,5),new Vector3Int(5,0,0),Vector3Int.zero})
        {
            var target=origin+delta;var line=AlphaWolfGeometry.ChargeLine(origin,target);
            Check(line.Count==Math.Abs(delta.x)+Math.Abs(delta.z),"Charge path length");
            Check(line.Count==line.Distinct().Count(),"Duplicate charge cells");
            var previous=origin;
            foreach(var cell in line){Check((cell-previous).sqrMagnitude==1,"Charge skipped a possible blocker");previous=cell;}
            Check(previous==target,"Charge endpoint");
        }
        foreach(var direction in new[]{Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)})
        {
            var cells=AlphaWolfGeometry.Sweep(origin,origin+direction*5);
            Check(cells.Count==9,"Sweep must have nine unique preview cells");
            Check(!cells.Contains(origin),"Sweep includes caster");
            Check(cells.All(c=>c.y==origin.y),"Sweep changed elevation");
            Check(cells.Contains(origin+direction*3),"Sweep front edge missing");
            Check(!cells.Contains(origin-direction),"Sweep hits behind");
        }
        Check(AlphaWolfGeometry.ChargeLine(origin,new Vector3Int(18,1,17)).Contains(new Vector3Int(15,1,16)),"First charge must intersect tutorial pillar");
        var path="Assets/GameContent/Playtests/AlphaWolf/";
        var mage=AssetDatabase.LoadAssetAtPath<miniRAID.MobDescriptorSO>(path+"LessonMage.asset");
        Check(mage.job==null && mage.race==null,"Tutorial must not inherit Astrologer mechanics");
        Check(((miniRAID.Weapon.StaffSO)mage.mainWeaponSO).regenTime==2,"Spark staff resonance differs from design");
        var wolf=AssetDatabase.LoadAssetAtPath<miniRAID.EnemyMobDescriptorSO>(path+"LessonWolf.asset");
        Check(wolf.rootAgent==null,"Legacy boss AI still attached");
        Check(UnityEditor.EditorBuildSettings.scenes.Any(x=>x.enabled && x.path.EndsWith("/AlphaWolfPlaytest.unity")),"Restart scene not in build settings");
        var timeline=AssetDatabase.LoadAssetAtPath<AlphaWolfTurnGenerator>(path+"Timeline.asset");
        var stamp=new miniRAID.TurnSchedule.Timestamp();stamp.currentTurnID=1;
        for(int round=1;round<=6;round++)
        {
            var queue=timeline.GetNewTurn(ref stamp).ToList();
            var players=queue.Select((x,i)=>(x,i)).Where(x=>x.x.data is AlphaWolfPlayerSegment).ToArray();
            var resolve=queue.FindIndex(x=>x.data is AlphaWolfLessonStep step && !step.prepare && !step.finishPhase);
            var finish=queue.FindIndex(x=>x.data is AlphaWolfLessonStep step && step.finishPhase);
            Check(players.Length==2,"Phase must contain exactly two total player segments");
            Check(!ReferenceEquals(players[0].x,players[1].x),"Player segments share runtime locking state");
            Check(resolve>players[0].i && (round%3==0?resolve>players[1].i:resolve<players[1].i),"1T/2T resolution boundary");
            Check(finish>players[1].i && finish>resolve,"Bite overlaps windup");
            Check(queue.Count(x=>x.data is miniRAID.TurnSchedule.RecoveryTurnSliceSO)==(round==1?2:1),"Recovery count / first wake-up");
            Check(queue.All(x=>x.metadata.timestamp.currentTurnID==round),"Queue timestamps");
        }
        return checks+" checks passed";
    }
}
#endif
