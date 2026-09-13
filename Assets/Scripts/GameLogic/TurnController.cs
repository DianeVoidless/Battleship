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

        if (_PendingCard != null && _PendingCard.GetTargetMode() == CardTargetMode.BranchChoice && card != _PendingCard) // NEW: clicking a different card cancels a pinned wildcard
        {
            _HandProximityZone.UnpinHand();
            _HandDisplay.UnpinAllCards();
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
            _PendingCard = card;
            _HandProximityZone.PinHand(); 
            _HandDisplay.PinCard(card); 
            Debug.Log("Selected wildcard - waiting for a branch choice");
            return;
        }

        _PendingCard = card;
        _HandProximityZone.ForceLowerHand(); 
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
        game.AddMoves(_PendingCard.GetBonusMoves());
        game.SpendMove();

        _GameTester.SyncViewToActivePlayer();

        activePlayer._Hand.Remove(_PendingCard);
        activePlayer._DiscardPile.Add(_PendingCard);

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

        _HandProximityZone.UnpinHand();
        _HandProximityZone.ReleaseForceLowerHand();
        _HandDisplay.UnpinAllCards();
        _PendingCard = null;

        Debug.Log("Cancelled - clicked outside");
    }
}