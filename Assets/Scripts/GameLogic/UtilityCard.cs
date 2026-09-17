using UnityEngine;
using System.Collections.Generic;

public class UtilityCard : Card
{
    public UtilityType _Type;
    public CardBranch _ChosenBranch = CardBranch.NotChosen; // NEW: which branch the player picked, for the two choice-cards

    public UtilityCard(UtilityType type)
    {
        _Type = type;
    }

    public void ChooseBranch(CardBranch branch) // NEW: called once the player picks Heal/Draw3 or Cleanse/ExtraPlay
    {
        _ChosenBranch = branch;
    }

    public bool IsBranchAvailable(CardBranch branch, PlayerState owner) // NEW: precondition check - used to grey out an icon the player can't currently pick
    {
        if (branch == CardBranch.Cleanse)
        {
            foreach (Card card in owner._Hand)
            {
                if (card is AttackCard attackCard && attackCard._Color == TargetColor.White)
                {
                    return true;
                }
            }
            return false;
        }

        if (branch == CardBranch.Heal)
        {
            foreach (GridCell cell in owner._Grid)
            {
                if (cell._Revealed && cell._DamageInstances.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        return true; // NEW: Draw3 and ExtraPlay have no precondition
    }

    public override CardTargetMode GetTargetMode()
    {
        if (_Type == UtilityType.Shield)
        {
            return CardTargetMode.OwnCell;
        }

        if (_ChosenBranch == CardBranch.NotChosen) // NEW: wildcards need a branch picked before we know their real target mode
        {
            return CardTargetMode.BranchChoice;
        }

        switch (_ChosenBranch) // NEW
        {
            case CardBranch.Heal: return CardTargetMode.OwnCell;
            case CardBranch.Draw3: return CardTargetMode.None;
            case CardBranch.Cleanse: return CardTargetMode.HandMultiSelect;
            case CardBranch.ExtraPlay: return CardTargetMode.None;
            default: return CardTargetMode.None;
        }
    }

    public override bool IsLegalTarget(GridCell cell)
    {
        if (_Type == UtilityType.Shield)
        {
            return cell._Revealed && cell._Ship != ShipType.None;
        }

        if (_ChosenBranch == CardBranch.Heal) // NEW
        {
            return cell._Revealed && cell._DamageInstances.Count > 0;
        }

        return true;
    }

    public override bool IsLegalHandCard(Card handCard) // NEW
    {
        if (_ChosenBranch == CardBranch.Cleanse)
        {
            return handCard is AttackCard attackCard && attackCard._Color == TargetColor.White;
        }

        return false;
    }

    public override int GetBonusMoves() // NEW
    {
        if (_ChosenBranch == CardBranch.Heal) return 1;
        if (_ChosenBranch == CardBranch.Draw3) return 1;
        if (_ChosenBranch == CardBranch.ExtraPlay) return 2;
        return 0; // Shield, Cleanse, or no branch chosen yet
    }

    public override void Resolve(GridCell target)
    {
        if (_Type == UtilityType.Shield)
        {
            target.AddShield();
        }
        else if (_ChosenBranch == CardBranch.Heal) // NEW
        {
            target.RemoveHighestDamage();
        }
    }

    public override void ResolveNoTarget(PlayerState owner) // NEW
    {
        if (_ChosenBranch == CardBranch.Draw3)
        {
            DrawCards(owner, 3);
        }
        // NEW: ExtraPlay needs no board/hand effect at all - its whole effect is the
        // bonus move, which GetBonusMoves() already provides to the controller
    }

    public override void ResolveHandSelection(PlayerState owner, List<Card> selectedCards) // NEW
    {
        if (_ChosenBranch != CardBranch.Cleanse)
        {
            return;
        }

        foreach (Card card in selectedCards)
        {
            owner._Hand.Remove(card);
            owner._DiscardPile.Add(card);
        }

        DrawCards(owner, selectedCards.Count);
    }

    private void DrawCards(PlayerState owner, int count) // NEW: shared helper - draws 'count' cards from the draw pile into hand
    {
        for (int i = 0; i < count; i++)
        {
            if (owner._DrawPile.Count == 0) // CHANGED: reshuffle the discard pile back in first, same as PlayerState.DrawUpToHandSize, instead of giving up early
            {
                if (owner._DiscardPile.Count == 0)
                {
                    break; // nothing left anywhere to draw - stop instead of looping forever
                }

                owner._DrawPile.AddRange(owner._DiscardPile);
                owner._DiscardPile.Clear();
                GameSetup.ShuffleDeck(owner._DrawPile);
            }

            Card drawnCard = owner._DrawPile[0];
            owner._DrawPile.RemoveAt(0);
            owner._Hand.Add(drawnCard);
        }
    }

    public override void OnDiscarded() // NEW: wildcards must forget their branch choice, since the same instance can be reshuffled and drawn again later
    {
        _ChosenBranch = CardBranch.NotChosen;
    }
}