using UnityEngine;

public class SettingsNavigation : MonoBehaviour // NEW: shared "open settings from here, go back to here" logic so both the Main Menu's Settings button and the in-game Settings button return you to wherever you actually opened it from, instead of Settings always hardcoding a trip back to the Main Menu
{
    public GameObject _SettingsScene;

    private GameObject _ReturnTarget; // whichever screen was showing right before Settings was opened

    public void OpenSettings(GameObject cameFrom) // wire this as a Static Parameter call on each entry point's OnClick, passing that entry point's own screen GameObject (MainMenuScene or InGame)
    {
        _ReturnTarget = cameFrom;
        if (cameFrom != null)
        {
            cameFrom.SetActive(false);
        }
        _SettingsScene.SetActive(true);
    }

    public void GoBack() // wire this as SettingsScene's BackButton OnClick, replacing its old hardcoded double SetActive calls
    {
        _SettingsScene.SetActive(false);
        if (_ReturnTarget != null)
        {
            _ReturnTarget.SetActive(true);
        }
    }
}
