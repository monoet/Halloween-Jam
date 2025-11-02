using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Optional metadata component for enemy prefabs (drop table, etc.).
    /// </summary>
    public sealed class EnemyMetadata : MonoBehaviour
    {
        [SerializeField] private ScriptableObject dropTable;

        public ScriptableObject DropTable => dropTable;
    }
}
