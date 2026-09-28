using System.Collections.Generic;
using System.IO;
using GameLogic.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// One-click bootstrap for the 3-act story cutscene system (see Docs - the campaign
    /// timeline doc): creates the CinematicData assets it references, adds the
    /// StoryCinematicController + CinematicPlayer to the gameplay scene, and wires everything.
    ///
    /// All cutscene clips are left EMPTY on creation, so the editor still needs no video files:
    /// the system falls back to a title/subtitle placeholder until the real footage is dropped
    /// in. Re-running is safe - it creates nothing that exists, and only adds the controller if
    /// the scene does not already have one.
    ///
    /// Everything is under version control: `git checkout Assets/Settings Assets/Scenes
    /// Assets/Scripts` undoes a bad run.
    /// </summary>
    public static class StorySetupTools
    {
        private const string StoryFolder = "Assets/Settings/Story";

        [MenuItem("Tools/Give Me A Sign/Story/1. Create Cinematic Assets")]
        public static void CreateCinematicAssets()
        {
            EnsureFolder(StoryFolder);

            // One opening + two act transitions (nights 3 and 6) + a training beat per night
            // that introduces a new anomaly (2,3,4,5 - night 1 is the tutorial, 6/7 add none).
            Create("Cinematic_Opening", CinematicKind.Opening,
                "C.N.A. NEWS - EVACUATION IN EFFECT",
                "Report to your station. The Bureau is moving in.");
            Create("Cinematic_Act2_Transition", CinematicKind.ActTransition,
                "C.N.A. NEWS - SITUATION CRITICAL",
                "Living space is shrinking. Everything changes tonight.");
            Create("Cinematic_Act3_Transition", CinematicKind.ActTransition,
                "C.N.A. NEWS - AUTHORITY TRANSFERRED",
                "The Bureau now answers to no one.");
            Create("Cinematic_Training_Night2", CinematicKind.Training,
                "TRAINING - NEW SUBJECTS DETECTED",
                "Review the signs before your shift.");
            Create("Cinematic_Training_Night3", CinematicKind.Training,
                "TRAINING - NEW SUBJECTS DETECTED",
                "Pay attention. This one does not wait.");
            Create("Cinematic_Training_Night4", CinematicKind.Training,
                "TRAINING - NEW SUBJECTS DETECTED",
                "Update your field manual.");
            Create("Cinematic_Training_Night5", CinematicKind.Training,
                "TRAINING - NEW SUBJECTS DETECTED",
                "Final warning before the long night.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StorySetupTools] Cinematic assets ready. Assign VideoClip values later; placeholders work until then.");
        }

        [MenuItem("Tools/Give Me A Sign/Story/2. Wire Story Controller Into Gameplay Scene")]
        public static void WireIntoGameplayScene()
        {
            const string sceneName = "GamePlay";

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var gameplay = SceneManager.GetSceneByName(sceneName);
            bool alreadyOpen = gameplay.IsValid() && gameplay.isLoaded;
            if (!alreadyOpen)
            {
                var previous = SceneManager.GetActiveScene();
                EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity");
                Debug.Log($"[StorySetupTools] Opened '{sceneName}' - re-run only creates what's missing.");
            }

            var controller = EnsureController();
            WireAssets(controller);
            MarkSceneDirty();
        }

        private static StoryCinematicController EnsureController()
        {
            var existing = Object.FindFirstObjectByType<StoryCinematicController>();
            if (existing != null) return existing;

            var host = new GameObject("StoryCinematics");
            var controller = host.AddComponent<StoryCinematicController>();
            host.AddComponent<CinematicPlayer>();

            Debug.Log("[StorySetupTools] Added 'StoryCinematics' GameObject with StoryCinematicController + CinematicPlayer.");
            return controller;
        }

        private static void WireAssets(StoryCinematicController controller)
        {
            var opening = Load<CinematicData>("Cinematic_Opening");

            controller.OpeningCutscene = opening;

            controller.ActTransitions.Clear();
            controller.ActTransitions.Add(new StoryCinematicController.ActTransition
            {
                startsOnNight = 3,
                cinematic = Load<CinematicData>("Cinematic_Act2_Transition"),
            });
            controller.ActTransitions.Add(new StoryCinematicController.ActTransition
            {
                startsOnNight = 6,
                cinematic = Load<CinematicData>("Cinematic_Act3_Transition"),
            });

            controller.TrainingBeats.Clear();
            AddTraining(controller, 2, "Cinematic_Training_Night2");
            AddTraining(controller, 3, "Cinematic_Training_Night3");
            AddTraining(controller, 4, "Cinematic_Training_Night4");
            AddTraining(controller, 5, "Cinematic_Training_Night5");

            EditorUtility.SetDirty(controller);
        }

        private static void AddTraining(StoryCinematicController controller, int night, string assetName)
        {
            controller.TrainingBeats.Add(new StoryCinematicController.TrainingBeat { nightIndex = night, cinematic = Load<CinematicData>(assetName) });
        }

        private static T Load<T>(string name) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets(name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        private static void MarkSceneDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[StorySetupTools] GamePlay scene saved with story controller wired.");
        }

        private static void Create(string assetName, CinematicKind kind, string title, string subtitle)
        {
            var path = $"{StoryFolder}/{assetName}.asset";
            if (File.Exists(path)) return; // idempotent - never clobber

            var data = ScriptableObject.CreateInstance<CinematicData>();
            data.cinematicId = assetName;
            data.kind = kind;
            data.placeholderTitle = title;
            data.placeholderSubtitle = subtitle;
            data.skippableAfterSeconds = 2f;

            AssetDatabase.CreateAsset(data, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}