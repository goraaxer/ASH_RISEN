#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ChurchAwakeningVerification
{
    const string Flag = "Temp/verify-church-awakening";
    const string ReturnScene = "church.verify.returnScene";
    static ChurchAwakeningVerification()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.delayCall += Begin;
    }
    static void Begin()
    {
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Flag);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) return;
        SessionState.SetString(ReturnScene, EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool("church.verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/BossArena.unity");
        EditorApplication.isPlaying = true;
    }
    [MenuItem("Tools/Ashrisen/Boss/Verify Church Intro")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(ReturnScene, EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool("church.verify", true);
        EditorSceneManager.OpenScene("Assets/Scenes/BossArena.unity");
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("church.verify", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
            new GameObject("Church verification").AddComponent<ChurchAwakeningCheck>();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("church.verify", false);
            string returnScene = SessionState.GetString(ReturnScene, "Assets/Scenes/BossArena.unity");
            if (!string.IsNullOrEmpty(returnScene)) EditorSceneManager.OpenScene(returnScene);
        }
    }
}
public sealed class ChurchAwakeningCheck : MonoBehaviour
{
    string report = "";
    void Check(bool value, string label) { report += (value ? "PASS " : "FAIL ") + label + "\n"; }
    IEnumerator Start()
    {
        yield return null;
        BossArenaProgress.Defeated = false;
        var arrival = FindFirstObjectByType<BossArrival>();
        var arena = FindFirstObjectByType<BossArenaController>();
        var player = FindFirstObjectByType<playercontroler>();
        var boss = arrival.boss;
        bool savedProgress = BossArenaProgress.Defeated;
        boss.gameObject.SetActive(true);
        BossArenaProgress.Defeated = false;
        if (arena.Defeated)
            typeof(BossArenaController).GetField("<Defeated>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arena, false);
        if (arrival.ReadyForCombat)
        {
            typeof(BossArrival).GetField("<ReadyForCombat>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(arrival, false);
            typeof(BossArrival).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(arrival, null);
        }
        var sprite = boss.GetComponent<SpriteRenderer>();
        Check(sprite.color.a > .99f && sprite.sprite.texture.name == "Idle" &&
            sprite.sprite.name == "Idle_00", "Original monster visible on entry");
        Check(!arena.CombatActive && !arrival.IsPlaying, "Entry permits approach before intro");
        player.transform.position += Vector3.right * 4.5f;
        yield return null;
        yield return null;
        Check(arrival.IsPlaying && PlayerControlLock.IsBlocked, "Approach starts intro and locks input");
        Check(boss.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("BossDyn_IntroAwakening") &&
            sprite.sprite.texture.name == "IntroAwakening" && sprite.sprite.rect.width == 96 &&
            sprite.sprite.rect.height == 128 && sprite.sprite.pivot == new Vector2(48, 21) &&
            sprite.sprite.texture.filterMode == FilterMode.Point,
            "Intro uses the same pixel canvas and pivot as the boss fight");
        Check(!arena.CombatActive, "Combat remains suspended during awakening");
        yield return new WaitForSeconds(2.7f);
        Check(arrival.IsPlaying && sprite.sprite.texture.name == "IntroAwakening" &&
            sprite.sprite.name != "IntroAwakening_00", "Head raise stays on the original boss sprite");
        Directory.CreateDirectory("Artifacts/ChurchAwakening");
        ScreenCapture.CaptureScreenshot("Artifacts/ChurchAwakening/intro.png");
        yield return new WaitForSeconds(2.6f);
        Check(arrival.ReadyForCombat && !arrival.IsPlaying, "Awakening completes in five seconds");
        Check(!PlayerControlLock.IsBlocked, "Player input restored");
        Check(arena.CombatActive && boss.IsFighting, "Existing combat starts after awakening");
        Check(boss.GetComponent<Animator>().speed > 0, "Animator playback restored");
        Check(sprite.sprite.texture.name != "BossHeadRaiseSheet" && !sprite.sprite.texture.name.StartsWith("HeadRaise"),
            "Combat begins without switching to the old cutscene artwork");
        Directory.CreateDirectory("Artifacts/ChurchAwakening");
        File.WriteAllText("Artifacts/ChurchAwakening/results.txt", report);
        BossArenaProgress.Defeated = savedProgress;
        EditorApplication.isPlaying = false;
    }
}
#endif
