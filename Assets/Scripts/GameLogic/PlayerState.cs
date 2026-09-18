using System.Collections.Generic;

public class PlayerState
{
    public PlayerColor _Color;
    public List<Card> _Hand = new List<Card>();
    public List<Card> _DrawPile = new List<Card>();
    public List<Card> _DiscardPile = new List<Card>();
    public List<GridCell> _Grid = new List<GridCell>();

    public PlayerState(PlayerColor color)
    {
        _Color = color;
    }

    public void DrawUpToHandSize(int targetSize) // NEW: draws from the draw pile until the hand reaches targetSize, reshuffling the discard pile into the draw pile if it runs out
    {
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
            }

            Card drawnCard = _DrawPile[0];
            _DrawPile.RemoveAt(0);
            _Hand.Add(drawnCard);
        }
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

    public List<GridCell> GetDamagedShipCells() // NEW: every one of this player's own ship cells that's taken at least one hit - same rule the Heal card branch already uses (IsLegalTarget)
    {
        List<GridCell> damaged = new List<GridCell>();
        foreach (GridCell cell in _Grid)
        {
            if (cell._Revealed && cell._DamageInstances.Count > 0)
            {
                damaged.Add(cell);
            }
        }
        return damaged;
    }
}
