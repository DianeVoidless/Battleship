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
    public bool _IsGameOver;          // NEW: true once one side has lost every ship
    public PlayerColor _WinningPlayer; // NEW: only meaningful once _IsGameOver is true
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

        const int columns = 4; // TEMP: matches BoardDisplay's Fixed Column Count, just for this debug print
        foreach (GridCell c in damaged) // TEMP: full diagnostic dump for the healer HP mismatch
        {
            int index = player._Grid.IndexOf(c);
            int row = index / columns;
            int col = index % columns;
            int maxHP = ShipStats.GetMaxHP(c._Ship);
            string hits = string.Join(", ", c._DamageInstances); // every individual damage amount this cell has ever taken

            Debug.Log("Healer sees: " + c._Ship
                + " at (row " + row + ", col " + col + ")"
                + " - full HP " + maxHP
                + " - damage instances taken: [" + hits + "]"
                + " - total damage " + c.GetTotalDamage()
                + " - remaining " + c.GetRemainingHP());
        }

        if (damaged.Count == 1)
        {
            GridCell healedCell = damaged[0];
            Debug.Log("Healer auto-healing " + healedCell._Ship + " - BEFORE: remaining " + healedCell.GetRemainingHP() + ", instances [" + string.Join(", ", healedCell._DamageInstances) + "]");
            healedCell.RemoveHighestDamage(); // only one possible choice, so it just happens
            Debug.Log("Healer auto-healing " + healedCell._Ship + " - AFTER: remaining " + healedCell.GetRemainingHP() + ", instances [" + string.Join(", ", healedCell._DamageInstances) + "]");
        }
        else if (damaged.Count > 1)
        {
            _AwaitingHealerChoice = true;
            Debug.Log("Healer: choose one of your damaged ships to heal");
        }
    }
}
