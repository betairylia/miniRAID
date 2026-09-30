using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using Backend.Map;
using Cinemachine;
using miniRAID.PixelArtRenderer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PixelArtRenderer;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* TODO:
 * UI logic refinement
 * Wait for all animations
 */

namespace miniRAID.UI
{
    public class GridUI : MonoBehaviour
    {
        private DefaultInputs inputs;
        public miniRAID.UIElements.CombatView combatView;

        public Camera cinemachineBrain;
        public Cinemachine.CinemachineVirtualCamera mainVCam;
        public Cinemachine.CinemachineVirtualCamera characterFocusVCam;
        public GridShapeCursor cursor;
        public float panSpeed = 5.0f;
        
        // Camera snap settings
        [Header("Camera Snap Settings")]
        public float snapDuration = 0.5f;
        public int maxInteger = 8;
        public float yMin = 8f;
        public float yMax = 45f;
        
        private CameraSnapHelper cameraSnapHelper;
        private Coroutine snapRoutine;
        private bool wasRotatingLastFrame = false;

        [Obsolete("Use cursor.position instead.")]
        public Vector3Int currentGridPos => cursor.GridPos;

        public float gridWorldUnitSize = 1.0f;
        Stack<UIState> stateStack = new Stack<UIState>();
        public UIState currentState { get; protected set; }

        // Reserved for views communication
        public UIMenu_UIContainer uimenu_uicontainer;

        // Main mob stats panel
        public GameObject mainMobStatPanel;
        public TMPro.TextMeshProUGUI mainMobStatText;

        bool waitingAnimation;
        MobRenderer _statViewMobRenderer;
        
        // Aim Circles
        public AimCircles circles;

        public BossTargetIndicator bossTargetIndicator;

        // Animation related
        public bool isInAnimation
        {
            get
            {
                //return currentState is WaitAnimState;
                return waitingAnimation;
            }
        }
        public delegate void OnAnimationFinishCallback();
        public OnAnimationFinishCallback OnAnimationFinish;

        private void Awake()
        {
            groundPlane = new Plane(Vector3.up, Vector3.zero);
            inputs = new DefaultInputs();
            combatView = FindObjectOfType<miniRAID.UIElements.CombatView>();
            cursor = new GridShapeCursor(new PointCollider(), GridOverlay.Types.SELECTED);
            
            // Initialize camera snap helper
            cameraSnapHelper = new CameraSnapHelper()
            {
                maxInteger = this.maxInteger,
                yMin = this.yMin,
                yMax = this.yMax
            };

            // TODO: Move me to somewhere else
            (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).renderScale = Settings.retro ? 0.25f : 1.0f;

            var renderer = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).GetRenderer(0);
            var property = typeof(ScriptableRenderer).GetProperty("rendererFeatures",
                BindingFlags.NonPublic | BindingFlags.Instance);

            List<ScriptableRendererFeature> features =
                property.GetValue(renderer) as List<ScriptableRendererFeature>;

            foreach (var feature in features)
            {
                if (feature.GetType() == typeof(PixelArtRendererFeature))
                {
                    (feature as PixelArtRendererFeature).SetActive(Settings.retro);
                }
            }

            if (Settings.retro)
            {
                mainVCam.m_Lens.Orthographic = true;
                mainVCam.m_Lens.OrthographicSize = 5.0f;
            }
            else
            {
                mainVCam.m_Lens.Orthographic = false;
                mainVCam.m_Lens.FieldOfView = 15.0f;
            }
        }

        private void OnEnable()
        {
            inputs.UI.Enable();
        }

        private void OnDisable()
        {
            inputs.UI.Disable();
        }

        // Start is called before the first frame update
        void Start()
        {
            EnterState();
        }

        // Update is called once per frame
        // TODO: Separate camera controller
        void Update()
        {
            Vector3 newRotation = mainVCam.transform.eulerAngles;
            newRotation.x = 0;
            newRotation.z = 0;
            
            var inp = inputs.UI.PanCamera.ReadValue<Vector2>();
            mainVCam.Follow.Translate(Quaternion.Euler(newRotation) * new Vector3(
                inp.x * panSpeed * Time.deltaTime,
                0, 
                inp.y * panSpeed * Time.deltaTime));

            bool isRotatingThisFrame = inputs.UI.ToggleRotateCamera.ReadValue<float>() > 0.5f;
            
            if (isRotatingThisFrame)
            {
                // Stop any ongoing snap when user starts rotating
                if (snapRoutine != null)
                {
                    StopCoroutine(snapRoutine);
                    snapRoutine = null;
                }
                
                Vector2 val = inputs.UI.RotateCamera.ReadValue<Vector2>();
                var mainVCamOrbital = mainVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                mainVCamOrbital.m_XAxis.Value += val.x * 50.0f * Time.deltaTime;
                mainVCamOrbital.m_FollowOffset.y -= val.y * 10.0f * Time.deltaTime;
                mainVCamOrbital.m_FollowOffset.y =
                    Mathf.Clamp(mainVCamOrbital.m_FollowOffset.y, 8, 45);
                
                var charCamOrbital = characterFocusVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                charCamOrbital.m_XAxis.Value =
                    mainVCamOrbital.m_XAxis.Value;
                charCamOrbital.m_FollowOffset.y = mainVCamOrbital.m_FollowOffset.y;
            }
            else if (Settings.retro && wasRotatingLastFrame && snapRoutine == null)
            {
                // User just released rotation input, start snap
                snapRoutine = StartCoroutine(SnapToNearestPosition());
            }
            
            wasRotatingLastFrame = isRotatingThisFrame;
        }
        
        private IEnumerator SnapToNearestPosition()
        {
            var mainVCamOrbital = mainVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
            float offsetZ = mainVCamOrbital.m_FollowOffset.z;
            
            // Precompute valid positions for current offsetZ if needed
            cameraSnapHelper.PrecomputeForOffset(offsetZ);
            
            // Find nearest valid position
            var (targetTheta, targetY) = cameraSnapHelper.FindNearestValidPosition(
                mainVCamOrbital.m_XAxis.Value,
                mainVCamOrbital.m_FollowOffset.y,
                offsetZ);
            
            float startTheta = mainVCamOrbital.m_XAxis.Value;
            float startY = mainVCamOrbital.m_FollowOffset.y;
            float deltaTheta = Mathf.DeltaAngle(startTheta, targetTheta);
            
            float t = 0f;
            while (t < snapDuration)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0, 1, t / snapDuration);
                
                // Update main camera
                mainVCamOrbital.m_XAxis.Value = startTheta + deltaTheta * u;
                var offset = mainVCamOrbital.m_FollowOffset;
                offset.y = Mathf.Lerp(startY, targetY, u);
                mainVCamOrbital.m_FollowOffset = offset;
                
                // Update character focus camera (ignoring for now as requested)
                // var charCamOrbital = characterFocusVCam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                // charCamOrbital.m_XAxis.Value = mainVCamOrbital.m_XAxis.Value;
                // charCamOrbital.m_FollowOffset.y = mainVCamOrbital.m_FollowOffset.y;
                
                yield return null;
            }
            
            // Ensure exact final values
            mainVCamOrbital.m_XAxis.Value = targetTheta;
            var finalOffset = mainVCamOrbital.m_FollowOffset;
            finalOffset.y = targetY;
            mainVCamOrbital.m_FollowOffset = finalOffset;
            
            snapRoutine = null;
        }

        /// <summary>
        /// Enters a new UI State.
        /// </summary>
        /// <param name="state">State that wants to enter</param>
        /// <param name="deeper">
        /// If true, then previous state will not be disabled, but "suspended" / "frozen" in stack and can be activated back via BackState();
        /// otherwise previous state will be removed.</param>
        public void EnterState(UIState state, bool deeper = false)
        {
            if(currentState != null)
            {
                currentState.OnStateExit();
            }

            // Start with fresh states
            if (deeper == false)
            {
                while (stateStack.Count > 0)
                {
                    UIState s = stateStack.Pop();
                    s.OnStateDestroyed();
                }
            }

            stateStack.Push(state);
            currentState = stateStack.Peek();
            
            // Configure UI Toolkit navigation for this state
            currentState.ConfigureUIToolkitNavigation(combatView);
            
            currentState.OnStateEnter();
            Debug.Log($"Entered {state.stateStr}");
        }

        /// <summary>
        /// Enters (Backs to) the base state: free-view state.
        /// </summary>
        public void EnterState() => EnterState(new FreeView());

        public void BackState()
        {
            // Go back 1 step
            if (stateStack.Count > 0)
            {
                UIState s = stateStack.Pop();
                s.OnStateExit();
                s.OnStateDestroyed();

                currentState = null;
                if(stateStack.Count <= 0) { EnterState(); }

                if (stateStack.Count > 0)
                {
                    currentState = stateStack.Peek();
                    
                    // Configure UI Toolkit navigation for the restored state
                    currentState.ConfigureUIToolkitNavigation(combatView);
                    
                    currentState.OnStateEnter();
                }
            }
        }

        public bool WaitFor(IEnumerator action, IEnumerator onFinished)
        {
            if (waitingAnimation)
            {
                return false;
            }
            
            IEnumerator Wrapper()
            {
                if (currentState is UnitMenu)
                {
                    combatView.menu.ShowMenu();
                    RefreshMainMobStats();
                    // mainMobStatPanel.SetActive(true);
                }

                waitingAnimation = false;
                yield return new JumpIn(onFinished);
            }
            
            bool actionAccepted = Globals.combatMgr.Instance.UIPickedAction(action, Wrapper());
            if (actionAccepted)
            {
                // Only change UI state if the action is accepted (there's no actions on-going right now)
                // This could be triggered by keyboard shortcuts while the animation is playing, in some rare cases.
                waitingAnimation = true;
                combatView.menu.HideMenu();
                // mainMobStatPanel.SetActive(false);
            }
            return actionAccepted;
        }

        public void WaitFor(IEnumerator action, System.Action onFinished)
        {
            IEnumerator Wrapper()
            {
                // Lazy evaluation
                onFinished();
                yield break;
            }

            WaitFor(action, Wrapper());
        }

        public void OnGlobalActionPostcast(MobData mob, RuntimeAction action, Spells.SpellTarget target)
        {
            circles.UpdateAllCircles();
        }

        private Plane groundPlane;
        
        // TODO: Modify for 3D worlds
        public void OnPoint(InputValue input)
        {
            // Vector2 cursorPos = cinemachineBrain.ScreenToWorldPoint(input.Get<Vector2>());

            Vector3Int cursorGPos = Vector3Int.zero;
            
            Ray pointer = cinemachineBrain.ScreenPointToRay(input.Get<Vector2>());
            var rayCastResult = Globals.backend.GetMapSystem()
                .DDAGridRaycast(pointer, MapSystem.RaycastTarget.SolidOrStandable);

            if (!rayCastResult.HasValue)
            {
                return;
            }
            
            cursorGPos = rayCastResult.Value.hitPos;
            GridData g = Globals.backend.GetMap(cursorGPos, false);
            if ((g.standable || g.solid) &&
                IntrusionBits.GetFaceIntrusion(g.intrusion, rayCastResult.Value.faceNormal) == 0)
            {
                cursorGPos += rayCastResult.Value.faceNormal;
            }
            
            // Vector2 _gridPos = (cursorPos / gridWorldUnitSize);
            Vector3Int oldPos = cursor.GridPos;
            Vector3Int newPos = cursorGPos;
            
            // cursor.position = new Vector3Int(Mathf.FloorToInt(_gridPos.x), 0, Mathf.FloorToInt(_gridPos.y));

            if (newPos != oldPos && currentState != null)
            {
                cursor.Position = Globals.backend.GridToBackendFloorPos(newPos);
                currentState.PointAtGrid(newPos);
            }
        }

        public bool CheckPassEvent()
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }
            return true;
        }

        public void OnSubmit(InputValue input)
        {
            if (isInAnimation && !(currentState is TargetRequester.TargetRequesterUIState)) return;
            if (currentState == null) return;
            if (CheckPassEvent())
            {
                currentState.Submit(input);
            }
        }

        public void OnUp(InputValue input)
        {
            MoveCursor(Vector2.up);
        }
        
        public void OnDown(InputValue input)
        {
            MoveCursor(Vector2.down);
        }
        
        public void OnLeft(InputValue input)
        {
            MoveCursor(Vector2.left);
        }
        
        public void OnRight(InputValue input)
        {
            MoveCursor(Vector2.right);
        }

        public void MoveCursor(Vector2 delta)
        {
            if (currentState.AllowFreeNavigation)
            {
                // Rotate input relative to camera
                float cameraY = mainVCam.transform.eulerAngles.y;
                Vector3 rotated = Quaternion.Euler(0, cameraY, 0) * new Vector3(delta.x, 0, delta.y);
                
                // Pick most significant axis
                Vector3Int movement = Vector3Int.zero;
                if (Mathf.Abs(rotated.x) > Mathf.Abs(rotated.z))
                    movement.x = rotated.x > 0 ? 1 : -1;
                else if (rotated.z != 0)
                    movement.z = rotated.z > 0 ? 1 : -1;
                
                if (movement != Vector3Int.zero)
                {
                    currentState.MoveCursor(movement);
                }
            }
        }
        
        public Vector3Int? FindNearestWalkablePosition(Vector3Int targetXZ, int currentY)
        {
            // Check current Y level first
            for (int distance = 0; distance <= 10; distance++)
            {
                // Check at currentY + distance
                if (distance > 0)
                {
                    Vector3Int posUp = new Vector3Int(targetXZ.x, currentY + distance, targetXZ.z);
                    if (IsWalkablePosition(posUp))
                        return posUp;
                }
                
                // Check at currentY - distance  
                Vector3Int posDown = new Vector3Int(targetXZ.x, currentY - distance, targetXZ.z);
                if (IsWalkablePosition(posDown))
                    return posDown;
            }
            
            return null;
        }
        
        private bool IsWalkablePosition(Vector3Int position)
        {
            GridData grid = Globals.backend.GetMap(position, false);
            
            // Check if passable
            if (!grid.passable) return false;
            
            // Check if supported (either standable here or standable below)
            if (grid.standable || grid.solid) return true;
            
            GridData belowGrid = Globals.backend.GetMap(position + Vector3Int.down, false);
            return (belowGrid.standable || belowGrid.solid) && IntrusionBits.GetFaceIntrusion(belowGrid.intrusion, Vector3Int.up) == 0;
        }

        public void OnLeftClick(InputValue input)
        {
            OnSubmit(input);
        }

        public void OnCancel(InputValue input)
        {
            if (isInAnimation && !(currentState is TargetRequester.TargetRequesterUIState)) return;
            if (currentState == null) return;
            currentState.Cancel(input);
        }

        public void RefreshMainMobStats()
        {
            var mob = _statViewMobRenderer;
            if (mob == null)
            {
                return;
            }

#if UNITY_EDITOR
            // Selection.activeGameObject = mob.gameObject;
#endif

            // string effects = "";
            // foreach (var fx in mob.data.listeners)
            // {
            //     if (
            //         fx.type is MobListenerSO.ListenerType.Buff or MobListenerSO.ListenerType.Passive)
            //     {
            //         effects += "\n" + fx.name;
            //     }
            // }
            //
            // mainMobStatText.text =
            //     //$"<mspace=0.58em>" +
            //     $"<style=\"Title\"><color=#fc0><b><size=12>{mob.gameObject.name}</size></b></color></style>\n" +
            //     $"\n" +
            //     $"<size=12><color=#0f0>HP {mob.data.health} / {mob.data.maxHealth}</color></size>\n" +
            //     $"<size=12><color=#ff0>AP {mob.data.actionPoints} / {mob.data.apMax}</color></size>\n\n" +
            //     $"{(mob.data.GCDstatus.Contains(GCDGroup.Common) ? "Action done" : "Action available")}" +
            //     $"\n\n" +
            //     $"<align=center><style=h3><size=12>===== STATS =====</size></style></align>\n\n" +
            //     $"VIT {(int)mob.data.baseStats.VIT,3} | STR {(int)mob.data.baseStats.STR,3}\n" +
            //     $"MAG {(int)mob.data.baseStats.MAG,3} | INT {(int)mob.data.baseStats.INT,3}\n" +
            //     $"DEX {(int)mob.data.baseStats.DEX,3} | TEC {(int)mob.data.baseStats.TEC,3}\n\n" +
            //     $"<align=center><style=h3><size=12>===== EQUIP =====</size></style></align>\n\n" +
            //     $"{mob.data.mainWeapon?.GetInformationString()}\n\n" +
            //     $"<align=center><style=h3><size=12>=====EFFECTS=====</size></style></align>\n" +
            //     $"{effects}" +
            //     //$"</mspace>" +
            //     $"\n";
            
            combatView.unitBar.RefreshWithContents(mob.data);
        }

        public void ShowMainMobStats(MobRenderer mobRenderer)
        {
            _statViewMobRenderer = mobRenderer;
            RefreshMainMobStats();
            // mainMobStatPanel.SetActive(true);

            if (mobRenderer.isBoss)
            {
                Globals.ui.Instance.combatView.BindAsBoss(mobRenderer);
            }
        }

        public void HideMainMobStats()
        {
            // mainMobStatPanel.SetActive(false);
        }

        public void BindAsBoss(MobRenderer renderer)
        {
            combatView.BindAsBoss(renderer);
        }

        #region Shortcuts

        public void OnActionMove(InputValue input)
        {
            combatView.menu.ShortCut("1");
        }

        public void OnActionPass(InputValue input)
        {
            combatView.menu.ShortCut("R");
        }

        public void OnAction1(InputValue input)
        {
            combatView.menu.ShortCut("1");
        }

        public void OnAction2(InputValue input)
        {
            combatView.menu.ShortCut("2");
        }

        public void OnAction3(InputValue input)
        {
            combatView.menu.ShortCut("3");
        }

        public void OnAction4(InputValue input)
        {
            combatView.menu.ShortCut("4");
        }

        public void OnAction5(InputValue input)
        {
            combatView.menu.ShortCut("5");
        }

        #endregion
    }
}
