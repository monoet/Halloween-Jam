using BattleV2.Core;
using BattleV2.Orchestration;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Minimal bridge from player input to the timed-hit service.
    /// Registers an input pulse whenever the configured key is pressed.
    /// </summary>
    public sealed class TimedHitInputRelay : MonoBehaviour
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private BattleManagerV2 battleManager;
        [SerializeField] private KeyCode inputKey = KeyCode.Space;
        [SerializeField] private bool usePlayerActor = true;
        [SerializeField] private CombatantState explicitActor;
        [SerializeField] private string sourceId = "Keyboard";

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
        }

        private void Update()
        {
            if (installer == null || installer.TimedHitService == null)
            {
                return;
            }

            if (Input.GetKeyDown(inputKey))
            {
                var actor = ResolveActor();
                Debug.Log($"[TimedHitInputRelay] KeyDown actor={(actor != null ? actor.name : "(null)")}", this);
                if (actor != null)
                {
                    installer.TimedHitService.RegisterInput(actor, sourceId);
                }
            }
        }

        private CombatantState ResolveActor()
        {
            if (!usePlayerActor)
            {
                return explicitActor;
            }

            return battleManager != null
                ? battleManager.Player
                : explicitActor;
        }

        public void SetExplicitActor(CombatantState actor)
        {
            SetActor(actor);
        }

        public void SetActor(CombatantState actor)
        {
            usePlayerActor = false;
            explicitActor = actor;
            Debug.Log($"[TimedHitInputRelay] Actor updated to {(actor != null ? actor.name : "(null)")}", this);
        }
    }
}
