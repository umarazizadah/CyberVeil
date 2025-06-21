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
        DASH
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

        private void Awake()
        {
            instance = this; //sets up singleton
            sfxAudioSource = gameObject.AddComponent<AudioSource>();

            // Creates new child gameobject attacthed to soundmanager
            GameObject footstepObject = new GameObject("FootstepAudioSource");
            footstepObject.transform.SetParent(transform);
            // Adds an AudioSource component to it and sets it to loop
            footstepAudioSource = footstepObject.AddComponent<AudioSource>();
            footstepAudioSource.loop = true;
        }

        private void Start()
        {
            audioSource = GetComponent<AudioSource>(); //accesses audio source
            PlayBackgroundMusic();
        }

        /// <summary>
        /// Plays a sound from a category defined by SoundType at a specific volume
        /// </summary>
        public static void PlaySound(SoundType sound, float volume)
        {
            // Looks up the SoundList array using the enum index
            AudioClip[] clips = instance.soundList[(int)sound].Sounds; // Converts the enum to an index to grab the correct sound group
            AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)]; // Picks random audio clip
            instance.sfxAudioSource.pitch = UnityEngine.Random.Range(0.7f, 1.1f); // Randomizes pitch for sound variety
            instance.sfxAudioSource.PlayOneShot(randomClip, volume); //Plays once
        }

        public static void PlayWalkingSound(float volume)
        {
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
            //Stop immediately, no waiting for clip to finish
            instance.footstepAudioSource.Stop();
        }

        public static void PlayBackgroundMusic()
        {
            AudioClip[] musicClips = instance.soundList[(int)SoundType.BACKGROUNDMUSIC].Sounds;
            AudioClip backgroundMusic = musicClips[0];
            instance.audioSource.clip = backgroundMusic;
            instance.audioSource.loop = true;
            instance.audioSource.Play();
        }

        public static void StopBackgroundMusic()
        {
            instance.audioSource.Stop();
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
                soundList[i].name = names[i];
            }
        }
#endif
    }

    [Serializable]
    // Defines the soundlist struct
    public struct SoundList
    {
        public AudioClip[] Sounds { get => sounds; } // Read only access to the sound array 
        [HideInInspector] public string name; // Hides the internal category label 
        [SerializeField] private AudioClip[] sounds; // Arry of clips 
    }
}