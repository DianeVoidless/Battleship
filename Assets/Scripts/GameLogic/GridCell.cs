using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;
public class GridCell
{
    public ShipType _Ship;
    public bool _Revealed;
    public List<int> _DamageInstances;
    public int _ShieldHP;

    public GridCell(ShipType ship)
    {
        _Ship = ship;
        _Revealed = false;
        _DamageInstances = new List<int>();
        _ShieldHP = 0;
    }

    public void TakeDamage(int amount)
    {
        if(_ShieldHP > 0)
        {
            if(_ShieldHP >= amount)
            {
                _ShieldHP -= amount;
                return;
            }
            else
            {
                amount -= _ShieldHP;
                _ShieldHP = 0;
            }
        }
        _DamageInstances.Add(amount);
    }

    public void AddShield()
    {
        _ShieldHP += 2;
    }

    public void RemoveHighestDamage()
    {
        if(_DamageInstances.Count == 0)
        {
            return;
        }
        int highestIndex = 0;
        for(int i=1; i< _DamageInstances.Count; i++)
        {
            if (_DamageInstances[i] > _DamageInstances[highestIndex])
            {
                highestIndex = i;
            }
        }
        _DamageInstances.RemoveAt(highestIndex);
    }

    public int GetTotalDamage() // NEW: sums every damage instance this cell has taken (shield hits never get added to this list, see TakeDamage)
    {
        int total = 0;
        foreach (int amount in _DamageInstances)
        {
            total += amount;
        }
        return total;
    }

    public int GetRemainingHP() // NEW: max HP for this ship, minus everything it's taken so far, never below 0
    {
        int remaining = ShipStats.GetMaxHP(_Ship) - GetTotalDamage();
        return remaining > 0 ? remaining : 0;
    }

    public bool IsSunk() // NEW: a real ship (not an empty cell) at 0 HP or less
    {
        return _Ship != ShipType.None && GetRemainingHP() <= 0;
    }
}
