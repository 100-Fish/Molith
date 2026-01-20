using UnityEngine;
using System.Collections;

public class AmbientSoundPlayer : MonoBehaviour
{
    [Header("Ambient Settings")]
    [SerializeField] private SoundID ambientSoundID = SoundID.AmbientWind;

    [SerializeField] private float minInterval = 10f;
    [SerializeField] private float maxInterval = 30f;

    [SerializeField] private bool playOnStart = true;

    private Coroutine playbackCoroutine;

    void Start()
    {
        if (playOnStart)
            StartAmbientLoop();
    }

    public void StartAmbientLoop()
    {
        if (playbackCoroutine != null)
            StopCoroutine(playbackCoroutine);

        playbackCoroutine = StartCoroutine(AmbientPlaybackLoop());
    }

    public void StopAmbientLoop()
    {
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }
    }

    IEnumerator AmbientPlaybackLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            if (ambientSoundID != SoundID.None)
                AudioEventDispatcher.PlaySound(ambientSoundID);
        }
    }

    void OnDestroy()
    {
        StopAmbientLoop();
    }
}
