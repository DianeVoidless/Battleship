using UnityEngine;

public class GameTester:MonoBehaviour 
{
    public BoardDisplay _RedBoardDisplay;
    public BoardDisplay _BlueBoardDisplay;
    public HandDisplay _HandDisplay;

    private GameState _CurrentGame;
    private PlayerColor _ViewingAs = PlayerColor.Red;
    public void BeginMatch()
    {
        _CurrentGame = GameSetup.StartNewGame();

        Debug.Log("Starting player: " + _CurrentGame._ActivePlayer);
        Debug.Log("Red hand size " + _CurrentGame._PlayerRed._Hand.Count);
        Debug.Log("Blue hand size " + _CurrentGame._PlayerBlue._Hand.Count);

        _RedBoardDisplay.ShowBoard(_CurrentGame._PlayerRed);
        _BlueBoardDisplay.ShowBoard(_CurrentGame._PlayerBlue);

        _ViewingAs = PlayerColor.Red;
        RefreshHandView();
    }    

    public void ToggleView()
    {
        if(_ViewingAs == PlayerColor.Red)
        {
            _ViewingAs = PlayerColor.Blue;
        }
        else
        {
            _ViewingAs = PlayerColor.Red;
        }
        RefreshHandView();
    }

    private void RefreshHandView()
    {
        if(_ViewingAs == PlayerColor.Red)
        {
            _HandDisplay.ShowHand(_CurrentGame._PlayerRed._Hand, PlayerColor.Blue);
        }
        else
        {
            _HandDisplay.ShowHand(_CurrentGame._PlayerBlue._Hand, PlayerColor.Blue);
        }
    }
}
