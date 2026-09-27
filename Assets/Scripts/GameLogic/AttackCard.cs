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

    public override bool IsLegalTarget(GridCell cell) // NEW: an unrevealed cell is always fair game (that's the whole point of an attack - you don't know what's there yet), but once a cell IS revealed it must still actually hold an active, unsunk ship - a revealed empty cell, or an already-sunk ship, has nothing left to hit. Without this override, Card's own base IsLegalTarget (always true) let a missile card be aimed and fired at a cell already known to be empty, wasting the card/turn for no effect at all.
    {
        return !cell._Revealed || (cell._Ship != ShipType.None && !cell.IsSunk());
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
                target.TakeShieldDamage(damage);
            }
            else
            {
                target.TakeDamage(damage);
            }
        }
        else // white missile
        {
            bool canHitAnyShip = owner.HasActiveShip(ShipType.Destroyer); // CHANGED: Destroyer lets white missiles target any ship, not just the submarine, AND empowers them to punch through shields like a red missile would - a plain, un-empowered white missile is still fully blocked by any shield layer (see TakeHullDamage), it can only ever reach an unshielded Submarine's hull.

            if (target._Ship == ShipType.Submarine || canHitAnyShip)
            {
                if (canHitAnyShip)
                {
                    target.TakeDamage(1); // NEW: empowered by Destroyer - cascades through and pops shield layers exactly like a red missile, only reaching the hull once every layer is gone
                }
                else
                {
                    // Un-empowered white missile - a shield is NEVER poppable by it, so this bypasses
                    // TakeDamage's shield-cascade entirely and just fully blocks on any shield at all.
                    target.TakeHullDamage(1);
                }
            }
        }
    }
}
