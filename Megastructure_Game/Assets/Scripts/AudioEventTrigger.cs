using UnityEngine;

public enum TriggerMode
{
    OnEnable,
    OnStart,
    Manual
}

public class AudioEventTrigger : MonoBehaviour
{
    [SerializeField] private SoundID soundID = SoundID.None;
    [SerializeField] private TriggerMode triggerMode = TriggerMode.Manual;
    [SerializeField] private bool isLooping = false;

    void OnEnable()
    {
        if (triggerMode == TriggerMode.OnEnable)
            TriggerSound();
    }

    void Start()
    {
        if (triggerMode == TriggerMode.OnStart)
            TriggerSound();
    }

    public void TriggerSound()
    {
        if (soundID == SoundID.None) return;

        if (isLooping)
            AudioEventDispatcher.PlayLoopingSound(soundID);
        else
            AudioEventDispatcher.PlaySound(soundID);
    }

    public void StopSound()
    {
        if (soundID == SoundID.None) return;

        if (isLooping)
            AudioEventDispatcher.StopLoopingSound(soundID);
    }
}
