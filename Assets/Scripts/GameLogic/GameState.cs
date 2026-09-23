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
    public bool _LastDrawReshuffled; // NEW: mirrors endingPlayer._LastDrawReshuffled right after SwitchActivePlayer's draw - read alongside _LastDrawnCards so the caller knows whether to play the discard-pile reshuffle merge before the draw-up animation

    public PlayerState SpendMove() // CHANGED: returns the player whose turn just ended (and whose hand was just redrawn), or null if this move didn't end the turn - lets callers animate the draw-up before refreshing the view
    {
        _MovesRemaining--;

        if(_MovesRemaining <= 0 || ShouldForcePass()) // CHANGED: also ends the turn early if the active player is left with moves but nothing left in hand they can actually play (e.g. a hand of nothing but Shield cards with no revealed own ship to target) - otherwise the match would just sit there waiting on an action that can never come
        {
            return SwitchActivePlayer();
        }
        return null;
    }

    public PlayerState PassIfStuck() // NEW: same soft-lock safety net as SpendMove, for the one path that doesn't go through it - right after the turn-start Healer's forced choice resolves. If the active player now has zero legal plays left in hand, end their turn immediately instead of leaving them stuck.
    {
        if (ShouldForcePass())
        {
            _MovesRemaining = 0;
            return SwitchActivePlayer();
        }
        return null;
    }

    private bool ShouldForcePass() // NEW: true only once any mandatory action elsewhere (the Healer's forced choice) is out of the way, and the active player's hand genuinely has nothing left they could legally play
    {
        return !_AwaitingHealerChoice && !ActivePlayerHasLegalPlay();
    }

    public bool ActivePlayerHasLegalPlay() // NEW: true if the active player has at least one card in hand that could actually be played right now, given the current board state
    {
        PlayerState activePlayer = (_ActivePlayer == PlayerColor.Red) ? _PlayerRed : _PlayerBlue;
        PlayerState opponent = (_ActivePlayer == PlayerColor.Red) ? _PlayerBlue : _PlayerRed;

        foreach (Card card in activePlayer._Hand)
        {
            if (CardHasLegalPlay(card, activePlayer, opponent))
            {
                return true;
            }
        }
        return false;
    }

    private bool CardHasLegalPlay(Card card, PlayerState activePlayer, PlayerState opponent) // NEW
    {
        if (card is UtilityCard utilityCard && utilityCard._Type != UtilityType.Shield)
        {
            // a non-Shield wildcard always has a no-target branch (Draw3 or ExtraPlay) with no
            // precondition of its own, so it's always playable no matter the board state
            return true;
        }

        CardTargetMode mode = card.GetTargetMode(); // an AttackCard (EnemyCell) or a Shield card (OwnCell) at this point

        if (mode == CardTargetMode.None)
        {
            return true;
        }

        if (mode == CardTargetMode.OwnCell)
        {
            foreach (GridCell cell in activePlayer._Grid)
            {
                if (card.IsLegalTarget(cell))
                {
                    return true;
                }
            }
            return false;
        }

        if (mode == CardTargetMode.EnemyCell)
        {
            foreach (GridCell cell in opponent._Grid)
            {
                if (card.IsLegalTarget(cell))
                {
                    return true;
                }
            }
            return false;
        }

        return false; // safety default - BranchChoice/HandMultiSelect never actually reach here (only a Shield card takes the mode path above, and it's always OwnCell)
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
        _LastDrawReshuffled = endingPlayer._LastDrawReshuffled; // NEW: mirrors the draw that just happened, so GameTester knows whether to play the reshuffle merge first

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

        if (ShouldForcePass()) // NEW: the player who just received the turn already has zero legal plays in hand (and no mandatory Healer choice to resolve first) - skip them right back instead of handing control to a turn nobody can act on
        {
            return SwitchActivePlayer();
        }

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
