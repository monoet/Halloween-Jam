using System;
using System.Collections.Generic;

namespace BattleV2.Targeting
{
    /// <summary>
    /// Immutable representation of resolved targets expressed via instance ids.
    /// </summary>
    public readonly struct TargetSet
    {
        private readonly IReadOnlyList<int> ids;

        private TargetSet(bool isGroup, int singleId, IReadOnlyList<int> ids)
        {
            IsGroup = isGroup;
            SingleId = singleId;
            this.ids = ids;
        }

        public bool IsGroup { get; }
        public int SingleId { get; }
        public IReadOnlyList<int> Ids => ids;

        public bool TryGetSingle(out int id)
        {
            if (!IsGroup && SingleId != 0)
            {
                id = SingleId;
                return true;
            }

            if (IsGroup && ids != null && ids.Count == 1)
            {
                id = ids[0];
                return true;
            }

            id = 0;
            return false;
        }

        public bool IsEmpty =>
            (!IsGroup && SingleId == 0) ||
            (IsGroup && (ids == null || ids.Count == 0));

        public static TargetSet None => new TargetSet(false, 0, Array.Empty<int>());

        public static TargetSet Single(int instanceId)
        {
            return new TargetSet(false, instanceId, Array.Empty<int>());
        }

        public static TargetSet Group(IReadOnlyList<int> instanceIds)
        {
            return instanceIds != null && instanceIds.Count > 0
                ? new TargetSet(true, 0, instanceIds)
                : None;
        }

        public TargetSet WithSingleFallback(int instanceId)
        {
            if (!IsEmpty)
            {
                return this;
            }

            return Single(instanceId);
        }
    }
}
