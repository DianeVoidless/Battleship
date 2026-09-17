using UnityEngine;

public class GameState
{
    public PlayerState _PlayerRed;
    public PlayerState _PlayerBlue;
    public PlayerColor _ActivePlayer;
    public int _MovesRemaining;

    public GameState()
    {
        _PlayerRed = new PlayerState(PlayerColor.Red);
        _PlayerBlue = new PlayerState(PlayerColor.Blue);
        _ActivePlayer = PlayerColor.Red;
        _MovesRemaining = 1;
    }

    public void SpendMove()
    {
        _MovesRemaining--;

        if(_MovesRemaining <= 0)
        {
            SwitchActivePlayer();
        }
    }

    public void AddMoves(int amount)
    {
        _MovesRemaining+=amount;
    }

    public void SwitchActivePlayer()
    {
        PlayerState endingPlayer = (_ActivePlayer == PlayerColor.Red) ? _PlayerRed : _PlayerBlue; // NEW: whoever's turn is ending draws back up before we hand control to the other player
        endingPlayer.DrawUpToHandSize(5); // NEW

        if (_ActivePlayer == PlayerColor.Red)
        {
            _ActivePlayer = PlayerColor.Blue;
        }
        else
        {
            _ActivePlayer = PlayerColor.Red;
        }

        _MovesRemaining = 1;
    }
}
