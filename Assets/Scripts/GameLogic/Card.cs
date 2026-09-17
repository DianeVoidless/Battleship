using System.Collections.Generic;
using UnityEngine;

public class Card
{
    public virtual CardTargetMode GetTargetMode() 
    { 
        return CardTargetMode.None; 
    }

    public virtual bool IsLegalTarget(GridCell cell)
    {
        return true;
    }

    public virtual bool IsLegalHandCard(Card handCard)
    {
        return false;
    }

    public virtual int GetBonusMoves()
    {
        return 0;
    }

    public virtual void Resolve(GridCell target)
    {
        //
    }

    public virtual void ResolveNoTarget(PlayerState owner) // NEW: for cards that need no cell/hand target at all (Draw 3, Extra Play)
    {
        //
    }

    public virtual void ResolveHandSelection(PlayerState owner, List<Card> selectedCards) // NEW: for Cleanse - acts on whichever hand cards were selected
    {
        //
    }

    public virtual void OnDiscarded() // NEW: hook for cleanup when a card leaves the hand for the discard pile
    {
        //
    }
}
