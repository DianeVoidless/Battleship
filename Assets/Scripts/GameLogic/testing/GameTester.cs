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

        _RedBoardDisplay.ShowBoard(_CurrentGame._PlayerRed);
        _BlueBoardDisplay.ShowBoard(_CurrentGame._PlayerBlue);

        _ViewingAs = PlayerColor.Red;
        RefreshView();
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
        if (_ViewingAs == PlayerColor.Red)
        {
            _HandDisplay.ShowHand(_CurrentGame._PlayerRed._Hand, PlayerColor.Red);
            _RedBoardPanel.anchoredPosition = _NearPos;
            _BlueBoardPanel.anchoredPosition = _FarPos;
        }
        else
        {
            _HandDisplay.ShowHand(_CurrentGame._PlayerBlue._Hand, PlayerColor.Blue);
            _BlueBoardPanel.anchoredPosition = _NearPos;
            _RedBoardPanel.anchoredPosition = _FarPos;
        }
    }

    public GameState GetGame()
    {
        return _CurrentGame;
    }

    public void RefreshBoardsAndHand()
    {
        _RedBoardDisplay.ShowBoard(_CurrentGame._PlayerRed);
        _BlueBoardDisplay.ShowBoard(_CurrentGame._PlayerBlue);
        RefreshView();
    }
}