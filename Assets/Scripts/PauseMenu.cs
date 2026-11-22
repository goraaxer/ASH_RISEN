using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    static int blockedThroughFrame = -1;
    public static bool IsPaused => Instance != null && Instance.paused;
    public static bool InputBlocked => PlayerControlLock.IsBlocked || IsPaused ||
        Time.frameCount <= blockedThroughFrame;
    public bool SettingsOpen => page == 1;
    public string Status => status;

    bool paused;
    int page;
    float previousTimeScale;
    bool previousAudioPause;
    CursorLockMode previousLock;
    bool previousCursor;
    string status = "";
    GameObject player;
    Rigidbody2D restoredBody;
    bool restoreSimulation;
    DynamicAshMenuView view;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        blockedThroughFrame = -1;
        SceneManager.sceneLoaded -= EnsureMenu;
        SceneManager.sceneLoaded += EnsureMenu;
    }
    static void EnsureMenu(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<playercontroler>(true) != null)
            {
                if (FindFirstObjectByType<PauseMenu>() == null)
                    new GameObject("Pause Menu").AddComponent<PauseMenu>();
                return;
            }
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        player = FindFirstObjectByType<playercontroler>()?.gameObject;
        AudioListener.volume = PlayerPrefs.GetFloat("settings.volume", 1f);
        if (PlayerPrefs.HasKey("settings.quality"))
            QualitySettings.SetQualityLevel(Mathf.Clamp(
                PlayerPrefs.GetInt("settings.quality"), 0,
                QualitySettings.names.Length - 1));
        if (PlayerPrefs.HasKey("settings.fullscreen"))
            Screen.fullScreen = PlayerPrefs.GetInt("settings.fullscreen") != 0;
        view = gameObject.AddComponent<DynamicAshMenuView>();
        view.Configure(this, false);
    }
    void Start()
    {
        if (player == null || !GameSaveSystem.RestorePending(player)) return;
        restoredBody = player.GetComponent<Rigidbody2D>();
        restoreSimulation = restoredBody.simulated;
        restoredBody.simulated = false;
        blockedThroughFrame = Time.frameCount + 1;
    }
    void LateUpdate()
    {
        if (restoredBody == null) return;
        restoredBody.simulated = restoreSimulation;
        restoredBody = null;
    }
    void Update()
    {
        if (PlayerControlLock.IsBlocked && !paused) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (!paused) { Pause(); return; }
        if (view != null && view.IsTransitioning)
        {
            view.Skip();
            if (paused && page == 0 && !view.IsTransitioning)
            {
                Resume();
                view.Skip();
            }
            return;
        }
        if (page != 0) { PlayerPrefs.Save(); OpenPage(0); }
        else Resume();
    }
    public void Pause()
    {
        if (paused) return;
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousLock = Cursor.lockState;
        previousCursor = Cursor.visible;
        PlayerControlLock.Acquire(this, null, false);
        paused = true;
        page = 0;
        status = "";
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        view.Open();
    }
    public void Resume()
    {
        if (!paused) return;
        if (view != null) view.Close(FinishResume);
        else FinishResume();
    }
    void FinishResume()
    {
        if (!paused) return;
        PlayerControlLock.Release(this);
        paused = false;
        blockedThroughFrame = Time.frameCount + 1;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursor;
        page = 0;
    }
    public void OpenPage(int target)
    {
        if (!paused) return;
        page = target;
        status = "";
        view.ShowPage(target);
    }
    public void OpenSettings() { OpenPage(1); }
    public bool SaveGame()
    {
        bool success = GameSaveSystem.Save(player, out var error);
        status = success ? GameText.Get("menu.saved") : error;
        return success;
    }
    public void SetVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("settings.volume", AudioListener.volume);
    }
    public void SetQuality(int quality)
    {
        QualitySettings.SetQualityLevel(Mathf.Clamp(quality, 0,
            QualitySettings.names.Length - 1));
        PlayerPrefs.SetInt("settings.quality", QualitySettings.GetQualityLevel());
        PlayerPrefs.Save();
    }
    public void SetFullscreen(bool full)
    {
        Screen.fullScreen = full;
        PlayerPrefs.SetInt("settings.fullscreen", full ? 1 : 0);
        PlayerPrefs.Save();
    }
    public void ReturnToMainMenu(bool save)
    {
        if (save && !SaveGame()) { view.SetStatus(status); return; }
        PlayerPrefs.Save();
        view.Close(() => {
            FinishResume();
            SceneManager.LoadScene("MainMenu");
        });
    }
    void OnDisable()
    {
        FinishResume();
        if (Instance == this) Instance = null;
    }
    void OnApplicationQuit()
    {
        PlayerPrefs.Save();
        FinishResume();
    }
}
