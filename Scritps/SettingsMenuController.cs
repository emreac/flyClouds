using UnityEngine;

/// <summary>
/// Attach this to any GameObject.
/// Assign the SettingsPanel, ControlBlur, AudioSource and both buttons in the Inspector.
/// </summary>
public class SettingsMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject settingsPanel;

    [Header("Effects")]
    public ControlBlur controlBlur;

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    void Start()
    {
        CloseSettings();
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        if (controlBlur != null) controlBlur.ToggleBGUI();
        PlaySound(openSound);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        if (controlBlur != null) controlBlur.ToggleBGUI();
        PlaySound(closeSound);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
