using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Save;
using UnityEngine;

namespace GameLogic.Story
{
    /// <summary>
    /// Builds the queue of story cinematics for the current night (opening broadcast, act
    /// transitions on nights 3/6, training on nights with a new anomaly) and plays them with
    /// the game paused. Drop one on a GameObject in the gameplay scene.
    /// </summary>
    public class StoryCinematicController : MonoBehaviour
    {
        [Header("Opening (before night 1, once per playthrough)")]
        [Tooltip("Game-opening news broadcast. Empty = no opening cutscene.")]
        public CinematicData OpeningCutscene;

        [Header("Act transitions (forced news on the first night of each act, once)")]
        public List<ActTransition> ActTransitions = new List<ActTransition>();

        [Header("Training (plays on the listed night, every attempt)")]
        public List<TrainingBeat> TrainingBeats = new List<TrainingBeat>();

        [Header("Playback")]
        [Tooltip("Auto-pause the game (timeScale = 0) while a cinematic plays. Off leaves the clock running under the cutscene.")]
        [SerializeField] private bool pauseDuringCinematic = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private CinematicPlayer _player;
        private bool _showing;

        void OnDestroy()
        {
            // If a scene unload kills this mid-playback, the coroutine dies with the restore
            // line unrun - same safety net as PauseMenuController.
            if (_showing) Time.timeScale = 1f;
            // Component destroy order across siblings on the same GameObject is undefined, so
            // this races CinematicPlayer's own OnDisable/Teardown - RestoreDuckedAudio is a no-op
            // once that has already run.
            _player?.RestoreDuckedAudio();
        }

        [System.Serializable]
        public class ActTransition
        {
            [Tooltip("First night this act begins (e.g. 3 or 6).")]
            [Min(1)] public int startsOnNight = 1;
            public CinematicData cinematic;
        }

        [System.Serializable]
        public class TrainingBeat
        {
            [Tooltip("Night this anomaly first appears. Training replays every retry of that night.")]
            [Min(1)] public int nightIndex = 2;
            public CinematicData cinematic;
        }

        void Start()
        {
            // Only run the cinematic gate when the scene was entered for an actual night (the
            // persistent GameFlowManager loads GamePlay for day gameplay). Guarded so the scene
            // can also be opened directly for editing/tests without auto-gating on something odd.
            if (GameFlowManager.State != GameFlowState.DayGameplay) return;

            if (showDebugInfo)
                Debug.Log($"[StoryCinematic] preparing night {GameFlowManager.CurrentDay} story queue.", this);

            StartCoroutine(RunQueue());
        }

        private IEnumerator RunQueue()
        {
            _player = GetComponent<CinematicPlayer>();
            if (_player == null) _player = gameObject.AddComponent<CinematicPlayer>();

            int night = GameFlowManager.CurrentDay;
            foreach (var cinematic in BuildQueue(night))
            {
                if (cinematic == null || !cinematic.IsPlayable()) continue;
                yield return PlayOne(cinematic);
            }
        }

        /// <summary>The cinematics that should play tonight, in story order.</summary>
        private IEnumerable<CinematicData> BuildQueue(int night)
        {
            var save = SaveManager.Current;
            var queue = new List<CinematicData>();

            // 1. Opening broadcast - once, before night 1.
            if (OpeningCutscene != null && night == 1 && !save.IsCinematicPlayed(OpeningCutscene.cinematicId))
                queue.Add(OpeningCutscene);

            // 2. Act transitions - once, on the first night of each act.
            foreach (var act in ActTransitions)
            {
                if (act == null || act.cinematic == null) continue;
                if (act.startsOnNight != night) continue;
                if (save.IsCinematicPlayed(act.cinematic.cinematicId)) continue;
                queue.Add(act.cinematic);
            }

            // 3. Training - every attempt of a night that introduces a new anomaly.
            foreach (var beat in TrainingBeats)
            {
                if (beat == null || beat.cinematic == null) continue;
                if (beat.nightIndex == night) queue.Add(beat.cinematic);
            }

            return queue;
        }

        private IEnumerator PlayOne(CinematicData cinematic)
        {
            if (showDebugInfo)
                Debug.Log($"[StoryCinematic] playing '{cinematic.cinematicId}' ({cinematic.kind}) for night {GameFlowManager.CurrentDay}.", this);

            float previousTimeScale = Time.timeScale;
            if (pauseDuringCinematic) Time.timeScale = 0f;

            _showing = true;
            bool done = false;
            _player.Play(cinematic, () => done = true);

            while (!done) yield return null;
            _showing = false;

            if (pauseDuringCinematic) Time.timeScale = previousTimeScale;
        }
    }
}