using UnityEngine;
using UnityEngine.EventSystems;
using BattleV2.AnimationSystem.Execution.Runtime;

namespace BattleV2.UI
{
    public interface IBattleUIState
    {
        void Enter(BattleUIInputDriver driver);
        void Exit(BattleUIInputDriver driver);
        void HandleInput(BattleUIInputDriver driver);
    }

    public class MenuState : IBattleUIState
    {
        public void Enter(BattleUIInputDriver driver)
        {
            Debug.Log("[UIState] Entering Menu State");
            driver.UiRoot?.EnterRoot();
        }

        public void Exit(BattleUIInputDriver driver) { }

        public void HandleInput(BattleUIInputDriver driver)
        {
            driver.HandleNavigation();
            driver.HandleCpIntentHotkeys();
            
            // Confirm
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z))
            {
                GameObject current = EventSystem.current.currentSelectedGameObject;
                if (current != null)
                {
                    ExecuteEvents.Execute(current, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    driver.PlayConfirmAudio();
                }
            }

            // Cancel
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.X))
            {
                if (driver.UiRoot != null)
                {
                    driver.UiRoot.GoBack();
                    driver.PlayCancelAudio();
                }
            }
        }
    }

    public class ExecutionState : IBattleUIState
    {
        public void Enter(BattleUIInputDriver driver)
        {
            Debug.Log("[UIState] Entering Execution State");
            if (driver.UiRoot != null)
            {
                driver.UiRoot.HideAll();
            }
        }

        public void Exit(BattleUIInputDriver driver) { }

        public void HandleInput(BattleUIInputDriver driver)
        {
            // Forward input to TimedHitService (Space only to avoid capturar confirm)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (driver.TimedHitService != null && driver.ActiveActor != null)
                {
                    if (driver.TimedHitService.HasActiveWindow(driver.ActiveActor))
                    {
                        Debug.Log($"PhasEvInput | [ExecutionState] RegisterInput for {driver.ActiveActor.name}");
                        driver.TimedHitService.RegisterInput(driver.ActiveActor, "Keyboard/Gamepad");
                    }
                    else
                    {
                        Debug.Log($"PhasEvInput | [ExecutionState] Ignored input (no active window) for {driver.ActiveActor.name}");
                    }
                }
            }
        }
    }

    public class TargetSelectionState : IBattleUIState
    {
        public void Enter(BattleUIInputDriver driver)
        {
            Debug.Log("[UIState] Entering Target Selection State");
            if (driver.UiRoot != null)
            {
                driver.UiRoot.EnterTarget();
            }
        }

        public void Exit(BattleUIInputDriver driver) 
        {
            // When exiting Target Selection (e.g. to Execution or Menu), 
            // the UI Root might handle hiding, or the next state will.
            // ExecutionState hides all. MenuState might show menu.
        }

        public void HandleInput(BattleUIInputDriver driver)
        {
            driver.HandleNavigation();

            // Cancel
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.X))
            {
                if (driver.UiRoot != null)
                {
                    // This triggers OnCancel event in UIRoot, which BattleUITargetInteractor listens to.
                    // The Interactor then resolves the task with Empty/Proposed.
                    // The Orchestrator then decides what to do (likely go back to Menu).
                    driver.UiRoot.GoBack(); 
                    driver.PlayCancelAudio();
                }
            }
        }
    }

    public class LockedState : IBattleUIState
    {
        public void Enter(BattleUIInputDriver driver)
        {
             if (driver.UiRoot != null)
            {
                driver.UiRoot.HideAll();
            }
        }
        public void Exit(BattleUIInputDriver driver) { }
        public void HandleInput(BattleUIInputDriver driver) { }
    }
}
