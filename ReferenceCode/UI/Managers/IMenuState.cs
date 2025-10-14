// Assets/Scripts/UI/Menu/IMenuState.cs
/// <summary>
/// Interfaz base para todos los menús.
/// Define el ciclo de vida de cada pantalla del sistema.
/// </summary>
public interface IMenuState
{
    void Enter();
    void Exit();
    void Update();
}
