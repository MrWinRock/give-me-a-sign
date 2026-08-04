using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// How an anomaly looks and sounds: animator triggers and its two AudioSources.
    /// Split out of Anomaly so adding a new visual or audio beat never touches the code that
    /// tracks game state.
    ///
    /// Every method is a no-op when the matching reference is unassigned, which is what lets
    /// the same component sit on a silent anomaly and a fully dressed one.
    /// </summary>
    public class AnomalyPresenter : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger fired when the anomaly starts moving at the player.")]
        [SerializeField] private string moveTriggerName = "StartMove";
        [Tooltip("Trigger fired when the anomaly is banished.")]
        [SerializeField] private string idleTriggerName = "Idle";

        [Header("Audio")]
        [Tooltip("Stinger played the moment the anomaly turns threatening.")]
        [SerializeField] private AudioSource jumpScareAudioSource;
        [Tooltip("Loop played while the player still has to banish it.")]
        [SerializeField] private AudioSource fightAudioSource;

        void Awake()
        {
            // A prefab that keeps its Animator on the root doesn't need to wire it by hand.
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        /// <summary>Plays the "coming for you" animation.</summary>
        public void PlayThreatening() => Trigger(moveTriggerName);

        /// <summary>Plays the banished/idle animation.</summary>
        public void PlayResolved() => Trigger(idleTriggerName);

        public void PlayJumpScare()
        {
            if (jumpScareAudioSource != null)
                jumpScareAudioSource.Play();
        }

        public void PlayFightLoop()
        {
            if (fightAudioSource != null)
                fightAudioSource.Play();
        }

        public void StopFightLoop()
        {
            if (fightAudioSource != null)
                fightAudioSource.Stop();
        }

        private void Trigger(string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName)) return;

            animator.SetTrigger(triggerName);
        }

        /// <summary>
        /// Seeds this component from the legacy fields still on Anomaly. Called only when Anomaly
        /// had to add the component itself at runtime, i.e. on a prefab that hasn't been through
        /// 'Tools/Give Me A Sign/Setup/2. Migrate Anomaly Prefabs' yet.
        /// </summary>
        public void ConfigureFromLegacy(Animator legacyAnimator, string moveTrigger, string idleTrigger,
                                        AudioSource jumpScare, AudioSource fight)
        {
            animator = legacyAnimator != null ? legacyAnimator : GetComponent<Animator>();
            moveTriggerName = moveTrigger;
            idleTriggerName = idleTrigger;
            jumpScareAudioSource = jumpScare;
            fightAudioSource = fight;
        }
    }
}
