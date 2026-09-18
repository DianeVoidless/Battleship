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

    [SerializeField] private bool _AutoSwitchView = true;

    public void SyncViewToActivePlayer()
    {
        if (!_AutoSwitchView)
        {
            return;
        }

        _ViewingAs = _CurrentGame._ActivePlayer;
    }

    void Awake()
    {
        _RedBoardPanel = _RedBoardDisplay.GetComponent<RectTransform>();
        _BlueBoardPanel = _BlueBoardDisplay.GetComponent<RectTransform>();

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

        Debug.Log("Starting player: " + _CurrentGame._ActivePlayer);
        Debug.Log("Red hand size " + _CurrentGame._PlayerRed._Hand.Count);
        Debug.Log("Blue hand size " + _CurrentGame._PlayerBlue._Hand.Count);

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
    }

    public GameState GetGame()
    {
        return _CurrentGame;
    }

    public void RefreshBoardsAndHand()
    {
        RefreshView(); // CHANGED: RefreshView already redraws both boards and the hand together now
    }
}