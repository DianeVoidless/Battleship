public enum ShipType
{
    None, 
    Carrier, 
    Cruiser, 
    Destroyer, 
    Submarine, 
    PatrolBoat
}

public enum PlayerColor
{
    Red,
    Blue
}

public enum TargetColor
{
    Red,
    White
}

public enum UtilityType
{
    Shield,
    HealOrDraw3,
    CleanseOrExtraPlay
}

public enum CardTargetMode
{
    None,
    EnemyCell,
    OwnCell,
    HandMultiSelect,
    BranchChoice 
}
public enum CardBranch 
{
    NotChosen,
    Heal,
    Draw3,
    Cleanse,
    ExtraPlay
}