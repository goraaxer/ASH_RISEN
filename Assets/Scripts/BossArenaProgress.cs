public static class BossArenaProgress
{
    static bool loaded, defeated;
    public static bool Defeated
    {
        get { if(!loaded){loaded=true;if(GameSaveSystem.TryRead(out var data,out _))defeated=data.churchBossDefeated;}return defeated; }
        set { loaded=true;defeated=value; }
    }
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){loaded=false;defeated=false;}
}
