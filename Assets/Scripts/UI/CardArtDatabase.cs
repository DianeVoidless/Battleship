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

    public Sprite GetShipSprite(ShipType ship, PlayerColor color, bool revealed)
    {
        if (!revealed)
        {
            if (color == PlayerColor.Red)

            {
                return _RedCoverBoard;
            }
            else
            {
                return _BlueCoverBoard;
            }
        }
        if (color == PlayerColor.Red)
        {
            switch (ship)
            {
                case ShipType.Carrier: return _RedCarrier;
                case ShipType.Cruiser: return _RedCruiser;
                case ShipType.Destroyer: return _RedDestroyer;
                case ShipType.Submarine: return _RedSubmarine;
                case ShipType.PatrolBoat: return _RedHealer;
                case ShipType.None: return _RedMiss;
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
                case ShipType.None: return _BlueMiss;
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
}