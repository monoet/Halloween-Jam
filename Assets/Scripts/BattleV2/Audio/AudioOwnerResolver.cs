using BattleV2.Core;

namespace BattleV2.Audio
{
    /// <summary>
    /// Punto único para resolver identidad de audio desde CombatantState.
    /// A futuro puede consultar equipamiento, arma, elemento, etc.
    /// </summary>
    public static class AudioOwnerResolver
    {
        public static AudioSignatureId ResolveSignature(CombatantState state)
        {
            if (state == null)
            {
                return AudioSignatureId.None;
            }

            // Hoy: leer directamente el enum configurado en CombatantState.
            // Mañana: se puede derivar desde inventario/equipamiento sin cambiar esta firma.
            return state.AudioSignatureId;
        }
    }
}

