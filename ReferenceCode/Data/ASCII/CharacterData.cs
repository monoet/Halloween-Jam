// Assets/Scripts/Data/ASCII/AsciiCharacterData.cs
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/ASCII/Character Data")]
public class AsciiCharacterData : ScriptableObject
{
    [Tooltip("Frames de ASCII Art que forman la animación")]
    [TextArea(5, 20)] 
    public string[] frames;

    [Tooltip("Frames por segundo (float, puede ser decimal como 0.5)")]
    public float frameRate = 5f;

    [Tooltip("¿Repetir en loop la animación?")]
    public bool loop = true;
}
