using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Marks this GameObject's AudioSource as belonging to a specific volume channel
    /// (e.g. Music for background/ambient loops). Without this component the AudioManager
    /// still picks the source up automatically - it just defaults to the SFX channel.
    ///
    /// Add it to any new looping music/ambience object so the Music slider controls it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ManagedAudioSource : MonoBehaviour
    {
        [Tooltip("Which volume slider controls this source. Music = background/ambient loops, Sfx = everything else.")]
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;

        public AudioChannel Channel => channel;

        void OnEnable()
        {
            // AudioManager bootstraps before the first scene loads, so it exists by now;
            // the null check just keeps edge cases (manager destroyed on quit) quiet.
            Instance()?.RegisterSource(GetComponent<AudioSource>(), channel, overrideChannel: true);
        }

        private static AudioManager Instance() => AudioManager.Instance;
    }
}
