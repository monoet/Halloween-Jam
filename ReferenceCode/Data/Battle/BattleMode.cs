using UnityEngine;

public abstract class BattleMode : ScriptableObject
{
    public abstract void Initialize(CharacterRuntime[] party, CharacterRuntime[] enemies);
    public abstract void UpdateBattle(float deltaTime);
    public abstract bool IsActionReady(CharacterRuntime crt);
}
