using System.Collections;
using System.Collections.Generic;
using miniRAID.UIElements;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace miniRAID.Weapon
{
    [CreateAssetMenu(menuName = "Weapon/Staff")]
    public class StaffSO : WeaponSO
    {
        public StaffSO() : base()
        {
            wpType = WeaponType.Staff;
        }
        
        public ActionSOEntry specialAttack;
        public int regenTime;

        public override MobListener Wrap(MobData parent)
        {
            return new Staff(parent, this);
        }
    }

    public class Staff : Weapon
    {
        public StaffSO staffData => (StaffSO)data;

        [SerializeField] private int regenTimer = 0;
        [SerializeField] RuntimeAction RspecialAttack;
        
        public Staff(MobData parent, WeaponSO data) : base(parent, data)
        {
            regenTimer = 0;
        }
        
        public override void OnAttach(MobData mob)
        {
            base.OnAttach(mob);
            
            RspecialAttack = mob.AddAction(staffData.specialAttack);

            mob.OnActionPostcast.AddListener(OnActionPostCast);
            mob.OnRecoveryStage.AddListener(OnRecoveryStage);
        }
        
        public override void OnRemove(MobData mob)
        {
            mob.OnActionPostcast.RemoveListener(OnActionPostCast);
            mob.OnRecoveryStage.RemoveListener(OnRecoveryStage);
            
            base.OnRemove(mob);
        }

        private IEnumerator OnRecoveryStage(MobData mob)
        {
            if (regenTimer > 0)
            {
                regenTimer -= 1;
            }
            yield break;
        }
        
        private IEnumerator OnActionPostCast(MobData mob, RuntimeAction ract, Spells.SpellTarget target)
        {
            if(ract.data == RspecialAttack.data)
            {
                regenTimer = staffData.regenTime;
            }
            yield break;
        }

        protected override void OnQueryActions(MobData mob, HashSet<RuntimeAction> actions)
        {
            base.OnQueryActions(mob, actions);
            if (regenTimer > 0)
            {
                actions.RemoveWhere(x => x.data == staffData.regularAttack.data);
                actions.RemoveWhere(x => x.data == staffData.specialAttack.data);
            }
        }

        public override RuntimeAction GetRegularAttackSpell()
        {
            if (regenTimer > 0)
            {
                return null;
            }
            return base.GetRegularAttackSpell();
        }

        public override string GetInformationString()
        {
            return regenTimer <= 0 ? $"{staffData.specialAttack.data.ActionName}" : $"共鸣：剩余{regenTimer}";
        }
        
        public override string GetActionMechanicTooltip(RuntimeAction action) =>
            action==RspecialAttack ? GetWeaponSpecialAttackTooltip() : null;

        public override void ShowInUI(EquipmentController ui)
        {
            base.ShowInUI(ui);

            if (RspecialAttack != null)
            {
                var skillInfo = ui.specialAttackTemplate.CloneTree();
                RspecialAttack.ShowInUI(skillInfo.Q("skillContainer"));
                
                skillInfo.Q<Label>("specialTitle").text = GetWeaponSpecialAttackTitle();
                
                string specialAttackTooltip = GetWeaponSpecialAttackTooltip();
                if (specialAttackTooltip != null)
                {
                    var specialLabel = skillInfo.Query("specialDescription").Children<Label>().First();
                    specialLabel.text = specialAttackTooltip;
                }
                
                ui.container.Add(skillInfo);
            }
        }
    }
}