# Runtime Tween Providers

Providers are `ScriptableObject`s that build a `TransformTween` in code but remain fully data-driven.

## Data Flow
1. Gameplay selects an action and builds a `BattleSelection`.
2. `BattleSelection.WithTargets(...)` stores the resolved `TargetSet`. When a primary combatant is available, we also call `WithTargetTransform(target.transform)`.
3. The Step Scheduler executes the recipe. `TweenExecutor` resolves the provider assigned to the tween binding and calls `BuildTween`.
4. The provider consumes runtime data (`actorTransform`, `BattleSelection`, `targets`, `ActionStepParameters`) and returns a fresh tween struct.
5. Standard overrides (`duration`, `posX`, etc.) are applied automatically by the executor.

## Standard Parameters
| Provider | Field | Description |
| -------- | ----- | ----------- |
| RunnerUpToSpotlight | `stopDistance` | Distance (world units) to keep from the target when finishing the dash. |
| RunnerUpToSpotlight | `duration` | Tween duration in seconds. |
| RunnerUpToSpotlight | `easing` | Optional `AnimationCurve` applied by the executor. |

### Troubleshooting
- **Missing target**: The provider logs a warning and returns `TransformTween.None` if `selection.TargetTransform` is null (e.g., the enemy died). The scheduler treats this as a no-op.
- **Actor null**: Happens if the wrapper lost its `CombatantState`. Fix the binding or ensure the wrapper registers before the recipe plays.

## RunnerUpToSpotlight Example
```csharp
[CreateAssetMenu(menuName = "Battle/Animation/Tween Providers/RunnerUp To Spotlight")]
public sealed class RunnerUpToSpotlightProvider : TransformTweenProvider
{
    [SerializeField] private float stopDistance = 1.2f;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public override TransformTween BuildTween(
        Transform actorTransform,
        BattleSelection selection,
        IReadOnlyList<CombatantState> targets,
        ActionStepParameters contextParameters)
    {
        // Validate actor/target
        // Compute destination relative to selection.TargetTransform
        // Return tween with duration/easing
    }
}
```

### Step Recipe Snippet
```json
{
  "stepId": "run_up",
  "executorId": "tween",
  "bindingId": "RunUpSpotlight",
  "parameters": {
    "duration": "0.55"
  }
}
```

Assign the provider asset to the `RunUpSpotlight` tween binding inside the `CharacterAnimationSet`. No other serializer changes are required.
