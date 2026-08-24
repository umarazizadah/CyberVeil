using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace CyberVeil.Systems
{
    // Reference specific sound categories
    public enum SoundType
    {
        WALK,
        ATTACK,
        BACKGROUNDMUSIC,
        SLASH,
        PLAYERDAMAGE,
        ENEMYDAMAGE,
        SLIMESPLAT,
        SLIMEJUMP,
        DASH,
        SHIELDATTK,
        ATTACKLOCK,
        TRIALEND,
        CARDHOVER,
        CARDCLICK,
        CARDCYBER,
        GROUNDSLAM1,
        WINDUP,
        MULTISLASHES,
        WHOOSH,
        GROUNDSLAM2,
        CHARGEUP,
        JABHIT,
        JABTHRUST,
        MUSHROOMWHOOSH,
        MUSHROOMAMBIENT1,
        MUSHROOMAMBIENT2,
        SLIMEAMBIENT1,
        ENEMYSPAWN,
        ENEMYDEATH,
        GOLEMAMBIENT1,
        GOLEMAMBIENT2,
        VEYMARAMBIENT1,
        VEYMARAMBIENT2,
        VEYMARAMBIENT3,
        VEYMARTALK,
        VEILSURGEACTIVATION,
        VEILSURGEACTIVE,
        BATPROJECTILEBLAST,
        BATAMBIENT1,
        HOMEUISELECT,
        HOMEUIENTER,
        HOMEUISELECT2,

    }

    // Ensures an audio source is on the game object and lets the script run in the editor
    [RequireComponent(typeof(AudioSource)), ExecuteInEditMode]

    /// <summary>
    /// Centralized audio management system for gameplay-related sounds in the game
    /// This implements a Singleton pattern to ensure global access to sound functionality. 
    /// It organizes audio into categories using the <see cref="SoundType"/> enum, and provides separate 
    /// audio channels for music, sound effects, and looping footsteps to prevent overlap or audio conflicts
    /// Responsibilities include:
    /// - Initializing AudioSources for different sound layers
    /// - Playing random variations of sound clips for variation
    /// - Controlling background music and environmental audio
    /// - Providing static access to core sound functions from other systems (e.g., Player, Combat)
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        // Holds all grouped sound clips categorized by SoundList
        [SerializeField] private SoundList[] soundList; // Array of sound categories
        private static SoundManager instance; // Gives global access to the soundmanagaer (singleton)
        private AudioSource audioSource; // Used for background music 
        private AudioSource footstepAudioSource; // Dedicated audio source for footsteps
        private AudioSource sfxAudioSource;// Uses one-shot SFX
        // Runtime-managed background music players (allow multiple simultaneous tracks)
        private Dictionary<int, AudioSource> backgroundMusicPlayers = new Dictionary<int, AudioSource>();
        private List<AudioClip> runtimeBackgroundClips = new List<AudioClip>();
        private int nextBgMusicId = 1;

        private void Awake()
        {
            // Enforce singleton: if another SoundManager exists, destroy this duplicate
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            // Ensure have a dedicated SFX AudioSource, there will always be at least one AudioSource due to RequireComponent,
            // so reuse an existing second AudioSource if present, otherwise create one
            var existingAudioSources = GetComponents<AudioSource>();
            if (existingAudioSources.Length > 1)
            {
                // Use the second AudioSource as the SFX source (first is reserved for background music)
                sfxAudioSource = existingAudioSources[1];
            }
            else
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
            }

            // Only create the FootstepAudioSource GameObject if it doesn't already exist as a child
            Transform footstepChild = transform.Find("FootstepAudioSource");
            if (footstepChild != null)
            {
                footstepAudioSource = footstepChild.GetComponent<AudioSource>();
                if (footstepAudioSource == null)
                    footstepAudioSource = footstepChild.gameObject.AddComponent<AudioSource>();
            }
            else
            {
                GameObject footstepObject = new GameObject("FootstepAudioSource");
                footstepObject.transform.SetParent(transform);
                footstepAudioSource = footstepObject.AddComponent<AudioSource>();
            }

            footstepAudioSource.loop = true;
        }


        private void Start()
        {
            audioSource = GetComponent<AudioSource>(); //accesses audio source
            // Play default first background track if available (legacy behavior)
            if (audioSource != null)
                PlayBackgroundMusic();
        }

        /// <summary>
        /// Plays a sound from a category defined by SoundType at a specific volume
        /// </summary>
        public static void PlaySound(SoundType sound, float volume)
        {
            int soundIndex = (int)sound;
            if (instance == null
                || instance.sfxAudioSource == null
                || instance.soundList == null
                || soundIndex < 0
                || soundIndex >= instance.soundList.Length
                || instance.soundList[soundIndex] == null)
            {
                return;
            }

            AudioClip[] clips = instance.soundList[soundIndex].Sounds;
            if (clips == null || clips.Length == 0)
                return;

            AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (randomClip == null)
                return;

            instance.sfxAudioSource.pitch = UnityEngine.Random.Range(0.7f, 1.1f);
            instance.sfxAudioSource.PlayOneShot(randomClip, volume);
        }

        public static void PlayWalkingSound(float volume)
        {
            if (instance == null || instance.soundList == null || (int)SoundType.WALK >= instance.soundList.Length)
                return;
            if (!instance.footstepAudioSource.isPlaying)
            {
                // Pulls a random walk sound
                AudioClip[] clips = instance.soundList[(int)SoundType.WALK].Sounds;
                AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];

                // Play it on the dedicated footstep audio source with looping
                instance.footstepAudioSource.clip = randomClip;
                instance.footstepAudioSource.volume = volume;
                instance.footstepAudioSource.pitch = UnityEngine.Random.Range(0.8f, 1.1f);
                instance.footstepAudioSource.Play();
            }
        }

        public static void StopWalkingSound()
        {
            if (instance == null || instance.soundList == null || (int)SoundType.WALK >= instance.soundList.Length)
                return;
            //Stop immediately, no waiting for clip to finish
            instance.footstepAudioSource.Stop();
        }

        public static void PlayBackgroundMusic()
        {
            // Legacy single-track background music: plays first clip from either runtime list or built-in list
            if (instance == null) return;

            AudioClip clipToPlay = null;
            if (instance.runtimeBackgroundClips.Count > 0)
                clipToPlay = instance.runtimeBackgroundClips[0];
            else
            {
                AudioClip[] musicClips = instance.soundList[(int)SoundType.BACKGROUNDMUSIC].Sounds;
                if (musicClips != null && musicClips.Length > 0)
                    clipToPlay = musicClips[0];
            }

            if (clipToPlay == null) return;

            instance.audioSource.clip = clipToPlay;
            instance.audioSource.loop = true;
            instance.audioSource.Play();
        }

        public static void StopBackgroundMusic()
        {
            if (instance == null) return;
            instance.audioSource.Stop();
            // stop any runtime background players as well
            foreach (var kv in instance.backgroundMusicPlayers)
            {
                if (kv.Value != null) kv.Value.Stop();
            }
            // destroy the runtime player GameObjects
            var keys = new List<int>(instance.backgroundMusicPlayers.Keys);
            foreach (var k in keys)
            {
                var src = instance.backgroundMusicPlayers[k];
                if (src != null) Destroy(src.gameObject);
                instance.backgroundMusicPlayers.Remove(k);
            }
        }

        /// <summary>
        /// Play a background music clip by index. Index first indexes into runtime-added clips then falls back to built-in BACKGROUNDMUSIC.
        /// Returns an id for the created music player (use StopBackgroundMusicById to stop), or -1 on failure.
        /// </summary>
        public static int PlayBackgroundMusicByIndex(int index, bool loop = true, float volume = 1f)
        {
            if (instance == null) return -1;

            AudioClip clip = null;
            if (index >= 0 && index < instance.runtimeBackgroundClips.Count)
                clip = instance.runtimeBackgroundClips[index];
            else
            {
                int builtInIndex = index - instance.runtimeBackgroundClips.Count;
                AudioClip[] built = instance.soundList[(int)SoundType.BACKGROUNDMUSIC].Sounds;
                if (built != null && builtInIndex >= 0 && builtInIndex < built.Length)
                    clip = built[builtInIndex];
            }

            if (clip == null) return -1;
            return PlayBackgroundMusicClip(clip, loop, volume);
        }

        /// <summary>
        /// Play a specific AudioClip as background music on its own AudioSource. Returns player id.
        /// </summary>
        public static int PlayBackgroundMusicClip(AudioClip clip, bool loop = true, float volume = 1f)
        {
            if (instance == null || clip == null) return -1;
            int id = instance.nextBgMusicId++;
            GameObject go = new GameObject($"BGMusic_{id}");
            go.transform.SetParent(instance.transform);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.volume = volume;
            src.spatialBlend = 0f; // 2D music
            src.playOnAwake = false;
            src.Play();
            instance.backgroundMusicPlayers[id] = src;
            return id;
        }

        /// <summary>
        /// Stop and remove a runtime background music player by id
        /// </summary>
        public static void StopBackgroundMusicById(int id)
        {
            if (instance == null) return;
            if (instance.backgroundMusicPlayers.TryGetValue(id, out var src))
            {
                if (src != null) src.Stop();
                if (src != null) Destroy(src.gameObject);
                instance.backgroundMusicPlayers.Remove(id);
            }
        }

        /// <summary>
        /// Stop all runtime background music players (keeps legacy audioSource untouched)
        /// </summary>
        public static void StopAllRuntimeBackgroundMusic()
        {
            if (instance == null) return;
            var keys = new List<int>(instance.backgroundMusicPlayers.Keys);
            foreach (var k in keys) StopBackgroundMusicById(k);
        }

        /// <summary>
        /// Add/remove runtime background clips (these are considered before built-in BACKGROUNDMUSIC when using PlayBackgroundMusicByIndex)
        /// </summary>
        public static int AddRuntimeBackgroundClip(AudioClip clip)
        {
            if (instance == null || clip == null) return -1;
            instance.runtimeBackgroundClips.Add(clip);
            return instance.runtimeBackgroundClips.Count - 1;
        }

        public static bool RemoveRuntimeBackgroundClip(AudioClip clip)
        {
            if (instance == null || clip == null) return false;
            return instance.runtimeBackgroundClips.Remove(clip);
        }

        /// <summary>
        /// This system auto-generates and labels the soundList in the editor, so can just drag in clips and be sure they align with 
        /// SoundType, no manual syncing needed
        /// </summary>
#if UNITY_EDITOR
        private void OnEnable()
        {
            // Checks what entries exist in the enum
            string[] names = Enum.GetNames(typeof(SoundType));
            // Resises soundlist to match the enum size
            Array.Resize(ref soundList, names.Length); // Dynamically changes the size of the soundList array
            // Labels each entry with the correct name
            for (int i = 0; i < soundList.Length; i++)
            {
                if (soundList[i] == null)
                    soundList[i] = new SoundList();
                soundList[i].name = names[i];
            }
        }
#endif
    }

    [Serializable]
    // Defines the soundlist struct
    public class SoundList
    {
        public AudioClip[] Sounds { get => sounds; } // Read only access to the sound array 
        [HideInInspector] public string name; // Hides the internal category label 
        [SerializeField] private AudioClip[] sounds; // Arry of clips 
    }
}
