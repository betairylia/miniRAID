using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Text;

namespace miniRAID.UIElements
{
    public class UnitMenuController
    {
        private readonly VisualTreeAsset unitMenuItemTemplate;
        private readonly ListView listView;
        private readonly VisualElement masterElement;
        private readonly VisualElement toolTipContainer;
        private readonly Label toolTipLabel;
        private readonly MobDetailsController mobDetailsController;
        private readonly MobInfoController mobInfoController;
        private readonly miniRAID.UI.GridUI ui;

        private List<UIMenuEntry> currentEntries;
        public IReadOnlyList<UIMenuEntry> Entries => currentEntries;
        private Dictionary<string, int> shortcutIndexMap;
        private int lastSelectedIndex = -1;

        public struct UIMenuEntry
        {
            public readonly string text;
            public readonly IEnumerator action;
            public readonly IEnumerator onFinished;
            public readonly bool useDefaultToolTip;
            public readonly string toolTip;
            public readonly System.Action onPointerEnter;
            public readonly System.Action onPointerLeave;
            public readonly string keycode;
            public readonly RuntimeAction runtimeAction;
            public readonly MobRenderer source;
            public readonly bool gameplayAction;

            public UIMenuEntry(string text, IEnumerator action = null, IEnumerator onFinished = null,
                              bool useDefaultToolTip = false, string toolTip = "", 
                              System.Action onPointerEnter = null, System.Action onPointerLeave = null,
                              string keycode = null, RuntimeAction runtimeAction = null, MobRenderer source = null, bool gameplayAction = true)
            {
                this.gameplayAction = gameplayAction;
                this.text = text;
                this.action = action;
                this.onFinished = onFinished;
                this.useDefaultToolTip = useDefaultToolTip;
                this.toolTip = toolTip;
                this.onPointerEnter = onPointerEnter;
                this.onPointerLeave = onPointerLeave;
                this.keycode = keycode;
                this.runtimeAction = runtimeAction;
                this.source = source;
            }

            public bool IsPerformable
            {
                get
                {
                    if (source != null && runtimeAction != null)
                    {
                        return source.data.CheckIsActionPerformable(runtimeAction);
                    }
                    return true;
                }
            }
        }

        public UnitMenuController(VisualElement element, MobDetailsController mobDetailsController, MobInfoController mobInfoController)
        {
            this.masterElement = element;
            this.mobDetailsController = mobDetailsController;
            this.mobInfoController = mobInfoController;
            this.ui = Globals.ui.Instance;

            unitMenuItemTemplate = Resources.Load<VisualTreeAsset>("UI/UnitMenuEntry");
            listView = element.Q<ListView>();
            toolTipLabel = element.Q<Label>("ToolTipLabel");
            toolTipContainer = element.Q("ToolTip");

            InitializeListView();
            RegisterEvents();
        }

        private void InitializeListView()
        {
            listView.makeItem = CreateListItem;
            listView.bindItem = BindListItem;
            listView.fixedItemHeight = 18;
            listView.selectionType = SelectionType.Single;
            listView.focusable = true;
            listView.tabIndex = 0;
            
            // Ensure ListView can receive navigation events
            listView.pickingMode = PickingMode.Position;
        }

        private void RegisterEvents()
        {
            listView.selectionChanged += OnSelectionChanged;
            listView.RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            listView.RegisterCallback<ClickEvent>(OnMouseClick);
            // listView.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
        }

        private void UnregisterEvents()
        {
            listView.selectionChanged -= OnSelectionChanged;
            listView.UnregisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            listView.UnregisterCallback<ClickEvent>(OnMouseClick);
            // listView.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
        }

        private VisualElement CreateListItem()
        {
            return unitMenuItemTemplate.CloneTree();
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (currentEntries == null || index >= currentEntries.Count) return;

            var entry = currentEntries[index];
            var menuEntry = element.Q("MenuEntry");

            element.Q<Label>("ActionName").text = entry.text;
            UpdateKeyDisplay(element, entry);
            UpdateCostDisplay(element, entry);
            UpdateUsabilityState(menuEntry, entry);
            
            // Store index in userData and register mouse hover events for automatic selection
            element.userData = index;
            element.UnregisterCallback<PointerEnterEvent>(OnItemPointerEnter);
            element.RegisterCallback<PointerEnterEvent>(OnItemPointerEnter);
        }

        private void UpdateKeyDisplay(VisualElement element, UIMenuEntry entry)
        {
            element.Q<Label>("Key").text = entry.keycode ?? "";
        }

        private void UpdateCostDisplay(VisualElement element, UIMenuEntry entry)
        {
            var apCostLabel = element.Q<Label>("APCost");
            var cooldownLabel = element.Q<Label>("Cooldown");

            if (entry.runtimeAction != null)
            {
                int apCost = GetAPCost(entry.runtimeAction);
                if (entry.runtimeAction.cooldownRemain > 0)
                {
                    apCostLabel.style.display = DisplayStyle.None;
                    cooldownLabel.style.display = DisplayStyle.Flex;
                    cooldownLabel.text = $"◷ {entry.runtimeAction.cooldownRemain}";
                }
                else
                {
                    apCostLabel.style.display = DisplayStyle.Flex;
                    apCostLabel.text = new StringBuilder().Insert(0, "o", apCost).ToString();
                    cooldownLabel.style.display = DisplayStyle.None;
                }
            }
            else
            {
                apCostLabel.style.display = DisplayStyle.None;
                cooldownLabel.style.display = DisplayStyle.None;
            }
        }

        private void UpdateUsabilityState(VisualElement menuEntry, UIMenuEntry entry)
        {
            if (entry.IsPerformable)
            {
                menuEntry.RemoveFromClassList("disabled");
                menuEntry.SetEnabled(true);
            }
            else
            {
                menuEntry.AddToClassList("disabled");
                menuEntry.SetEnabled(false);
            }
        }

        private int GetAPCost(RuntimeAction runtimeAction)
        {
            foreach (var costBound in runtimeAction.costBounds)
            {
                if (costBound.Item1.type == Cost.Type.AP)
                {
                    return costBound.Item1.value;
                }
            }
            return 0;
        }

        private void OnSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (currentEntries == null) return;
            HandleTooltipDisplay(listView.selectedIndex);
        }

        private void HandleTooltipDisplay(int selectedIndex)
        {
            ClearTooltips();

            if (selectedIndex >= 0 && selectedIndex < currentEntries.Count)
            {
                var entry = currentEntries[selectedIndex];
                if (entry.useDefaultToolTip)
                {
                    toolTipContainer.style.visibility = Visibility.Visible;
                    toolTipLabel.text = entry.toolTip;
                }
                else
                {
                    entry.onPointerEnter?.Invoke();
                }
            }

            lastSelectedIndex = selectedIndex;
        }

        private void ClearTooltips()
        {
            if (lastSelectedIndex >= 0 && lastSelectedIndex < currentEntries.Count)
            {
                var lastEntry = currentEntries[lastSelectedIndex];
                if (!lastEntry.useDefaultToolTip)
                {
                    lastEntry.onPointerLeave?.Invoke();
                }
            }
            toolTipContainer.style.visibility = Visibility.Hidden;
        }

        private void OnNavigationSubmit(NavigationSubmitEvent evt)
        {
            Debug.Log($"OnNavigationSubmit received - selectedIndex: {listView.selectedIndex}");
            if (ExecuteSelectedAction())
            {
                evt.StopImmediatePropagation();
            }
        }


        private void OnMouseClick(ClickEvent evt)
        {
            if (listView.selectedIndex >= 0 && listView.selectedIndex < currentEntries.Count)
            {
                if (ExecuteSelectedAction())
                {
                    evt.StopImmediatePropagation();
                }
                // ExecuteSelectedAction();
            }
        }

        private void OnItemPointerEnter(PointerEnterEvent evt)
        {
            var hoveredElement = evt.currentTarget as VisualElement;
            if (hoveredElement?.userData is int index)
            {
                listView.selectedIndex = index;
            }
        }

        private void OnInitialGeometryChanged(GeometryChangedEvent evt)
        {
            listView.UnregisterCallback<GeometryChangedEvent>(OnInitialGeometryChanged);
            // TODO: FIXME: Mouse clicks sometimes don't focus ListView on menu open, keyboard/gamepad always works
            // Likely cause: Mouse click event on opener element interferes with focus transfer timing
            listView.Focus();
            HandleTooltipDisplay(0);
        }

        // private void OnNavigationMove(NavigationMoveEvent evt)
        // {
        //     if (currentEntries == null || currentEntries.Count == 0) return;
        //
        //     int currentIndex = listView.selectedIndex;
        //     int newIndex = currentIndex;
        //
        //     switch (evt.direction)
        //     {
        //         case NavigationMoveEvent.Direction.Up:
        //             newIndex = (currentIndex - 1 + currentEntries.Count) % currentEntries.Count;
        //             break;
        //         case NavigationMoveEvent.Direction.Down:
        //             newIndex = (currentIndex + 1) % currentEntries.Count;
        //             break;
        //         default:
        //             return;
        //     }
        //
        //     if (newIndex != currentIndex)
        //     {
        //         listView.selectedIndex = newIndex;
        //         evt.StopImmediatePropagation();
        //     }
        // }

        private bool ExecuteSelectedAction() => TryExecuteEntry(listView.selectedIndex);

        public bool TryExecuteEntry(int index)
        {
            if (!IsMenuShown || ui.isInAnimation || currentEntries == null || index < 0 || index >= currentEntries.Count)
                return false;

            var entry = currentEntries[index];
            
            if (entry.IsPerformable && entry.action != null)
            {
                return ui.WaitFor(entry.action, entry.onFinished);
            }
            
            return false;
        }

        public void ShortCut(string keyCode)
        {
            if (!IsMenuShown || shortcutIndexMap == null) return;

            if (shortcutIndexMap.TryGetValue(keyCode, out int index))
            {
                if (index >= 0 && index < currentEntries.Count && currentEntries[index].IsPerformable)
                {
                    listView.selectedIndex = index;
                    ExecuteSelectedAction();
                }
            }
        }

        public void PrepareMenu(List<UIMenuEntry> entries)
        {
            currentEntries = entries ?? new List<UIMenuEntry>();
            BuildShortcutMap();
            
            listView.itemsSource = currentEntries;
            listView.RefreshItems();
            
            ShowMenu();
            
            if (currentEntries.Count > 0)
            {
                listView.selectedIndex = 0;
            }
        }

        private void BuildShortcutMap()
        {
            shortcutIndexMap = new Dictionary<string, int>();
            for (int i = 0; i < currentEntries.Count; i++)
            {
                var entry = currentEntries[i];
                if (!string.IsNullOrEmpty(entry.keycode) && entry.IsPerformable)
                {
                    shortcutIndexMap[entry.keycode] = i;
                }
            }
        }

        public void ShowMenu()
        {
            if (currentEntries?.Count > 0)
            {
                listView.RegisterCallback<GeometryChangedEvent>(OnInitialGeometryChanged, TrickleDown.NoTrickleDown);
                masterElement.style.visibility = Visibility.Visible;
            }
        }

        public void HideMenu()
        {
            masterElement.style.visibility = Visibility.Hidden;
            ClearTooltips();
            lastSelectedIndex = -1;
        }

        public bool IsMenuShown => masterElement.style.visibility == Visibility.Visible;

        public void ClearMenu()
        {
            ClearTooltips();
            currentEntries = new List<UIMenuEntry>();
            shortcutIndexMap = new Dictionary<string, int>();
            listView.itemsSource = currentEntries;
            lastSelectedIndex = -1;
            HideMenu();
        }

        public void Dispose()
        {
            UnregisterEvents();
        }

        public static UIMenuEntry GetActionEntry(RuntimeAction action, MobRenderer source, string keycode = null,
                                                 string nameOverride = null, IEnumerator onFinished = null)
        {
            return new UIMenuEntry(
                text: nameOverride ?? action.data.ActionName,
                action: action.RequestInUI(source.data),
                onFinished: onFinished,
                useDefaultToolTip: true,
                toolTip: action.GetFullTooltip(source.data),
                keycode: keycode,
                runtimeAction: action,
                source: source
            );
        }

        public UIMenuEntry GetEquipmentDetailsEntry(MobRenderer source, string keycode = null,
                                                   string nameOverride = null, IEnumerator onFinished = null)
        {
            return new UIMenuEntry(
                text: nameOverride ?? "Equipments",
                onFinished: onFinished,
                useDefaultToolTip: false,
                onPointerEnter: () => mobDetailsController.Show(source.data),
                onPointerLeave: () => mobDetailsController.Hide(),
                keycode: keycode,
                source: source
            );
        }

        public UIMenuEntry GetMobDetailsEntry(MobRenderer source, string keycode = null,
                                             string nameOverride = null, IEnumerator onFinished = null)
        {
            return new UIMenuEntry(
                text: nameOverride ?? "Info",
                onFinished: onFinished,
                useDefaultToolTip: false,
                onPointerEnter: () => mobInfoController.Show(source.data),
                onPointerLeave: () => mobInfoController.Hide(),
                keycode: keycode,
                source: source
            );
        }
    }
}
