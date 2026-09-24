using UnityEngine;
using System.Collections.Generic; // NEW: for the List<Sprite> GetShieldSprites now returns, one entry per stacked shield layer

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

    // CHANGED: the on-ship shield badge (GetShieldSprite, below) used to reuse _RedShield/_BlueShield
    // (the Shield utility CARD's own hand-face art) for its "2 HP" tier, plus a separate
    // _RedShield1HP/_BlueShield1HP pair for its "1 HP" tier - four fields total, still split by
    // color. The badge itself is a plain black/white icon that looks identical on either side, so
    // it's now just these two shared fields instead - _RedShield/_BlueShield stay exactly as they
    // were, untouched, still only used for the Shield card's own red/blue-themed face in hand.
    public Sprite _Shield1HP; // NEW: the on-ship badge shown while a shield has exactly 1 HP left
    public Sprite _Shield2HP; // NEW: the on-ship badge shown while a shield has 2 (or more) HP left

    public Sprite _DamageBoostBadge; // NEW: the "+1" badge shown on a red damage card in hand while the Cruiser's damage-boost passive is currently active

    public Sprite _RedWhiteMissileEnhanced; // NEW: replaces _RedWhiteMissile entirely on a white missile card in hand while the Destroyer's "can hit any ship" passive is active for its Red owner
    public Sprite _BlueWhiteMissileEnhanced; // NEW: replaces _BlueWhiteMissile entirely on a white missile card in hand while the Destroyer's "can hit any ship" passive is active for its Blue owner

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

    public Sprite GetCardSprite(Card card, PlayerState owner) // NEW: overload used wherever a PlayerState (rather than just a PlayerColor) is on hand - swaps in the Destroyer-enhanced white missile art in place of the plain one while that passive is active for this card's owner, and otherwise behaves exactly like the PlayerColor overload above
    {
        if (card is AttackCard attackCard && attackCard._Color == TargetColor.White && owner.HasActiveShip(ShipType.Destroyer))
        {
            return owner._Color == PlayerColor.Red ? _RedWhiteMissileEnhanced : _BlueWhiteMissileEnhanced;
        }

        return GetCardSprite(card, owner._Color);
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

    public List<Sprite> GetShieldSprites(GridCell cell, PlayerColor color) // CHANGED: shields now stack, so this returns one sprite per active layer instead of a single sprite - an empty list means "no shield overlay to show at all". Ordered top-to-bottom, exactly like GridCell._ShieldLayers: index 0 is the newest/top-most shield (shown leftmost), the last entry is the oldest/bottom-most one (shown in the original corner slot). 'color' is unused now that the badge is a single shared icon for both sides, kept as a parameter so every call site (which always has a color on hand anyway) doesn't need to change
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (int layerHP in cell._ShieldLayers)
        {
            sprites.Add(layerHP == 1 ? _Shield1HP : _Shield2HP);
        }
        return sprites;
    }

    public Sprite GetDamageBoostBadge(Card card, PlayerState owner) // NEW: a "+1" badge shown on a red damage card in hand for as long as Cruiser's damage-boost passive is active for its owner - null means "don't show a badge", either because this isn't a red attack card at all or because the Cruiser bonus isn't currently in effect
    {
        AttackCard attackCard = card as AttackCard;
        if (attackCard == null || attackCard._Color != TargetColor.Red)
        {
            return null;
        }

        return owner.HasActiveShip(ShipType.Cruiser) ? _DamageBoostBadge : null;
    }
}