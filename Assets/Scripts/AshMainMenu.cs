using UnityEngine;

public sealed class AshMainMenu : MonoBehaviour
{
    const float ThemeStartSeconds = 5.2f;
    AudioSource music;
    float previousMusicTime;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = PlayerPrefs.GetFloat("settings.volume", 1f);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        music = GetComponent<AudioSource>();
        if (music != null && music.clip != null)
        {
            music.time = Mathf.Min(ThemeStartSeconds, music.clip.length - .1f);
            previousMusicTime = music.time;
            music.Play();
        }
    }

    void Update()
    {
        if (music == null || !music.isPlaying) return;
        if (music.time < previousMusicTime - 1f)
            music.time = Mathf.Min(ThemeStartSeconds, music.clip.length - .1f);
        previousMusicTime = music.time;
    }

    void Start()
    {
        var view = gameObject.AddComponent<DynamicAshMenuView>();
        view.Configure(null, true);
    }
}
