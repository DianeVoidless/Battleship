using UnityEngine;
using UnityEngine.UI;

public class SoundSettings : MonoBehaviour
{
    // NEW: PlayerPrefs keys, public so a future AudioManager can read these directly without needing
    // a reference to this script - e.g. PlayerPrefs.GetFloat(SoundSettings.MasterVolumeKey, 1f)
    public const string MasterVolumeKey = "MasterVolume";
    public const string MusicVolumeKey = "MusicVolume";
    public const string SFXVolumeKey = "SFXVolume";
    public const string MutedKey = "Muted";

    [Header("Drag the 3 sliders and the mute toggle from the Sound tab here")]
    public Slider _MasterSlider;
    public Slider _MusicSlider;
    public Slider _SFXSlider;
    public Toggle _MuteToggle;

    void Awake()
    {
        // Restore saved values into the UI without re-triggering the OnValueChanged callbacks below
        // (SetValueWithoutNotify avoids an unnecessary save-to-self loop on startup)
        _MasterSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        _MusicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
        _SFXSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SFXVolumeKey, 1f));
        _MuteToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(MutedKey, 0) == 1);
    }

    public void SetMasterVolume(float value) // NEW: wire the Master slider's OnValueChanged to this
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, value);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value) // NEW: wire the Music slider's OnValueChanged to this
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, value);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value) // NEW: wire the SFX slider's OnValueChanged to this
    {
        PlayerPrefs.SetFloat(SFXVolumeKey, value);
        PlayerPrefs.Save();
    }

    public void SetMuted(bool muted) // NEW: wire the Mute toggle's OnValueChanged to this
    {
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
    }
}
