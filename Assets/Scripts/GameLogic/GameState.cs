using UnityEngine;

public class GameState
{
    public PlayerState _PlayerRed;
    public PlayerState _PlayerBlue;
    public PlayerColor _ActivePlayer;

    public GameState()
    {
        _PlayerRed = new PlayerState(PlayerColor.Red);
        _PlayerBlue = new PlayerState(PlayerColor.Blue);
        _ActivePlayer = PlayerColor.Red;
    }
}
