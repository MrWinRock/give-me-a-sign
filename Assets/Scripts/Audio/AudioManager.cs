using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Audio
{
    /// <summary>Which volume slider a sound belongs to. Master applies on top of both.</summary>
    public enum AudioChannel
    {
        Music, // looping background music / ambience
        Sfx    // everything else: jumpscares, UI clicks, mic feedback, voices
    }

    /// <summary>
    /// One named sound in the AudioManager's library. Add an entry here (Inspector on the
    /// AudioManager prefab in Assets/Resources) and any script can play it with
    /// <c>AudioManager.Instance.Play("name")</c> - no AudioSource setup needed.
    /// </summary>
    [Serializable]
    public class SoundDef
    {
        [Tooltip("The name scripts use to play this sound: AudioManager.Instance.Play(\"name\").")]
        public string soundName = "New Sound";
        public AudioClip clip;
        public AudioChannel channel = AudioChannel.Sfx;
        [Tooltip("Base volume of this sound, before the channel/master sliders are applied.")]
        [Range(0f, 1f)] public float volume = 1f;
    }

    /// <summary>
    /// Central audio hub. Lives on a prefab in Assets/Resources and bootstraps itself before
    /// the first scene loads, then survives every scene change (DontDestroyOnLoad) - so the
    /// player's volume settings work from StartScene all the way through Result.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const string MasterKey = "Vol_Master";
        private const string MusicKey = "Vol_Music";
        private const string SfxKey = "Vol_Sfx";

        public static AudioManager Instance { get; private set; }

        [Header("Volumes (live-tunable in Play mode; saved to PlayerPrefs)")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Header("Sound Library (for Play(\"name\") - add future sounds here)")]
        [SerializeField] private List<SoundDef> soundLibrary = new List<SoundDef>();

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public event Action OnVolumesChanged;

        private class Registered
        {
            public AudioChannel channel;
            public float baseVolume; // the volume authored on the source, captured at registration
        }

        private readonly Dictionary<AudioSource, Registered> _sources = new Dictionary<AudioSource, Registered>();
        private readonly Dictionary<string, AudioSource> _loops = new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);
        private AudioSource _sfxOneShot;   // shared 2D source for Play() one-shots
        private AudioSource _musicOneShot;
        private static readonly List<AudioSource> _prune = new List<AudioSource>();

        // =======================================================================================
        // Bootstrap & lifecycle
        // =======================================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<GameObject>("AudioManager");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning("AudioManager: no 'AudioManager' prefab found in a Resources folder - creating a bare instance (empty sound library).");
                new GameObject("AudioManager").AddComponent<AudioManager>();
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            masterVolume = PlayerPrefs.GetFloat(MasterKey, masterVolume);
            musicVolume = PlayerPrefs.GetFloat(MusicKey, musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxKey, sfxVolume);

            _sfxOneShot = CreateChildSource("SfxOneShot");
            _musicOneShot = CreateChildSource("MusicOneShot");

            SceneManager.sceneLoaded += OnSceneLoaded;
            RegisterAllInScene();
            ApplyAll();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
                Instance = null;
        }

        private AudioSource CreateChildSource(string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D
            return src;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterAllInScene();
            ApplyAll();
        }

        // =======================================================================================
        // Volume properties (bind the future options UI's sliders to these)
        // =======================================================================================

        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MasterKey, masterVolume); ApplyAll(); }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set { musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, musicVolume); ApplyAll(); }
        }

        public float SfxVolume
        {
            get => sfxVolume;
            set { sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxKey, sfxVolume); ApplyAll(); }
        }

        public float GetEffectiveVolume(AudioChannel channel)
        {
            float ch = channel == AudioChannel.Music ? musicVolume : sfxVolume;
            return ch * masterVolume;
        }

        // =======================================================================================
        // Source registration
        // =======================================================================================

        public void RegisterSource(AudioSource source, AudioChannel channel, bool overrideChannel = false)
        {
            if (source == null) return;

            if (_sources.TryGetValue(source, out var existing))
            {
                if (overrideChannel && existing.channel != channel)
                {
                    existing.channel = channel;
                    ApplyTo(source, existing);
                }
                return;
            }

            var reg = new Registered { channel = channel, baseVolume = source.volume };
            _sources[source] = reg;
            ApplyTo(source, reg);
        }

        public static void RegisterHierarchy(GameObject root)
        {
            if (Instance == null || root == null) return;

            foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
            {
                var managed = src.GetComponent<ManagedAudioSource>();
                Instance.RegisterSource(src, managed != null ? managed.Channel : AudioChannel.Sfx,
                    overrideChannel: managed != null);
            }
        }

        private void RegisterAllInScene()
        {
            var all = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var src in all)
            {
                if (src.transform.IsChildOf(transform)) continue; // our own one-shot/loop players

                var managed = src.GetComponent<ManagedAudioSource>();
                RegisterSource(src, managed != null ? managed.Channel : AudioChannel.Sfx,
                    overrideChannel: managed != null);
            }

            if (showDebugInfo)
                Debug.Log($"AudioManager: {_sources.Count} sources registered after scene sweep.", this);
        }

        private void ApplyAll()
        {
            // Prune sources destroyed by scene changes.
            _prune.Clear();
            foreach (var kvp in _sources)
                if (kvp.Key == null) _prune.Add(kvp.Key);
            foreach (var dead in _prune)
                _sources.Remove(dead);

            foreach (var kvp in _sources)
                ApplyTo(kvp.Key, kvp.Value);

            // Shared one-shot players carry the channel volume; Play() adds the per-sound volume.
            if (_sfxOneShot != null) _sfxOneShot.volume = GetEffectiveVolume(AudioChannel.Sfx);
            if (_musicOneShot != null) _musicOneShot.volume = GetEffectiveVolume(AudioChannel.Music);

            OnVolumesChanged?.Invoke();
        }

        private void ApplyTo(AudioSource source, Registered reg)
        {
            source.volume = reg.baseVolume * GetEffectiveVolume(reg.channel);
        }

        // =======================================================================================
        // Sound library (the easy path for future sounds)
        // =======================================================================================

        public void Play(string soundName)
        {
            var def = FindDef(soundName);
            if (def == null || def.clip == null) return;

            var player = def.channel == AudioChannel.Music ? _musicOneShot : _sfxOneShot;
            player.PlayOneShot(def.clip, def.volume);

            if (showDebugInfo)
                Debug.Log($"AudioManager: Play('{soundName}')", this);
        }

        public void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _sfxOneShot == null) return;
            _sfxOneShot.PlayOneShot(clip, Mathf.Clamp01(volume));

            if (showDebugInfo)
                Debug.Log($"AudioManager: PlayClip('{clip.name}')", this);
        }

        public AudioSource PlayLoop(string soundName)
        {
            var def = FindDef(soundName);
            if (def == null || def.clip == null) return null;

            if (!_loops.TryGetValue(def.soundName, out var src) || src == null)
            {
                src = CreateChildSource($"Loop_{def.soundName}");
                src.loop = true;
                _loops[def.soundName] = src;
            }

            src.clip = def.clip;
            src.volume = def.volume; // authored level; RegisterSource captures it as base
            RegisterSource(src, def.channel);
            src.Play();
            return src;
        }

        public void StopLoop(string soundName)
        {
            if (_loops.TryGetValue(soundName, out var src) && src != null)
                src.Stop();
        }

        private SoundDef FindDef(string soundName)
        {
            if (string.IsNullOrWhiteSpace(soundName)) return null;

            foreach (var def in soundLibrary)
            {
                if (def != null && string.Equals(def.soundName, soundName, StringComparison.OrdinalIgnoreCase))
                    return def;
            }

            Debug.LogWarning($"AudioManager: no sound named '{soundName}' in the library.", this);
            return null;
        }

        // =======================================================================================
        // Editor live-tuning
        // =======================================================================================

        void OnValidate()
        {
            // Dragging the sliders in Play mode applies (and saves) immediately, so the mix
            // can be tuned by ear. Outside Play mode this is just the serialized default.
            if (Application.isPlaying && Instance == this)
            {
                PlayerPrefs.SetFloat(MasterKey, masterVolume);
                PlayerPrefs.SetFloat(MusicKey, musicVolume);
                PlayerPrefs.SetFloat(SfxKey, sfxVolume);
                ApplyAll();
            }
        }
    }
}
