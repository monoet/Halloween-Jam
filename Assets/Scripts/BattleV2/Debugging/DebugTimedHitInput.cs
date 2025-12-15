using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime;
using UnityEngine;

namespace BattleV2.Debugging
{
    public class DebugTimedHitInput : MonoBehaviour, ITimedHitInputProvider
    {
        [SerializeField] private AnimationSystemInstaller installer;
        
        private void Start()
        {
            installer ??= AnimationSystemInstaller.Current;
            if (installer != null && installer.TimedHitService != null)
            {
                installer.TimedHitService.SetInputProvider(this);
                Debug.Log("[DebugTimedHitInput] Registered as Input Provider.");
            }
            else
            {
                Debug.LogError("[DebugTimedHitInput] Failed to register: Installer or Service is null.");
            }
        }

        public bool TryConsumeInput(out double timestamp)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                timestamp = Time.timeAsDouble;
                return true;
            }
            timestamp = 0;
            return false;
        }
    }
}
