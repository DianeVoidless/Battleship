public static class ShipStats 
{
    public static int GetMaxHP(ShipType ship)
    {
        switch (ship)
        {
            case ShipType.Carrier: return 5;
            case ShipType.Cruiser: return 4;
            case ShipType.Destroyer: return 3;
            case ShipType.Submarine: return 3;
            case ShipType.PatrolBoat: return 2; 
            default: return 0; 
        }
    }
}