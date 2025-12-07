using UnityEngine;
using UnityEngine.EventSystems;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Core;

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
        private readonly IInputGate gate = new NoGate();

        public void Enter(BattleUIInputDriver driver)
        {
            Debug.Log("[UIState] Entering Menu State (Input Only)");
            gate.OnEnter(driver);
            // No longer forcing UI Root entry. The UI Manager (BattleUIRoot) handles the visual stack.
            BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.State.Enter state=MenuState frame={Time.frameCount}", driver);
        }

        public void Exit(BattleUIInputDriver driver)
        {
            BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.State.Exit state=MenuState frame={Time.frameCount}", driver);
        }

        public void HandleInput(BattleUIInputDriver driver)
        {
            // If the root menu is hidden, Confirm/Back simply restores it and consumes input.
            if (driver.UiRoot != null && driver.UiRoot.IsRootHidden)
            {
                bool confirm = gate.AllowConfirm(driver);
                bool cancel = gate.AllowCancel(driver);
                if (confirm || cancel)
                {
                    if (driver.UiRoot.StackCount == 0)
                    {
                        driver.UiRoot.EnterRoot();
                    }
                    else
                    {
                        driver.UiRoot.ShowRootFromHidden();
                    }

                    if (confirm)
                    {
                        driver.PlayConfirmAudio();
                    }
                    else
                    {
                        driver.PlayCancelAudio();
                    }
                }
                return;
            }

            driver.HandleNavigation();
            driver.HandleCpIntentHotkeys();
            
            // Confirm
            if (gate.AllowConfirm(driver))
            {
                GameObject current = EventSystem.current.currentSelectedGameObject;
                if (current != null)
                {
                    BattleDiagnostics.Log("PAE.BUITI", $"phase=UI.Submit state=MenuState frame={Time.frameCount} selected={current.name}", driver);
                    ExecuteEvents.Execute(current, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    driver.PlayConfirmAudio();
                }
            }

            // Cancel
            if (gate.AllowCancel(driver))
            {
                if (driver.UiRoot != null)
                {
                    if (driver.UiRoot.StackCount > 1)
                    {
                        driver.UiRoot.GoBack();
                    }
                    else
                    {
                        driver.UiRoot.HideRootToHidden();
                    }
                    driver.PlayCancelAudio();
                }
            }
        }
    }

    // ... ExecutionState ...



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
            // Forward input to TimedHitService
            if (driver.TimedHitPressedThisFrame)
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
        private readonly IInputGate gate = new WaitReleaseThenPressGate();
        private readonly bool isVirtual;

        public TargetSelectionState(bool isVirtual = false)
        {
            this.isVirtual = isVirtual;
        }

        public void Enter(BattleUIInputDriver driver)
        {
            Debug.Log($"[UIState] Entering Target Selection State (Virtual={isVirtual})");
            gate.OnEnter(driver);
            driver.ResetInputAxes(); // Ensure gate sees a clean state (release)
            BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.State.Enter state=TargetSelectionState virtual={isVirtual} frame={Time.frameCount}", driver);
            
            if (isVirtual)
            {
                // In Virtual Mode, we don't want to show the panel, and we don't want to interact with previous UI.
                EventSystem.current.SetSelectedGameObject(null);
            }
            else if (driver.UiRoot != null)
            {
                driver.UiRoot.EnterTarget();
            }
        }

        public void Exit(BattleUIInputDriver driver) 
        {
            // When exiting Target Selection (e.g. to Execution or Menu), 
            // the UI Root might handle hiding, or the next state will.
            // ExecutionState hides all. MenuState might show menu.
            BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.State.Exit state=TargetSelectionState virtual={isVirtual} frame={Time.frameCount}", driver);
        }

        public void HandleInput(BattleUIInputDriver driver)
        {
            driver.HandleNavigation();
            driver.HandleCpIntentHotkeys();

            // Confirm
            if (gate.AllowConfirm(driver))
            {
                // In Virtual Mode, ignore UI selection and go straight to ConfirmTarget
                if (isVirtual)
                {
                    Debug.Log("[TargetSelectionState] Virtual Confirm. Calling ConfirmTarget.");
                    BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.Submit state=TargetSelectionState(Virtual) frame={Time.frameCount} selected=null", driver);
                    driver.UiRoot?.ConfirmTarget();
                }
                else
                {
                    GameObject current = EventSystem.current.currentSelectedGameObject;
                    if (current != null)
                    {
                        Debug.Log($"[TargetSelectionState] Confirm allowed. Executing Submit on {current.name}");
                        BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.Submit state=TargetSelectionState frame={Time.frameCount} selected={current.name}", driver);
                        ExecuteEvents.Execute(current, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    }
                    else if (driver.UiRoot != null)
                    {
                        // Fallback if no selection even in Panel Mode
                        Debug.Log("[TargetSelectionState] Panel Mode but no selection. Calling ConfirmTarget.");
                        BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase=UI.Submit state=TargetSelectionState frame={Time.frameCount} selected=null", driver);
                        driver.UiRoot.ConfirmTarget();
                    }
                }
            }

            // Cancel
            if (gate.AllowCancel(driver))
            {
                Debug.Log("[TargetSelectionState] Cancel allowed.");
                if (driver.UiRoot != null)
                {
                    driver.UiRoot.RequestCancelTarget(goBack: !isVirtual);
                    driver.SetState(new MenuState());
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
