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
}
