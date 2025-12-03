using UnityEngine;
using UnityEngine.EventSystems;
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
    public class BattleUIInputDriver : MonoBehaviour, ITimedHitInputProvider
    {
        [Header("References")]
        [SerializeField] private BattleUIRoot uiRoot;

        [Header("Input Configuration")]
        [SerializeField] private float repeatDelay = 0.5f;
        [SerializeField] private float repeatRate = 0.1f;

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
        [Header("CP Intent Hotkeys (Menu only)")]
        [SerializeField] private KeyCode increaseCpKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseCpKey = KeyCode.L;

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
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z))
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
            currentState?.Exit(this);
            currentState = newState;
            string stateName = currentState?.GetType().Name ?? "null";
            BattleDiagnostics.Log("UI", $"State Change -> {stateName}", this);
            currentState?.Enter(this);
            Debug.Log($"[InputDriver] State Changed to {stateName}", this);
        }

        public void SetActiveActor(CombatantState actor)
        {
            ActiveActor = actor;
            Debug.Log($"[InputDriver] ActiveActor -> {(actor != null ? actor.name : "null")}", this);
        }

        private void Awake()
        {
            uiRoot ??= GetComponent<BattleUIRoot>();
            // Default to shared CP intent if nothing is injected.
            SetCpIntent(RuntimeCPIntent.Shared);
            suppressNavigateSfx = false;
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
    }
}
