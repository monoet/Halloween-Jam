using BattleV2.Charge;
using UnityEngine;

// Optional standalone hotkey driver; keep namespace non-conflicting with UnityEngine.Input.
namespace BattleV2.InputDrivers
{
    public sealed class CpIntentHotkeysDriver : MonoBehaviour
    {
        [SerializeField] private KeyCode increaseKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseKey = KeyCode.L;
        [SerializeField] private int step = 1;
        [SerializeField] private bool requireActiveTurn = true;
        [SerializeField] private bool disableWhileExecuting = true;
        [SerializeField] private bool isExecutingAction;
        [SerializeField] private bool useSharedInstance = true;
        [SerializeField] private RuntimeCPIntent cpIntentInstance;

        private ICpIntentSink sink;
        private ICpIntentSource source;

        private void Awake()
        {
            ResolveIntent();
        }

        private void Update()
        {
            if (sink == null || source == null)
            {
                return;
            }

            if (requireActiveTurn && !source.IsActiveTurn)
            {
                return;
            }

            if (disableWhileExecuting && isExecutingAction)
            {
                return;
            }

            if (Input.GetKeyDown(increaseKey))
            {
                sink.Add(step, "HotkeyIncrease");
            }
            else if (Input.GetKeyDown(decreaseKey))
            {
                sink.Add(-step, "HotkeyDecrease");
            }
        }

        public void SetIsExecuting(bool executing)
        {
            isExecutingAction = executing;
        }

        private void ResolveIntent()
        {
            RuntimeCPIntent runtime = null;
            if (cpIntentInstance != null)
            {
                runtime = cpIntentInstance;
            }
            else if (useSharedInstance)
            {
                runtime = RuntimeCPIntent.Shared;
            }

            sink = runtime;
            source = runtime;
        }
    }
}
