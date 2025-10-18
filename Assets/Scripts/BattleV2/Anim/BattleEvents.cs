// Assets/Scripts/BattleV2/Anim/BattleEvents.cs
using System;

namespace BattleV2.Anim
{
    /// <summary>
    /// Event hub global (temporal o permanente) para el sistema de combate.
    /// Permite que animaciones y VFX escuchen sin depender del manager.
    /// </summary>
    public static class BattleEvents
    {
        // --- Turnos ---
        public static event Action PlayerTurnCommitted;
        public static event Action ActionResolving;

        // --- Estado global ---
        public static event Action OnCombatReset;
        public static event Action<bool> OnLockChanged;

        // Emitters (helpers)
        public static void EmitPlayerTurnCommitted() => PlayerTurnCommitted?.Invoke();
        public static void EmitActionResolving() => ActionResolving?.Invoke();
        public static void EmitCombatReset() => OnCombatReset?.Invoke();
        public static void EmitLockChanged(bool locked) => OnLockChanged?.Invoke(locked);
    }
}
