using UnityEngine;
using UnityEngine.UI;

public class GameplaySettings : MonoBehaviour
{
    // NEW: public so other scripts (GameTester, TopMenuProximityZone) can read these directly via
    // PlayerPrefs without needing a reference to this script - same pattern as SoundSettings.
    public const string AutoSwitchViewKey = "AutoSwitchView";
    public const string ReduceMotionKey = "ReduceMotion";

    [Header("Drag the Auto Switch View and Reduce Motion toggles from the Gameplay tab here")]
    public Toggle _AutoSwitchViewToggle;
    public Toggle _ReduceMotionToggle;

    [Header("Needed so toggling Auto Switch View applies immediately, not just on next launch")]
    public GameTester _GameTester;

    void Awake()
    {
        bool savedAutoSwitchView = PlayerPrefs.GetInt(AutoSwitchViewKey, 1) == 1; // NEW: defaults to on, matching GameTester's original default
        bool savedReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;

        _AutoSwitchViewToggle.SetIsOnWithoutNotify(savedAutoSwitchView);
        _ReduceMotionToggle.SetIsOnWithoutNotify(savedReduceMotion);

        if (_GameTester != null)
        {
            _GameTester.SetAutoSwitchView(savedAutoSwitchView);
        }
    }

    public void SetAutoSwitchView(bool value) // NEW: wire the Auto Switch View toggle's OnValueChanged to this
    {
        PlayerPrefs.SetInt(AutoSwitchViewKey, value ? 1 : 0);
        PlayerPrefs.Save();

        if (_GameTester != null)
        {
            _GameTester.SetAutoSwitchView(value);
        }
    }

    public void SetReduceMotion(bool value) // NEW: wire the Reduce Motion toggle's OnValueChanged to this
    {
        PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
