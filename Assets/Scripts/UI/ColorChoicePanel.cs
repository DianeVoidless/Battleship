using UnityEngine;
using UnityEngine.UI; // NEW: for the crown Image icons
using TMPro;

public class ColorChoicePanel : MonoBehaviour // NEW: the color-switch button on CreateMatchScene - LOCAL-ONLY for now, since there's no networking yet to actually reserve/sync a color between two real clients. Just tracks which color the local player currently intends to play as, and updates the two name bars to show the local username under whichever color is currently picked (the other bar shows a placeholder until a real opponent can actually connect). Exposes LocalPlayerColor so a future match-start/networking script can read the final choice.
{
    [Header("Drag the two name-bar text labels from the Players panel here (the ones showing 'Blue Player Name' / 'Red Player Name')")]
    public TMP_Text _RedBarText;
    public TMP_Text _BlueBarText;

    [Header("Shown in whichever bar ISN'T the local player right now - there's no real opponent to show yet")]
    public string _WaitingForOpponentText = "Waiting for opponent...";

    [Header("True when THIS copy of the panel belongs to whoever created the room (always true for now, since there's no networking yet to tell a host's client from a joiner's) - shows a crown icon next to the local player's name")]
    public bool _IsHost = true;

    [Header("Drag two small crown Image icons here - one sitting in the Red bar, one in the Blue bar. CHANGED: a text-emoji prefix was tried first, but TextMeshPro's font doesn't have a crown glyph baked in (showed as an empty box) - an actual icon image always renders correctly regardless of font")]
    public Image _RedCrownIcon;
    public Image _BlueCrownIcon;

    [Header("The name text's anchored X position while its bar IS showing the crown - it scoots over to make room for the icon, then returns to its normal spot once the crown moves to the other bar")]
    public float _NameTextXWhenCrowned = 40f;

    private bool _LocalPlayerIsRed; // NEW: false = local player is Blue (the default a fresh host starts as) - flips every time the button is clicked

    // NEW: each bar's own normal (uncrowned) X position, cached once the first time RefreshBars runs -
    // same pattern CardDisplay uses for its shield badges, so repeated refreshes always measure from
    // the true original spot instead of from wherever a previous refresh happened to leave it.
    private float _RedBarTextRestX;
    private float _BlueBarTextRestX;
    private bool _RestPositionsCached;

    public PlayerColor LocalPlayerColor => _LocalPlayerIsRed ? PlayerColor.Red : PlayerColor.Blue; // NEW: read this from wherever a match actually gets started, once that exists

    void OnEnable() // CHANGED from Awake/Start - this panel gets shown and hidden repeatedly (open lobby, back out, open again), and the displayed name should always reflect whatever's currently saved, not just whatever it was the first time this object woke up
    {
        RefreshBars();
    }

    public void ToggleColor() // wire this as the color-switch button's OnClick
    {
        _LocalPlayerIsRed = !_LocalPlayerIsRed;
        RefreshBars();
    }

    private void RefreshBars()
    {
        if (!_RestPositionsCached) // NEW: capture each bar's normal X exactly once, before this method ever moves it
        {
            _RedBarTextRestX = _RedBarText.rectTransform.anchoredPosition.x;
            _BlueBarTextRestX = _BlueBarText.rectTransform.anchoredPosition.x;
            _RestPositionsCached = true;
        }

        string localName = PlayerPrefs.GetString(GameplaySettings.UsernameKey, "Player"); // NEW: same shared key as the username screen and Settings tab

        _RedBarText.text = _LocalPlayerIsRed ? localName : _WaitingForOpponentText;
        _BlueBarText.text = _LocalPlayerIsRed ? _WaitingForOpponentText : localName;

        // NEW: the crown follows the LOCAL PLAYER, not a fixed color slot - since ToggleColor can
        // swap which bar the local player is shown in, the crown icon (and the text scooting over
        // to make room for it) has to move with them too, not stay pinned to (say) the Red bar
        // regardless of who's actually shown there.
        bool showCrownOnRed = _IsHost && _LocalPlayerIsRed;
        bool showCrownOnBlue = _IsHost && !_LocalPlayerIsRed;

        if (_RedCrownIcon != null)
        {
            _RedCrownIcon.enabled = showCrownOnRed;
        }
        if (_BlueCrownIcon != null)
        {
            _BlueCrownIcon.enabled = showCrownOnBlue;
        }

        SetTextX(_RedBarText, showCrownOnRed ? _NameTextXWhenCrowned : _RedBarTextRestX);
        SetTextX(_BlueBarText, showCrownOnBlue ? _NameTextXWhenCrowned : _BlueBarTextRestX);
    }

    private void SetTextX(TMP_Text text, float x) // NEW: nudges just the X of a bar's name text, leaving Y (and everything else about its RectTransform) untouched
    {
        Vector2 pos = text.rectTransform.anchoredPosition;
        pos.x = x;
        text.rectTransform.anchoredPosition = pos;
    }
}
