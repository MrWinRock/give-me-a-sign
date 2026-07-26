using Report;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whisper;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Cross-wires the scene's WhisperMicInput and Incident Report components. These two
    /// scripts reference each other (WhisperMicInput needs to know where to route recognized
    /// text; IncidentReportUI needs to know which mic to start/stop), and since both fields
    /// were added after the objects already existed in the scene, they're easy to leave
    /// unassigned - which silently breaks routing without throwing any error (the recognized
    /// keyword shows up in the console via Whisper's own internal logging, but never reaches
    /// the Incident Report window's text field because IncidentReportManager.Route() is never
    /// called in the first place).
    /// </summary>
    public static class SceneWiringTools
    {
        [MenuItem("Tools/Give Me A Sign/Wire Whisper + Incident Report References")]
        public static void WireReferences()
        {
            var whisperMic = Object.FindObjectOfType<WhisperMicInput>(true);
            var reportManager = Object.FindObjectOfType<IncidentReportManager>(true);
            var reportUI = Object.FindObjectOfType<IncidentReportUI>(true);

            if (whisperMic == null || reportManager == null || reportUI == null)
            {
                Debug.LogError("SceneWiringTools: missing component(s) in the active scene - " +
                    $"WhisperMicInput={(whisperMic != null)}, IncidentReportManager={(reportManager != null)}, IncidentReportUI={(reportUI != null)}");
                return;
            }

            // WhisperMicInput.incidentReportManager is a public field.
            if (whisperMic.incidentReportManager != reportManager)
            {
                whisperMic.incidentReportManager = reportManager;
                EditorUtility.SetDirty(whisperMic);
                Debug.Log("SceneWiringTools: set WhisperMicInput.incidentReportManager.");
            }
            else
            {
                Debug.Log("SceneWiringTools: WhisperMicInput.incidentReportManager already wired.");
            }

            // IncidentReportUI.whisperMicInput is a private [SerializeField] field.
            var so = new SerializedObject(reportUI);
            var prop = so.FindProperty("whisperMicInput");
            if (prop == null)
            {
                Debug.LogError("SceneWiringTools: couldn't find 'whisperMicInput' field on IncidentReportUI.");
            }
            else if (prop.objectReferenceValue != whisperMic)
            {
                prop.objectReferenceValue = whisperMic;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(reportUI);
                Debug.Log("SceneWiringTools: set IncidentReportUI.whisperMicInput.");
            }
            else
            {
                Debug.Log("SceneWiringTools: IncidentReportUI.whisperMicInput already wired.");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("SceneWiringTools: done. Scene has unsaved changes - review then Ctrl+S.");
        }
    }
}
