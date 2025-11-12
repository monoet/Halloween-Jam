using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using UnityEngine;

namespace BattleV2.QA
{
    /// <summary>
    /// Lightweight actor rig used by QA harnesses to fabricate CombatEvent contexts.
    /// </summary>
    public sealed class QAActorRig : MonoBehaviour
    {
        [Header("Caster Transforms")]
        [SerializeField] private Transform casterRoot;
        [SerializeField] private Transform casterAnchor;
        [SerializeField] private int casterId = 10;
        [SerializeField] private CombatantAlignment alignment = CombatantAlignment.Ally;

        private void Awake()
        {
            EnsureRig();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRig();
        }

        [ContextMenu("Ensure Rig (Manual)")]
        private void EditorEnsureRig() => EnsureRig();
#endif

        private void EnsureRig()
        {
            if (casterRoot == null)
            {
                casterRoot = new GameObject("CasterRoot").transform;
                casterRoot.SetParent(transform, false);
            }

            if (casterAnchor == null)
            {
                casterAnchor = new GameObject("CasterAnchor").transform;
                casterAnchor.SetParent(transform, false);
                casterAnchor.localPosition = new Vector3(1.5f, 0f, 0f);
            }
        }

        public CombatEventContext BuildContext(CombatEventContext template = null)
        {
            bool ownsTemplate = template == null;
            template ??= CombatEventContext.CreateStub();

            var context = CombatEventContext.Acquire();
            context.Populate(
                new CombatEventContext.ActorView(
                    casterId,
                    alignment,
                    null,
                    casterRoot,
                    casterAnchor),
                template.Action,
                template.Targets.All,
                template.Targets.PerTarget,
                template.Tags);

            if (ownsTemplate)
            {
                template.Release();
            }

            return context;
        }

        public void DisableAnchor()
        {
            if (casterAnchor != null)
            {
                casterAnchor.gameObject.SetActive(false);
            }
        }

        public void RemoveRoot()
        {
            if (casterRoot != null)
            {
                DestroyImmediate(casterRoot.gameObject);
                casterRoot = null;
            }
        }
    }
}
