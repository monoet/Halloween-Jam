# Animation System Asset Layout

Updated tree of the main assets and scripts that compose the revamped animation execution stack. Use it as an index when wiring new recipes, bindings, or diagnostics.

```
Assets/
  Animation/
    Anim Sets/
      Ciro_AS.asset
    Timelines/
      Legacy/
        basic_attack.asset
  Scripts/
    BattleV2/
      AnimationSystem/
        Catalog/
          TimelineCatalog.cs
          TimelineCatalog.meta
        Execution/
          Routers/                         (Legacy routing glue – untouched)
          Runtime/
            ActionStepParser.cs
            IActionStepExecutor.cs        (public executor contract)
            IAnimationBindingResolver.cs  (bridge to binding caches)
            IStepSchedulerObserver.cs
            StepContracts.cs              (ActionStep, ActionRecipe DTOs)
            StepScheduler.cs
            implementacion log.md         (running dev log)
            Core/
              Conflict/
                ActiveExecutionRegistry.cs
              GroupRunners/
                ParallelGroupRunner.cs
                SequentialGroupRunner.cs
            Executors/
              AnimatorClipExecutor.cs
              FlipbookExecutor.cs
              TweenExecutor.cs
              SfxExecutor.cs
              VfxExecutor.cs
              WaitExecutor.cs
              System/
                GateSystemStep.cs
                WindowSystemStep.cs
            Recipes/
              ActionRecipeBuilder.cs
              ActionRecipeCatalog.cs
              ActionRecipeCatalogDiagnostics.cs
              PilotActionRecipes.cs
            SystemSteps/
              SystemStepRunner.cs
            Telemetry/
              StepTelemetryBuffer.cs
              StepTelemetryReporter.cs
          Testing/
            StepSchedulerTests.cs          (placeholder – not yet implemented)
        Runtime/
          AnimationBindingConfig.cs
          AnimationSystemInstaller.cs
          BattleAnimationSystemBridge.cs
          NewAnimOrchestratorAdapter.cs
          TimedHitHudBridge.cs
          TimedHitInputRelay.cs
          TimedHitToleranceProfileAsset.cs
          Bindings/
            AnimationClipBindings.asset
            FlipbookBindings.asset
            TweenBindings.asset
            SfxBindings.asset
            VfxBindings.asset
          Internal/
            AnimationRouterBundle.cs
            AnimationSequenceSession.cs
            AnimatorWrapperResolver.cs
Docs/
  AnimationSystem/
    AssetLayout.md
    NamingConventions.md
```

> Tip: keep this file in sync after moving executors, recipes, or bindings so the next person can orient themselves in seconds.
