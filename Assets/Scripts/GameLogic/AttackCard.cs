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
}
