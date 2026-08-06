using GameLogic.Data;
using GameLogic.Night;
using UnityEngine;

namespace Report
{
    /// <summary>
    /// HL-6 Impostor Case. When this fires, nothing happens immediately - it just arms a "phantom"
    /// case number a few ahead of the real one. The next time the report form opens, that phantom
    /// number briefly appears where the real case number should be (FormGlitchController's Case
    /// Corruption) alongside a status-bar line implying someone else already filed it (Status
    /// Intrusion) - reusing two glitches that are already proven and revertible instead of building
    /// new UI, since both are exactly the right shape for "the form is lying about what's on record".
    ///
    /// A full "case pre-filled with the player's own name" version needs a player-identity concept
    /// the game doesn't have yet - the roadmap's own cut list already flags all of HL-6 as safely
    /// cuttable/deferrable, so this is the honest, shippable MVP of it rather than a placeholder.
    ///
    /// Reports no encounter of its own (IsActive is always false) - it never blocks another haunt
    /// and is never blocked by one; it just queues a one-shot trap for the next form open, the same
    /// "fire the moment the form next opens" contract GlitchScheduler already documents for its own
    /// beats scheduled while the form happens to be closed.
    /// </summary>
    public class ImpostorCaseHaunt : MonoBehaviour, IHauntLoop
    {
        [Header("Phantom case")]
        [Tooltip("The fake case number is the real next one plus a random jump in this range (inclusive).")]
        [SerializeField] private Vector2Int caseJumpRange = new Vector2Int(2, 4);

        [Header("Status line shown alongside the phantom case number")]
        [SerializeField]
        private string[] statusMessages =
        {
            "PREVIOUS OFFICER DID NOT REPORT",
            "CASE ALREADY ON FILE",
            "SEC-03 FILED THIS REPORT",
        };

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public HauntLoopId LoopId => HauntLoopId.ImpostorCase;
        public bool IsActive => false; // no encounter of its own - see class remarks
        public bool IsExclusive => false;

        private FormGlitchController _formGlitch;
        private bool _pending;
        private bool _wasReportOpen;

        void Awake()
        {
            _formGlitch = FindFirstObjectByType<FormGlitchController>();
        }

        void OnEnable() => HauntDirector.Instance?.Register(this);
        void OnDisable() => HauntDirector.ExistingInstance?.Unregister(this);

        public void Trigger(HauntBeat beat)
        {
            _pending = true;

            if (showDebugInfo)
                Debug.Log("ImpostorCaseHaunt: armed - will surface the next time the report form opens.", this);
        }

        void Update()
        {
            if (!_pending || _formGlitch == null) return;

            var reportManager = IncidentReportManager.Instance;
            bool open = reportManager != null && reportManager.IsReportOpen;

            if (open && !_wasReportOpen)
            {
                Fire();
                _pending = false;
            }

            _wasReportOpen = open;
        }

        private void Fire()
        {
            int phantom = IncidentReportManager.NextCaseNumber + Random.Range(caseJumpRange.x, caseJumpRange.y + 1);
            string message = statusMessages.Length > 0 ? statusMessages[Random.Range(0, statusMessages.Length)] : null;

            _formGlitch.TriggerCaseCorruption($"#{phantom:D4}");
            if (!string.IsNullOrEmpty(message))
                _formGlitch.TriggerStatusIntrusion(message);

            if (showDebugInfo)
                Debug.Log($"ImpostorCaseHaunt: fired - phantom case #{phantom:D4}, \"{message}\".", this);
        }
    }
}
