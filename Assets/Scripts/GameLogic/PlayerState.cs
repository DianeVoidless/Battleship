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
}
