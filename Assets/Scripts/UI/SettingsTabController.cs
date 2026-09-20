using UnityEngine;
using UnityEngine.UI;

public class SettingsTabController : MonoBehaviour
{
    [Header("Drag the 4 tab CONTENT panels here, in the same order as the buttons below")]
    public GameObject[] _TabPanels; // e.g. [0]=BackgroundTab, [1]=SoundTab, [2]=VideoTab, [3]=GameplayTab

    [Header("Drag the 4 tab BUTTONS here, same order as above")]
    public Button[] _TabButtons; // used to show which tab is currently selected

    void Start()
    {
        ShowTab(0); // default to the first tab (Background) when the Settings screen opens
    }

    public void ShowTab(int index)
    {
        for (int i = 0; i < _TabPanels.Length; i++)
        {
            _TabPanels[i].SetActive(i == index);
        }

        // Simple "selected" look: disable the active tab's button (so it visually appears pressed/greyed
        // via its own Disabled Color) and make sure every other tab button is clickable.
        for (int i = 0; i < _TabButtons.Length; i++)
        {
            _TabButtons[i].interactable = (i != index);
        }
    }
}
