using UnityEngine;

public class AppLaunchFlow : MonoBehaviour // NEW: decides which single top-level panel is showing the instant the game actually launches - UsernameInputScene the very first time ever, MainMenuScene every time after. Also forces every OTHER top-level panel off first, so whatever state the Editor happened to leave the scene saved in (e.g. CreateMatchScene left active while you were building it) never leaks into a real play session.
{
    [Header("Drag every top-level scene/panel under Canvas here (the ones GameChoiceScene, CreateMatchScene, etc. sit alongside)")]
    public GameObject _UsernameInputScene;
    public GameObject _MainMenuScene;
    public GameObject _GameChoiceScene;
    public GameObject _CreateMatchScene;
    public GameObject _JoinMatchScene;
    public GameObject _SettingsScene;
    public GameObject _InGame;

    void Awake() // CHANGED to Awake rather than Start - runs before anything else has a chance to render even a single frame of whatever panel the scene happened to be saved with active
    {
        _UsernameInputScene.SetActive(false);
        _MainMenuScene.SetActive(false);
        _GameChoiceScene.SetActive(false);
        _CreateMatchScene.SetActive(false);
        _JoinMatchScene.SetActive(false);
        _SettingsScene.SetActive(false);
        _InGame.SetActive(false);

        bool hasUsername = PlayerPrefs.HasKey(GameplaySettings.UsernameKey) && PlayerPrefs.GetString(GameplaySettings.UsernameKey).Trim().Length > 0; // NEW: the ONLY thing that decides "first ever launch" - a saved, non-blank username. No separate "have I launched before" flag needed, since the two questions are really the same one here.

        if (hasUsername)
        {
            _MainMenuScene.SetActive(true);
        }
        else
        {
            _UsernameInputScene.SetActive(true);
        }
    }
}
