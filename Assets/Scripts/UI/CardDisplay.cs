using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardDisplay : MonoBehaviour, IPointerClickHandler
{
    public Image _CardImage;
    public Card _RepresentedCard;
    public GridCell _RepresentedCell;

    public CardArtDatabase _ArtDatabase; // NEW: needed to look up wildcard sprites
    public PlayerState _Owner; // NEW: needed to check IsBranchAvailable for this card's owner

    private bool _HoveringGated; // NEW: is the mouse over this wildcard's gated half right now?
    private bool _HoveringOther; // NEW: is the mouse over this wildcard's other half right now?
    public Image _ShieldOverlayImage; // NEW: a second Image, layered on top of _CardImage, for the shield icon

    public void SetSprite(Sprite sprite) // CHANGED: null now hides the card's image entirely, instead of showing a blank white box
    {
        if (sprite == null)
        {
            _CardImage.enabled = false;
        }
        else
        {
            _CardImage.enabled = true;
            _CardImage.sprite = sprite;
        }
    }
    public void SetShieldOverlay(Sprite sprite) // NEW: null hides the overlay entirely, a sprite shows and updates it
    {
        if (_ShieldOverlayImage == null)
        {
            return;
        }
        if (sprite == null)
        {
            _ShieldOverlayImage.enabled = false;
        }
        else
        {
            _ShieldOverlayImage.enabled = true;
            _ShieldOverlayImage.sprite = sprite;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_RepresentedCard != null)
        {
            TurnController._Instance.OnCardClicked(this, _RepresentedCard);
        }
        else if (_RepresentedCell != null)
        {
            TurnController._Instance.OnCellClicked(this, _RepresentedCell);
        }
    }

    public void SetWildcardHover(bool isGatedZone, bool hovering) // NEW: called by WildcardBranchZone when the mouse enters/exits a half
    {
        if (isGatedZone)
        {
            _HoveringGated = hovering;
        }
        else
        {
            _HoveringOther = hovering;
        }
        RefreshWildcardSprite();
    }

    public void RefreshWildcardSprite()
    {
        UtilityCard utilityCard = _RepresentedCard as UtilityCard;
        if (utilityCard == null || utilityCard._Type == UtilityType.Shield)
        {
            return;
        }

        CardBranch gatedBranch = GetGatedBranch(utilityCard._Type);
        bool available = utilityCard.IsBranchAvailable(gatedBranch, _Owner);

        Sprite sprite = _ArtDatabase.GetWildcardSprite(utilityCard._Type, _Owner._Color, available, _HoveringGated, _HoveringOther);
        SetSprite(sprite);
    }

    private CardBranch GetGatedBranch(UtilityType type) 
    {
        return type == UtilityType.CleanseOrExtraPlay ? CardBranch.Cleanse : CardBranch.Heal;
    }

    public void OnWildcardBranchClicked(bool isGatedZone, PointerEventData eventData)
    {
        UtilityCard utilityCard = _RepresentedCard as UtilityCard;

        // CHANGED: used to only route to the branch picker while GetTargetMode() was still
        // BranchChoice (i.e. before any branch had been picked yet) - once a branch was chosen
        // (even a wrong one, like a misclicked Heal), clicking the OTHER half fell through to a
        // plain card click instead, which couldn't change the branch at all. That's the "stuck"
        // bug: there was no way to correct a misclick on the same card without cancelling out of
        // it first. Now any non-Shield wildcard always routes its zone clicks to the branch picker,
        // so switching branches works no matter which one is currently chosen.
        if (utilityCard == null || utilityCard._Type == UtilityType.Shield)
        {
            OnPointerClick(eventData);
            return;
        }

        TurnController._Instance.OnWildcardBranchClicked(this, utilityCard, isGatedZone);
    }
}