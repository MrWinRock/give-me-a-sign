using UnityEngine;
using UnityEngine.Video;

namespace GameLogic
{
    /// <summary>
    /// Only lets a VideoPlayer decode while the thing that displays it is actually visible.
    ///
    /// The screen-transition video feeds a 1920x1080 RenderTexture but is only ever shown for the
    /// moment a room switch takes - left on Play On Awake + Loop it decodes a full-HD clip every
    /// frame for the whole night into a texture nobody is looking at.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoPlaybackGate : MonoBehaviour
    {
        [Tooltip("The object that displays this video (e.g. the RawImage overlay). Playback runs only while it is active.")]
        [SerializeField] private GameObject displayObject;

        [Tooltip("Restart from the first frame each time the display appears, instead of resuming where it paused.")]
        [SerializeField] private bool restartOnShow = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private VideoPlayer _player;
        private bool _wasVisible;

        void Awake()
        {
            _player = GetComponent<VideoPlayer>();

            // Owned here from now on, whatever the Inspector says - otherwise the clip would
            // still decode for a frame or two before the first gate check runs.
            _player.playOnAwake = false;

            if (displayObject == null)
                Debug.LogWarning("VideoPlaybackGate: no Display Object assigned - the video will never play.", this);
        }

        void Start() => Apply(IsVisible(), force: true);

        void LateUpdate()
        {
            bool visible = IsVisible();
            if (visible == _wasVisible) return;

            Apply(visible, force: false);
        }

        private bool IsVisible() => displayObject != null && displayObject.activeInHierarchy;

        private void Apply(bool visible, bool force)
        {
            _wasVisible = visible;

            if (visible)
            {
                if (restartOnShow) _player.frame = 0;
                _player.Play();
            }
            else
            {
                // Pause, not Stop: Stop tears down the decoder and makes the next show hitch.
                _player.Pause();
            }

            if (showDebugInfo && !force)
                Debug.Log($"VideoPlaybackGate: {(visible ? "playing" : "paused")} '{_player.clip?.name}'.", this);
        }
    }
}
