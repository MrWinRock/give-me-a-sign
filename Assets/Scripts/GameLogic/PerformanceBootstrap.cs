using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Runs once before the first scene loads (no scene setup needed).
    ///
    /// On low-spec machines the game competes with Whisper's speech-recognition threads
    /// for CPU. Two cheap global wins:
    ///   1. Cap the frame rate at 60 when vSync is off - otherwise Unity renders this 2D
    ///      game as fast as the GPU allows, burning CPU/GPU that Whisper needs.
    ///   2. In release builds, stop collecting stack traces for plain Debug.Log calls -
    ///      trace capture is surprisingly expensive and worthless outside development.
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
