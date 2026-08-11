using System.Collections;
using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.SpawnAndTime;
using Pray;
using Report;
using UnityEngine;

namespace GameLogic
{
    /// <summary>Where an anomaly is in its life. Only ever moves forward.</summary>
    public enum AnomalyState
    {
        Hidden,
        Visible,
        Threatening,
        Resolved,
    }

    /// <summary>
    /// One supernatural entity. This is the only anomaly type the rest of the game talks to; it
    /// owns identity, the state machine and the global registry, and delegates the actual doing
    /// to three siblings: <see cref="AnomalyMovement"/>, <see cref="AnomalyPresenter"/> and
    /// <see cref="AnomalyThreatTimer"/>.
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

        private const float RespondDelay = 4f;   // beat between a failed report and reacting to it
        private const float DespawnDelay = 0.6f; // lets the banish animation play before it vanishes

        // ── Static registry ──────────────────────────────────────────────────────────────

        private static readonly List<Anomaly> _activeAnomalies = new List<Anomaly>();
        public static IReadOnlyList<Anomaly> ActiveAnomalies => _activeAnomalies;

        public static event System.Action<Anomaly> OnAnyAnomalyDisappeared;

        public static event System.Action<Anomaly> OnAnyThreatExpired;

        // ── Identity ─────────────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("What KIND of anomaly this is. Supplies its keywords, respond type, speed and threat window.")]
        [SerializeField] private AnomalyDefinition definition;

        [Header("Despawn")]
        [Tooltip("Destroy the GameObject after banishing instead of just deactivating it.")]
        [SerializeField] private bool destroyAfterDisappear;

        public AnomalyDefinition Definition => definition;

        public RoomDefinition AssignedRoom { get; private set; }

        public void AssignRoom(RoomDefinition room) => AssignedRoom = room;

        // ── State ────────────────────────────────────────────────────────────────────────

        // Starts Hidden and is promoted by OnEnable, which makes the demon's "spawned but not
        // revealed yet" case correct without depending on component Awake ordering: it disables
        // the Anomaly component, so OnEnable simply never runs until the camera finds it.
        public AnomalyState State { get; private set; } = AnomalyState.Hidden;

        public System.Action<Anomaly> OnThreatExpired;

        public System.Action<Anomaly> OnAnomalyDisappeared;

        private bool _isReported;
        public bool IsReported => _isReported;

        private bool _canPrayDisappear;  // the prayer window is open
        private bool _alertRaised;       // this anomaly incremented IncidentReportManager's alert counter
        private bool _disappearNotified; // guards the disappear events so one activation can only ever score once

        private AnomalyMovement _movement;
        private AnomalyPresenter _presenter;
        private AnomalyThreatTimer _threatTimer;
        private PrayUiManager _prayManager;

        public RespondType EffectiveRespondType => definition != null
            ? definition.respondType
            : RespondType.MoveToTargetThenDisappear;

        // ── Lifecycle ────────────────────────────────────────────────────────────────────

        void Awake()
        {
            _movement = Attach<AnomalyMovement>();
            _presenter = Attach<AnomalyPresenter>();
            _threatTimer = Attach<AnomalyThreatTimer>();

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

        // Called when a report comes back wrong: this anomaly leaves without scoring and extra
        // ones spawn as the penalty.
        public void Respond()
        {
            StartCoroutine(RespondAfterDelay());
        }

        private IEnumerator RespondAfterDelay()
        {
            yield return new WaitForSeconds(RespondDelay);

            if (EffectiveRespondType != RespondType.DisappearInstantly && !_movement.HasTarget)
            {
                // e.g. the Demon: nothing to escalate into, so it survives to be reported again.
                yield break;
            }

            // scores:false - a wrong report must never pay out.
            HandleDisappear(scores: false);
            AnomalyScheduler.Instance?.SpawnPenaltyAnomalies();
        }

        // ── Resolution ───────────────────────────────────────────────────────────────────

        public void OnPrayerSuccessful()
        {
            if (!CanBePrayerBanished()) return;

            Debug.Log($"Prayer successful for anomaly {name}. Banishing...");

            // Clear the flag first so the threat timer can't fire while we tear down.
            _canPrayDisappear = false;
            StopEverything();
            HandleDisappear();
        }

        public bool CanBePrayerBanished()
        {
            return _canPrayDisappear && EffectiveRespondType == RespondType.MoveToTargetThenDisappear;
        }

        public void ResolveByReport()
        {
            _canPrayDisappear = false;
            StopEverything();
            HandleDisappear();
        }

        private void HandleDisappear(bool scores = true)
        {
            if (State == AnomalyState.Resolved) return;
            State = AnomalyState.Resolved;

            _presenter.PlayResolved();
            _presenter.StopFightLoop();

            if (_prayManager != null)
                _prayManager.HidePrayPanel();

            ClearAlert();

            // Fire before disappearing, so scoring sees it.
            if (scores)
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

        private void HandleThreatExpired()
        {
            if (!_canPrayDisappear || State == AnomalyState.Resolved) return;

            _canPrayDisappear = false;

            Debug.Log($"Anomaly {name} timeout reached. Player loses.", this);

            OnThreatExpired?.Invoke(this);
            OnAnyThreatExpired?.Invoke(this);

            GameFlowManager.Instance?.EndNight(
                NightOutcome.KilledByAnomaly,
                definition != null ? definition.anomalyId : name,
                AssignedRoom != null ? AssignedRoom.roomId : null);
        }

        private void RaiseDisappeared()
        {
            if (_disappearNotified) return;
            _disappearNotified = true;

            OnAnomalyDisappeared?.Invoke(this);
            OnAnyAnomalyDisappeared?.Invoke(this);
        }

        private void StopEverything()
        {
            StopAllCoroutines();
            _movement.Stop();
            _threatTimer.Cancel();
        }

        // ── Report bookkeeping ───────────────────────────────────────────────────────────

        public void MarkReported() => _isReported = true;

        public void ClearReportedFlag() => _isReported = false;

        private void ClearAlert()
        {
            if (!_alertRaised) return;

            _alertRaised = false;
            IncidentReportManager.Instance?.SetAlert(false);
        }
    }
}
