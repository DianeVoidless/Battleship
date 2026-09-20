using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class VideoSettings : MonoBehaviour
{
    private const string FullscreenKey = "Fullscreen";
    private const string ResolutionIndexKey = "ResolutionIndex";
    private const string FrameRateIndexKey = "FrameRateIndex";
    private const string VSyncKey = "VSync";

    [Header("Drag the Fullscreen toggle and VSync toggle from the Video tab here")]
    public Toggle _FullscreenToggle;
    public Toggle _VSyncToggle;

    [Header("Drag the Resolution and Frame Rate dropdowns from the Video tab here")]
    public TMP_Dropdown _ResolutionDropdown;
    public TMP_Dropdown _FrameRateDropdown;

    // NEW: the actual frame rate each Frame Rate dropdown entry maps to, in the SAME ORDER as the
    // labels below. -1 means "uncapped" (Unity's own meaning for Application.targetFrameRate).
    private readonly int[] _FrameRateValues = { 30, 60, 120, 144, -1 };
    private readonly string[] _FrameRateLabels = { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "Unlimited" };

    private List<Resolution> _AvailableResolutions; // NEW: de-duplicated (by width/height only, ignoring refresh rate) list backing the dropdown

    void Awake()
    {
        BuildResolutionOptions();
        BuildFrameRateOptions();

        bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        int savedResolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, GetCurrentResolutionIndex());
        int savedFrameRateIndex = PlayerPrefs.GetInt(FrameRateIndexKey, 1); // NEW: defaults to 60 FPS (index 1) the very first time the game ever runs
        bool savedVSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;

        savedResolutionIndex = Mathf.Clamp(savedResolutionIndex, 0, _AvailableResolutions.Count - 1);
        savedFrameRateIndex = Mathf.Clamp(savedFrameRateIndex, 0, _FrameRateValues.Length - 1);

        // Restore saved values into the UI without re-triggering the OnValueChanged callbacks below,
        // then explicitly apply each one so the actual game window matches on startup.
        _FullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
        _ResolutionDropdown.SetValueWithoutNotify(savedResolutionIndex);
        _FrameRateDropdown.SetValueWithoutNotify(savedFrameRateIndex);
        _VSyncToggle.SetIsOnWithoutNotify(savedVSync);

        ApplyVSync(savedVSync);       // NEW: applied before frame rate, since VSync overrides any frame rate cap while it's on
        ApplyFrameRate(savedFrameRateIndex);
        ApplyResolution(savedResolutionIndex, savedFullscreen);
    }

    private void BuildResolutionOptions() // NEW: Screen.resolutions often lists the same width/height multiple times (once per refresh rate) - collapse those down to one entry each
    {
        _AvailableResolutions = Screen.resolutions
            .GroupBy(r => new { r.width, r.height })
            .Select(g => g.First())
            .OrderByDescending(r => r.width * r.height)
            .ToList();

        List<string> labels = _AvailableResolutions.Select(r => r.width + " x " + r.height).ToList();

        _ResolutionDropdown.ClearOptions();
        _ResolutionDropdown.AddOptions(labels);
    }

    private void BuildFrameRateOptions()
    {
        _FrameRateDropdown.ClearOptions();
        _FrameRateDropdown.AddOptions(_FrameRateLabels.ToList());
    }

    private int GetCurrentResolutionIndex() // NEW: finds the dropdown entry matching whatever resolution the game happened to launch at
    {
        for (int i = 0; i < _AvailableResolutions.Count; i++)
        {
            if (_AvailableResolutions[i].width == Screen.currentResolution.width
                && _AvailableResolutions[i].height == Screen.currentResolution.height)
            {
                return i;
            }
        }
        return 0;
    }

    public void SetFullscreen(bool isFullscreen) // NEW: wire the Fullscreen toggle's OnValueChanged to this
    {
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
        ApplyResolution(_ResolutionDropdown.value, isFullscreen);
    }

    public void SetResolution(int index) // NEW: wire the Resolution dropdown's OnValueChanged to this
    {
        PlayerPrefs.SetInt(ResolutionIndexKey, index);
        PlayerPrefs.Save();
        ApplyResolution(index, _FullscreenToggle.isOn);
    }

    public void SetFrameRate(int index) // NEW: wire the Frame Rate dropdown's OnValueChanged to this
    {
        PlayerPrefs.SetInt(FrameRateIndexKey, index);
        PlayerPrefs.Save();
        ApplyFrameRate(index);
    }

    public void SetVSync(bool enabled) // NEW: wire the VSync toggle's OnValueChanged to this
    {
        PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVSync(enabled);
    }

    private void ApplyResolution(int index, bool fullscreen)
    {
        if (_AvailableResolutions == null || index < 0 || index >= _AvailableResolutions.Count)
        {
            return;
        }

        Resolution target = _AvailableResolutions[index];
        Screen.SetResolution(target.width, target.height, fullscreen);
    }

    private void ApplyFrameRate(int index)
    {
        index = Mathf.Clamp(index, 0, _FrameRateValues.Length - 1);
        Application.targetFrameRate = _FrameRateValues[index]; // -1 here means uncapped, matching Unity's own convention
    }

    private void ApplyVSync(bool enabled)
    {
        // NEW: while VSync is on, Unity syncs to the monitor's refresh rate and ignores
        // Application.targetFrameRate entirely - the Frame Rate dropdown has no effect until VSync is off.
        QualitySettings.vSyncCount = enabled ? 1 : 0;
    }
}
