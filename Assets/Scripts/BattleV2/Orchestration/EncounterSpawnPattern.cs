using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Defines positional offsets for different enemy counts so encounters can spawn in formations.
    /// </summary>
    [CreateAssetMenu(menuName = "Loadouts/Encounter Spawn Pattern")]
    public class EncounterSpawnPattern : ScriptableObject
    {
        public enum SpawnDimension
        {
            TwoD,
            ThreeD
        }

        [System.Serializable]
        private struct SpawnLayout
        {
            public int size;
            public Vector3[] offsets;
        }

        [SerializeField] private SpawnDimension dimension = SpawnDimension.ThreeD;
        [SerializeField] private SpawnLayout[] layouts = new SpawnLayout[0];

        public SpawnDimension Dimension => dimension;

        public bool Is2D => dimension == SpawnDimension.TwoD;

        private void OnValidate()
        {
            EnforceDimensionConstraints();
        }

        public void SetDimension(SpawnDimension newDimension)
        {
            if (dimension == newDimension)
            {
                return;
            }

            dimension = newDimension;
            EnforceDimensionConstraints();
        }

        public bool TryGetOffsets(int size, out Vector3[] offsets)
        {
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i].size == size && layouts[i].offsets != null)
                {
                    offsets = layouts[i].offsets;
                    if (Is2D)
                    {
                        Apply2DProjection(offsets);
                    }

                    return true;
                }
            }

            offsets = null;
            return false;
        }

        public Vector3 GetOffset(int size, int index)
        {
            if (TryGetOffsets(size, out var offsets) && offsets != null && offsets.Length > 0)
            {
                index = Mathf.Clamp(index, 0, offsets.Length - 1);

                var offset = offsets[index];
                if (Is2D)
                {
                    offset = ProjectTo2D(offset);
                }

                return offset;
            }

            return Vector3.zero;
        }

        private static void Apply2DProjection(Vector3[] offsets)
        {
            if (offsets == null)
            {
                return;
            }

            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = ProjectTo2D(offsets[i]);
            }
        }

        private static Vector3 ProjectTo2D(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private void EnforceDimensionConstraints()
        {
            if (!Is2D || layouts == null)
            {
                return;
            }

            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i].offsets == null)
                {
                    continue;
                }

                Apply2DProjection(layouts[i].offsets);
            }
        }
    }
}
