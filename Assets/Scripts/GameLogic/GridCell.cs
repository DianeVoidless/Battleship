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

    public void TakeDamage(int amount) // red missiles vs any ship OTHER than a Submarine - shield absorbs first, any leftover goes to the hull
    {
        UnityEngine.Debug.Log("TakeDamage: called on " + _Ship + " with amount=" + amount + ", _ShieldHP(before)=" + _ShieldHP + ", GetRemainingHP(before)=" + GetRemainingHP()); // TEMP: traces every damage application so we can catch the rare case where HP doesn't drop as expected
        if(_ShieldHP > 0)
        {
            if(_ShieldHP >= amount)
            {
                _ShieldHP -= amount;
                UnityEngine.Debug.Log("TakeDamage: fully absorbed by shield - _ShieldHP(after)=" + _ShieldHP + ", GetRemainingHP(after)=" + GetRemainingHP()); // TEMP
                return;
            }
            else
            {
                amount -= _ShieldHP;
                _ShieldHP = 0;
            }
        }
        _DamageInstances.Add(amount);
        UnityEngine.Debug.Log("TakeDamage: recorded " + amount + " damage - GetRemainingHP(after)=" + GetRemainingHP()); // TEMP
    }

    public void TakeShieldDamage(int amount) // NEW: red missiles vs a Submarine - a shield is ALWAYS targetable by red missiles even on a Submarine, so this still pops it exactly like TakeDamage would, but red can never touch a Submarine's actual hull, so any leftover damage beyond the shield's HP is simply wasted instead of being recorded against the ship
    {
        UnityEngine.Debug.Log("TakeShieldDamage: called on " + _Ship + " with amount=" + amount + ", _ShieldHP(before)=" + _ShieldHP + " (red missile vs Submarine - shield-only, hull is immune)"); // TEMP
        if(_ShieldHP > 0)
        {
            if(_ShieldHP >= amount)
            {
                _ShieldHP -= amount;
            }
            else
            {
                _ShieldHP = 0;
            }
        }
        UnityEngine.Debug.Log("TakeShieldDamage: _ShieldHP(after)=" + _ShieldHP + ", GetRemainingHP(unchanged)=" + GetRemainingHP()); // TEMP
    }

    public void TakeHullDamage(int amount) // CHANGED: white missiles - a shield is NEVER poppable by a white missile (only red can pop one), so a white hit can't drain it down the way TakeDamage does. But an active shield still fully PROTECTS the hull from white: while _ShieldHP > 0 this does nothing at all - no shield damage, no hull damage - until red has brought the shield down to 0. Only once the shield is gone does a white hit reach the hull directly.
    {
        UnityEngine.Debug.Log("TakeHullDamage: called on " + _Ship + " with amount=" + amount + ", _ShieldHP=" + _ShieldHP + " (white missile), GetRemainingHP(before)=" + GetRemainingHP()); // TEMP
        if (_ShieldHP > 0)
        {
            UnityEngine.Debug.Log("TakeHullDamage: blocked entirely - shield still up (only red can pop it), hull untouched"); // TEMP
            return;
        }
        _DamageInstances.Add(amount);
        UnityEngine.Debug.Log("TakeHullDamage: recorded " + amount + " damage - GetRemainingHP(after)=" + GetRemainingHP()); // TEMP
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
