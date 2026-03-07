using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class CloudWindSpeedController : MonoBehaviour
{
    [Header("Volume")]
    public Volume globalVolume;

    [Header("Wind Speed UI")]
    public Slider windSlider;

    [Header("Wind Sound")]
    [Tooltip("AudioSource for wind — set Loop = true in the Inspector")]
    public AudioSource windAudioSource;
    public AudioClip windLow;
    public AudioClip windHigh;

    [Header("Volume Sliders")]
    public Slider windVolumeSlider;
    public Slider pianoVolumeSlider;

    [Header("Piano Sound")]
    public AudioSource pianoAudioSource;

    private VolumetricClouds _clouds;
    private const float THRESHOLD = 3000f;

    void Start()
    {
        // ── Wind speed ──────────────────────────────────────
        if (globalVolume.profile.TryGet(out _clouds))
        {
            windSlider.minValue = 1500f;
            windSlider.maxValue = 5000f;
            windSlider.value    = _clouds.globalSpeed.value;
            windSlider.onValueChanged.AddListener(OnWindSpeedChanged);
            UpdateWindSound(windSlider.value);
        }

        // ── Wind volume slider ──────────────────────────────
        if (windVolumeSlider != null && windAudioSource != null)
        {
            windVolumeSlider.minValue = 0f;
            windVolumeSlider.maxValue = 1f;
            windVolumeSlider.value    = windAudioSource.volume;
            windVolumeSlider.onValueChanged.AddListener(OnWindVolumeChanged);
        }

        // ── Piano volume slider ─────────────────────────────
        if (pianoVolumeSlider != null && pianoAudioSource != null)
        {
            pianoVolumeSlider.minValue = 0f;
            pianoVolumeSlider.maxValue = 1f;
            pianoVolumeSlider.value    = pianoAudioSource.volume;
            pianoVolumeSlider.onValueChanged.AddListener(OnPianoVolumeChanged);
        }
    }

    // ── Callbacks ───────────────────────────────────────────
    void OnWindSpeedChanged(float value)
    {
        if (_clouds != null) _clouds.globalSpeed.value = value;
        UpdateWindSound(value);
    }

    void OnWindVolumeChanged(float value)
    {
        if (windAudioSource != null) windAudioSource.volume = value;
    }

    void OnPianoVolumeChanged(float value)
    {
        if (pianoAudioSource != null) pianoAudioSource.volume = value;
    }

    // ── Wind clip swap ──────────────────────────────────────
    void UpdateWindSound(float value)
    {
        if (windAudioSource == null) return;

        AudioClip targetClip = value < THRESHOLD ? windLow : windHigh;
        if (windAudioSource.clip == targetClip) return;

        windAudioSource.clip = targetClip;
        windAudioSource.Play();
    }

    void OnDestroy()
    {
        windSlider.onValueChanged.RemoveListener(OnWindSpeedChanged);
        if (windVolumeSlider  != null) windVolumeSlider.onValueChanged.RemoveListener(OnWindVolumeChanged);
        if (pianoVolumeSlider != null) pianoVolumeSlider.onValueChanged.RemoveListener(OnPianoVolumeChanged);
    }
}
