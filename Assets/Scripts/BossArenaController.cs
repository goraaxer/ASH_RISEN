using UnityEngine;
using UnityEngine.Events;

// Bridge for the existing AI/death system. No replacement boss AI or damage rules.
public interface IBossArenaCombat
{
    void BeginArenaCombat(Transform player);
    void SuspendArenaCombat(bool suspended);
    bool IsDefeated { get; }
}
public sealed class BossArenaController : MonoBehaviour
{
    public GameObject boss;
    public Collider2D combatArea;
    public AudioSource battleMusic;
    public UnityEvent onCombatStarted = new UnityEvent();
    public UnityEvent onBossDefeated = new UnityEvent();
    public bool CombatActive { get; private set; }
    public bool Defeated { get; private set; }
    public static bool ExitLocked { get { var a=FindFirstObjectByType<BossArenaController>(); return a!=null && a.CombatActive && !a.Defeated; } }
    IBossArenaCombat combat;
    bool warned;
    Vector3 initialPosition;
    void Start()
    {
        if (battleMusic == null) battleMusic = GetComponent<AudioSource>();
        if (battleMusic != null) { battleMusic.playOnAwake = false; battleMusic.loop = true; battleMusic.Stop(); }
        if(boss!=null)initialPosition=boss.transform.position;
        Defeated=BossArenaProgress.Defeated;
        if(Defeated && boss!=null)boss.SetActive(false);
        if (boss != null) foreach (var c in boss.GetComponents<MonoBehaviour>()) if (c is IBossArenaCombat source) { combat=source; break; }
    }
    void Update()
    {
        bool suspended = PauseMenu.InputBlocked || SceneTransitionManager.IsBusy;
        if (combat != null) { combat.SuspendArenaCombat(suspended || !CombatActive); if (combat.IsDefeated && !Defeated) MarkDefeated(); }
        var arrival = GetComponent<BossArrival>();
        if (suspended || Defeated || CombatActive || combatArea==null ||
            (arrival != null && arrival.enabled && !arrival.ReadyForCombat)) return;
        var p=FindFirstObjectByType<playercontroler>();
        if (p == null || !combatArea.OverlapPoint(p.transform.position)) return;
        // Never trap the player inside an arena with no implemented combat backend.
        if (combat == null && onCombatStarted.GetPersistentEventCount()==0)
        { if(!warned) { Debug.LogWarning("BossArena: existing boss AI/health not found. Combat hook is ready; exit remains open.",this); warned=true; } return; }
        CombatActive=true; combat?.BeginArenaCombat(p.transform);
        if (battleMusic != null && battleMusic.clip != null) battleMusic.Play();
        onCombatStarted.Invoke();
    }
    public void MarkDefeated()
    {
        Defeated=true; CombatActive=false; BossArenaProgress.Defeated=true;
        if (battleMusic != null) battleMusic.Stop();
        onBossDefeated.Invoke();
        var p=FindFirstObjectByType<playercontroler>();if(p!=null&&!GameSaveSystem.Save(p.gameObject,out var error))SceneTransitionManager.Ensure().Notify(error);
    }
    public static void ResetAfterRespawn(bool revive)
    {
        if (!revive) return;
        var a=FindFirstObjectByType<BossArenaController>(); if(a!=null) { a.CombatActive=false;if(a.battleMusic!=null)a.battleMusic.Stop();if(a.boss!=null&&!a.Defeated){a.boss.transform.position=a.initialPosition;a.boss.GetComponent<BossArenaCombat>()?.ResetFight();} }
    }
}
