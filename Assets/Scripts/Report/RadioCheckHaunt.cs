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
    /// to answer by voice. A real call must be answered exactly as written on screen; a decoy call
    /// (wrong call sign, or a mimic asking to "confirm all clear") must be left alone. Getting it
    /// wrong either way lets another anomaly into the building.
    /// </summary>
    public class RadioCheckHaunt : MonoBehaviour, IHauntLoop
    {
        public enum Variant { Normal, OwnVoice, WrongId, Mimic }

        [Header("Identity")]
        [SerializeField] private string radioId = "SEC-04";
        [SerializeField] private string[] wrongIds = { "SEC-01", "SEC-02", "SEC-03" };

        [Header("Timing")]
        [SerializeField] private float responseWindowSeconds = 8f;
        [Tooltip("How long the PASS / FAIL result stays on screen after a call.")]
        [Min(0f)] [SerializeField] private float outcomeDisplaySeconds = 2.5f;
        [Tooltip("Speech heard within this long of the call audio ending is ignored - the mic can pick up the call itself (especially the player's own recorded voice).")]
        [Min(0f)] [SerializeField] private float echoGraceSeconds = 0.4f;

        [Header("Variant weights")]
        [SerializeField] private float normalWeight = 3f;
        [SerializeField] private float ownVoiceWeight = 1.5f;
        [SerializeField] private float wrongIdWeight = 1.5f;
        [Tooltip("A voice that sounds like HQ but uses no call sign and asks you to 'confirm all clear'. Answering it lets an anomaly in; the right move is silence.")]
        [SerializeField] private float mimicWeight = 1f;
        [Tooltip("The mimic only starts calling from this night on (Act 2).")]
        [Min(1)] [SerializeField] private int mimicMinNight = 3;

        [Header("Decoy consequence")]
        [Tooltip("When a decoy call is answered, GlitchDirector's intensity is floored to at least this for the rest of the night (on top of the extra anomaly).")]
        [SerializeField] private float decoyIntensityFloor = 1.25f;

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
        private Variant? _forcedVariant;
        private float _listenFrom;

        // Debug/tooling: place this call as a specific variant, ignoring weights and the Mimic night gate.
        public bool DebugTrigger(Variant variant)
        {
            if (IsActive) return false;
            _forcedVariant = variant;
            Trigger(default);
            return true;
        }

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
                EndEncounter(silent: true);
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
            bool decoy = variant == Variant.WrongId || variant == Variant.Mimic;
            string calledId = variant == Variant.WrongId ? PickWrongId() : radioId;

            _hud = RadioCheckHud.Create();
            if (variant == Variant.Mimic)
            {
                _hud.SetCall("\"...confirm all clear.\"");
                _hud.SetHint("...no call sign.");
            }
            else
            {
                _hud.SetCall($"\"{calledId}, radio check.\"");
                _hud.SetHint(variant == Variant.WrongId ? "...that's not your call sign." : $"say: \"{radioId}, copy\"");
            }

            float callSeconds = 0f;
            var audio = AudioManager.Instance;
            if ((variant == Variant.OwnVoice || variant == Variant.Mimic) && _recorder.HasClip)
            {
                audio?.PlayClip(_recorder.LastClip);
                callSeconds = _recorder.LastClip.length;
            }
            else
            {
                audio?.Play(callSoundName);
            }
            _listenFrom = Time.time + callSeconds + echoGraceSeconds;

            bool matched = false;
            var voice = VoicePromptSystem.Instance;
            voice?.Expect(text => Time.time >= _listenFrom && Heard(variant, text), ok => matched = ok);

            // Decoys are answered with silence, so recording them would never capture a usable
            // "own voice answering normally" sample for a future Own-Voice call.
            bool shouldRecord = !decoy;
            if (shouldRecord) _recorder.BeginCapture(responseWindowSeconds);

            float end = Time.time + responseWindowSeconds;
            while (Time.time < end && !matched)
            {
                _hud.SetCountdown(Mathf.Max(0f, end - Time.time), responseWindowSeconds);
                yield return null;
            }

            voice?.Cancel();
            if (shouldRecord) _recorder.EndCapture();

            // Real call: answering is right. Decoy: answering is the mistake.
            bool passed = decoy ? !matched : matched;

            if (showDebugInfo)
                Debug.Log($"RadioCheckHaunt: variant={variant} calledId={calledId} answered={matched} passed={passed}.", this);

            string headline, detail;
            if (passed)
            {
                headline = decoy ? "GOOD CALL" : "COPY THAT";
                detail = decoy ? "That wasn't HQ." : "HQ is satisfied.";
            }
            else if (decoy)
            {
                headline = variant == Variant.Mimic ? "IT HEARD YOU" : "WRONG SIGN-IN";
                detail = "Another anomaly got in.";
            }
            else
            {
                headline = "NO RESPONSE";
                detail = "Another anomaly got in.";
            }

            EndEncounter(passed, decoy, headline, detail);
        }

        // Real calls must be answered as written; decoys count as "answered" on any sensible attempt.
        private bool Heard(Variant variant, string text)
        {
            switch (variant)
            {
                case Variant.Mimic: return RadioPhrases.SaidAllClear(text);
                case Variant.WrongId: return RadioPhrases.SaidCopy(text);
                default: return RadioPhrases.SaidCopy(text) && RadioPhrases.SaidCallSign(text, radioId);
            }
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

        private string PickWrongId()
        {
            if (wrongIds == null || wrongIds.Length == 0) return "SEC-00";
            return wrongIds[Random.Range(0, wrongIds.Length)];
        }

        private void EndEncounter(bool passed = false, bool decoy = false, string headline = null, string detail = null, bool silent = false)
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
                if (!silent) _hud.ShowOutcome(passed, headline ?? (passed ? "COPY THAT" : "NO RESPONSE"), detail ?? "");
                _hud.Destroy(silent ? 0f : outcomeDisplaySeconds);
                _hud = null;
            }

            if (silent || passed) return;

            // Fail, either way: the punishment is another anomaly in the building.
            if (!decoy) AudioManager.Instance?.Play(missedSoundName);
            AnomalyScheduler.Instance?.SpawnPenaltyAnomalies();

            if (decoy)
            {
                _glitchDirector?.SetFlag("decoy_answered", true);
                _glitchDirector?.SetIntensity(decoyIntensityFloor);
            }

            if (showDebugInfo)
                Debug.Log($"RadioCheckHaunt: failed ({(decoy ? "answered a decoy" : "no answer")}) - anomaly added.", this);
        }
    }
}
