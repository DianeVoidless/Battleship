using UnityEngine;

public class AttackCard : Card
{
    public TargetColor _Color;
    public int _Damage;

    public AttackCard(TargetColor color, int damage)
    {
        _Color = color;
        _Damage = damage;
    }

    public override CardTargetMode GetTargetMode()
    {
        return CardTargetMode.EnemyCell;
    }

    public override void Resolve(GridCell target)
    {
        target._Revealed = true;

        if (target._Ship == ShipType.None) // NEW: nothing to damage on an empty cell, just reveal it as a miss and stop here
        {
            return;
        }

        if (_Color == TargetColor.Red)
        {
            if (target._Ship != ShipType.Submarine)
            {
                target.TakeDamage(_Damage);
            }
        }
        else
        {
            if (target._Ship == ShipType.Submarine)
            {
                target.TakeDamage(1);
            }
        }
    }
}
