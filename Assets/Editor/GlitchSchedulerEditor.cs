using Report;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for GlitchScheduler: draws the default fields, then a read-only timeline preview
/// that converts each entry's real-minute fire time into the in-game clock (0:00-6:00 AM)
/// using the NightTimer duration in the open scene, plus a warning for anything misconfigured.
/// </summary>
[UnityEditor.CustomEditor(typeof(GlitchScheduler))]
public class GlitchSchedulerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var schedule = serializedObject.FindProperty("schedule");
        if (schedule == null) return;

        var timerProp = serializedObject.FindProperty("nightTimer");
        var nightTimer = timerProp != null ? timerProp.objectReferenceValue as GameLogic.SpawnAndTime.NightTimer : null;
        if (nightTimer == null)
            nightTimer = Object.FindFirstObjectByType<GameLogic.SpawnAndTime.NightTimer>();

        float duration = nightTimer != null ? nightTimer.NightDurationMinutes : 5f;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Timeline Preview (night = {duration:0.##} real minutes)", EditorStyles.boldLabel);

        if (nightTimer == null)
            EditorGUILayout.HelpBox("No NightTimer found in the scene - preview assumes a 5-minute night.", MessageType.Warning);

        for (int i = 0; i < schedule.arraySize; i++)
        {
            var entry = schedule.GetArrayElementAtIndex(i);
            float minute = entry.FindPropertyRelative("atMinute").floatValue;
            var glitchTypeProp = entry.FindPropertyRelative("glitchType");
            string glitchType = glitchTypeProp.enumDisplayNames[glitchTypeProp.enumValueIndex];
            float fireDelay = entry.FindPropertyRelative("fireDelay").floatValue;

            float gameHours = Mathf.Clamp01(duration > 0f ? minute / duration : 0f) * GameLogic.SpawnAndTime.NightTimer.GameHoursPerNight;
            string clock = GameLogic.SpawnAndTime.NightTimer.FormatGameTime(gameHours, includeSeconds: false);
            string delaySuffix = fireDelay > 0f ? $" (+{fireDelay:0.#}s delay)" : "";

            EditorGUILayout.LabelField($"  {minute,6:0.##} min  ->  {clock,-8}  {glitchType}{delaySuffix}");

            if (minute > duration)
                EditorGUILayout.HelpBox($"Entry {i}: minute {minute:0.##} is after the night ends ({duration:0.##} min) - it will never fire.", MessageType.Warning);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Glitches only affect the Incident Report window's own widgets, so a scheduled " +
            "entry whose minute passes while the form is closed queues up and fires the moment " +
            "the form next opens - nothing scheduled is ever silently skipped.",
            MessageType.Info);
    }
}
