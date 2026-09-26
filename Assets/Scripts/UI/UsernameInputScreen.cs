using UnityEngine;
using TMPro;

public class UsernameInputScreen : MonoBehaviour // NEW: the first-launch-only "pick a username" screen - AppLaunchFlow decides whether this or MainMenuScene is what actually shows when the game starts, this script only handles what happens once the player actually confirms a name here
{
    [Header("Drag the 'Your username...' input field from this screen here")]
    public TMP_InputField _UsernameField;

    [Header("Drag this screen's own root GameObject, and MainMenuScene's root, here")]
    public GameObject _Self;
    public GameObject _MainMenuScene;

    public void ConfirmUsername() // wire this as the green arrow button's OnClick
    {
        string sanitized = GameplaySettings.SanitizeUsername(_UsernameField.text); // CHANGED: same letters/digits-only, 15-character-max sanitizing as the Settings screen's field, via the shared helper - keeps both entry points consistent from one place
        if (sanitized.Length == 0)
        {
            return; // NEW: an empty name can't be confirmed - the arrow simply does nothing until something's actually typed, no need for a separate error message for something this obvious
        }

        PlayerPrefs.SetString(GameplaySettings.UsernameKey, sanitized); // CHANGED: shares the same PlayerPrefs key as the Settings screen's own username field, so whichever one last wrote it is always what the other reads
        PlayerPrefs.Save();

        _Self.SetActive(false);
        _MainMenuScene.SetActive(true);
    }
}
