using UnityEngine;

public class GameTester : MonoBehaviour
{
    public BoardDisplay _RedBoardDisplay;
    public BoardDisplay _BlueBoardDisplay;
    public HandDisplay _HandDisplay;

    private RectTransform _RedBoardPanel;
    private RectTransform _BlueBoardPanel;

    private GameState _CurrentGame;
    private PlayerColor _ViewingAs = PlayerColor.Red;

    private Vector2 _NearPos;
    private Vector2 _FarPos;

    public RectTransform _RedPileGroup;  // NEW
    public RectTransform _BluePileGroup; // NEW
    public CapturedShipsPileDisplay _RedCapturedPile; // NEW
    public CapturedShipsPileDisplay _BlueCapturedPile; // NEW
    public GameObject _HealerChoicePrompt; // NEW: the "Select a Ship to heal" banner, shown while _AwaitingHealerChoice is true
    public GameObject _WinScreen;  // NEW
    public GameObject _LoseScreen; // NEW
    public GameObject _TopMenuZoneObject; // CHANGED: plain GameObject reference instead of the component type directly, to work around the Inspector refusing the typed field
    private TopMenuProximityZone _TopMenuZone; // NEW: the actual component, fetched in code instead of dragged in the Inspector
    private bool _RematchPending; // NEW: true while waiting on the opposing player to confirm a rematch
    private PlayerColor _RematchRequestedBy; // NEW: who clicked Play Again
    public GameObject _RematchWaitScreen;    // NEW: "Awaiting opponent confirmation..." - shown to whoever requested it
    public GameObject _RematchConfirmPrompt; // NEW: "Your opponent is requesting a rematch" - shown to the other player
    public GameObject _InputBlocker; // NEW: full-screen invisible raycast blocker - active whenever the board shouldn't be clickable (rematch pending or game over)
    public GameObject _InGameRoot;   // NEW: dragged to the "InGame" object - deactivated when a post-match rematch request gets declined
    public GameObject _MainMenuRoot; // NEW: dragged to "MainMenuScene" - activated in that same case
    [SerializeField] private bool _AutoSwitchView = true;

    public void SyncViewToActivePlayer()
    {
        if (!_AutoSwitchView)
        {
            return;
        }

        _ViewingAs = _CurrentGame._ActivePlayer;
    }

    public void SetAutoSwitchView(bool value) // NEW: lets GameplaySettings update this live when the Gameplay tab's toggle changes, not just at launch
    {
        _AutoSwitchView = value;
    }

    void Awake()
    {
        _AutoSwitchView = PlayerPrefs.GetInt(GameplaySettings.AutoSwitchViewKey, _AutoSwitchView ? 1 : 0) == 1; // NEW: restores the saved setting on launch, falling back to this field's own Inspector default the very first time

        _RedBoardPanel = _RedBoardDisplay.GetComponent<RectTransform>();
        _BlueBoardPanel = _BlueBoardDisplay.GetComponent<RectTransform>();

        _TopMenuZone = _TopMenuZoneObject.GetComponent<TopMenuProximityZone>(); // NEW

        Vector2 redPos = _RedBoardPanel.anchoredPosition;
        Vector2 bluePos = _BlueBoardPanel.anchoredPosition;

        if (redPos.y < bluePos.y)
        {
            _NearPos = redPos;
            _FarPos = bluePos;
        }
        else
        {
            _NearPos = bluePos;
            _FarPos = redPos;
        }

    }

    public void BeginMatch()
    {
        _CurrentGame = GameSetup.StartNewGame();

        _ViewingAs = _CurrentGame._ActivePlayer; // CHANGED: start viewing whoever actually goes first, instead of always defaulting to Red
        RefreshView(); // CHANGED: RefreshView now draws both boards too (with correct rotation), so the separate ShowBoard calls that used to be here aren't needed
    }

    public void ToggleView()
    {
        if (_ViewingAs == PlayerColor.Red)
        {
            _ViewingAs = PlayerColor.Blue;
        }
        else
        {
            _ViewingAs = PlayerColor.Red;
        }
        RefreshView();
    }

    private void RefreshView()
    {
        bool viewingRed = _ViewingAs == PlayerColor.Red;
        _RedBoardDisplay.ShowBoard(_CurrentGame._PlayerRed, !viewingRed);
        _BlueBoardDisplay.ShowBoard(_CurrentGame._PlayerBlue, viewingRed);
        if (viewingRed)
        {
            _RedPileGroup.localEulerAngles = new Vector3(0, 0, 0f);
            _BluePileGroup.localEulerAngles = new Vector3(0, 0, -180f);
        }
        else
        {
            _RedPileGroup.localEulerAngles = new Vector3(0, 0, -180f);
            _BluePileGroup.localEulerAngles = new Vector3(0, 0, 0f);
        }
        if (viewingRed)
        {
            _HandDisplay.ShowHand(_CurrentGame._PlayerRed);
            _RedBoardPanel.anchoredPosition = _NearPos;
            _BlueBoardPanel.anchoredPosition = _FarPos;
        }
        else
        {
            _HandDisplay.ShowHand(_CurrentGame._PlayerBlue);
            _BlueBoardPanel.anchoredPosition = _NearPos;
            _RedBoardPanel.anchoredPosition = _FarPos;
        }
        _RedCapturedPile.Refresh(_CurrentGame._PlayerRed._CapturedShipCount); // NEW
        _BlueCapturedPile.Refresh(_CurrentGame._PlayerBlue._CapturedShipCount); // NEW
        _HealerChoicePrompt.SetActive(_CurrentGame._AwaitingHealerChoice); // NEW: shows/hides the banner based on whether the active player's Healer is waiting for a pick

        if (_InputBlocker != null) // NEW: block board clicks the whole time a rematch decision is pending or the match has ended, so the underlying game can't be played mid-prompt
        {
            _InputBlocker.SetActive(_RematchPending || _CurrentGame._IsGameOver);
        }

        if (_RematchPending) // CHANGED: checked first now, independent of _IsGameOver - a rematch can be requested mid-match (e.g. from the top menu's Restart Match button), not just from the Win/Lose screens
        {
            bool viewingRequester = _ViewingAs == _RematchRequestedBy;
            _WinScreen.SetActive(false);
            _LoseScreen.SetActive(false);
            _RematchWaitScreen.SetActive(viewingRequester);
            _RematchConfirmPrompt.SetActive(!viewingRequester);
            _TopMenuZone.DisableMenu();
        }
        else if (_CurrentGame._IsGameOver)
        {
            bool viewingWinner = _ViewingAs == _CurrentGame._WinningPlayer;
            _WinScreen.SetActive(viewingWinner);
            _LoseScreen.SetActive(!viewingWinner);
            _RematchWaitScreen.SetActive(false);
            _RematchConfirmPrompt.SetActive(false);
            _TopMenuZone.DisableMenu(); // NEW
        }
        else
        {
            _WinScreen.SetActive(false);
            _LoseScreen.SetActive(false);
            _RematchWaitScreen.SetActive(false); // NEW
            _RematchConfirmPrompt.SetActive(false); // NEW
            _TopMenuZone.EnableMenu(); // NEW
        }
    }

    public GameState GetGame()
    {
        return _CurrentGame;
    }

    public void RefreshBoardsAndHand()
    {
        RefreshView(); // CHANGED: RefreshView already redraws both boards and the hand together now
    }

    public void RequestRematch() // NEW: called by "Play Again" on either the Win or Lose screen
    {
        _RematchPending = true;
        _RematchRequestedBy = _ViewingAs;
        _ViewingAs = (_ViewingAs == PlayerColor.Red) ? PlayerColor.Blue : PlayerColor.Red; // pass the screen to the other player so they see the confirmation prompt
        RefreshView();
    }

    public void ConfirmRematch() // NEW: opponent agreed - start a fresh match
    {
        AudioManager.Instance?.PlayConfirmSFX(); // NEW
        _RematchPending = false;
        BeginMatch();
    }

    public void DeclineRematch() // NEW: opponent said no - go back to the original result
    {
        AudioManager.Instance?.PlayDeclineSFX(); // NEW
        _RematchPending = false;
        _ViewingAs = _RematchRequestedBy;

        if (_CurrentGame._IsGameOver) // CHANGED: the match had already ended when the rematch was requested (from the Win/Lose screen), so there's no match to return to - send the requester to the main menu instead
        {
            if (_InGameRoot != null) _InGameRoot.SetActive(false);
            if (_MainMenuRoot != null) _MainMenuRoot.SetActive(true);
        }
        else // a rematch requested mid-match (e.g. the top menu's Restart Match button) - just resume where things left off
        {
            RefreshView();
        }
    }
}