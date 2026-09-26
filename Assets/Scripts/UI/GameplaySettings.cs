using UnityEngine;
using UnityEngine.UI;
using TMPro; // NEW: for the Username TMP_InputField

public class GameplaySettings : MonoBehaviour
{
    // NEW: public so other scripts (GameTester, TopMenuProximityZone) can read these directly via
    // PlayerPrefs without needing a reference to this script - same pattern as SoundSettings.
    public const string AutoSwitchViewKey = "AutoSwitchView";
    public const string ReduceMotionKey = "ReduceMotion";
    public const string UsernameKey = "Username"; // NEW: shared with UsernameInputScreen (first launch) and ColorChoicePanel (reads this to display the local player's name)
    public const int MaxUsernameLength = 15; // NEW: keep this in sync with the Character Limit set on both username TMP_InputFields in the Inspector - this is the code-side backstop, not the primary enforcement (that's the fields' own Content Type = Alphanumeric + Character Limit settings)

    [Header("Drag the Auto Switch View and Reduce Motion toggles from the Gameplay tab here")]
    public Toggle _AutoSwitchViewToggle;
    public Toggle _ReduceMotionToggle;

    [Header("Drag the 'Your In-Game name' input field from the Gameplay tab here")] // NEW
    public TMP_InputField _UsernameField;

    [Header("Needed so toggling Auto Switch View applies immediately, not just on next launch")]
    public GameTester _GameTester;

    void Awake()
    {
        bool savedAutoSwitchView = PlayerPrefs.GetInt(AutoSwitchViewKey, 1) == 1; // NEW: defaults to on, matching GameTester's original default
        bool savedReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;

        _AutoSwitchViewToggle.SetIsOnWithoutNotify(savedAutoSwitchView);
        _ReduceMotionToggle.SetIsOnWithoutNotify(savedReduceMotion);
        _UsernameField.SetTextWithoutNotify(PlayerPrefs.GetString(UsernameKey, "")); // NEW: shows whatever name was already saved (from here or from the first-launch screen)

        if (_GameTester != null)
        {
            _GameTester.SetAutoSwitchView(savedAutoSwitchView);
        }
    }

    public void SetUsername(string value) // CHANGED: wire the Username field's OnValueChanged to this now (not OnEndEdit) - saves live as the player types, instead of waiting for them to click away or press Enter
    {
        string sanitized = SanitizeUsername(value);
        if (sanitized.Length == 0)
        {
            return; // NEW: never overwrite a real saved name with a blank one - while the player is still mid-clearing/retyping, the old name just stays saved until a valid one replaces it
        }

        PlayerPrefs.SetString(UsernameKey, sanitized);
        PlayerPrefs.Save();
    }

    public static string SanitizeUsername(string raw) // NEW: shared by both username entry points (this Settings field and UsernameInputScreen's first-launch field) - strips anything that isn't a letter or digit and caps the length, as a code-side backstop alongside each field's own Content Type = Alphanumeric + Character Limit Inspector settings (which are what actually stop the player from typing a bad character in the first place)
    {
        string lettersAndDigitsOnly = System.Text.RegularExpressions.Regex.Replace(raw, "[^a-zA-Z0-9]", "");
        return lettersAndDigitsOnly.Length > MaxUsernameLength
            ? lettersAndDigitsOnly.Substring(0, MaxUsernameLength)
            : lettersAndDigitsOnly;
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
