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

    public override void Resolve(GridCell target, PlayerState owner) // CHANGED: added owner, needed for Cruiser/Destroyer passives
    {
        target._Revealed = true;

        if (target._Ship == ShipType.None)
        {
            return;
        }

        if (_Color == TargetColor.Red)
        {
            if (target._Ship == ShipType.Submarine)
            {
                return; // Submarine's passive: red missiles simply can't touch it
            }

            int damage = _Damage;
            if (owner.HasActiveShip(ShipType.Cruiser)) // NEW: Cruiser buffs the player's own outgoing red missile damage
            {
                damage += 1;
            }

            target.TakeDamage(damage);
        }
        else // white missile
        {
            bool canHitAnyShip = owner.HasActiveShip(ShipType.Destroyer); // NEW: Destroyer lets white missiles target any ship, not just the submarine

            if (target._Ship == ShipType.Submarine || canHitAnyShip)
            {
                target.TakeDamage(1);
            }
        }
    }
}
