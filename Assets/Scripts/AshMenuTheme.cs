using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Ashrisen/Ash Menu Theme")]
public sealed class AshMenuTheme : ScriptableObject
{
    public TMP_FontAsset font;
    public Sprite city;
    public Sprite cracks;
    public Sprite panel;
    public Sprite emblem;
    public Sprite[] heroineIdle;
    public Texture2D noise;
    public AudioClip open;
    public AudioClip ember;
    public AudioClip ash;
    public AudioClip close;
}
