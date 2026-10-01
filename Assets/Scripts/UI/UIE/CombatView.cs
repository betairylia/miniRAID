using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace miniRAID.UIElements
{
    public enum UINavigationPolicy
    {
        DisableAll,           // Block all navigation (FreeView default)
        EnableMenuOnly,       // Only UnitMenu can navigate (UnitMenu)
        EnableButtonsOnly,    // Only MessagePanel buttons (future use)
        EnableAll            // Full navigation (future use)
    }
    [RequireComponent(typeof(UIDocument))]
    public class CombatView : MonoBehaviour
    {
        public UnitMenuController menu;
        public BossStatsController bossStats;
        public CombatStatsController combatStats;
        public MessagePoolController messagePool;
        public MobDetailsController mobDetails;
        public MobInfoController mobInfo;
        public UnitBarController unitBar;

        public Label battlePreview, centerTitleText, importantText, schedulerPlaceholder, currentTurnPlaceholder, debugText;
        public VisualElement centerTitlePanel, importantPanel, importantPanelProgressBar;

        private UIDocument uiDocument;
        [SerializeField] private Vector2Int referenceResolution;
        [SerializeField] private int minimumScale;
        [SerializeField] private Canvas mainCanvas;
        
        // Navigation policy management
        private EventCallback<NavigationMoveEvent> currentNavigationHandler;
        private EventCallback<NavigationSubmitEvent> currentNavigationSubmitHandler;

        private void OnEnable()
        {
            menu?.Dispose();
            // The UXML is already instantiated by the UIDocument component
            uiDocument = GetComponent<UIDocument>();

            // uiDocument.rootVisualElement.RegisterCallback<NavigationMoveEvent>(evt =>
            // {
            //     evt.StopImmediatePropagation();
            // });

            mobDetails = new MobDetailsController(
                uiDocument.rootVisualElement.Q("MobDetails")
            );
            
            mobInfo = new MobInfoController(
                uiDocument.rootVisualElement.Q("MobInfo")
            );

            VisualElement unitMenu = uiDocument.rootVisualElement.Q("UnitMenuContainer");
            menu = new UnitMenuController(unitMenu, mobDetails, mobInfo);
            
            // TODO: Multiple boss stats panels
            bossStats = new BossStatsController(
                uiDocument.rootVisualElement.Q("BossStats")
            );

            combatStats = new CombatStatsController(
                uiDocument.rootVisualElement.Q("CombatStats-global")
            );

            battlePreview = uiDocument.rootVisualElement.Q<Label>("BattlePreview");
            schedulerPlaceholder = uiDocument.rootVisualElement.Q<Label>("ActionBar");
            currentTurnPlaceholder = uiDocument.rootVisualElement.Q<Label>("CurrentTurn");
            centerTitlePanel = uiDocument.rootVisualElement.Q("MiddleTitle");
            centerTitleText = centerTitlePanel.Q<Label>("TitleText");
            debugText = uiDocument.rootVisualElement.Q<Label>("TempInformation");
            
            importantPanel = uiDocument.rootVisualElement.Q("ImportantTextPanel");
            importantText = importantPanel.Q<Label>("ImportantText");
            importantPanelProgressBar = importantPanel.Q("ProgressBar");

            messagePool = new MessagePoolController(
                uiDocument.rootVisualElement.Q("MessagePanel")
            );

            unitBar = new UnitBarController(
                uiDocument.rootVisualElement.Q("UnitBar")
            );
        }

        public void ShowBattlePreview(string text)
        {
            battlePreview.text = $"{text}";
            battlePreview.style.visibility = Visibility.Visible;
        }

        public void HideBattlePreview()
        {
            battlePreview.style.visibility = Visibility.Hidden;
        }
        
        public void ShowCenterTitle(string text)
        {
            centerTitleText.text = $"{text}";
            centerTitlePanel.style.visibility = Visibility.Visible;
        }
        
        public void HideCenterTitle()
        {
            centerTitlePanel.style.visibility = Visibility.Hidden;
        }
        
        public void ShowImportantText(string text)
        {
            importantText.text = $"{text}";
            importantPanel.style.visibility = Visibility.Visible;
        }
        
        public void HideImportantText()
        {
            importantPanel.style.visibility = Visibility.Hidden;
        }

        public IEnumerator ShowImportantText(string text, float time)
        {
            ShowImportantText(text);

            float timer = time;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                importantPanelProgressBar.style.width = Length.Percent(100.0f * timer / time);
                yield return null;
            }
            
            HideImportantText();
        }

        public void BindAsBoss(MobRenderer mobRenderer)
        {
            bossStats.Register(mobRenderer);
        }
        
        /// <summary>
        /// Configure UI Toolkit navigation behavior based on current game state
        /// </summary>
        /// <param name="policy">Navigation policy to apply</param>
        public void SetNavigationPolicy(UINavigationPolicy policy)
        {
            // Remove existing handler
            if (currentNavigationHandler != null)
            {
                uiDocument.rootVisualElement.UnregisterCallback(
                    currentNavigationHandler, TrickleDown.TrickleDown);
                uiDocument.rootVisualElement.UnregisterCallback(
                    currentNavigationSubmitHandler, TrickleDown.TrickleDown);
                currentNavigationHandler = null;
                currentNavigationSubmitHandler = null;
            }
            
            // Apply new policy
            switch (policy)
            {
                case UINavigationPolicy.DisableAll:
                    currentNavigationHandler = BlockAllNavigation;
                    currentNavigationSubmitHandler = BlockAllNavigation;
                    break;
                case UINavigationPolicy.EnableMenuOnly:
                    currentNavigationHandler = AllowMenuNavigationOnly;
                    currentNavigationSubmitHandler = AllowMenuNavigationOnly;
                    break;
                case UINavigationPolicy.EnableButtonsOnly:
                    currentNavigationHandler = AllowButtonsNavigationOnly;
                    currentNavigationSubmitHandler = AllowButtonsNavigationOnly;
                    break;
                case UINavigationPolicy.EnableAll:
                    // No handler needed - allow all navigation
                    break;
            }
            
            if (currentNavigationHandler != null)
            {
                uiDocument.rootVisualElement.RegisterCallback(
                    currentNavigationHandler, TrickleDown.TrickleDown);
                uiDocument.rootVisualElement.RegisterCallback(
                    currentNavigationSubmitHandler, TrickleDown.TrickleDown);
            }
            
            // Blur currently focused element for clean state transition
            uiDocument.rootVisualElement.focusController.focusedElement?.Blur();
        }
        
        private void BlockAllNavigation<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            evt.StopPropagation();
            evt.PreventDefault();
        }
        
        private void AllowMenuNavigationOnly<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            // Check if navigation is within menu container
            var target = evt.target as VisualElement;
            var menuContainer = uiDocument.rootVisualElement.Q("UnitMenuContainer");
            
            if (target == null || !IsElementWithinContainer(target, menuContainer))
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }
        
        private void AllowButtonsNavigationOnly<T>(EventBase<T> evt) where T : EventBase<T>, new()
        {
            // Check if navigation is within message panel buttons
            var target = evt.target as VisualElement;
            var buttonsContainer = uiDocument.rootVisualElement.Q("MessagePanel").Q("Buttons");
            
            if (target == null || !IsElementWithinContainer(target, buttonsContainer))
            {
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }
        
        /// <summary>
        /// Check if target element is within or is the container
        /// </summary>
        private bool IsElementWithinContainer(VisualElement target, VisualElement container)
        {
            if (container == null || target == null) return false;
            
            VisualElement current = target;
            while (current != null)
            {
                if (current == container) return true;
                current = current.parent;
            }
            return false;
        }

        public void Update()
        {
            bossStats.Update();
            UpdateSize();
        }

        [ContextMenu("UpdateSize")]
        private void UpdateSize()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            int widthRatio = Mathf.FloorToInt(Screen.width / referenceResolution.x);
            int heightRatio = Mathf.FloorToInt(Screen.height / referenceResolution.y);

            int scale = Mathf.Max(minimumScale, Mathf.Min(widthRatio, heightRatio));

            uiDocument.panelSettings.scale = scale;
            mainCanvas.scaleFactor = scale;
        }
    }
}