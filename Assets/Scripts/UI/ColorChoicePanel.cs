using Unity.Netcode; // NEW: for NetworkManager.Singleton.IsHost, and now also for reading LobbySync's synced NetworkVariables
using UnityEngine;
using UnityEngine.UI; // NEW: for the crown Image icons
using TMPro;

public class ColorChoicePanel : MonoBehaviour // CHANGED: no longer local-only - both the host's chosen color and each player's username now come from LobbySync, the shared networked state, so both screens show the SAME two players instead of each screen only knowing about itself. The color-swap button itself is still host-only (see the HostOnlyButton also on that button) - the joining player never picks their own color, they're just always the opposite of whatever the host picked.
{
    [Header("Drag the two name-bar text labels from the Players panel here (the ones showing 'Blue Player Name' / 'Red Player Name')")]
    public TMP_Text _RedBarText;
    public TMP_Text _BlueBarText;

    [Header("Shown in whichever bar's player hasn't been heard from yet (host not spawned in / client not connected yet)")]
    public string _WaitingForOpponentText = "Waiting for opponent...";

    [Header("Drag two small crown Image icons here - one sitting in the Red bar, one in the Blue bar. CHANGED: a text-emoji prefix was tried first, but TextMeshPro's font doesn't have a crown glyph baked in (showed as an empty box) - an actual icon image always renders correctly regardless of font")]
    public Image _RedCrownIcon;
    public Image _BlueCrownIcon;

    [Header("The name text's anchored X position while its bar IS showing the crown - it scoots over to make room for the icon, then returns to its normal spot once the crown moves to the other bar")]
    public float _NameTextXWhenCrowned = 40f;

    private bool _LocalPlayerIsRed; // NEW: only meaningful for the HOST now - which color the host has currently picked. A joining client never sets this locally; it just reads LobbySync.HostIsRed instead.

    private float _RedBarTextRestX;
    private float _BlueBarTextRestX;
    private bool _RestPositionsCached;

    public PlayerColor LocalPlayerColor => IsLocalPlayerHost
        ? (_LocalPlayerIsRed ? PlayerColor.Red : PlayerColor.Blue)
        : (LobbySync.Instance != null && LobbySync.Instance.HostIsRed.Value ? PlayerColor.Blue : PlayerColor.Red); // CHANGED: a joining client's color is always the opposite of whatever the host picked

    private bool IsLocalPlayerHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

    void OnEnable() // CHANGED: no longer a one-shot check - HostMatch is now async (it awaits a real Relay network call before StartHost() actually runs), so this panel can turn on before hosting has technically started yet. A single check made right here could permanently lock in a wrong snapshot (not host yet, no LobbySync yet) with nothing to ever correct it. Update() below re-checks every frame instead, so whenever hosting/LobbySync/the opponent's data actually becomes available, the very next frame picks it up correctly - same fix already used on HostOnlyButton for the same underlying timing problem.
    {
        RefreshBars();
    }

    void Update()
    {
        RefreshBars();
    }

    public void ToggleColor() // wire this as the color-switch button's OnClick - host-only (see HostOnlyButton on that same button)
    {
        _LocalPlayerIsRed = !_LocalPlayerIsRed;

        if (LobbySync.Instance != null)
        {
            LobbySync.Instance.SetHostIsRed(_LocalPlayerIsRed); // NEW: pushes the host's choice out to the network so the joining client's screen updates too
        }

        RefreshBars();
    }

    private void RefreshBars()
    {
        if (!_RestPositionsCached)
        {
            _RedBarTextRestX = _RedBarText.rectTransform.anchoredPosition.x;
            _BlueBarTextRestX = _BlueBarText.rectTransform.anchoredPosition.x;
            _RestPositionsCached = true;
        }

        string localName = PlayerPrefs.GetString(ClonePrefs.Key(GameplaySettings.UsernameKey), "Player");
        bool isHost = IsLocalPlayerHost;

        // NEW: figure out the HOST's color and each player's display name from the shared LobbySync
        // state rather than only ever knowing about the local player - this is what makes both
        // screens agree on who's who instead of each one just describing itself.
        bool hostIsRed = isHost ? _LocalPlayerIsRed : (LobbySync.Instance != null && LobbySync.Instance.HostIsRed.Value);

        string hostName = isHost
            ? localName
            : (LobbySync.Instance != null && LobbySync.Instance.HostUsername.Value.Length > 0 ? LobbySync.Instance.HostUsername.Value.ToString() : null);

        string clientName = isHost
            ? (LobbySync.Instance != null && LobbySync.Instance.ClientUsername.Value.Length > 0 ? LobbySync.Instance.ClientUsername.Value.ToString() : null)
            : localName;

        string hostDisplay = hostName ?? _WaitingForOpponentText;
        string clientDisplay = clientName ?? _WaitingForOpponentText;

        _RedBarText.text = hostIsRed ? hostDisplay : clientDisplay;
        _BlueBarText.text = hostIsRed ? clientDisplay : hostDisplay;

        // NEW: the crown always marks the HOST's bar now, identically on both screens - not "am I
        // the host and is this my own bar" like before, since both players need to see the SAME
        // answer to "who's hosting", not just their own local guess.
        bool showCrownOnRed = hostIsRed;
        bool showCrownOnBlue = !hostIsRed;

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
