namespace BattleV2.Audio
{
    /// <summary>
    /// Identidad de audio por combatiente. Hoy es un enum simple;
    /// a futuro se puede mapear a equipamiento/perfil más complejo.
    /// </summary>
    public enum AudioSignatureId
    {
        None = 0,

        // Valores de ejemplo; se pueden ajustar a personajes reales.
        PlayerDefault = 1,
        EnemyDefault = 2
    }
}

