using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Runs once before the first scene loads (no scene setup needed).
    /// </summary>
    public static class PerformanceBootstrap
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // With vSync on, the display already caps the frame rate; only cap it ourselves
            // when vSync is off (the project's low quality levels have vSyncCount 0).
            if (QualitySettings.vSyncCount == 0)
                Application.targetFrameRate = TargetFrameRate;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
        }
    }
}
