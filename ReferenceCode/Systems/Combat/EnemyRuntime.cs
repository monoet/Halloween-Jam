using UnityEngine;

/// <summary>
/// Runtime de enemigos con ganchos para IA básica.
/// </summary>
public class EnemyRuntime : CharacterRuntime
{
    [Header("AI Settings")]
    [SerializeField, Range(0f, 1f)] private float aggressiveness = 0.5f;
    [SerializeField] private ActionData defaultAction;

    public ActionData DefaultAction => defaultAction;

    public void SetDefaultAction(ActionData action)
    {
        defaultAction = action;
    }

    public ActionData DecideAction()
    {
        // TODO: Integrar árbol de comportamiento o utility AI.
        Debug.Log("[EnemyRuntime] IA placeholder usando defaultAction.");
        return defaultAction;
    }

    public CharacterRuntime DecideTarget()
    {
        // TODO: Seleccionar objetivo válido según reglas de batalla.
        Debug.Log("[EnemyRuntime] IA placeholder devolviendo null como objetivo.");
        return null;
    }
}
