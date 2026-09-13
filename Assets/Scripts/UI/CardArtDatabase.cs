using UnityEngine;

public class CardArtDatabase : MonoBehaviour
{
    public Sprite _RedCarrier;
    public Sprite _RedCruiser;
    public Sprite _RedDestroyer;
    public Sprite _RedSubmarine;
    public Sprite _RedHealer;
    public Sprite _RedCoverBoard;
    public Sprite _RedMiss;

    public Sprite _BlueCarrier;
    public Sprite _BlueCruiser;
    public Sprite _BlueDestroyer;
    public Sprite _BlueSubmarine;
    public Sprite _BlueHealer;
    public Sprite _BlueCoverBoard;
    public Sprite _BlueMiss;

    public Sprite _Red1DmgStrike;
    public Sprite _Red2DmgStrike;
    public Sprite _Red4DmgStrike;
    public Sprite _RedWhiteMissile;
    public Sprite _RedShield;

    public Sprite _Blue1DmgStrike;
    public Sprite _Blue2DmgStrike;
    public Sprite _Blue4DmgStrike;
    public Sprite _BlueWhiteMissile;
    public Sprite _BlueShield;

    [System.Serializable] 
    public class WildcardSpriteSet
    {
        public Sprite _Base;                    
        public Sprite _GatedGray;                
        public Sprite _GatedHighlight;            
        public Sprite _OtherHighlight;            
        public Sprite _OtherHighlightGatedGray;   
    }

    public WildcardSpriteSet _RedWildcard1;   
    public WildcardSpriteSet _RedWildcard2;   
    public WildcardSpriteSet _BlueWildcard1;  
    public WildcardSpriteSet _BlueWildcard2;


    public Sprite[] _RedCarrierDamaged;   // NEW: index 0 = 1 HP remaining, index 1 = 2 HP remaining, etc.
    public Sprite[] _RedCruiserDamaged;   // NEW
    public Sprite[] _RedDestroyerDamaged; // NEW
    public Sprite[] _RedSubmarineDamaged; // NEW
    public Sprite[] _RedHealerDamaged;    // NEW

    public Sprite[] _BlueCarrierDamaged;   // NEW
    public Sprite[] _BlueCruiserDamaged;   // NEW
    public Sprite[] _BlueDestroyerDamaged; // NEW
    public Sprite[] _BlueSubmarineDamaged; // NEW
    public Sprite[] _BlueHealerDamaged;    // NEW

    public Sprite _RedShield1HP;  // NEW: shield damaged down to 1 HP ("SHIELD x1")
    public Sprite _BlueShield1HP; // NEW
    public Sprite GetShipSprite(GridCell cell, PlayerColor color) // CHANGED: now takes the whole cell, so it can factor in remaining HP
    {
        if (!cell._Revealed)
        {
            return color == PlayerColor.Red ? _RedCoverBoard : _BlueCoverBoard;
        }

        if (cell._Ship == ShipType.None)
        {
            return color == PlayerColor.Red ? _RedMiss : _BlueMiss;
        }

        if (cell.IsSunk()) // CHANGED: sunk ships are now fully invisible, not shown as a miss
        {
            return null;
        }

        int maxHP = ShipStats.GetMaxHP(cell._Ship);
        int remaining = cell.GetRemainingHP();
        Sprite fullSprite = GetFullShipSprite(cell._Ship, color);

        if (remaining >= maxHP) // NEW: undamaged - use the existing plain sprite
        {
            return fullSprite;
        }

        Sprite[] damagedStages = GetDamagedShipSprites(cell._Ship, color);
        if (remaining <= 0 || damagedStages == null || damagedStages.Length < remaining) // NEW: safety net - shouldn't normally trigger, sunk cells get removed by BoardDisplay instead
        {
            return fullSprite;
        }

        return damagedStages[remaining - 1]; // NEW: e.g. remaining = 3 -> index 2 -> the "3HP" sprite
    }

    private Sprite GetFullShipSprite(ShipType ship, PlayerColor color) // NEW: pulled out of the old GetShipSprite so both full and damaged lookups can share it
    {
        if (color == PlayerColor.Red)
        {
            switch (ship)
            {
                case ShipType.Carrier: return _RedCarrier;
                case ShipType.Cruiser: return _RedCruiser;
                case ShipType.Destroyer: return _RedDestroyer;
                case ShipType.Submarine: return _RedSubmarine;
                case ShipType.PatrolBoat: return _RedHealer;
                default: return null;
            }
        }
        else
        {
            switch (ship)
            {
                case ShipType.Carrier: return _BlueCarrier;
                case ShipType.Cruiser: return _BlueCruiser;
                case ShipType.Destroyer: return _BlueDestroyer;
                case ShipType.Submarine: return _BlueSubmarine;
                case ShipType.PatrolBoat: return _BlueHealer;
                default: return null;
            }
        }
    }

    private Sprite[] GetDamagedShipSprites(ShipType ship, PlayerColor color) // NEW
    {
        if (color == PlayerColor.Red)
        {
            switch (ship)
            {
                case ShipType.Carrier: return _RedCarrierDamaged;
                case ShipType.Cruiser: return _RedCruiserDamaged;
                case ShipType.Destroyer: return _RedDestroyerDamaged;
                case ShipType.Submarine: return _RedSubmarineDamaged;
                case ShipType.PatrolBoat: return _RedHealerDamaged;
                default: return null;
            }
        }
        else
        {
            switch (ship)
            {
                case ShipType.Carrier: return _BlueCarrierDamaged;
                case ShipType.Cruiser: return _BlueCruiserDamaged;
                case ShipType.Destroyer: return _BlueDestroyerDamaged;
                case ShipType.Submarine: return _BlueSubmarineDamaged;
                case ShipType.PatrolBoat: return _BlueHealerDamaged;
                default: return null;
            }
        }
    }

    public Sprite GetCardSprite(Card card, PlayerColor owner)
    {
        if (card is AttackCard attackCard)
        {
            if (attackCard._Color == TargetColor.White)
            {
                return owner == PlayerColor.Red ? _RedWhiteMissile : _BlueWhiteMissile;
            }

            switch (attackCard._Damage)
            {
                case 4: return owner == PlayerColor.Red ? _Red4DmgStrike : _Blue4DmgStrike;
                case 2: return owner == PlayerColor.Red ? _Red2DmgStrike : _Blue2DmgStrike;
                case 1: return owner == PlayerColor.Red ? _Red1DmgStrike : _Blue1DmgStrike;
                default: return null;
            }
        }
        else if (card is UtilityCard utilityCard)
        {
            switch (utilityCard._Type)
            {
                case UtilityType.Shield: return owner == PlayerColor.Red ? _RedShield : _BlueShield;
                case UtilityType.HealOrDraw3: return (owner == PlayerColor.Red ? _RedWildcard2 : _BlueWildcard2)._Base; 
                case UtilityType.CleanseOrExtraPlay: return (owner == PlayerColor.Red ? _RedWildcard1 : _BlueWildcard1)._Base; 
                default: return null;
            }
        }
        return null;
    }

    public Sprite GetWildcardSprite(UtilityType type, PlayerColor owner, bool gatedAvailable, bool hoveringGated, bool hoveringOther) 
    {
        WildcardSpriteSet set = (type == UtilityType.CleanseOrExtraPlay)
            ? (owner == PlayerColor.Red ? _RedWildcard1 : _BlueWildcard1)
            : (owner == PlayerColor.Red ? _RedWildcard2 : _BlueWildcard2);

        if (hoveringGated)
        {
            return gatedAvailable ? set._GatedHighlight : set._GatedGray; 
        }

        if (hoveringOther)
        {
            return gatedAvailable ? set._OtherHighlight : set._OtherHighlightGatedGray;
        }

        return gatedAvailable ? set._Base : set._GatedGray;
    }

    public Sprite GetShieldSprite(GridCell cell, PlayerColor color) // NEW: null means "no shield overlay to show"
    {
        if (cell._ShieldHP <= 0)
        {
            return null;
        }
        if (cell._ShieldHP == 1)
        {
            return color == PlayerColor.Red ? _RedShield1HP : _BlueShield1HP;
        }
        return color == PlayerColor.Red ? _RedShield : _BlueShield; // 2 (or more) - full shield
    }
}