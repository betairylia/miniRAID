using System.Collections;
using miniRAID.ActionHelpers;
using miniRAID.Spells;
namespace miniRAID.AlphaWolfPlaytest
{
    // Enemy encounter actions still use normal damage, armor, hit, death and log processing.
    public class AlphaWolfLessonAction : ActionDataSO<SingleMobTarget>
    {
        public float amount=20;
        public Consts.Elements element=Consts.Elements.Slash;
        public override IEnumerator OnPerform(RuntimeAction<SingleMobTarget> action, MobData mob, SingleMobTarget target)
        {
            if(target.Target==null || target.Target.isDead) yield break;
            var damage=new SpellDamageHeal { type=element,
                power=new FloatModifier {type=FloatModifierType.Expression,expression=amount},
                hit=new FloatModifier {type=FloatModifierType.Expression,expression=10000f},
                crit=new FloatModifier {type=FloatModifierType.Expression,expression=0f} };
            yield return new JumpIn(damage.Do(action,mob,target.Target));
        }
    }
}
