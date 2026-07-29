using Audio;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only control panel for testing AudioManager without needing a real in-game Options
/// menu yet. Reachable via Window > Audio > Volume Mixer (Debug).
///
/// This drives AudioManager.Instance directly - the SAME object a future in-game Options
/// menu would bind its sliders to - so tuning here previews exactly what players will hear.
/// Only usable in Play mode: outside Play mode there is no live AudioManager instance to
/// drive (dragging the sliders on the AudioManager PREFAB ASSET in the Project window only
/// changes its default starting values for the next Play session, it does not affect a
/// running game - this window is what actually lets you hear changes live).
/// </summary>
public class AudioDebugWindow : EditorWindow
{
    [MenuItem("Window/Audio/Volume Mixer (Debug)")]
    private static void Open()
    {
        GetWindow<AudioDebugWindow>("Audio Mixer");
    }

    // AudioManager.MasterVolume etc. can change from outside this window (PlayerPrefs load,
    // another tool, a future in-game slider) - repaint continuously while playing so the
    // sliders shown here never go stale.
    void OnEnable() => EditorApplication.update += Repaint;
    void OnDisable() => EditorApplication.update -= Repaint;

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode to adjust volumes live.\n\n" +
                "Editing the sliders on the AudioManager prefab asset (in the Project window) " +
                "only changes its default starting values for next time - it will NOT change " +
                "anything while the game is already running. Use this window instead once " +
                "you're in Play mode.",
                MessageType.Info);
            return;
        }

        var mgr = AudioManager.Instance;
        if (mgr == null)
        {
            EditorGUILayout.HelpBox(
                "AudioManager.Instance is null. It's supposed to bootstrap itself automatically " +
                "before the first scene loads - if this stays empty, check the Console for errors " +
                "and confirm Assets/Resources/AudioManager.prefab still exists.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Live Volume Mixer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Changes here apply immediately to every sound in the game.", MessageType.None);
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        float master = EditorGUILayout.Slider("Master", mgr.MasterVolume, 0f, 1f);
        float music = EditorGUILayout.Slider("Music", mgr.MusicVolume, 0f, 1f);
        float sfx = EditorGUILayout.Slider("SFX", mgr.SfxVolume, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            mgr.MasterVolume = master;
            mgr.MusicVolume = music;
            mgr.SfxVolume = sfx;
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Sound Library Test", EditorStyles.boldLabel);
        if (GUILayout.Button("Play \"JumpScare\""))
            mgr.Play("JumpScare");
        if (GUILayout.Button("Play \"Transition\""))
            mgr.Play("Transition");
        if (GUILayout.Button("Play \"CameraShutter\""))
            mgr.Play("CameraShutter");
    }
}
