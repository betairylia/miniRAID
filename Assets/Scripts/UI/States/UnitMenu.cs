using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using miniRAID.Agents;
using miniRAID.Spells;
using miniRAID.UIElements;

namespace miniRAID.UI
{
    public class UnitMenu : UIState
    {
        public enum MenuSubState
        {
            Root,
            Action,
        }

        MenuSubState subState;

        public UIMenu_UIContainer menu;
        MobRenderer currentUnit;

        bool keepCameraPosition = false;

        public override bool AllowFreeNavigation
        {
            get => false;
        }
        
        public override void ConfigureUIToolkitNavigation(CombatView combatView)
        {
            // Enable navigation only for the unit menu when it's shown
            combatView.SetNavigationPolicy(UINavigationPolicy.EnableMenuOnly);
        }

        public UnitMenu(MobRenderer mobRenderer)
        {
            stateStr = $"UnitMenu: {mobRenderer?.name}";

            currentUnit = mobRenderer;
            subState = MenuSubState.Root;

            keepCameraPosition = false;

            if (currentUnit != null)
            {
                // SpellTarget target = currentUnit.data?.FindListener<PlayerAutoAttackAgentBase>()?.GetTarget(currentUnit.data);
                currentUnit.data.SelectedInUI();

                currentUnit.GetComponentInChildren<BillboardSpriteRenderer>().Color = Color.red;

                ui.ShowMainMobStats(currentUnit);
            }
        }

        public override void OnStateEnter()
        {
            base.OnStateEnter();

            if (currentUnit != null)
            {
                currentUnit.data.RecalculateStats();

                switch(subState)
                {
                    case MenuSubState.Root:
                        PrepareTopMenu();
                        break;
                    case MenuSubState.Action:
                        PrepareSpellMenu();
                        break;
                }
            }
            else
            {
                PrepareGeneralMenu();
            }
            
            //ui.uimenu_uicontainer.Show();
        }

        public override void OnStateDestroyed()
        {
            base.OnStateDestroyed();
            if (currentUnit)
            {
                currentUnit.UpdateStatusColor();
                //ui.uimenu_uicontainer.Clear();
                ui.characterFocusVCam.Priority = 0; // Deactivate character camera

                // Set camera position to current place if keep position (used action)
                if (keepCameraPosition)
                {
                    // ui.mainVCam.Follow.position = ui.characterFocusVCam.Follow.position;
                    keepCameraPosition = false;
                }
                
                currentUnit.data.DeselectedInUI();
            }
            
            ui.combatView.menu.ClearMenu();
            ui.HideMainMobStats();
        }

        private void PrepareGeneralMenu()
        {
            List<miniRAID.UIElements.UnitMenuController.UIMenuEntry> entries = new ();
            
            entries.Add(new miniRAID.UIElements.UnitMenuController.UIMenuEntry(
                text: "Cheat",
                gameplayAction: false,
                action: OnCheat(Consts.UnitGroup.Player),
                onFinished: UIMenuPostAction(null),
                useDefaultToolTip: true,
                toolTip: "复活所有阵亡单位，并恢复所有单位20%生命值。",
                keycode: "1"
            ));
            
            // entries.Add(new miniRAID.UIElements.UnitMenuController.UIMenuEntry
            // {
            //     text = "EndTurn",
            //     action = OnAutoAttackFinishingSelected(Consts.UnitGroup.Player),
            //     onFinished = UIMenuPostAction(null),
            //     toolTip = "各可行动单位用武器攻击其目标，然后结束回合。",
            //     useDefaultToolTip = true,
            //     keycode = "R"
            // });
            
            entries.Add(new miniRAID.UIElements.UnitMenuController.UIMenuEntry(
                text: "EndTurn",
                action: EndTurnAction(),
                onFinished: UIMenuPostAction(null),
                useDefaultToolTip: true,
                toolTip: "结束回合。",
                keycode: "R"
            ));
            
            // Add "Return to Turn Start" option if there's a saved backup state
            if (SaveDataSerializer.saveSlotBackup != null)
            {
                entries.Add(new miniRAID.UIElements.UnitMenuController.UIMenuEntry(
                    text: "Return to Turn Start",
                    gameplayAction: false,
                    action: ReturnToTurnStartAction(),
                    onFinished: UIMenuPostAction(null),
                    useDefaultToolTip: true,
                    toolTip: "恢复到回合开始时的状态。",
                    keycode: "U"
                ));
            }

            ui.combatView.menu.PrepareMenu(entries);
        }

        private IEnumerator EndTurnAction()
        {
            Globals.combatMgr.Instance.MarkPlayerTurnEnd();
            yield break;
        }

        private IEnumerator ReturnToTurnStartAction()
        {
            if (SaveDataSerializer.saveSlotBackup != null)
            {
                SaveDataSerializer.BeginDeserializeEverything(SaveDataSerializer.saveSlotBackup);
                Globals.logger?.Log($"[UnitMenu] Restored to turn start using saveSlotBackup");
            }
            yield break;
        }

        private void PrepareTopMenu()
        {
            //ui.uimenu_uicontainer.Clear();

            // TODO: Move as ActionDataSO.
            //var moveAction = currentUnit.GetAction<Actions.Move>();
            //if (moveAction != null)
            //{
            //    // TODO: Check moveable ... finished ?
            //    AddActionEntry(moveAction, true);
            //}

            // Get Mob-specific Menu
            Debug.LogWarning("Mob - ShowMobSpecificMenu unimplemented");
            //currentUnit.ShowMenu(this, ui.uimenu_uicontainer);

            List<miniRAID.UIElements.UnitMenuController.UIMenuEntry> entries = new ();

            bool isUnitControllable = currentUnit.data.isControllable;
            if (isUnitControllable)
            {
                // Check if this player can act based on turn slice locking
                var lockedTurnSlice = Globals.combatMgr.Instance.GetCurrentLockedPlayerTurnSlice();
                isUnitControllable &= lockedTurnSlice?.CanPlayerAct(currentUnit.data) ?? true;
            }

            if (isUnitControllable)
            {
                //ui.uimenu_uicontainer.AddEntry("Action", () => { subState = MenuSubState.Action; PrepareSpellMenu(); }, "Show all avilable actions");
                //bool first = true;
                int currentKey = 1;
                foreach (var action in currentUnit.data.availableActions)
                {
                    if (action.data.isActivelyUsed.Eval(currentUnit.data))
                    {
                        //AddActionEntry(action, first);
                        //first = false;
                        entries.Add(miniRAID.UIElements.UnitMenuController.GetActionEntry(action, currentUnit,
                            $"{currentKey}", null, UIMenuPostAction(currentUnit)));
                        currentKey++;
                    }
                }
            }

            entries.Add(ui.combatView.menu.GetEquipmentDetailsEntry(currentUnit, "E"));
            entries.Add(ui.combatView.menu.GetMobDetailsEntry(currentUnit, "I"));

            if (isUnitControllable)
            {
                //ui.uimenu_uicontainer.AddEntry("*DEBUG", () => { Globals.debugMessage.Instance.Message("Test"); });
                //ui.uimenu_uicontainer.AddEntry("Pass", OnPassSelected());
                entries.Add(new miniRAID.UIElements.UnitMenuController.UIMenuEntry(
                    text: "Pass",
                    action: OnPassSelected(),
                    onFinished: null,
                    toolTip: "",
                    keycode: "R"
                ));
            }

            if (Consts.ApplyMask(Consts.EnemyMask(Consts.UnitGroup.Player), currentUnit.data.unitGroup))
            {
                entries.Add(new miniRAID.UIElements.UnitMenuController.UIMenuEntry(
                    text: "Aim",
                    action: AimOnTarget(currentUnit.data),
                    onFinished: UIMenuPostAction(null),
                    useDefaultToolTip: true,
                    toolTip: "令所有可以瞄准目标的单位瞄准目标。",
                    keycode: "K"
                ));
            }

            ui.combatView.menu.PrepareMenu(entries);
        }

        protected IEnumerator AimOnTarget(MobData mob)
        {
            Globals.backend.AimOnTarget(mob);
            ui.circles.UpdateAllCircles();
            yield break;
        }

        private void PrepareSpellMenu()
        {
            ui.uimenu_uicontainer.Clear();
            bool first = true;
            foreach (var action in currentUnit.data.availableActions)
            {
                if(action.data.isActivelyUsed.Eval(currentUnit.data))
                {
                    AddActionEntry(action, first);
                    first = false;
                }
            }
        }

        private void AddActionEntry(RuntimeAction action, bool isFirst)
        {
            ui.uimenu_uicontainer.AddActionEntry(
                action, currentUnit,
                null, isFirst, AutoPass(currentUnit));

            //ui.uimenu_uicontainer.AddActionEntry(
            //    action, currentUnit,
            //    // Before Animation
            //    null, //WaitForAnimation,
            //    // After Animation
            //    () => {
            //        //AnimationFinish();
            //        PrepareSpellMenu();
            //        ui.ShowMainMobStats(currentUnit);
            //        if (currentUnit.isActive == false)
            //        {
            //            OnPassSelected();
            //        }
            //    },
            //    null, isFirst
            //);
        }

        public override void OnStateExit()
        {
            base.OnStateExit();
            ui.combatView.menu.HideMenu();
        }

        public IEnumerator OnPassSelected(bool set = true)
        {
            ui.EnterState();

            if (set)
            {
                yield return new JumpIn(currentUnit.data.SetActive(false));
            }
            
            Globals.combatMgr.Instance.MarkPlayerTurnEnd();
        }

        public IEnumerator AutoPass(MobRenderer mobRenderer)
        {
            yield return new JumpIn(mobRenderer.data.TryAutoEndTurn());

            if(mobRenderer.data.isActive == false)
            {
                yield return new JumpIn(OnPassSelected(false));
            }
        }

        // public IEnumerator OnAutoAttackFinishingSelected(Consts.UnitGroup group)
        // {
        //     var mobs = Globals.backend.allMobs
        //         .Where(x => x.unitGroup == group)
        //         .OrderByDescending(x => x.DEX)
        //         .ToList();
        //     foreach (MobData mob in mobs)
        //     {
        //         if (mob.isControllable)
        //         {
        //             yield return new JumpIn(mob.AutoAttackFinish());
        //         }
        //     }
        // }

        public IEnumerator OnCheat(Consts.UnitGroup group)
        {
            var mobs = Globals.backend.allMobs
                .Where(x => x.unitGroup == group);

            foreach (MobData mob in mobs)
            {
                Consts.DamageHeal_FrontEndInput info = new Consts.DamageHeal_FrontEndInput()
                {
                    type = Consts.Elements.Heal,
                    popup = true,
                    value = 0,
                    source = mob,
                    sourceAction = mob.FindListener<RuntimeAction>(),
                    crit = 0,
                    hit = 100000000,
                    flags = Consts.DamageHealFlags.Indirect
                };

                yield return new JumpIn(mob.Revive(info));

                info.value = mob.maxHealth * 0.2f;

                yield return new JumpIn(Globals.backend.DealDmgHeal(mob, info));
            }
        }

        public IEnumerator UIMenuPostAction(MobRenderer mobRenderer)
        {
            keepCameraPosition = true;
            
            // Check if the mob dead
            if (mobRenderer == null || mobRenderer.data.isDead)
            {
                // Finish our work
                ui.EnterState();
            }
            else
            {
                yield return new JumpIn(AutoPass(mobRenderer));
            }
        }
    }
}
