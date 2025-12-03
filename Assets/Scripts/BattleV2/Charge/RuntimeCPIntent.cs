using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.Audio;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.Charge
{
    /// <summary>
    /// Turn-scoped CP intent state (selection phase only).
    /// </summary>
    public sealed class RuntimeCPIntent : ICpIntentSource, ICpIntentSink
    {
        public static RuntimeCPIntent Shared { get; private set; } = new RuntimeCPIntent();

        private readonly HashSet<int> consumedSelectionIds = new HashSet<int>();
        private CombatEventDispatcher dispatcher;
        private CombatantState defaultActor;

        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsActiveTurn { get; private set; }

        public event Action<CpIntentChangedEvent> OnChanged;
        public event Action<CpIntentConsumedEvent> OnConsumed;
        public event Action<CpIntentCanceledEvent> OnCanceled;
        public event Action<CpIntentTurnStartedEvent> OnTurnStarted;
        public event Action<CpIntentTurnEndedEvent> OnTurnEnded;

        public void BeginTurn(int maxCp)
        {
            Max = Math.Max(0, maxCp);
            Current = 0;
            IsActiveTurn = true;
            consumedSelectionIds.Clear();
            OnTurnStarted?.Invoke(new CpIntentTurnStartedEvent(Max));
            RaiseChanged(0, Current, "BeginTurn");
        }

        public void EndTurn(string reason = null)
        {
            if (!IsActiveTurn)
            {
                return;
            }

            IsActiveTurn = false;
            OnTurnEnded?.Invoke(new CpIntentTurnEndedEvent(Current, reason ?? "EndTurn"));
        }

        public void Set(int value, string reason = null)
        {
            if (!IsActiveTurn)
            {
                return;
            }

            int clamped = Clamp(value);
            if (clamped == Current)
            {
                return;
            }

            int prev = Current;
            Current = clamped;
            RaiseChanged(prev, Current, reason ?? "Set");
        }

        public void Add(int delta, string reason = null)
        {
            if (!IsActiveTurn || delta == 0)
            {
                return;
            }

            int prev = Current;
            int next = Clamp(prev + delta);
            if (next == prev)
            {
                return;
            }

            Current = next;
            RaiseChanged(prev, Current, reason ?? "Add");
        }

        public int ConsumeOnce(int selectionId, string reason = null)
        {
            if (!IsActiveTurn)
            {
                return 0;
            }

            if (consumedSelectionIds.Contains(selectionId))
            {
                return 0;
            }

            consumedSelectionIds.Add(selectionId);
            int amount = Current;
            Current = 0;
            OnConsumed?.Invoke(new CpIntentConsumedEvent(amount, Current, reason ?? "ConsumeOnce", selectionId));
            RaiseChanged(amount, Current, reason ?? "ConsumeOnce");
            return amount;
        }

        public void Cancel(string reason = null)
        {
            if (!IsActiveTurn)
            {
                return;
            }

            if (Current == 0)
            {
                OnCanceled?.Invoke(new CpIntentCanceledEvent(0, reason ?? "Cancel"));
                return;
            }

            int prev = Current;
            Current = 0;
            OnCanceled?.Invoke(new CpIntentCanceledEvent(prev, reason ?? "Cancel"));
            RaiseChanged(prev, Current, reason ?? "Cancel");
        }

        private int Clamp(int value) => Math.Max(0, Math.Min(Max, value));

        private void RaiseChanged(int previous, int current, string reason)
        {
            OnChanged?.Invoke(new CpIntentChangedEvent(previous, current, Max, reason ?? "Changed"));
            EmitAudioFlag(previous, current);
        }

        public void SetDispatcher(CombatEventDispatcher combatDispatcher)
        {
            dispatcher = combatDispatcher;
        }

        public void SetDefaultActor(CombatantState actor)
        {
            defaultActor = actor;
        }

        private void EmitAudioFlag(int previous, int current)
        {
            if (dispatcher == null)
            {
                dispatcher = AnimationSystemInstaller.Current?.CombatEvents;
            }

            if (dispatcher == null)
            {
                return;
            }

            int delta = current - previous;
            if (delta == 0)
            {
                return;
            }

            string flag = delta > 0 ? BattleAudioFlags.UiCpIncrease : BattleAudioFlags.UiCpDecrease;
            var actor = defaultActor;
            if (actor == null)
            {
                return;
            }

            UnityEngine.Debug.Log($"[CPIntent] Emit flag={flag} delta={delta}", actor);
            dispatcher.EmitExternalFlag(flag, actor: actor, target: null, weaponKind: null, element: null, isCritical: false, targetCount: 0);
        }
    }
}
