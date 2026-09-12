using System.Collections.Generic;

public static class GameSetup
{
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
        System.Random _rng = new System.Random();
        
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i+1);

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
    }

    public static GameState StartNewGame()
    {
        GameState game = new GameState();

        SetupPlayer(game._PlayerRed);
        SetupPlayer(game._PlayerBlue);

        System.Random rng = new System.Random();
        if(rng.Next(2) == 0)
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