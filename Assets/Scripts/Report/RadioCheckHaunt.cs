using System.Collections;
using Audio;
using GameLogic.Data;
using GameLogic.Night;
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
        private enum Variant { Normal, OwnVoice, WrongId }

        [Header("Identity")]
        [SerializeField] private string radioId = "SEC-04";
        [SerializeField] private string[] wrongIds = { "SEC-01", "SEC-02", "SEC-03" };

        [Header("Timing")]
        [SerializeField] private float responseWindowSeconds = 8f;
        [Range(0f, 1f)] [SerializeField] private float wordSimilarity = 0.7f;

        [Header("Variant weights")]
        [SerializeField] private float normalWeight = 3f;
        [SerializeField] private float ownVoiceWeight = 1.5f;
        [SerializeField] private float wrongIdWeight = 1.5f;

        [Header("Negligence")]
        [Tooltip("Missed (unanswered) calls before HQ 'sends someone to check' - forces the next scheduled haunt beat to fire immediately.")]
        [SerializeField] private int strikesForConsequence = 3;

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
            // ExistingInstance, not Instance - same teardown-safety rule as SilenceProtocolHaunt.
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
            string expectedPhrase = $"{radioId} copy";

            _hud = RadioCheckHud.Create();
            _hud.SetCall($"\"{calledId}, radio check.\"");
            _hud.SetHint(variant == Variant.WrongId ? "...that's not your call sign." : $"say: \"{radioId}, copy\"");

            if (variant == Variant.OwnVoice && _recorder.HasClip)
                AudioManager.Instance?.PlayClip(_recorder.LastClip);
            else
                AudioManager.Instance?.Play(callSoundName);

            bool matched = false;
            var voice = VoicePromptSystem.Instance;
            voice?.Expect(expectedPhrase, ok => matched = ok, minimumWordsRequired: 2, wordSimilarity: wordSimilarity);

            // A WrongId call is answered with silence, so recording it would never capture a usable
            // "own voice answering normally" sample for a future Own-Voice call.
            bool shouldRecord = variant != Variant.WrongId;
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

            if (variant == Variant.WrongId)
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

            EndEncounter(respondedCorrectly, wrongIdAdmitted);
        }

        private Variant PickVariant()
        {
            float total = Mathf.Max(0f, normalWeight) + Mathf.Max(0f, ownVoiceWeight) + Mathf.Max(0f, wrongIdWeight);
            if (total <= 0f) return Variant.Normal;

            float roll = Random.value * total;
            if (roll < normalWeight) return Variant.Normal;
            roll -= normalWeight;
            if (roll < ownVoiceWeight) return Variant.OwnVoice;
            return Variant.WrongId;
        }

        private string PickWrongId()
        {
            if (wrongIds == null || wrongIds.Length == 0) return "SEC-00";
            return wrongIds[Random.Range(0, wrongIds.Length)];
        }

        private void EndEncounter(bool respondedCorrectly, bool wrongIdAdmitted, bool silent = false)
        {
            IsActive = false;

            if (_encounter != null)
            {
                StopCoroutine(_encounter);
                _encounter = null;
            }

            if (_hud != null)
            {
                if (!silent) _hud.FlashResult(respondedCorrectly);
                _hud.Destroy();
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
