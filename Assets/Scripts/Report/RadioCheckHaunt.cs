using System.Collections;
using Audio;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Night;
using GameLogic.SpawnAndTime;
using UnityEngine;
using Whisper;

namespace Report
{
    /// <summary>
    /// HL-4 Radio Check. Every so often HQ pings over the radio and the player has a few seconds
    /// to answer by voice - the mechanic that keeps the mic "alive" all night instead of only
    /// during the Incident Report form (see the roadmap's "ไมค์มีชีวิตตลอดคืน").
    /// </summary>
    public class RadioCheckHaunt : MonoBehaviour, IHauntLoop
    {
        public enum Variant { Normal, OwnVoice, WrongId, Mimic }

        private Variant? _forcedVariant;

        // Debug/tooling: place this call as a specific variant, ignoring weights and the Mimic night gate.
        public bool DebugTrigger(Variant variant)
        {
            if (IsActive) return false;
            _forcedVariant = variant;
            Trigger(default);
            return true;
        }

        [Header("Identity")]
        [SerializeField] private string radioId = "SEC-04";
        [SerializeField] private string[] wrongIds = { "SEC-01", "SEC-02", "SEC-03" };

        [Header("Timing")]
        [SerializeField] private float responseWindowSeconds = 8f;
        [Range(0f, 1f)] [SerializeField] private float wordSimilarity = 0.7f;
        [Tooltip("How long the PASS / FAIL result stays on screen after a call.")]
        [Min(0f)] [SerializeField] private float outcomeDisplaySeconds = 2.5f;

        [Header("Variant weights")]
        [SerializeField] private float normalWeight = 3f;
        [SerializeField] private float ownVoiceWeight = 1.5f;
        [SerializeField] private float wrongIdWeight = 1.5f;
        [Tooltip("A voice that sounds like HQ but uses no call sign and asks you to 'confirm all clear'. Answering it invites the entity in; the right move is silence.")]
        [SerializeField] private float mimicWeight = 1f;
        [Tooltip("The mimic only starts calling from this night on (Act 2).")]
        [Min(1)] [SerializeField] private int mimicMinNight = 3;

        [Header("Mimic consequence")]
        [Tooltip("Noise added to the Noise Meter when the player answers the mimic.")]
        [Min(0f)] [SerializeField] private float mimicNoisePenalty = 40f;
        [SerializeField] private string mimicPhrase = "all clear";

        [Header("Negligence")]
        [Tooltip("Missed (unanswered) calls before HQ 'sends someone to check' - forces the next scheduled haunt beat to fire immediately.")]
        [SerializeField] private int strikesForConsequence = 3;

        [Tooltip("Noise Meter added the moment a call goes unanswered - HQ shouting down the line carries.")]
        [Min(0f)] [SerializeField] private float missedCallNoise = 25f;

        [Header("Wrong ID consequence")]
        [Tooltip("If the player answers a call meant for someone else, GlitchDirector's intensity is floored to at least this for the rest of the night.")]
        [SerializeField] private float wrongIdIntensityFloor = 1.25f;

        [Header("Audio (best-effort - a missing library entry just stays silent)")]
        [SerializeField] private string callSoundName = "RadioCall";
        [SerializeField] private string missedSoundName = "RadioMissed";

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public HauntLoopId LoopId => HauntLoopId.RadioCheck;
        public bool IsActive { get; private set; }
        public bool IsExclusive => false;

        private PlayerVoiceRecorder _recorder;
        private RadioCheckHud _hud;
        private GlitchDirector _glitchDirector;
        private Coroutine _encounter;
        private int _negligenceStrikes;

        void Awake()
        {
            _recorder = gameObject.AddComponent<PlayerVoiceRecorder>();
        }

        void Start()
        {
            _glitchDirector = FindFirstObjectByType<GlitchDirector>();
        }

        void OnEnable() => HauntDirector.Instance?.Register(this);

        void OnDisable()
        {
            // ExistingInstance, not Instance.
            HauntDirector.ExistingInstance?.Unregister(this);

            if (IsActive)
                EndEncounter(respondedCorrectly: false, wrongIdAdmitted: false, silent: true);
        }

        public void Trigger(HauntBeat beat)
        {
            if (IsActive) return; // HauntDirector already guards re-entrancy per loop - belt and braces
            _encounter = StartCoroutine(BeginEncounter());
        }

        private IEnumerator BeginEncounter()
        {
            IsActive = true;

            var variant = PickVariant();
            string calledId = variant == Variant.WrongId ? PickWrongId() : radioId;
            string expectedPhrase = variant == Variant.Mimic ? mimicPhrase : $"{radioId} copy";

            _hud = RadioCheckHud.Create();
            if (variant == Variant.Mimic)
            {
                _hud.SetCall("\"...confirm all clear.\"");
                _hud.SetHint("...no call sign.");
            }
            else
            {
                _hud.SetCall($"\"{calledId}, radio check.\"");
                string strikes = _negligenceStrikes > 0 ? $"  (missed {_negligenceStrikes}/{strikesForConsequence})" : "";
                _hud.SetHint(variant == Variant.WrongId ? "...that's not your call sign." : $"say: \"{radioId}, copy\"{strikes}");
            }

            if ((variant == Variant.OwnVoice || variant == Variant.Mimic) && _recorder.HasClip)
                AudioManager.Instance?.PlayClip(_recorder.LastClip);
            else
                AudioManager.Instance?.Play(callSoundName);

            bool matched = false;
            var voice = VoicePromptSystem.Instance;
            voice?.Expect(expectedPhrase, ok => matched = ok, minimumWordsRequired: 2, wordSimilarity: wordSimilarity);

            // WrongId and Mimic calls are answered with silence, so recording them would never capture
            // a usable "own voice answering normally" sample for a future Own-Voice call.
            bool shouldRecord = variant == Variant.Normal || variant == Variant.OwnVoice;
            if (shouldRecord) _recorder.BeginCapture(responseWindowSeconds);

            float end = Time.time + responseWindowSeconds;
            while (Time.time < end && !matched)
            {
                _hud.SetCountdown(Mathf.Max(0f, end - Time.time), responseWindowSeconds);
                yield return null;
            }

            voice?.Cancel();
            if (shouldRecord) _recorder.EndCapture();

            bool respondedCorrectly;
            bool wrongIdAdmitted = false;

            if (variant == Variant.Mimic)
            {
                respondedCorrectly = !matched; // HQ never asks this - the right move is silence
                if (matched)
                    InviteEntity();
            }
            else if (variant == Variant.WrongId)
            {
                wrongIdAdmitted = matched;
                respondedCorrectly = !matched; // the correct move on a call that isn't yours is silence
            }
            else
            {
                respondedCorrectly = matched;
            }

            if (showDebugInfo)
                Debug.Log($"RadioCheckHaunt: variant={variant} calledId={calledId} matched={matched}.", this);

            bool invited = variant == Variant.Mimic && matched;
            string headline, detail;
            if (invited)
            {
                headline = "IT HEARD YOU";
                detail = "That wasn't HQ.";
            }
            else if (wrongIdAdmitted)
            {
                headline = "WRONG SIGN-IN";
                detail = "That call wasn't for you.";
            }
            else if (respondedCorrectly)
            {
                bool declined = variant == Variant.WrongId || variant == Variant.Mimic;
                headline = declined ? "GOOD CALL" : "COPY THAT";
                detail = declined ? "That wasn't HQ." : "HQ is satisfied.";
            }
            else
            {
                int missed = _negligenceStrikes + 1;
                headline = "NO RESPONSE  (NOISE +" + Mathf.RoundToInt(missedCallNoise) + ")";
                detail = missed >= strikesForConsequence
                    ? "HQ is sending someone to check."
                    : $"Missed {missed}/{strikesForConsequence} - at {strikesForConsequence}, HQ sends someone.";
            }

            EndEncounter(respondedCorrectly, wrongIdAdmitted, headline: headline, detail: detail);
        }

        private Variant PickVariant()
        {
            if (_forcedVariant.HasValue)
            {
                var forced = _forcedVariant.Value;
                _forcedVariant = null;
                return forced;
            }

            float mimic = GameFlowManager.CurrentNightIndex >= mimicMinNight ? Mathf.Max(0f, mimicWeight) : 0f;
            float total = Mathf.Max(0f, normalWeight) + Mathf.Max(0f, ownVoiceWeight)
                        + Mathf.Max(0f, wrongIdWeight) + mimic;
            if (total <= 0f) return Variant.Normal;

            float roll = Random.value * total;
            if (roll < normalWeight) return Variant.Normal;
            roll -= normalWeight;
            if (roll < ownVoiceWeight) return Variant.OwnVoice;
            roll -= ownVoiceWeight;
            if (roll < wrongIdWeight) return Variant.WrongId;
            return Variant.Mimic;
        }

        // Answering the mimic "invites it in": extra anomalies, a noisier line, and a permanent glitch floor.
        private void InviteEntity()
        {
            AnomalyScheduler.Instance?.SpawnPenaltyAnomalies();
            NoiseMeter.Instance?.AddNoise(mimicNoisePenalty);
            _glitchDirector?.SetFlag("mimic_invited", true);
            _glitchDirector?.SetIntensity(wrongIdIntensityFloor);

            if (showDebugInfo)
                Debug.Log("RadioCheckHaunt: player answered the mimic - entity invited.", this);
        }

        private string PickWrongId()
        {
            if (wrongIds == null || wrongIds.Length == 0) return "SEC-00";
            return wrongIds[Random.Range(0, wrongIds.Length)];
        }

        private void EndEncounter(bool respondedCorrectly, bool wrongIdAdmitted, bool silent = false,
                                  string headline = null, string detail = null)
        {
            IsActive = false;

            if (_encounter != null)
            {
                StopCoroutine(_encounter);
                _encounter = null;
            }

            if (_hud != null)
            {
                // Linger so the player can actually read whether the check passed.
                if (!silent) _hud.ShowOutcome(respondedCorrectly, headline ?? (respondedCorrectly ? "COPY THAT" : "NO RESPONSE"), detail ?? "");
                _hud.Destroy(silent ? 0f : outcomeDisplaySeconds);
                _hud = null;
            }

            if (silent) return;

            if (wrongIdAdmitted)
            {
                // Soft consequence, not a fail state. A floor rather than a stacking bump, so
                // admitting twice doesn't compound.
                _glitchDirector?.SetFlag("impostor_admitted", true);
                _glitchDirector?.SetIntensity(wrongIdIntensityFloor);

                if (showDebugInfo)
                    Debug.Log("RadioCheckHaunt: player answered a call that wasn't theirs.", this);
                return;
            }

            if (respondedCorrectly)
            {
                _negligenceStrikes = 0;
                return;
            }

            AudioManager.Instance?.Play(missedSoundName);
            NoiseMeter.Instance?.AddNoise(missedCallNoise);
            _negligenceStrikes++;

            if (showDebugInfo)
                Debug.Log($"RadioCheckHaunt: missed call - negligence {_negligenceStrikes}/{strikesForConsequence}.", this);

            if (_negligenceStrikes < strikesForConsequence) return;

            _negligenceStrikes = 0;

            // "HQ sends someone to check" - reuse the existing scheduled-beat mechanism instead of
            // inventing a new consequence system: force whatever haunt is next in this night's plan
            // to fire immediately.
            if (showDebugInfo)
                Debug.Log("RadioCheckHaunt: 3 missed calls - forcing the next scheduled haunt.", this);

            HauntDirector.ExistingInstance?.ForceFireNext();
        }
    }
}
