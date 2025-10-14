using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Datos para configurar encuentros de combate.
/// </summary>
[CreateAssetMenu(fileName = "EncounterData", menuName = "Combat/Encounter Data")]
public class EncounterData : ScriptableObject
{
    [SerializeField] private string encounterName;
    [SerializeField] private List<EnemyRuntime> enemiesToSpawn = new List<EnemyRuntime>();
    [SerializeField] private List<Vector3> spawnPoints = new List<Vector3>();

    public string EncounterName => encounterName;
    public List<EnemyRuntime> EnemiesToSpawn => enemiesToSpawn;
    public List<Vector3> SpawnPoints
    {
        get
        {
            if (spawnPoints == null) spawnPoints = new List<Vector3>();
            return spawnPoints;
        }
    }

    public Vector3 GetSpawnPoint(int index)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return Vector3.zero;

        if (index < 0 || index >= spawnPoints.Count)
            return spawnPoints[spawnPoints.Count - 1];

        return spawnPoints[index];
    }
}
