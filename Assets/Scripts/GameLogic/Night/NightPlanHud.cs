using GameLogic.Flow;
using GameLogic.SpawnAndTime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.Night
{
    /// <summary>
    /// On-screen readout of the night being played, toggled with a key. Exists so "what did that
    /// seed actually do?" is answerable while playing instead of only from the console afterwards -
    /// and so a night that felt wrong can be replayed exactly.
    ///
    /// Debug-only: leave it off the shipped scene, or leave <see cref="visibleByDefault"/> off.
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

        void Awake()
        {
            _visible = visibleByDefault;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 20f), $"[{toggleKey}] night plan");
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

        /// <summary>
        /// Reloads the gameplay scene through GameFlowManager, so plan and result are cleared the
        /// same way the Play Again button does it rather than by a second copy of that logic.
        /// </summary>
        private void RestartNight()
        {
            GameFlowManager.StartNewNight(SceneManager.GetActiveScene().name);
        }

        private static string NextSpawnLabel(NightPlan plan)
        {
            var timer = FindFirstObjectByType<NightTimer>();
            if (timer == null) return "?";

            float elapsed = timer.ElapsedMinutes;
            foreach (var placement in plan.anomalies)
            {
                if (placement.atMinute < elapsed) continue;

                return $"{placement.definition?.Label ?? "?"} in {placement.room?.Label ?? "?"} " +
                       $"({placement.atMinute - elapsed:0.0}m)";
            }

            return "none left";
        }

        private static int Spawned()
        {
            var scheduler = FindFirstObjectByType<AnomalyScheduler>();
            return scheduler != null ? scheduler.TotalSpawned : 0;
        }

        private static int CurrentScore()
        {
            var score = Score.ScoreManager.Instance;
            return score != null ? score.GetCurrentScore() : 0;
        }

        private static GUIStyle RichLabel()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true };
            return style;
        }
    }
}
