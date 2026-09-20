using UnityEngine;

public class AppQuit : MonoBehaviour // NEW: wire to the Exit button's OnClick
{
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // lets you actually test the button while in the Editor - Application.Quit() alone does nothing there
#else
        Application.Quit();
#endif
    }
}
