using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class SelectedHoundVerification
{
    static SelectedHoundVerification(){EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin(){SessionState.SetBool("hound.verify",true);AssetDatabase.DisallowAutoRefresh();EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange s)
    {
        if(!SessionState.GetBool("hound.verify",false))return;
        if(s==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>new GameObject("Selected hound play check").AddComponent<SelectedHoundPlayCheck>();
        if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool("hound.verify",false);AssetDatabase.AllowAutoRefresh();}
    }
}
