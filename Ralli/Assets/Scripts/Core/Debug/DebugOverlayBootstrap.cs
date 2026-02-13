using UnityEngine;

public static class DebugOverlayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOverlayExists()
    {
        DebugOverlay existing = Object.FindFirstObjectByType<DebugOverlay>();
        if (existing != null)
        {
            return;
        }

        GameObject debugRoot = new GameObject("DebugOverlay");
        Object.DontDestroyOnLoad(debugRoot);
        debugRoot.AddComponent<DebugOverlay>();
    }
}
