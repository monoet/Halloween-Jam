using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Linq;
using BattleV2.Actions;
using BattleV2.Providers;
#if FMOD_PRESENT
using FMODUnity;
#endif
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Core;
using BattleV2.Charge;

namespace BattleV2.UI
{
    public enum BattleInputMode
    {
        Menu,
        Execution,
        Locked
    }

    /// <summary>
    /// Polls keyboard input (WASD + Arrows + Confirm/Cancel) and drives the Unity EventSystem.
    /// This replaces or augments the StandaloneInputModule for specific battle needs.
    /// </summary>
    public class BattleUIInputDriver : MonoBehaviour, ITimedHitInputProvider, IBattleUIInput
    {
        // IBattleUIInput Implementation
        public bool ConfirmHeld => IsAnyKeyHeld(confirmKeys);
        public bool ConfirmPressedThisFrame => IsAnyKeyPressed(confirmKeys);
        public bool CancelHeld => IsAnyKeyHeld(cancelKeys);
        public bool CancelPressedThisFrame => IsAnyKeyPressed(cancelKeys);
        public Vector2 NavigationDirection => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        public bool TimedHitPressedThisFrame => Input.GetKeyDown(timedHitKey);

        [Header("References")]
        [SerializeField] private BattleUIRoot uiRoot;

        [Header("Input Configuration")]
        [SerializeField] private KeyCode[] confirmKeys = new[] { KeyCode.Return, KeyCode.Space, KeyCode.Z };
        [SerializeField] private KeyCode[] cancelKeys = new[] { KeyCode.Escape, KeyCode.Backspace, KeyCode.X };
        [SerializeField] private KeyCode timedHitKey = KeyCode.Space;
        [SerializeField] private float repeatDelay = 0.5f;
        [SerializeField] private float repeatRate = 0.1f;
        [SerializeField] private KeyCode increaseCpKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseCpKey = KeyCode.L;

        [Header("Audio")]
        [SerializeField] private bool playNavigateSfx = true;
#if FMOD_PRESENT
        [SerializeField] private string navigateEventPath = "event:/SFX/combat/ui/Cursor_Movement";
#endif

        [SerializeField] private bool playConfirmSfx = true;
#if FMOD_PRESENT
        [SerializeField] private string confirmEventPath = "event:/SFX/combat/ui/Cursor_Select";
#endif

        [SerializeField] private bool playCancelSfx = true;
#if FMOD_PRESENT
        [SerializeField] private string cancelEventPath = "event:/SFX/combat/ui/Cursor_Back";
#endif

        private float nextMoveTime;
        private Vector2 lastDirection;
        private IBattleUIState currentState;
        private ICpIntentSink cpIntentSink;
        private ICpIntentSource cpIntentSource;
        private bool suppressNavigateSfx;

        public BattleUIRoot UiRoot => uiRoot;
        public ITimedHitService TimedHitService { get; private set; }
        public CombatantState ActiveActor { get; private set; }
        public ICpIntentSink CpIntentSink => cpIntentSink;
        public ICpIntentSource CpIntentSource => cpIntentSource;

        public void Initialize(ITimedHitService service)
        {
            TimedHitService = service;
            service?.SetInputProvider(this); // Auto-wire
            Debug.Log($"[InputDriver] Initialized with TimedHitService={(service != null)}", this);
        }

        public void SetCpIntent(ICpIntentSink sink, ICpIntentSource source = null)
        {
            cpIntentSink = sink ?? RuntimeCPIntent.Shared;
            cpIntentSource = source ?? sink as ICpIntentSource ?? RuntimeCPIntent.Shared;
        }

        public bool TryConsumeInput(out double timestamp)
        {
            if (ConfirmPressedThisFrame)
            {
                timestamp = Time.timeAsDouble;
                return true;
            }

            timestamp = 0;
            return false;
        }

        public void SetMode(BattleInputMode mode)
        {
            switch (mode)
            {
                case BattleInputMode.Menu:
                    // Ensure UI is at Root if we are just switching to Menu mode "blindly"
                    // However, SetMode is usually called by ShowMenu which might want to be specific.
                    // But ShowMenu calls SetMode(Menu).
                    // If we are starting fresh, we should probably EnterRoot.
                    // If we are returning, we might want to respect stack?
                    // SetMode is a "Force" mode. Let's assume it means "Start Menu Interaction".
                    // If stack is empty, EnterRoot.
                    if (uiRoot != null && (uiRoot.StackCount == 0)) 
                    {
                        uiRoot.EnterRoot();
                    }
                    ChangeState(new MenuState());
                    break;
                case BattleInputMode.Execution:
                    ChangeState(new ExecutionState());
                    break;
                case BattleInputMode.Locked:
                    ChangeState(new LockedState());
                    break;
            }
        }

        public void SetState(IBattleUIState newState)
        {
            ChangeState(newState);
        }

        private void ChangeState(IBattleUIState newState)
        {
            if (currentState != null)
            {
                currentState.Exit(this);
            }
            
            string oldStateName = currentState?.GetType().Name ?? "null";
            currentState = newState;
            string newStateName = currentState?.GetType().Name ?? "null";
            
            BattleDiagnostics.Log("UI", $"State Change: {oldStateName} -> {newStateName}", this);
            Debug.Log($"[InputDriver] State Change: {oldStateName} -> {newStateName}", this);
            
            currentState?.Enter(this);
        }

        public void SetActiveActor(CombatantState actor)
        {
            ActiveActor = actor;
            Debug.Log($"[InputDriver] ActiveActor -> {(actor != null ? actor.name : "null")}", this);
        }

        private Action<BattleActionData> onActionSelected;
        private Action onMenuCancel;
        private BattleActionContext currentContext;

        public event Action<Vector2> OnNavigate;

        private void Awake()
        {
            uiRoot ??= FindFirstObjectByType<BattleUIRoot>();
            // Default to shared CP intent if nothing is injected.
            SetCpIntent(RuntimeCPIntent.Shared);
            suppressNavigateSfx = false;

            if (uiRoot != null)
            {
                uiRoot.OnAttackChosen += HandleActionChosen;
                uiRoot.OnSpellChosen += HandleActionChosen;
                uiRoot.OnItemChosen += HandleActionChosen;
                uiRoot.OnRootActionSelected += HandleRootAction;
                uiRoot.OnMenuCancel += HandleCancel;
            }
        }

        public void ShowMenu(BattleActionContext context, Action<BattleActionData> onSelected, Action onCancel)
        {
            currentContext = context;
            onActionSelected = onSelected;
            onMenuCancel = onCancel;

            SetMode(BattleInputMode.Menu);
        }

        private void HandleActionChosen(string actionId)
        {
            if (currentContext == null || currentContext.AvailableActions == null) return;

            var action = currentContext.AvailableActions.FirstOrDefault(a => a.id == actionId);
            if (action != null)
            {
                onActionSelected?.Invoke(action);
            }
            else
            {
                Debug.LogWarning($"[InputDriver] Action '{actionId}' chosen but not found in available actions.");
            }
        }

        private void HandleRootAction(string actionId)
        {
             // For Defend/Flee which might not be in the sub-menus but are actions.
             HandleActionChosen(actionId);
        }

        private void HandleCancel()
        {
            onMenuCancel?.Invoke();
        }

        private void Start()
        {
            if (currentState == null)
            {
                ChangeState(new MenuState());
            }
        }

        private void Update()
        {
            if (EventSystem.current == null)
            {
                Debug.LogError("BattleUIInputDriver: No EventSystem found!");
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.X))
            {
                Debug.Log($"[InputDriver] Cancel Key Pressed! UiRoot is {(uiRoot != null ? "Valid" : "NULL")}");
            }

            currentState?.HandleInput(this);
        }

        public void HandleNavigation()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector2 direction = new Vector2(h, v);

            if (direction.sqrMagnitude < 0.1f)
            {
                lastDirection = Vector2.zero;
                return;
            }

            if (Mathf.Abs(h) > Mathf.Abs(v))
            {
                direction = new Vector2(Mathf.Sign(h), 0);
            }
            else
            {
                direction = new Vector2(0, Mathf.Sign(v));
            }

            if (direction != lastDirection || Time.unscaledTime >= nextMoveTime)
            {
                nextMoveTime = Time.unscaledTime + (direction != lastDirection ? repeatDelay : repeatRate);
                lastDirection = direction;
                Navigate(direction);
            }
        }

        private void Navigate(Vector2 direction)
        {
            GameObject current = EventSystem.current.currentSelectedGameObject;
            if (current == null) return;

            AxisEventData data = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.None };

            if (direction.x > 0) data.moveDir = MoveDirection.Right;
            else if (direction.x < 0) data.moveDir = MoveDirection.Left;
            else if (direction.y > 0) data.moveDir = MoveDirection.Up;
            else if (direction.y < 0) data.moveDir = MoveDirection.Down;

            ExecuteEvents.Execute(current, data, ExecuteEvents.moveHandler);
            OnNavigate?.Invoke(direction);
            PlayNavigateAudio();
        }

        public void PlayNavigateAudio()
        {
#if FMOD_PRESENT
            if (suppressNavigateSfx)
            {
                return;
            }
            if (playNavigateSfx && !string.IsNullOrWhiteSpace(navigateEventPath))
            {
                RuntimeManager.PlayOneShot(navigateEventPath);
            }
#endif
        }

        public void PlayConfirmAudio()
        {
#if FMOD_PRESENT
            if (playConfirmSfx && !string.IsNullOrWhiteSpace(confirmEventPath))
            {
                RuntimeManager.PlayOneShot(confirmEventPath);
            }
#endif
        }

        public void PlayCancelAudio()
        {
#if FMOD_PRESENT
            if (playCancelSfx && !string.IsNullOrWhiteSpace(cancelEventPath))
            {
                RuntimeManager.PlayOneShot(cancelEventPath);
            }
#endif
        }

        /// <summary>
        /// Handles CP intent hotkeys (L/R) only when CP intent turn is active and while in Menu.
        /// </summary>
        public void HandleCpIntentHotkeys()
        {
            if (cpIntentSink == null || cpIntentSource == null || !cpIntentSource.IsActiveTurn)
            {
                return;
            }

            if (Input.GetKeyDown(decreaseCpKey))
            {
                cpIntentSink.Add(-1, "Hotkey");
            }

            if (Input.GetKeyDown(increaseCpKey))
            {
                cpIntentSink.Add(1, "Hotkey");
            }
        }
        private bool IsAnyKeyHeld(KeyCode[] keys)
        {
            if (keys == null) return false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKey(keys[i])) return true;
            }
            return false;
        }

        private bool IsAnyKeyPressed(KeyCode[] keys)
        {
            if (keys == null) return false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i])) return true;
            }
            return false;
        }
        public void ResetInputAxes()
        {
            Input.ResetInputAxes();
            lastDirection = Vector2.zero;
            nextMoveTime = 0;
            Debug.Log("[InputDriver] Input Axes Reset.");
        }
    }
}
