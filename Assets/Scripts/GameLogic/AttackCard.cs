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
            int damage = _Damage;
            if (owner.HasActiveShip(ShipType.Cruiser)) // NEW: Cruiser buffs the player's own outgoing red missile damage
            {
                damage += 1;
            }

            if (target._Ship == ShipType.Submarine)
            {
                // CHANGED: a Submarine's hull is still immune to red missiles, but its shield (if any)
                // is NOT - a shield is always targetable by red regardless of what it's protecting, so
                // this still pops it exactly like a normal hit would. Any leftover damage beyond the
                // shield's own HP is simply wasted - it never reaches the Submarine's actual hull.
                UnityEngine.Debug.Log("AttackCard.Resolve: RED hitting Submarine for " + damage + " damage - shield-only, hull is immune"); // TEMP
                target.TakeShieldDamage(damage);
            }
            else
            {
                UnityEngine.Debug.Log("AttackCard.Resolve: RED hitting " + target._Ship + " for " + damage + " damage"); // TEMP
                target.TakeDamage(damage);
            }
        }
        else // white missile
        {
            bool canHitAnyShip = owner.HasActiveShip(ShipType.Destroyer); // NEW: Destroyer lets white missiles target any ship, not just the submarine

            UnityEngine.Debug.Log("AttackCard.Resolve: WHITE hitting " + target._Ship + " - canHitAnyShip(Destroyer active)=" + canHitAnyShip); // TEMP: confirms whether the Destroyer passive is actually detected as active for this hit
            if (target._Ship == ShipType.Submarine || canHitAnyShip)
            {
                // CHANGED: white missiles can never target a shield at all - only red can pop one - so
                // this always bypasses _ShieldHP entirely and hits the hull directly, shielded or not.
                target.TakeHullDamage(1);
            }
            else
            {
                UnityEngine.Debug.Log("AttackCard.Resolve: WHITE missile did nothing - not a Submarine and Destroyer passive not active"); // TEMP
            }
        }
    }
}
