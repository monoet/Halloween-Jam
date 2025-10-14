using UnityEngine;

public class AsciiSpawner : MonoBehaviour
{
    [SerializeField] private GameObject asciiPrefab;
    [SerializeField] private Transform parent;

    public void SpawnAscii(AsciiCharacterData data)
    {
        var go = Instantiate(asciiPrefab, parent);
        var animator = go.GetComponent<AsciiAnimator>();
        animator.SetData(data);
    }
}
