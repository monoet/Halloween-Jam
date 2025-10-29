using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution
{
    public interface IActionLockManager
    {
        void PushLock(string reason);
        void PopLock(string reason);
        bool IsLocked { get; }
        IReadOnlyList<string> Reasons { get; }
    }

    public sealed class ActionLockManager : IActionLockManager
    {
        private readonly List<string> reasons = new();

        public bool IsLocked => reasons.Count > 0;
        public IReadOnlyList<string> Reasons => reasons;

        public void PushLock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "unknown";
            }

            reasons.Add(reason);
        }

        public void PopLock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason must be provided to pop a lock.", nameof(reason));
            }

            int index = reasons.LastIndexOf(reason);
            if (index >= 0)
            {
                reasons.RemoveAt(index);
            }
            else
            {
                Debug.LogWarning($"[ActionLockManager] Attempted to release lock '{reason}' but it was not registered.");
            }
        }
    }
}
