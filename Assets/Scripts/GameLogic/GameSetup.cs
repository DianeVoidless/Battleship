using System.Collections.Generic;

public static class GameSetup
{
    private static System.Random _SharedRng; // NEW: shared across every shuffle/coin-flip in a match - see SeedSharedRng. Both players seed this with the SAME number at match start, and since neither player ever calls System.Random anywhere else in the actual gameplay code, giving every shuffle (including a mid-match reshuffle when a draw pile runs dry) the SAME seeded sequence keeps both machines' decks - and therefore both boards - dealt identically forever, as long as both sides process the same moves in the same order.

    public static void SeedSharedRng(int seed) // NEW: call this ONCE, with the same seed on both machines, before BeginMatch() builds anything - see GameTester.BeginMatch
    {
        _SharedRng = new System.Random(seed);
    }

    public static List<GridCell> BuildBoardDeck()
    {
        List<GridCell> deck = new List<GridCell>();

        deck.Add(new GridCell(ShipType.Carrier));
        deck.Add(new GridCell(ShipType.Cruiser));
        deck.Add(new GridCell(ShipType.Destroyer));
        deck.Add(new GridCell(ShipType.Submarine));
        deck.Add(new GridCell(ShipType.PatrolBoat));

        for (int i = 0; i < 7; i++)
        {
            deck.Add(new GridCell(ShipType.None));
        }
        return deck;
    }


    public static void ShuffleDeck<T>(List<T> deck)
    {
        if (_SharedRng == null) // CHANGED: fall back to a fresh, wall-clock-seeded Random if nobody ever called SeedSharedRng - keeps this working exactly as before for local/offline testing that never goes through the networked match flow
        {
            _SharedRng = new System.Random();
        }

        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = _SharedRng.Next(i+1);

            T temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }
    
    public static List<Card> BuildCardDeck()
    {
        List<Card> deck = new List<Card>();

        for(int i=0; i < 2; i++)
        {
            deck.Add(new UtilityCard(UtilityType.Shield));
        }

        for (int i = 0; i < 2; i++)
        {
            deck.Add(new UtilityCard(UtilityType.HealOrDraw3));
        }

        for (int i = 0; i < 3; i++)
        {
            deck.Add(new UtilityCard(UtilityType.CleanseOrExtraPlay));
        }

        deck.Add(new AttackCard(TargetColor.Red, 4));

        for(int i=0; i<3; i++)
        {
            deck.Add(new AttackCard(TargetColor.Red, 2));
        }

        for (int i = 0; i < 6; i++)
        {
            deck.Add(new AttackCard(TargetColor.Red, 1));
        }

        for (int i = 0; i < 9; i++)
        {
            deck.Add(new AttackCard(TargetColor.White, 1));
        }

        for (int i = 0; i < deck.Count; i++) // NEW: give every card in this deck a stable ID (0..count-1) - the network relay uses this to tell the other machine "this exact card" instead of a reference, which can't cross the network
        {
            deck[i]._Id = i;
        }

        return deck;
    }

    public static void SetupPlayer(PlayerState player)
    {
        List<GridCell> board = BuildBoardDeck();
        ShuffleDeck(board);
        player._Grid = board;

        List<Card> cards = BuildCardDeck();
        ShuffleDeck(cards);
        player._DrawPile = cards;

        for(int i = 0; i < 5; i++)
        {
            Card drawnCard = player._DrawPile[0];
            player._DrawPile.RemoveAt(0);
            player._Hand.Add(drawnCard);
        }

        player.SortHand(); // NEW: puts the freshly-dealt hand into its fixed order - the deal flourish in BoardDisplay reads this hand by index, so it needs to already be in the same order HandDisplay will show it in
    }

    public static GameState StartNewGame()
    {
        GameState game = new GameState();

        SetupPlayer(game._PlayerRed);
        SetupPlayer(game._PlayerBlue);

        if (_SharedRng == null) // CHANGED: same fallback as ShuffleDeck, for local/offline testing
        {
            _SharedRng = new System.Random();
        }

        if(_SharedRng.Next(2) == 0)
        {
            game._ActivePlayer = PlayerColor.Red;
        }
        else
        {
            game._ActivePlayer = PlayerColor.Blue;
        }
        return game;
    }
}