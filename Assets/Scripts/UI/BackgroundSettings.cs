using UnityEngine;
using UnityEngine.UI;

public class BackgroundSettings : MonoBehaviour
{
    private const string PrefsKey = "SelectedBackgroundIndex"; // NEW: PlayerPrefs key, shared between here and any future menu that needs to know the current pick

    public Image _TableBG; // drag the TableBG Image component here (can be this same GameObject's Image, or a different one if you move things around later)

    [Header("Add one Sprite per wood texture you make - index 0 is the default")]
    public Sprite[] _BackgroundOptions;

    [Header("NEW: one RectTransform per swatch button, SAME ORDER as the Sprites above - used to position the selection frame")]
    public RectTransform[] _OptionButtonRects;

    [Header("NEW: a single reusable border/frame Image (with a BorderFrameController on it), parented anywhere under the grid - it gets moved on top of whichever swatch is selected")]
    public BorderFrameController _SelectionHighlight;

    void Awake()
    {
        int savedIndex = PlayerPrefs.GetInt(PrefsKey, 0); // defaults to 0 (your current texture) the very first time the game ever runs
        ApplyBackground(savedIndex);
    }

    public void SelectBackground(int index) // NEW: called by each thumbnail button in the Background tab
    {
        ApplyBackground(index);
        PlayerPrefs.SetInt(PrefsKey, index);
        PlayerPrefs.Save();
    }

    private void ApplyBackground(int index)
    {
        if (_BackgroundOptions == null || _BackgroundOptions.Length == 0)
        {
            return; // no options assigned yet - nothing to do
        }

        index = Mathf.Clamp(index, 0, _BackgroundOptions.Length - 1); // guards against a saved index that no longer exists (e.g. you removed an option)
        _TableBG.sprite = _BackgroundOptions[index];
        MoveHighlightToButton(index);
    }

    private void MoveHighlightToButton(int index) // NEW: snaps the reusable border frame onto the selected swatch's position and size
    {
        if (_SelectionHighlight == null || _OptionButtonRects == null || index >= _OptionButtonRects.Length || _OptionButtonRects[index] == null)
        {
            return; // not wired up yet - safe to skip
        }

        _SelectionHighlight.ShowOn(_OptionButtonRects[index]);
    }
}
