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
}
