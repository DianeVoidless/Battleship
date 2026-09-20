using System.Collections.Generic;
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

    public List<Card> _LastDrawnCards = new List<Card>(); // NEW: the cards SwitchActivePlayer() most recently drew for whichever player's turn just ended - read this right after SpendMove() returns non-null, before calling it again, so the caller can animate exactly those cards

    public PlayerState SpendMove() // CHANGED: returns the player whose turn just ended (and whose hand was just redrawn), or null if this move didn't end the turn - lets callers animate the draw-up before refreshing the view
    {
        _MovesRemaining--;

        if(_MovesRemaining <= 0)
        {
            return SwitchActivePlayer();
        }
        return null;
    }

    public void AddMoves(int amount)
    {
        _MovesRemaining+=amount;
    }

    public bool _AwaitingHealerChoice; // NEW: true when the player whose turn just started has a Healer and more than one damaged ship, and must click one to heal
    public bool _IsGameOver;          // NEW: true once one side has lost every ship
    public PlayerColor _WinningPlayer; // NEW: only meaningful once _IsGameOver is true
    public PlayerState SwitchActivePlayer() // CHANGED: returns the player whose turn just ended, so callers can animate the cards _LastDrawnCards just added to their hand
    {
        PlayerState endingPlayer = (_ActivePlayer == PlayerColor.Red) ? _PlayerRed : _PlayerBlue;
        int handCap = endingPlayer.HasActiveShip(ShipType.Carrier) ? 7 : 5; // NEW: Carrier raises the draw-back-up-to target
        _LastDrawnCards = endingPlayer.DrawUpToHandSize(handCap, playSound: false); // CHANGED: was hardcoded to 5, the result is now kept for animation purposes, and its own batch sound is skipped - GameTester's turn-end draw animation plays one shove sound per card as each one visually arrives instead

        if (_ActivePlayer == PlayerColor.Red)
        {
            _ActivePlayer = PlayerColor.Blue;
        }
        else
        {
            _ActivePlayer = PlayerColor.Red;
        }

        _MovesRemaining = 1;

        PlayerState newActivePlayer = (_ActivePlayer == PlayerColor.Red) ? _PlayerRed : _PlayerBlue; // NEW
        TriggerHealerAtTurnStart(newActivePlayer); // NEW

        return endingPlayer; // NEW
    }

    private void TriggerHealerAtTurnStart(PlayerState player) // NEW: PatrolBoat is the Healer ship in this game
    {
        if (!player.HasActiveShip(ShipType.PatrolBoat))
        {
            return;
        }

        List<GridCell> damaged = player.GetDamagedShipCells();

        if (damaged.Count == 1)
        {
            GridCell healedCell = damaged[0];
            healedCell.RemoveHighestDamage(); // only one possible choice, so it just happens
        }
        else if (damaged.Count > 1)
        {
            _AwaitingHealerChoice = true;
            Debug.Log("Healer: choose one of your damaged ships to heal");
        }
    }
}
