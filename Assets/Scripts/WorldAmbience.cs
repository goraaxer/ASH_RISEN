using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class WorldAmbience : MonoBehaviour
{
    public AudioClip occasionalSound;
    public float minInterval = 12f;
    public float maxInterval = 25f;
    [Range(0f, 1f)] public float occasionalVolume = .35f;

    AudioSource bed;
    AudioSource[] layers;
    float[] levels;
    float nextSoundAt;
    float phase;

    void Awake()
    {
        bed = GetComponent<AudioSource>();
        layers = GetComponents<AudioSource>();
        levels = new float[layers.Length];
        for (int i = 0; i < layers.Length; i++) levels[i] = layers[i].volume;
        phase = Random.Range(0f, 100f);
        Schedule();
    }

    void Update()
    {
        if (PauseMenu.IsPaused) return;
        float time = Time.time + phase;
        for (int i = 0; i < layers.Length; i++)
        {
            float rate = i == 0 ? .09f : i == 1 ? .16f : i == 2 ? .23f : .07f;
            float range = i == 0 ? .12f : i == 1 ? .32f : i == 2 ? .45f : .2f;
            float wave = Mathf.Sin(time * rate + i * 1.7f);
            float drift = Mathf.PerlinNoise(i * 13.7f, time * .025f) * 2f - 1f;
            layers[i].volume = levels[i] * (1f + range * (.55f * wave + .45f * drift));
        }
        if (Time.time < nextSoundAt) return;
        if (occasionalSound != null) bed.PlayOneShot(occasionalSound, occasionalVolume);
        Schedule();
    }

    void Schedule()
    {
        nextSoundAt = Time.time + Random.Range(minInterval, Mathf.Max(minInterval, maxInterval));
    }
}
