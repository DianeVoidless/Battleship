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

    public bool _AwaitingHealerChoice; // NEW: true when the player whose turn just started has a Healer and more than one damaged ship, and must click one to heal

    public void SwitchActivePlayer()
    {
        PlayerState endingPlayer = (_ActivePlayer == PlayerColor.Red) ? _PlayerRed : _PlayerBlue;
        int handCap = endingPlayer.HasActiveShip(ShipType.Carrier) ? 7 : 5; // NEW: Carrier raises the draw-back-up-to target
        endingPlayer.DrawUpToHandSize(handCap); // CHANGED: was hardcoded to 5

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
            damaged[0].RemoveHighestDamage(); // only one possible choice, so it just happens
        }
        else if (damaged.Count > 1)
        {
            _AwaitingHealerChoice = true;
            Debug.Log("Healer: choose one of your damaged ships to heal");
        }
    }
}
