using System.Collections;
using System.Collections.Generic;
using miniRAID.Spells;
using miniRAID.UI.TargetRequester;
using miniRAID.UIElements;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace miniRAID.Weapon
{
    [CreateAssetMenu(menuName = "Weapon/BaseWeaponData")]
    public class WeaponSO : EquipmentSO
    {
        public enum WeaponType
        {
            Sword,
            Spear,
            HeavyWeapon,
            Bow,
            Shield,

            Staff,
            Instrument,
            Grimoire,
            MagicItem,
            MystItem
        }

        public static string[] weaponTypeNames = new string[]
        {
            "sword",
            "spear",
            "heavyweapon",
            "bow",
            "shield",
            
            "staff",
            "instrument",
            "grimoire",
            "magicitem",
            "mystitem"
        };

        public static string GetWeaponName(WeaponType t) => weaponTypeNames[(int)t];

        [Title("Weapon", "", TitleAlignments.Centered)]
        public WeaponType wpType;

        public ActionSOEntry regularAttack;
        public Consts.Elements mainElement;

        public WeaponSO()
        {
            base.type = ListenerType.Weapon;
        }

        //public virtual void OnShowMobMenu(Mob mob, UI.UIState state, ref UI.UIMenu_UIContainer menu)
        //{
        //    // TODO: Modify UI based on weapon status
        //    menu.AddActionEntry(GetRegularAttackSpell(), mob, state, "Attack", false, OnPostAttack);
        //}

        //public virtual void OnPostAttack(Action spell, Mob mob) { }

        public override MobListenerSO Clone()
        {
            var copied = base.Clone();

            if (regularAttack.data != null)
            {
                (copied as WeaponSO).regularAttack = new ActionSOEntry() { data = Instantiate(regularAttack.data), level = regularAttack.level };
            }

            return copied;
        }

        public override MobListener Wrap(MobData parent)
        {
            return new Weapon(parent, this);
        }

        public override string GetType()
        {
            switch (wpType)
            {
                case WeaponType.Sword:
                    return "剑";
                case WeaponType.Spear:
                    return "矛";
                case WeaponType.HeavyWeapon:
                    return "重武器";
                case WeaponType.Bow:
                    return "弓";
                case WeaponType.Shield:
                    return "盾";
                case WeaponType.Staff:
                    return "法杖";
                case WeaponType.Instrument:
                    return "乐器";
                case WeaponType.Grimoire:
                    return "魔导书";
                case WeaponType.MagicItem:
                    return "魔法道具";
                case WeaponType.MystItem:
                    return "神秘道具";
                default:
                    return string.Empty;
            }
        }
    }

    public class Weapon : Equipment
    {
        public WeaponSO weaponData => (WeaponSO)data;
        public Weapon(MobData parent, WeaponSO data) : base(parent, data) { this.data = data; }
        
        [SerializeField]
        protected RuntimeAction RregularAttack;

        public override void OnAttach(MobData mob)
        {
            base.OnAttach(mob);

            RregularAttack = mob.AddAction(weaponData.regularAttack);

            mob.OnQueryActions.AddListener(OnQueryActions);
        }

        protected virtual void OnQueryActions(MobData mob, HashSet<RuntimeAction> actions)
        {
            // Remove regular attack if not mainWeapon
            if (mob.mainWeapon != this)
            {
                actions.RemoveWhere(x => x == RregularAttack);
            }
        }

        public virtual RuntimeAction GetRegularAttackSpell()
        {
            return RregularAttack;
        }

        public SpellTarget QueryTarget(MobData source)
        {
            RuntimeAction action = GetRegularAttackSpell();
            return action?.QueryAbstractTarget(source);
        }

        public virtual string GetActionMechanicTooltip(RuntimeAction action) => null;

        public virtual string GetWeaponSpecialAttackTooltip()
        {
            return Globals.localizer.L(new LocalizedString("Actions", $"weapon.{WeaponSO.GetWeaponName(weaponData.wpType)}.tooltip"));
        }
        
        public virtual string GetWeaponSpecialAttackTitle()
        {
            return Globals.localizer.L(new LocalizedString("Actions", $"weapon.{WeaponSO.GetWeaponName(weaponData.wpType)}.specialname"));
        }

        public override void ShowInUI(EquipmentController ui)
        {
            base.ShowInUI(ui);

            if (RregularAttack != null)
            {
                var skillInfo = ui.regularAttackTemplate.CloneTree();
                RregularAttack.ShowInUI(skillInfo.Q("skillContainer"));
                ui.container.Add(skillInfo);
            }

            if (Tooltip != null)
            {
                ui.descriptionContainer.style.display = DisplayStyle.Flex;
                ui.description.text = Tooltip;
            }
        }
    }
}
