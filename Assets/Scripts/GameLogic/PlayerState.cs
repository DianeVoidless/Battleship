using System.Collections.Generic;

public class PlayerState
{
    public PlayerColor _Color;
    public List<Card> _Hand = new List<Card>();
    public List<Card> _DrawPile = new List<Card>();
    public List<Card> _DiscardPile = new List<Card>();
    public List<GridCell> _Grid = new List<GridCell>();
    public int _CapturedShipCount; // NEW: how many enemy ships this player has sunk/captured this match

    public PlayerState(PlayerColor color)
    {
        _Color = color;
    }

    public bool _LastDrawReshuffled; // NEW: set true whenever the most recent DrawUpToHandSize call had to reshuffle the discard pile back into the draw pile - read by callers right after the call so they can play a "the discard pile just merged into the draw pile" visual before animating the draw itself

    public List<Card> DrawUpToHandSize(int targetSize, bool playSound = true) // CHANGED: now returns the cards that were actually newly drawn (so callers like the turn-end draw-up animation know exactly what to animate), and takes playSound so callers that animate this draw themselves - one shove sound per card as it visually arrives - can skip this method's own single batch sound instead of layering both
    {
        List<Card> drawnCards = new List<Card>(); // CHANGED: was just a bool - now the actual cards
        _LastDrawReshuffled = false; // NEW: reset at the start of every call - only this call's own reshuffle (if any) should be reported

        while (_Hand.Count < targetSize)
        {
            if (_DrawPile.Count == 0)
            {
                if (_DiscardPile.Count == 0)
                {
                    break; // NEW: nothing left anywhere to draw - stop instead of looping forever
                }

                _DrawPile.AddRange(_DiscardPile);
                _DiscardPile.Clear();
                GameSetup.ShuffleDeck(_DrawPile);
                _LastDrawReshuffled = true; // NEW
            }

            Card drawnCard = _DrawPile[0];
            _DrawPile.RemoveAt(0);
            _Hand.Add(drawnCard);
            drawnCards.Add(drawnCard); // CHANGED
        }

        if (drawnCards.Count > 0 && playSound) // CHANGED: one sound for the whole batch, not one per card - only when nothing else is going to play a sound of its own (the turn-end draw-up animation now plays one shove per card as each visually arrives, so it passes playSound: false here to avoid a redundant extra sound on top)
        {
            AudioManager.Instance?.PlayShoveSFX();
        }

        return drawnCards; // NEW
    }

    public bool HasActiveShip(ShipType type) // CHANGED: a ship's passive only kicks in once it's actually been discovered - matches how "damaged" status already works elsewhere in your game
    {
        foreach (GridCell cell in _Grid)
        {
            if (cell._Ship == type && cell._Revealed && !cell.IsSunk())
            {
                return true;
            }
        }
        return false;
    }

    public List<GridCell> GetDamagedShipCells() // CHANGED: sunk ships no longer count as "damaged" - they're gone, not healable
    {
        List<GridCell> damaged = new List<GridCell>();
        foreach (GridCell cell in _Grid)
        {
            if (cell._Revealed && cell._DamageInstances.Count > 0 && !cell.IsSunk()) // NEW: exclude sunk cells
            {
                damaged.Add(cell);
            }
        }
        return damaged;
    }

    public bool AreAllShipsSunk() // NEW: true once every one of this player's ship cells has been sunk
    {
        foreach (GridCell cell in _Grid)
        {
            if (cell._Ship != ShipType.None && !cell.IsSunk())
            {
                return false;
            }
        }
        return true;
    }

    public void SortHand() // NEW: puts the hand into a fixed, readable order - used right after the initial deal, so the deal flourish (which reads _Hand by index) already shows cards in their final sorted order instead of jumping into place once HandDisplay takes over
    {
        _Hand.Sort((a, b) => GetHandSortKey(a).CompareTo(GetHandSortKey(b)));
    }

    public static int GetHandSortKey(Card card) // NEW: lower sorts first - White missiles, then Red missiles (grouped, then sorted by damage ascending within the group), then the Cleanse/ExtraPlay wildcard, then the Heal/Draw3 wildcard, then Shield. Public/static so HandDisplay can use the exact same ordering when it re-sorts a display copy of the hand on every render, without duplicating this rule in two places.
    {
        if (card is AttackCard attackCard)
        {
            if (attackCard._Color == TargetColor.White)
            {
                return 0;
            }
            return 1000 + attackCard._Damage; // red missiles - grouped after white, sorted by damage ascending within the group
        }

        if (card is UtilityCard utilityCard)
        {
            if (utilityCard._Type == UtilityType.CleanseOrExtraPlay) return 2000;
            if (utilityCard._Type == UtilityType.HealOrDraw3) return 3000;
            if (utilityCard._Type == UtilityType.Shield) return 4000;
        }

        return 5000; // safety fallback - shouldn't happen given the current card types
    }
}
