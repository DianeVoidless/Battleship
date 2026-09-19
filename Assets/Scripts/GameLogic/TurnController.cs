using UnityEngine;

public class TurnController : MonoBehaviour
{
    public static TurnController _Instance;
    public GameTester _GameTester;
    public HandProximityZone _HandProximityZone;
    public HandDisplay _HandDisplay;

    private Card _PendingCard;
    private System.Collections.Generic.List<Card> _SelectedHandCards = new System.Collections.Generic.List<Card>(); // NEW: which hand cards the player has picked so far for a Cleanse-style multi-select

    void Awake()
    {
        _Instance = this;
    }

    private void CancelPendingCard() // NEW: shared cleanup for whenever a pending card selection is abandoned (clicking elsewhere, clicking a different card)
    {
        if (_PendingCard != null && _PendingCard.GetTargetMode() == CardTargetMode.HandMultiSelect && _PendingCard is UtilityCard utilityCard)
        {
            utilityCard.ChooseBranch(CardBranch.NotChosen); // NEW: otherwise this exact card instance gets stuck in Cleanse mode forever
        }
        _SelectedHandCards.Clear(); // NEW
        _HandDisplay.UnpinAllCards();
        _HandProximityZone.ReleaseForceLowerHand();
        _HandProximityZone.UnpinHand(); // NEW: release the whole-hand-raised state a Cleanse selection may have set
        _PendingCard = null;
    }

    public void OnCardClicked(CardDisplay display, Card card)
    {
        GameState game = _GameTester.GetGame();
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        if (!activePlayer._Hand.Contains(card))
        {
            Debug.Log("Ignored - not your card to play right now");
            return;
        }

        if (_PendingCard != null && _PendingCard.GetTargetMode() == CardTargetMode.HandMultiSelect) // NEW: mid-Cleanse - clicking hand cards toggles selection; confirming happens by clicking the draw pile instead
        {
            if (card == _PendingCard)
            {
                Debug.Log("Click your draw pile to confirm the cleanse");
                return;
            }

            if (!_PendingCard.IsLegalHandCard(card))
            {
                Debug.Log("Ignored - that card can't be selected for this");
                return;
            }

            if (_SelectedHandCards.Contains(card))
            {
                _SelectedHandCards.Remove(card);
                _HandDisplay.UnpinCard(card);
            }
            else
            {
                _SelectedHandCards.Add(card);
                _HandDisplay.PinCard(card);
            }
            return;
        }

        if (_PendingCard != null && card != _PendingCard)
        {
            CancelPendingCard(); // CHANGED: shared cleanup, so a mid-selection wildcard's branch also gets reset
        }

        CardTargetMode mode = card.GetTargetMode();

        if (mode == CardTargetMode.HandMultiSelect)
        {
            Debug.Log("This card needs a feature we haven't built yet (" + mode + ")");
            return;
        }

        if (mode == CardTargetMode.BranchChoice)
        {
            Debug.Log("Choose one of the two options shown on the card");
            return;
        }

        _PendingCard = card;
        _HandProximityZone.ForceLowerHand();
        _HandDisplay.PinCard(card);
        Debug.Log("Selected " + card.GetType().Name + " - waiting for a " + mode + " target");
    }

    public void OnDrawPileClicked(PlayerColor owner) // NEW: clicking your own draw pile confirms a Cleanse selection in progress
    {
        if (_PendingCard == null || _PendingCard.GetTargetMode() != CardTargetMode.HandMultiSelect)
        {
            return; // not mid-Cleanse - clicking the draw pile does nothing right now
        }

        GameState game = _GameTester.GetGame();

        if (owner != game._ActivePlayer)
        {
            Debug.Log("Ignored - that's not your draw pile");
            return;
        }

        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;
        ConfirmHandMultiSelect((UtilityCard)_PendingCard, activePlayer);
    }

    private void ConfirmHandMultiSelect(UtilityCard card, PlayerState activePlayer) // NEW: player clicked their draw pile to confirm the Cleanse selection
    {
        card.ResolveHandSelection(activePlayer, _SelectedHandCards);

        activePlayer._Hand.Remove(card);
        activePlayer._DiscardPile.Add(card);

        GameState game = _GameTester.GetGame();
        game.AddMoves(card.GetBonusMoves());
        card.OnDiscarded();
        game.SpendMove();

        _GameTester.SyncViewToActivePlayer();

        _HandDisplay.UnpinAllCards();
        _HandProximityZone.ReleaseForceLowerHand();
        _HandProximityZone.UnpinHand(); // NEW: release the whole-hand-raised state Cleanse selection set
        _SelectedHandCards.Clear();
        _PendingCard = null;
        _GameTester.RefreshBoardsAndHand();
    }

    public void OnCellClicked(CardDisplay display, GridCell cell)
    {
        GameState game = _GameTester.GetGame(); // MOVED: needed up here now, for the Healer check below
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue; // MOVED

        if (game._AwaitingHealerChoice) // NEW: the new active player's Healer needs a damaged ship picked before anything else can happen
        {
            bool isOwnDamagedCell = activePlayer._Grid.Contains(cell) && cell._Revealed && cell._DamageInstances.Count > 0;
            if (!isOwnDamagedCell)
            {
                Debug.Log("Ignored - choose one of your own damaged ships to heal");
                return;
            }

            cell.RemoveHighestDamage();
            game._AwaitingHealerChoice = false;
            _GameTester.RefreshBoardsAndHand();
            return;
        }

        if (_PendingCard == null)
        {
            Debug.Log("Ignored - select a hand card first");
            return;
        }

        PlayerState opponent = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerBlue : game._PlayerRed;

        CardTargetMode mode = _PendingCard.GetTargetMode();

        if (mode == CardTargetMode.BranchChoice)
        {
            Debug.Log("Ignored - choose a branch for this wildcard first");
            return;
        }

        bool isOwnCell = activePlayer._Grid.Contains(cell);
        bool isEnemyCell = opponent._Grid.Contains(cell);

        if (mode == CardTargetMode.EnemyCell && !isEnemyCell)
        {
            Debug.Log("Ignored - that's not an enemy cell");
            return;
        }

        if (mode == CardTargetMode.OwnCell && !isOwnCell)
        {
            Debug.Log("Ignored - that's not your own cell");
            return;
        }

        if (!_PendingCard.IsLegalTarget(cell))
        {
            Debug.Log("Ignored - illegal target for this card");
            return;
        }

        bool wasSunkBefore = cell.IsSunk(); // NEW: snapshot so we can tell the exact moment a ship becomes captured

        _PendingCard.Resolve(cell, activePlayer); // CHANGED: now passes the active player as owner

        if (!wasSunkBefore && cell.IsSunk()) // NEW: this attack just brought the ship to 0 HP - credit whoever landed the hit
        {
            activePlayer._CapturedShipCount++;
        }

        if (opponent.AreAllShipsSunk()) // NEW: every enemy ship is gone - the active player just won, stop the match right here
        {
            game._IsGameOver = true;
            game._WinningPlayer = activePlayer._Color;

            activePlayer._Hand.Remove(_PendingCard);
            activePlayer._DiscardPile.Add(_PendingCard);
            _PendingCard.OnDiscarded();

            _PendingCard = null;
            _HandProximityZone.ReleaseForceLowerHand();
            _GameTester.RefreshBoardsAndHand();
            return; // NEW: no more turn processing once the match is decided - don't switch the active player or trigger the next turn's Healer
        }

        if (cell._Ship == ShipType.Carrier && cell._Revealed) // NEW: Carrier's hand-size bonus applies the instant it's revealed, not just at a turn boundary
        {
            PlayerState carrierOwner = isOwnCell ? activePlayer : opponent; // NEW: whoever actually owns this cell, not necessarily whoever just played the card
            int handCap = carrierOwner.HasActiveShip(ShipType.Carrier) ? 7 : 5;
            carrierOwner.DrawUpToHandSize(handCap);
        }

        activePlayer._Hand.Remove(_PendingCard); // MOVED: must happen before SpendMove(), since that can trigger the end-of-turn draw-up-to-5 check
        activePlayer._DiscardPile.Add(_PendingCard); // MOVED

        game.AddMoves(_PendingCard.GetBonusMoves()); // CHANGED: must read GetBonusMoves() before OnDiscarded() resets the chosen branch
        _PendingCard.OnDiscarded(); // NEW: reset any wildcard's chosen branch so it works correctly if drawn again later
        game.SpendMove();

        _GameTester.SyncViewToActivePlayer();

        _PendingCard = null;
        _HandProximityZone.ReleaseForceLowerHand();
        _GameTester.RefreshBoardsAndHand();
    }

    public void OnBackgroundClicked()
    {
        if (_PendingCard == null)
        {
            return;
        }

        CancelPendingCard(); // CHANGED: shared cleanup, so cancelling out of a Cleanse selection also resets its branch

        Debug.Log("Cancelled - clicked outside");
    }

    public void OnWildcardBranchClicked(CardDisplay display, UtilityCard card, bool isGatedZone)
    {
        GameState game = _GameTester.GetGame();
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        if (!activePlayer._Hand.Contains(card))
        {
            Debug.Log("Ignored - not your card to play right now");
            return;
        }

        if (_PendingCard != null && card != _PendingCard)
        {
            CancelPendingCard(); // CHANGED: shared cleanup, so a mid-selection wildcard's branch also gets reset
        }

        CardBranch branch = GetBranchForZone(card._Type, isGatedZone);

        if (isGatedZone && !card.IsBranchAvailable(branch, activePlayer))
        {
            Debug.Log("Ignored - that branch isn't available right now");
            return;
        }

        card.ChooseBranch(branch);

        CardTargetMode mode = card.GetTargetMode();

        if (mode == CardTargetMode.None)
        {
            ResolveNoTargetCard(card, activePlayer);
            return;
        }

        _PendingCard = card;
        _HandDisplay.PinCard(card);

        if (mode == CardTargetMode.HandMultiSelect)
        {
            _SelectedHandCards.Clear(); // NEW: start a fresh selection for Cleanse
            _HandProximityZone.PinHand(); // NEW: keep the whole hand raised while picking cards, instead of forcing it low like a board-target card would
            Debug.Log("Select attack cards to cleanse, then click your draw pile to confirm");
            return;
        }

        _HandProximityZone.ForceLowerHand(); // MOVED: only board/cell-targeting branches should force the hand back down
        Debug.Log("Branch chosen - waiting for a " + mode + " target");
    }

    private CardBranch GetBranchForZone(UtilityType type, bool isGatedZone)
    {
        if (type == UtilityType.CleanseOrExtraPlay)
        {
            return isGatedZone ? CardBranch.Cleanse : CardBranch.ExtraPlay;
        }
        return isGatedZone ? CardBranch.Heal : CardBranch.Draw3;
    }

    private void ResolveNoTargetCard(Card card, PlayerState activePlayer)
    {
        card.ResolveNoTarget(activePlayer);

        activePlayer._Hand.Remove(card); // MOVED: must happen before SpendMove()
        activePlayer._DiscardPile.Add(card); // MOVED

        GameState game = _GameTester.GetGame();
        game.AddMoves(card.GetBonusMoves()); // CHANGED: must read GetBonusMoves() before OnDiscarded() resets the chosen branch
        card.OnDiscarded(); // NEW: reset any wildcard's chosen branch so it works correctly if drawn again later
        game.SpendMove();

        _GameTester.SyncViewToActivePlayer();

        _PendingCard = null;
        _GameTester.RefreshBoardsAndHand();
    }

    public Card GetPendingCard()
    {
        return _PendingCard;
    }
}
