#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using miniRAID;
using miniRAID.Spells;
using miniRAID.TurnSchedule;
using miniRAID.MobBehaviour.TurnSlices;
// Isolated presentation fixtures. They neither cast the action nor alter combat resources.
public static class TacticalPresentationChecks
{
    sealed class WallModeValidator : miniRAID.UI.TargetRequester.FourDirectionalRequestValidator
    {
        public bool mode; public GridCollider preview;
        public override bool IgnoreWall => mode;
        public override GridCollider Shape => preview;
    }
    public static string DirectionalPreview()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play required");
        var mob=Globals.backend.allMobs.First(m=>m.unitGroup==Consts.UnitGroup.Enemy && m.nickname=="AlphaWolf");
        var asset=AssetDatabase.LoadAssetAtPath<ActionDataSO<FourDirectionalTarget>>("Assets/GameContent/Enemies/SlimeBoss/MeleeAttackSet/AcidBreath.asset");
        var action=UnityEngine.Object.Instantiate(asset);
        var step=ScriptableObject.CreateInstance<SimpleUseFourDirectionalActionTurnSliceSO>();
        var oldText=Globals.ui.Instance.combatView.debugText.text;
        string result="";
        try
        {
            foreach(var ignore in new[]{false,true})
            {
                action.RequestValidator=new WallModeValidator{mode=ignore,preview=asset.MainShape.CloneWithNewGuid() as GridCollider};
                var slice=new SimpleUseFourDirectionalActionTurnSlice(mob,action.LeveledWrap(mob,0),step,new TurnSliceMetadata(null));
                try
                {
                    slice.ConstructRenderer();
                    var indicator=(GridColliderIndicator)slice.renderer;
                    if(indicator.losOrigin.HasValue==ignore)throw new Exception("Preview LOS policy differs from actual attack");
                    if(!ignore && indicator.losOrigin.Value!=mob.SpellCastPivot)throw new Exception("Wrong LOS origin");
                    var all=Globals.backend.GetColliderMapIntersect(indicator.collider).ToArray();
                    var visible=all.Where(p=>ignore || Globals.backend.HasLineOfSight(mob.SpellCastPivot,p+Globals.half)).ToArray();
                    result+=$"ignoreWalls={ignore}: {visible.Length}/{all.Length} cells; ";
                    foreach(var target in Globals.backend.allMobs.Where(m=>m.unitGroup==Consts.UnitGroup.Player))
                    {
                        var actual=indicator.collider.Overlaps(target.Collider) && (ignore || Globals.backend.HasLineOfSight(mob.SpellCastPivot,target.Position+Vector3.one*.5f));
                        var predicted=visible.Any(p=>new PointCollider{Position=p}.Overlaps(target.Collider));
                        if(actual!=predicted)throw new Exception("Preview/actual mismatch for "+target.nickname);
                    }
                }
                finally {slice.renderer?.Destroy();slice.OnRemove(Globals.combatMgr.Instance);}
            }
        }
        finally{Globals.ui.Instance.combatView.debugText.text=oldText;UnityEngine.Object.Destroy(action);UnityEngine.Object.Destroy(step);}
        return result+"LOS and current-party coverage checks passed";
    }
}
#endif
