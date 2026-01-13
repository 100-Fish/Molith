using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class SoundIDEvent : UnityEvent<SoundID> { }

public static class AudioEventDispatcher
{
    public static event System.Action<SoundID> OnPlaySound;
    public static event System.Action<SoundID> OnPlayLoopingSound;
    public static event System.Action<SoundID> OnStopLoopingSound;

    public static void PlaySound(SoundID id)
    {
        OnPlaySound?.Invoke(id);
    }

    public static void PlayLoopingSound(SoundID id)
    {
        OnPlayLoopingSound?.Invoke(id);
    }

    public static void StopLoopingSound(SoundID id)
    {
        OnStopLoopingSound?.Invoke(id);
    }
}
