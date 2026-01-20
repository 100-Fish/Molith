using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// manages sound effects playback and provides global access through singleton pattern
/// </summary>
public class SoundManager : MonoBehaviour
{
    private const float DEFAULT_PITCH_VARIATION = 0.1f;
    private const float PITCH_MODIFIER_MULTIPLIER = 0.05f;
    private const float SOUND_CLEANUP_DELAY_MULTIPLIER = 1.5f;

    private bool gameOver;

    [Header("Ambient Music")]
    [SerializeField] private AudioClip ambientMusic;
    [SerializeField] private AudioSource musicPlayer;
    [SerializeField][Range(0, 1)] private float musicVolume = 0.5f;

    [Header("Sound Effects")]
    [SerializeField]
    [Range(0, 1)]
    private float volumeMultiplier = 1;

    [SerializeField]
    private SoundIDMapping[] soundMappings = new SoundIDMapping[7];

    private Dictionary<SoundID, SoundEffect> soundLookup;
    private Dictionary<SoundID, GameObject> loopingSounds = new Dictionary<SoundID, GameObject>();

    private static SoundManager instance;
    public static SoundManager Instance => instance;

    /// <summary>
    /// initializes the singleton instance
    /// </summary>
    private void Awake()
    {
        InitializeSingleton();
    }

    private void Start()
    {
        // Start ambient music
        if (musicPlayer != null && ambientMusic != null)
        {
            musicPlayer.clip = ambientMusic;
            musicPlayer.volume = musicVolume;
            musicPlayer.loop = true;
            musicPlayer.Play();
        }
    }

    private void InitializeSingleton()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Build lookup dictionary from sound mappings
        soundLookup = new Dictionary<SoundID, SoundEffect>();
        foreach (var mapping in soundMappings)
        {
            if (mapping.soundID != SoundID.None)
                soundLookup[mapping.soundID] = mapping.soundEffect;
        }

        // Subscribe to audio events
        AudioEventDispatcher.OnPlaySound += PlaySound;
        AudioEventDispatcher.OnPlayLoopingSound += PlayLoopingSound;
        AudioEventDispatcher.OnStopLoopingSound += StopLoopingSound;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        AudioEventDispatcher.OnPlaySound -= PlaySound;
        AudioEventDispatcher.OnPlayLoopingSound -= PlayLoopingSound;
        AudioEventDispatcher.OnStopLoopingSound -= StopLoopingSound;

        // Clean up looping sounds
        foreach (var soundObject in loopingSounds.Values)
        {
            if (soundObject != null)
                Destroy(soundObject);
        }
        loopingSounds.Clear();
    }

    /// <summary>
    /// checks if the sound effect has any clips available
    /// </summary>
    private bool HasClips(SoundEffect soundEffect)
    {
        return soundEffect.Clips != null && soundEffect.Clips.Count > 0;
    }

    /// <summary>
    /// gets a random clip from the sound effect
    /// </summary>
    private AudioClip GetRandomClip(SoundEffect soundEffect)
    {
        return soundEffect.Clips[Random.Range(0, soundEffect.Clips.Count)];
    }

    /// <summary>
    /// creates a game object to play the sound effect
    /// </summary>
    private GameObject CreateSoundGameObject(string soundName, AudioClip clip)
    {
        var soundObject = new GameObject($"Sound: {soundName}, {clip.length}s");
        soundObject.transform.parent = transform;
        StartCoroutine(DestroyAfterDelay(soundObject, clip.length * SOUND_CLEANUP_DELAY_MULTIPLIER));
        return soundObject;
    }

    /// <summary>
    /// coroutine to destroy sound object after it finishes playing
    /// </summary>
    private IEnumerator DestroyAfterDelay(GameObject soundObject, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (soundObject != null)
            Destroy(soundObject);
    }

    /// <summary>
    /// configures and plays the sound effect
    /// </summary>
    private void ConfigureAndPlaySound(GameObject soundObject, SoundEffect soundEffect, AudioClip clip, int modifier)
    {
        AudioSource source = AddAndConfigureAudioSource(soundObject, clip, soundEffect);
        ApplyPitchModification(source, soundEffect, modifier);
        source.Play();
    }

    /// <summary>
    /// adds and configures the audio source component
    /// </summary>
    private AudioSource AddAndConfigureAudioSource(GameObject soundObject, AudioClip clip, SoundEffect soundEffect)
    {
        var source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = soundEffect.Volume * volumeMultiplier;
        return source;
    }

    /// <summary>
    /// applies pitch modifications to the audio source
    /// </summary>
    private void ApplyPitchModification(AudioSource source, SoundEffect soundEffect, int modifier)
    {
        if (soundEffect.Vary)
        {
            source.pitch += Random.Range(-DEFAULT_PITCH_VARIATION, DEFAULT_PITCH_VARIATION);
        }
        source.pitch += PITCH_MODIFIER_MULTIPLIER * modifier;
    }

    /// <summary>
    /// plays a sound effect by SoundID (new event-based system)
    /// </summary>
    public void PlaySound(SoundID soundID)
    {
        if (gameOver || soundID == SoundID.None) return;
        if (!soundLookup.ContainsKey(soundID)) return;

        SoundEffect soundEffect = soundLookup[soundID];
        if (!HasClips(soundEffect)) return;

        AudioClip clip = GetRandomClip(soundEffect);
        GameObject soundObject = CreateSoundGameObject(soundID.ToString(), clip);
        ConfigureAndPlaySound(soundObject, soundEffect, clip, 0);
    }

    /// <summary>
    /// plays a looping sound effect by SoundID
    /// </summary>
    public void PlayLoopingSound(SoundID soundID)
    {
        if (gameOver || soundID == SoundID.None) return;
        if (loopingSounds.ContainsKey(soundID)) return; // Already looping
        if (!soundLookup.ContainsKey(soundID)) return;

        SoundEffect soundEffect = soundLookup[soundID];
        if (!HasClips(soundEffect)) return;

        AudioClip clip = GetRandomClip(soundEffect);
        GameObject soundObject = new GameObject($"Looping Sound: {soundID}");
        soundObject.transform.parent = transform;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = soundEffect.Volume * volumeMultiplier;
        source.loop = true;
        source.Play();

        loopingSounds[soundID] = soundObject;
    }

    /// <summary>
    /// stops a looping sound effect by SoundID
    /// </summary>
    public void StopLoopingSound(SoundID soundID)
    {
        if (!loopingSounds.ContainsKey(soundID)) return;

        GameObject soundObject = loopingSounds[soundID];
        if (soundObject != null)
        {
            AudioSource source = soundObject.GetComponent<AudioSource>();
            if (source != null)
            {
                // Optional: Fade out
                source.DOFade(0, 0.2f).OnComplete(() => Destroy(soundObject));
            }
            else
            {
                Destroy(soundObject);
            }
        }

        loopingSounds.Remove(soundID);
    }

    /// <summary>
    /// plays a raw AudioClip with optional volume (used for footsteps)
    /// </summary>
    public void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        GameObject soundObject = new GameObject($"Sound: {clip.name}");
        soundObject.transform.parent = transform;
        StartCoroutine(DestroyAfterDelay(soundObject, clip.length * SOUND_CLEANUP_DELAY_MULTIPLIER));

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume * volumeMultiplier;
        source.pitch = 1f + Random.Range(-DEFAULT_PITCH_VARIATION, DEFAULT_PITCH_VARIATION);
        source.Play();
    }
}

/// <summary>
/// data structure to define a sound effect with its properties
/// </summary>
[System.Serializable]
public struct SoundEffect
{
    public List<AudioClip> Clips;

    [Range(0, 1)]
    public float Volume;

    public bool Vary;
}

/// <summary>
/// maps a SoundID enum to its corresponding SoundEffect configuration
/// </summary>
[System.Serializable]
public struct SoundIDMapping
{
    public SoundID soundID;
    public SoundEffect soundEffect;
}