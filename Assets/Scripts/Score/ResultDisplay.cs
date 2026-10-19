using System.Collections;
using GameLogic.Flow;
using GameLogic.Night;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Score
{
    /// <summary>
    /// Shows how the night went. Reads the single <see cref="NightResult"/> that
    /// <see cref="GameFlowManager"/> built, instead of reassembling it from four PlayerPrefs
    /// keys written by three different scripts.
    /// </summary>
    public class ResultDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI thresholdText; // Shows what score was needed to win

        [Header("Status Colors")]
        [SerializeField] private Color winColor = Color.green;
        [SerializeField] private Color loseColor = Color.red;

        [Header("Sprint 6 - Seed / Progression")]
        [Tooltip("Optional. Shows 'Seed 123456 · Night 2'.")]
        [SerializeField] private TextMeshProUGUI seedText;
        [Tooltip("Optional. Shows an 'unlocked Night N' message on a win, blank otherwise.")]
        [SerializeField] private TextMeshProUGUI progressionText;
        [Tooltip("Optional. Reloads the gameplay scene forced back onto this exact seed - same NightPlanProvider.ForcedSeed mechanism the debug Night Plan HUD (F3) already uses.")]
        [SerializeField] private Button replaySeedButton;
        [Tooltip("Optional. Resets progression to Night 1 and starts a fresh run - most relevant after finishing the campaign, but works from any Result screen.")]
        [SerializeField] private Button restartCampaignButton;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button quitButton;

        [Header("Exit Shutter")]
        [Tooltip("Played fullscreen for a moment when Play Again or Quit is pressed. Leave empty for no transition.")]
        [SerializeField] private VideoClip shutterClip;
        [Tooltip("How long it plays before the button's action happens.")]
        [Min(0f)] [SerializeField] private float shutterSeconds = 0.3f;

        [Header("Scene Names")]
        [SerializeField] private string gameSceneName = "GameScene"; // Scene 1 (Night gameplay)

        [Header("Anomaly Defeat Objects")]
        [SerializeField] private GameObject[] anomalyDefeatObjects; // Objects to activate when defeated by anomaly
        [SerializeField] private GameObject[] normalResultObjects; // Objects to activate for normal results

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private int _resultSeed;
        private Canvas _shutterCanvas;
        private VideoPlayer _shutterPlayer;
        private RenderTexture _shutterTexture;
        private bool _leaving;

        void Start()
        {
            Display(ResolveResult());
            SetupButtons();
            PrepareShutter();
        }

        void OnDestroy()
        {
            if (_shutterTexture != null) _shutterTexture.Release();
        }

        // Built hidden at Start so the clip is already decoded when a button is pressed.
        private void PrepareShutter()
        {
            if (shutterClip == null) return;

            var root = new GameObject("ExitShutter", typeof(RectTransform));
            _shutterCanvas = root.AddComponent<Canvas>();
            _shutterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _shutterCanvas.sortingOrder = 2000;
            _shutterCanvas.enabled = false;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            _shutterTexture = new RenderTexture(1280, 720, 0);

            var imageObject = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(root.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.texture = _shutterTexture;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _shutterPlayer = root.AddComponent<VideoPlayer>();
            _shutterPlayer.playOnAwake = false;
            _shutterPlayer.clip = shutterClip;
            _shutterPlayer.isLooping = false;
            _shutterPlayer.renderMode = VideoRenderMode.RenderTexture;
            _shutterPlayer.targetTexture = _shutterTexture;
            _shutterPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _shutterPlayer.Prepare();
        }

        // Shows the shutter for a beat, then runs the button's action. Presses during it are ignored.
        private IEnumerator ShutterThen(System.Action action)
        {
            if (_leaving) yield break;
            _leaving = true;

            if (_shutterPlayer != null && shutterSeconds > 0f)
            {
                // A press in the first moments after the scene loads: wait briefly for the clip.
                float waited = 0f;
                while (!_shutterPlayer.isPrepared && waited < 1f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                var audio = Audio.AudioManager.Instance;
                if (audio != null)
                    _shutterPlayer.SetDirectAudioVolume(0, audio.GetEffectiveVolume(Audio.AudioChannel.Sfx));

                _shutterCanvas.enabled = true;
                _shutterPlayer.Play();
                yield return new WaitForSecondsRealtime(shutterSeconds);
            }

            action();
        }

        private NightResult ResolveResult()
        {
            var result = GameFlowManager.LastResult;
            if (result != null) return result;

            Debug.LogWarning("ResultDisplay: no NightResult recorded (Result scene opened directly?). Showing placeholder values.", this);
            return NightResult.Dummy();
        }

        private void Display(NightResult result)
        {
            _resultSeed = result.seed;

            if (seedText != null)
                seedText.text = $"Seed {result.seed} · Night {result.nightIndex}";

            if (progressionText != null)
            {
                progressionText.text = result.IsCampaignComplete
                    ? "Campaign complete."
                    : result.Won
                        ? $"Night {GameFlowManager.UnlockedNightIndex} unlocked."
                        : string.Empty;
            }

            // Being caught reads as a different kind of ending from simply not scoring enough,
            // so it gets its own objects and copy.
            if (result.KilledByThreat)
            {
                SetTexts(
                    status: "YOU LOSE!",
                    statusColor: loseColor,
                    score: "You were consumed by the darkness...",
                    threshold: "");

                SetActiveAll(anomalyDefeatObjects, true);
                SetActiveAll(normalResultObjects, false);
            }
            else if (result.IsCampaignComplete)
            {
                // Sprint 6, S-603: the designed 5-night arc ends here, not on a silently-scaling
                // night 6 - see NightResult.FinalNightIndex.
                SetTexts(
                    status: "YOU SURVIVED THE WEEK",
                    statusColor: winColor,
                    score: "Thank you for playing.",
                    threshold: "");

                SetActiveAll(normalResultObjects, true);
                SetActiveAll(anomalyDefeatObjects, false);
            }
            else
            {
                SetTexts(
                    status: result.Won ? "YOU WIN!" : "YOU LOSE!",
                    statusColor: result.Won ? winColor : loseColor,
                    score: "",
                    threshold: "");

                SetActiveAll(normalResultObjects, true);
                SetActiveAll(anomalyDefeatObjects, false);
            }

            if (showDebugInfo)
            {
                Debug.Log(
                    $"Results: outcome={result.outcome}, score={result.score}/{result.requiredScore}, " +
                    $"won={result.Won}, reports={result.reportsFiled} ({result.reportsFailed} failed), " +
                    $"survivedUntil={result.survivedUntilHour:0.00}h, killedBy='{result.killedByAnomalyId ?? "-"}'.", this);
            }
        }

        private void SetTexts(string status, Color statusColor, string score, string threshold)
        {
            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = statusColor;
            }

            if (scoreText != null) scoreText.text = score;
            if (thresholdText != null) thresholdText.text = threshold;
        }

        private void SetActiveAll(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                obj.SetActive(active);

                if (active && showDebugInfo)
                    Debug.Log($"Activated result object: {obj.name}");
            }
        }

        private void SetupButtons()
        {
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(PlayAgain);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);

            if (replaySeedButton != null)
                replaySeedButton.onClick.AddListener(ReplaySeed);

            if (restartCampaignButton != null)
                restartCampaignButton.onClick.AddListener(RestartCampaign);
        }

        public void RestartCampaign()
        {
            if (showDebugInfo)
                Debug.Log("Restarting campaign from Night 1...");

            GameFlowManager.ResetProgression();
            GameFlowManager.StartNewNight(gameSceneName);
        }

        /// <summary>
        /// The one "continue" button. Hands back to the day loop rather than reloading the scene
        /// itself: a survived day goes on to the day-end event and the next day, a lost one
        /// retries the same day with a fresh roll.
        /// </summary>
        public void PlayAgain() => StartCoroutine(ShutterThen(PlayAgainNow));

        private void PlayAgainNow()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null)
            {
                // Result scene opened directly while testing - nothing to continue into.
                Debug.LogWarning("ResultDisplay: no GameFlowManager - reloading the gameplay scene directly.", this);
                GameFlowManager.StartNewNight(gameSceneName);
                return;
            }

            if (showDebugInfo)
                Debug.Log("Continuing from result...");

            flow.ContinueFromResult();
        }

        public void ReplaySeed()
        {
            if (showDebugInfo)
                Debug.Log($"Replaying seed {_resultSeed}...");

            NightPlanProvider.ForcedSeed = _resultSeed;
            GameFlowManager.StartNewNight(gameSceneName);
        }

        public void QuitGame() => StartCoroutine(ShutterThen(QuitNow));

        private void QuitNow()
        {
            if (showDebugInfo)
                Debug.Log("Quitting game...");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Context menus for checking the layout without playing a whole night.
        [ContextMenu("Test Win Result")]
        public void TestWinResult() => Display(new NightResult
        {
            outcome = NightOutcome.Survived, score = 5, requiredScore = 3, survivedUntilHour = 6f,
        });

        [ContextMenu("Test Lose Result")]
        public void TestLoseResult() => Display(new NightResult
        {
            outcome = NightOutcome.Survived, score = 1, requiredScore = 3, survivedUntilHour = 6f,
        });

        [ContextMenu("Test Anomaly Defeat")]
        public void TestAnomalyDefeat() => Display(new NightResult
        {
            outcome = NightOutcome.KilledByAnomaly, score = 2, requiredScore = 3,
            survivedUntilHour = 3.4f, killedByAnomalyId = "shadow",
        });

        [ContextMenu("Test Demon Defeat")]
        public void TestDemonDefeat() => Display(new NightResult
        {
            outcome = NightOutcome.KilledByDemon, score = 2, requiredScore = 3,
            survivedUntilHour = 4.1f, killedByAnomalyId = "demon",
        });
    }
}
