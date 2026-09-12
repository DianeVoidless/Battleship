using UnityEngine;

public class GridCell
{
    public ShipType _Ship;
    public bool _Revealed;
    public int _HitsTaken;

    public GridCell(ShipType ship)
    {
        _Ship = ship;
        _Revealed = false;
        _HitsTaken = 0;
    }
}
