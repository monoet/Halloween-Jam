using System;
using UnityEngine;

namespace BattleV2.Core
{
    public enum BattleState
    {
        Idle,
        AwaitingAction,
        Resolving,
        Victory,
        Defeat
    }

    /// <summary>
    /// Lightweight state machine used by the new battle pipeline.
    /// </summary>
    public class BattleStateController : MonoBehaviour
    {
        [SerializeField] private BattleState initialState = BattleState.Idle;

        public BattleState State { get; private set; }

        public event Action<BattleState> OnChanged;

        private void Awake()
        {
            State = initialState;
            BattleLogger.Log("State", $"Initial → {State}");
        }

        public void Set(BattleState newState)
        {
            if (State == newState)
            {
                return;
            }

            var old = State;
            State = newState;
            BattleLogger.Log("State", $"{old} → {newState}");
            OnChanged?.Invoke(State);
        }

        public void ResetToIdle()
        {
            State = BattleState.Idle;
            BattleLogger.Log("State", $"Reset → {State}");
            OnChanged?.Invoke(State);
        }
    }
}
