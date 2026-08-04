using System.Collections;
using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Flow;
using Pray;
using Report;
using UnityEngine;

namespace GameLogic
{
    /// <summary>Where an anomaly is in its life. Only ever moves forward.</summary>
    public enum AnomalyState
    {
        /// <summary>Spawned but not in play yet - the demon sits here until the camera finds it.</summary>
        Hidden,
        /// <summary>On screen and reportable, not yet coming for the player.</summary>
        Visible,
        /// <summary>Escalated: moving in, prayer window open, threat timer running.</summary>
        Threatening,
        /// <summary>Banished. Terminal.</summary>
        Resolved,
    }

    /// <summary>
    /// One supernatural entity. This is the only anomaly type the rest of the game talks to; it
    /// owns identity, the state machine and the global registry, and delegates the actual doing
    /// to three siblings: <see cref="AnomalyMovement"/>, <see cref="AnomalyPresenter"/> and
    /// <see cref="AnomalyThreatTimer"/>.
    ///
    /// The rule that keeps it small: an anomaly reports WHAT HAPPENED, it never decides HOW THE
    /// NIGHT ENDS. When its threat timer runs out it says so and hands the outcome to
    /// <see cref="GameFlowManager"/>; it does not write save data or load scenes. That is what
    /// lets new lose conditions be added without touching this file.
    /// </summary>
    [RequireComponent(typeof(AnomalyMovement))]
    [RequireComponent(typeof(AnomalyPresenter))]
    [RequireComponent(typeof(AnomalyThreatTimer))]
    public partial class Anomaly : MonoBehaviour
    {
        public enum RespondType
        {
            DisappearInstantly,        // หายทันที
            MoveToTargetThenDisappear, // เคลื่อนไปหา target แล้วค่อยหาย
            MoveOnly                   // แค่เคลื่อนไปหา target ไม่หาย
        }

        /// <summary>Delay between a failed report and the anomaly reacting to it.</summary>
        private const float RespondDelay = 4f;
        /// <summary>Pause after the banish animation starts before the object goes away.</summary>
        private const float DespawnDelay = 0.6f;
        /// <summary>Gap between the jumpscare stinger and the fight loop.</summary>
        private const float FightAudioDelay = 0.2f;

        // ── Static registry ──────────────────────────────────────────────────────────────

        private static readonly List<Anomaly> _activeAnomalies = new List<Anomaly>();
        public static IReadOnlyList<Anomaly> ActiveAnomalies => _activeAnomalies;

        /// <summary>
        /// Fired once per anomaly activation when it disappears/is banished, no matter how it
        /// was spawned. ScoreManager listens here instead of hunting for instances with
        /// FindObjectsOfType, so scoring works for scene-placed and runtime-spawned anomalies alike.
        /// </summary>
        public static event System.Action<Anomaly> OnAnyAnomalyDisappeared;

        /// <summary>
        /// Fired when any anomaly's threat window closes without it being banished. Subscribe here
        /// to react to the player being caught (Sprint 4's negligence strikes) without editing
        /// this class.
        /// </summary>
        public static event System.Action<Anomaly> OnAnyThreatExpired;

        // ── Identity ─────────────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("What KIND of anomaly this is. Supplies its keywords, respond type, speed and threat window.")]
        [SerializeField] private AnomalyDefinition definition;

        [Header("Despawn")]
        [Tooltip("Destroy the GameObject after banishing instead of just deactivating it.")]
        [SerializeField] private bool destroyAfterDisappear;

        /// <summary>The kind of anomaly this is, or null on a prefab that hasn't been migrated yet.</summary>
        public AnomalyDefinition Definition => definition;

        /// <summary>
        /// Which room it turned up in. Set when it spawns, NOT baked into the prefab - that is
        /// what allows a night to place the same kind of anomaly in a different room each run.
        /// </summary>
        public RoomDefinition AssignedRoom { get; private set; }

        /// <summary>Called by whatever spawned this anomaly.</summary>
        public void AssignRoom(RoomDefinition room) => AssignedRoom = room;

        // ── State ────────────────────────────────────────────────────────────────────────

        // Starts Hidden and is promoted by OnEnable, which makes the demon's "spawned but not
        // revealed yet" case correct without depending on component Awake ordering: it disables
        // the Anomaly component, so OnEnable simply never runs until the camera finds it.
        public AnomalyState State { get; private set; } = AnomalyState.Hidden;

        /// <summary>Fired when this anomaly's threat window closes without it being banished.</summary>
        public System.Action<Anomaly> OnThreatExpired;

        /// <summary>Fired when this anomaly is banished (for scoring).</summary>
        public System.Action<Anomaly> OnAnomalyDisappeared;

        private bool _isReported;
        /// <summary>True once an Incident Report has been opened for this anomaly (prevents duplicate reports).</summary>
        public bool IsReported => _isReported;

        private bool _canPrayDisappear;  // the prayer window is open
        private bool _alertRaised;       // this anomaly incremented IncidentReportManager's alert counter
        private bool _disappearNotified; // guards the disappear events so one activation can only ever score once

        private AnomalyMovement _movement;
        private AnomalyPresenter _presenter;
        private AnomalyThreatTimer _threatTimer;
        private PrayUiManager _prayManager;

        // The transitional pre-split configuration lives in Anomaly.Legacy.cs - see that file.

        /// <summary>Respond behaviour from the Definition when there is one, else the legacy field.</summary>
        public RespondType EffectiveRespondType => definition != null ? definition.respondType : respondType;

        // ── Lifecycle ────────────────────────────────────────────────────────────────────

        void Awake()
        {
            _movement = Attach<AnomalyMovement>();
            _presenter = Attach<AnomalyPresenter>();
            _threatTimer = Attach<AnomalyThreatTimer>();

            // Until the migration tool has run, the legacy fields are still the real
            // configuration, so push them into the siblings that now own them.
            if (!migrated)
                SeedSiblingsFromLegacy(_movement, _presenter, _threatTimer);

            // A Definition, when present, is authoritative - editing the asset has to change the
            // anomaly without anyone re-touching prefabs.
            if (definition != null)
            {
                _movement.SetMoveSpeed(definition.moveSpeed);
                _threatTimer.SetTimeout(definition.threatTimeoutSeconds);
            }

            _threatTimer.OnExpired += HandleThreatExpired;
        }

        private T Attach<T>() where T : Component
        {
            var existing = GetComponent<T>();
            return existing != null ? existing : gameObject.AddComponent<T>();
        }

        void Start()
        {
            _prayManager = FindFirstObjectByType<PrayUiManager>();
        }

        void OnEnable()
        {
            if (!_activeAnomalies.Contains(this))
                _activeAnomalies.Add(this);

            // A re-activated anomaly counts as a fresh appearance and may score again.
            _disappearNotified = false;

            if (State == AnomalyState.Hidden)
                State = AnomalyState.Visible;
        }

        void OnDisable()
        {
            _activeAnomalies.Remove(this);

            // Being switched off is only "hidden again" if it wasn't banished - HandleDisappear
            // deactivates the object on its way out and that must stay Resolved.
            if (State != AnomalyState.Resolved)
                State = AnomalyState.Hidden;
        }

        void OnDestroy()
        {
            _activeAnomalies.Remove(this);

            if (_threatTimer != null)
                _threatTimer.OnExpired -= HandleThreatExpired;

            // Safety net: if this anomaly is destroyed mid-jumpscare, don't leave the
            // Incident Report window's ALERT badge stuck on.
            ClearAlert();
        }

        // ── Escalation ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called when a report comes back wrong (or the player clicked without reporting).
        /// The anomaly reacts according to its respond type after a short beat.
        /// </summary>
        public void Respond()
        {
            StartCoroutine(RespondAfterDelay());
        }

        private IEnumerator RespondAfterDelay()
        {
            yield return new WaitForSeconds(RespondDelay);

            switch (EffectiveRespondType)
            {
                case RespondType.DisappearInstantly:
                    HandleDisappear();
                    break;

                case RespondType.MoveToTargetThenDisappear:
                    if (RequireTarget())
                        yield return StartCoroutine(Threaten());
                    break;

                case RespondType.MoveOnly:
                    if (RequireTarget())
                        yield return StartCoroutine(Approach());
                    break;
            }
        }

        /// <summary>
        /// An anomaly with nothing to walk toward stays put and does nothing - which is exactly
        /// how the demon survives a wrong report so the player can try again.
        /// </summary>
        private bool RequireTarget()
        {
            if (_movement.HasTarget) return true;

            Debug.LogWarning($"{name} has no target assigned!", this);
            return false;
        }

        /// <summary>Comes for the player, opens the prayer window, then starts the countdown.</summary>
        private IEnumerator Threaten()
        {
            State = AnomalyState.Threatening;
            _canPrayDisappear = true;
            _presenter.PlayThreatening();

            if (_prayManager != null)
            {
                _prayManager.ShowPrayPanel();
                _presenter.PlayJumpScare();

                RaiseAlert();

                yield return new WaitForSeconds(FightAudioDelay);
                _presenter.PlayFightLoop();
            }

            yield return _movement.MoveToTarget();

            // Arrived and still not banished - the clock starts.
            if (_canPrayDisappear)
                _threatTimer.Begin();
        }

        /// <summary>Walks in and stays. Counts as dealt with on arrival - no prayer needed.</summary>
        private IEnumerator Approach()
        {
            State = AnomalyState.Threatening;
            _presenter.PlayThreatening();

            yield return _movement.MoveToTarget();

            _canPrayDisappear = false;
            RaiseDisappeared();
        }

        // ── Resolution ───────────────────────────────────────────────────────────────────

        /// <summary>Called by VoiceCommandRouter when the prayer is recognised.</summary>
        public void OnPrayerSuccessful()
        {
            if (!CanBePrayerBanished()) return;

            Debug.Log($"Prayer successful for anomaly {name}. Banishing...");

            // Clear the flag first so the threat timer can't fire while we tear down.
            _canPrayDisappear = false;
            StopEverything();
            HandleDisappear();
        }

        /// <summary>Check if this anomaly can be banished by prayer.</summary>
        public bool CanBePrayerBanished()
        {
            return _canPrayDisappear && EffectiveRespondType == RespondType.MoveToTargetThenDisappear;
        }

        /// <summary>
        /// Called by IncidentReportManager when the submitted report correctly matches this anomaly.
        /// Resolves it immediately, the same way a successful prayer banishment does.
        /// </summary>
        public void ResolveByReport()
        {
            _canPrayDisappear = false;
            StopEverything();
            HandleDisappear();
        }

        private void HandleDisappear()
        {
            if (State == AnomalyState.Resolved) return;
            State = AnomalyState.Resolved;

            _presenter.PlayResolved();
            _presenter.StopFightLoop();

            if (_prayManager != null)
                _prayManager.HidePrayPanel();

            ClearAlert();

            // Fire before disappearing, so scoring sees it.
            RaiseDisappeared();

            if (destroyAfterDisappear)
                Destroy(gameObject, DespawnDelay); // delayed so the banish animation can play
            else
                StartCoroutine(DeactivateAfterDelay(DespawnDelay));
        }

        private IEnumerator DeactivateAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The threat window closed. Announce it and let GameFlowManager decide what it costs -
        /// this class deliberately knows nothing about save data or scenes.
        /// </summary>
        private void HandleThreatExpired()
        {
            if (!_canPrayDisappear || State == AnomalyState.Resolved) return;

            _canPrayDisappear = false;

            Debug.Log($"Anomaly {name} timeout reached. Player loses.", this);

            OnThreatExpired?.Invoke(this);
            OnAnyThreatExpired?.Invoke(this);

            GameFlowManager.Instance?.EndNight(
                NightOutcome.KilledByAnomaly,
                definition != null ? definition.anomalyId : correctAnomalyType,
                AssignedRoom != null ? AssignedRoom.roomId : null);
        }

        /// <summary>Fires both disappear events, but only once per activation.</summary>
        private void RaiseDisappeared()
        {
            if (_disappearNotified) return;
            _disappearNotified = true;

            OnAnomalyDisappeared?.Invoke(this);
            OnAnyAnomalyDisappeared?.Invoke(this);
        }

        /// <summary>
        /// Stops this anomaly's own coroutines AND the movement component's - StopAllCoroutines
        /// only reaches coroutines started by the MonoBehaviour that owns them.
        /// </summary>
        private void StopEverything()
        {
            StopAllCoroutines();
            _movement.Stop();
            _threatTimer.Cancel();
        }

        // ── Report bookkeeping ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called by IncidentReportManager as soon as the report form is opened for this anomaly,
        /// so a second click can't open another report while one is pending or resolved.
        /// </summary>
        public void MarkReported() => _isReported = true;

        /// <summary>
        /// Called by IncidentReportManager when a report is cancelled, so the anomaly can be
        /// clicked and reported again later instead of being permanently un-clickable.
        /// </summary>
        public void ClearReportedFlag() => _isReported = false;

        private void RaiseAlert()
        {
            if (_alertRaised) return;

            _alertRaised = true;
            IncidentReportManager.Instance?.SetAlert(true);
        }

        private void ClearAlert()
        {
            if (!_alertRaised) return;

            _alertRaised = false;
            IncidentReportManager.Instance?.SetAlert(false);
        }
    }
}
