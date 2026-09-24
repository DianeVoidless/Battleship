using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Serialization;
public class GridCell
{
    public ShipType _Ship;
    public bool _Revealed;
    public List<int> _DamageInstances;

    // CHANGED: shields now stack - a card can be shielded more than once, and each Shield play adds
    // its own independent 2 HP layer instead of just topping up one shared pool. _ShieldLayers[0] is
    // always the TOP-most/most-recently-applied shield (the one shown leftmost in the UI and the one
    // that absorbs damage FIRST); the LAST entry is the oldest/bottom-most shield (shown in the
    // original corner slot, absorbs damage last). Empty list = no shield at all.
    public List<int> _ShieldLayers = new List<int>();

    public GridCell(ShipType ship)
    {
        _Ship = ship;
        _Revealed = false;
        _DamageInstances = new List<int>();
    }

    public bool HasShield() // NEW: convenience check - true while at least one shield layer remains, however many are stacked
    {
        return _ShieldLayers.Count > 0;
    }

    private int AbsorbIntoShieldLayers(int amount) // NEW: shared cascade logic - drains 'amount' through the shield stack front-to-back (top layer first), destroying and removing any layer it fully drains, and returns whatever amount is left over once every layer is gone (0 if the stack fully absorbed it)
    {
        while (amount > 0 && _ShieldLayers.Count > 0)
        {
            int topLayer = _ShieldLayers[0];
            if (topLayer > amount)
            {
                _ShieldLayers[0] = topLayer - amount;
                amount = 0;
            }
            else
            {
                amount -= topLayer;
                _ShieldLayers.RemoveAt(0); // that layer is destroyed - whatever was beneath it (if anything) is now the new top layer
            }
        }
        return amount;
    }

    public void TakeDamage(int amount) // red missiles vs any ship OTHER than a Submarine - cascades through every shield layer (top first) before any leftover overkill reaches the hull
    {
        int leftover = AbsorbIntoShieldLayers(amount);
        if (leftover <= 0)
        {
            return;
        }
        _DamageInstances.Add(leftover);
    }

    public void TakeShieldDamage(int amount) // NEW: red missiles vs a Submarine - shields are ALWAYS targetable by red missiles even on a Submarine, so this still cascades through the stack exactly like TakeDamage would, but red can never touch a Submarine's actual hull, so any leftover damage once every layer is gone is simply wasted instead of being recorded against the ship
    {
        AbsorbIntoShieldLayers(amount);
    }

    public void TakeHullDamage(int amount) // CHANGED: white missiles - a shield is NEVER poppable by a white missile (only red can pop one), so a white hit can't drain any layer the way TakeDamage does. But ANY remaining shield layer still fully PROTECTS the hull from white: while at least one layer is up, this does nothing at all - no shield damage, no hull damage - until red has brought every layer down to 0. Only once the whole stack is gone does a white hit reach the hull directly.
    {
        if (HasShield())
        {
            return;
        }
        _DamageInstances.Add(amount);
    }

    public void AddShield() // CHANGED: adds a brand new 2 HP layer ON TOP of any shield(s) already there, instead of just adding 2 to one shared pool - the new layer becomes the first one damage has to get through
    {
        _ShieldLayers.Insert(0, 2);
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
