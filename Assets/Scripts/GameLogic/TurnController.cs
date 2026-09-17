using UnityEngine;

public class TurnController : MonoBehaviour
{
    public static TurnController _Instance;
    public GameTester _GameTester;
    public HandProximityZone _HandProximityZone;
    public HandDisplay _HandDisplay;

    private Card _PendingCard;

    void Awake()
    {
        _Instance = this;
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

        if (_PendingCard != null && card != _PendingCard) 
        {
            _HandDisplay.UnpinAllCards();
            _HandProximityZone.ReleaseForceLowerHand();
            _PendingCard = null;
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

    public void OnCellClicked(CardDisplay display, GridCell cell)
    {
        if (_PendingCard == null)
        {
            Debug.Log("Ignored - select a hand card first");
            return;
        }

        GameState game = _GameTester.GetGame();
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;
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

        _PendingCard.Resolve(cell);

        activePlayer._Hand.Remove(_PendingCard); // MOVED: must happen before SpendMove(), since that can trigger the end-of-turn draw-up-to-5 check
        activePlayer._DiscardPile.Add(_PendingCard); // MOVED

        game.AddMoves(_PendingCard.GetBonusMoves());
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

        _HandDisplay.UnpinAllCards();
        _HandProximityZone.ReleaseForceLowerHand();
        _PendingCard = null;

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
            _HandDisplay.UnpinAllCards();
            _HandProximityZone.ReleaseForceLowerHand();
            _PendingCard = null;
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

        if (mode == CardTargetMode.HandMultiSelect)
        {
            Debug.Log("This card needs a feature we haven't built yet (" + mode + ")");
            _PendingCard = null;
            return;
        }

        _PendingCard = card;
        _HandProximityZone.ForceLowerHand();
        _HandDisplay.PinCard(card);
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
        game.AddMoves(card.GetBonusMoves());
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