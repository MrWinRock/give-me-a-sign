using GameLogic.SpawnAndTime;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for AnomalyScheduler: draws the default fields, then a read-only
/// timeline preview that converts each entry's real-minute spawn time into the
/// in-game clock (0:00-6:00 AM) using the NightTimer duration in the open scene,
/// plus warnings for anything misconfigured.
/// </summary>
[UnityEditor.CustomEditor(typeof(AnomalyScheduler))]
public class AnomalySchedulerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var scheduler = (AnomalyScheduler)target;
        var schedule = serializedObject.FindProperty("schedule");
        if (schedule == null) return;

        // In NightPlan mode the list below is ignored entirely, so say so rather than letting
        // someone edit a timeline that will never run.
        var sourceProp = serializedObject.FindProperty("source");
        bool usingPlan = sourceProp != null && sourceProp.enumValueIndex == (int)ScheduleSource.NightPlan;

        if (usingPlan)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Source is NightPlan: anomalies come from the generated night, and the Schedule list " +
                "below is ignored. Use 'Tools/Give Me A Sign/Night Plan Debugger' to see what a seed " +
                "produces, or switch Source to ManualList to run the list below.",
                MessageType.Info);
            return;
        }

        var timerProp = serializedObject.FindProperty("nightTimer");
        var nightTimer = timerProp != null ? timerProp.objectReferenceValue as NightTimer : null;
        if (nightTimer == null)
            nightTimer = Object.FindFirstObjectByType<NightTimer>();

        float duration = nightTimer != null ? nightTimer.NightDurationMinutes : 5f;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Timeline Preview (night = {duration:0.##} real minutes)", EditorStyles.boldLabel);

        if (nightTimer == null)
            EditorGUILayout.HelpBox("No NightTimer found in the scene - preview assumes a 5-minute night.", MessageType.Warning);

        for (int i = 0; i < schedule.arraySize; i++)
        {
            var entry = schedule.GetArrayElementAtIndex(i);
            float minute = entry.FindPropertyRelative("spawnAtMinute").floatValue;
            var prefab = entry.FindPropertyRelative("anomalyPrefab").objectReferenceValue;
            var spawnPoint = entry.FindPropertyRelative("spawnPoint").objectReferenceValue;

            float gameHours = Mathf.Clamp01(duration > 0f ? minute / duration : 0f) * NightTimer.GameHoursPerNight;
            string clock = NightTimer.FormatGameTime(gameHours, includeSeconds: false);
            string prefabName = prefab != null ? prefab.name : "(no prefab!)";
            string where = spawnPoint != null ? spawnPoint.name : "prefab position";

            EditorGUILayout.LabelField($"  {minute,6:0.##} min  ->  {clock,-8}  {prefabName}  @ {where}");

            if (prefab == null)
                EditorGUILayout.HelpBox($"Entry {i}: no prefab assigned - it will be skipped.", MessageType.Error);
            else if (minute > duration)
                EditorGUILayout.HelpBox($"Entry {i}: minute {minute:0.##} is after the night ends ({duration:0.##} min) - it will never spawn.", MessageType.Warning);
        }
    }
}
