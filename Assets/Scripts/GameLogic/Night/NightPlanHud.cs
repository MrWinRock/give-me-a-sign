using GameLogic.Flow;
using GameLogic.SpawnAndTime;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic.Night
{
    /// <summary>
    /// On-screen readout of the night being played, toggled with a key. Exists so "what did that
    /// seed actually do?" is answerable while playing instead of only from the console afterwards -
    /// and so a night that felt wrong can be replayed exactly.
    /// </summary>
    public class NightPlanHud : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool visibleByDefault;

        [Header("Layout")]
        [SerializeField] private int width = 330;

        private bool _visible;
        private string _seedInput = "";

        // Cached so OnGUI (which runs every single frame, visible or not) doesn't allocate a
        // new string/GUIStyle/component-search every tick - that was showing up in the Profiler
        // as constant GC churn even while this HUD was fully collapsed.
        private string _collapsedLabel;
        private GUIStyle _richLabelStyle;
        private NightTimer _cachedTimer;
        private AnomalyScheduler _cachedScheduler;

        void Awake()
        {
            _visible = visibleByDefault;
            _collapsedLabel = $"[{toggleKey}] night plan";
        }

        void Update()
        {
            // Guarded: this project ships with the new Input System as the only handler, where
            // legacy UnityEngine.Input throws instead of returning false.
            bool togglePressed;
#if ENABLE_INPUT_SYSTEM
            togglePressed = Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#else
            togglePressed = Input.GetKeyDown(toggleKey);
#endif
            if (togglePressed)
                _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 20f), _collapsedLabel);
                return;
            }

            var plan = NightPlanProvider.HasPlan ? NightPlanProvider.Current : null;

            GUILayout.BeginArea(new Rect(10f, 10f, width, 320f), GUI.skin.box);
            GUILayout.Label("<b>NIGHT PLAN</b>", RichLabel());

            if (plan == null)
            {
                GUILayout.Label("No plan published yet.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"night {plan.nightIndex}   seed {plan.seed}");
            GUILayout.Label($"anomalies {Spawned()}/{plan.anomalies.Count}   glitches {plan.glitches.Count}");
            GUILayout.Label($"score {CurrentScore()} / {plan.requiredScore} needed");
            GUILayout.Label($"next spawn: {NextSpawnLabel(plan)}");

            GUILayout.Space(6);
            GUILayout.Label("Replay a seed:");

            GUILayout.BeginHorizontal();
            _seedInput = GUILayout.TextField(_seedInput, 12);
            if (GUILayout.Button("Go", GUILayout.Width(40f)))
                ReplaySeed();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Replay THIS seed"))
            {
                NightPlanProvider.ForcedSeed = plan.seed;
                RestartNight();
            }

            if (GUILayout.Button("New random night"))
            {
                NightPlanProvider.ForcedSeed = null;
                RestartNight();
            }

            if (GUILayout.Button("Dump plan to console"))
                Debug.Log(NightPlanRunner.Describe(plan));

            GUILayout.EndArea();
        }

        private void ReplaySeed()
        {
            if (!int.TryParse(_seedInput, out int seed))
            {
                Debug.LogWarning($"NightPlanHud: '{_seedInput}' is not a number.", this);
                return;
            }

            NightPlanProvider.ForcedSeed = seed;
            RestartNight();
        }

        private void RestartNight()
        {
            GameFlowManager.StartNewNight(SceneManager.GetActiveScene().name);
        }

        private string NextSpawnLabel(NightPlan plan)
        {
            // Cached instead of FindFirstObjectByType every OnGUI frame while the panel is open -
            // a scene search is not something you want to pay for 60+ times a second.
            if (_cachedTimer == null)
                _cachedTimer = FindFirstObjectByType<NightTimer>();
            if (_cachedTimer == null) return "?";

            float elapsed = _cachedTimer.ElapsedMinutes;
            foreach (var placement in plan.anomalies)
            {
                if (placement.atMinute < elapsed) continue;

                return $"{placement.definition?.Label ?? "?"} in {placement.room?.Label ?? "?"} " +
                       $"({placement.atMinute - elapsed:0.0}m)";
            }

            return "none left";
        }

        private int Spawned()
        {
            if (_cachedScheduler == null)
                _cachedScheduler = FindFirstObjectByType<AnomalyScheduler>();
            return _cachedScheduler != null ? _cachedScheduler.TotalSpawned : 0;
        }

        private static int CurrentScore()
        {
            var score = Score.ScoreManager.Instance;
            return score != null ? score.GetCurrentScore() : 0;
        }

        private GUIStyle RichLabel()
        {
            // Was `new GUIStyle(...)` every frame the panel was open - a real per-frame GC
            // allocation for no reason, since the style itself never changes.
            return _richLabelStyle ??= new GUIStyle(GUI.skin.label) { richText = true };
        }
    }
}
