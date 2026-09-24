using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic; // NEW: for the stacked-shield sprite list SetShieldOverlays takes

public class CardDisplay : MonoBehaviour, IPointerClickHandler
{
    public Image _CardImage;
    public Card _RepresentedCard;
    public GridCell _RepresentedCell;

    public CardArtDatabase _ArtDatabase; // NEW: needed to look up wildcard sprites
    public PlayerState _Owner; // NEW: needed to check IsBranchAvailable for this card's owner

    private bool _HoveringGated; // NEW: is the mouse over this wildcard's gated half right now?
    private bool _HoveringOther; // NEW: is the mouse over this wildcard's other half right now?
    public Image _ShieldOverlayImage; // a second Image, layered on top of _CardImage, for the shield icon - this is always the RIGHTMOST badge (the original corner slot), showing the OLDEST/bottom-most shield layer whenever more than one is stacked
    public float _ShieldOverlaySpacing = 4f; // NEW: gap between stacked shield badges (in the same units as the RectTransform), when a card has more than one shield

    private List<Image> _ExtraShieldOverlayImages = new List<Image>(); // NEW: clones of _ShieldOverlayImage, created on demand as more shields stack up - index 0 is the first extra badge (one step left of the base/corner slot), index 1 the next one over, etc.
    private Vector2 _BaseShieldOverlayAnchoredPosition; // NEW: _ShieldOverlayImage's original corner position, cached once so repeated calls don't keep shifting it
    private bool _BaseShieldOverlayPositionCached;

    public Image _DamageBoostOverlayImage; // NEW: a third Image, layered on top of _CardImage, for the Cruiser damage-boost "+1" badge - unlike the shield badges this is a simple on/off overlay (a card is either currently boosted or it isn't, never stacked)

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

    public void SetDamageBoostOverlay(Sprite sprite) // NEW: shows or hides the Cruiser damage-boost "+1" badge on this card - null hides it
    {
        if (_DamageBoostOverlayImage == null)
        {
            return;
        }

        if (sprite == null)
        {
            _DamageBoostOverlayImage.enabled = false;
        }
        else
        {
            _DamageBoostOverlayImage.enabled = true;
            _DamageBoostOverlayImage.sprite = sprite;
        }
    }

    public void SetShieldOverlays(List<Sprite> sprites) // CHANGED: shields can now stack, so this takes a list instead of a single sprite - null or empty hides every badge. Ordered top-to-bottom exactly like GridCell._ShieldLayers/CardArtDatabase.GetShieldSprites: index 0 is the newest/top-most shield (rendered leftmost, furthest from the card's corner), and the last entry is the oldest/bottom-most one (rendered in the original corner slot, _ShieldOverlayImage itself)
    {
        if (_ShieldOverlayImage == null)
        {
            return;
        }

        if (!_BaseShieldOverlayPositionCached) // NEW: remember the corner slot's original position exactly once, before we ever move it, so later calls always measure offsets from the true original spot rather than from wherever a previous call left it
        {
            _BaseShieldOverlayAnchoredPosition = _ShieldOverlayImage.rectTransform.anchoredPosition;
            _BaseShieldOverlayPositionCached = true;
        }

        int count = (sprites != null) ? sprites.Count : 0;
        int totalExistingSlots = 1 + _ExtraShieldOverlayImages.Count; // the base corner slot plus however many extra badges have ever been created so far

        if (count == 0)
        {
            for (int slot = 0; slot < totalExistingSlots; slot++)
            {
                GetShieldOverlaySlot(slot).enabled = false;
            }
            return;
        }

        float stepX = _ShieldOverlayImage.rectTransform.sizeDelta.x + _ShieldOverlaySpacing; // NEW: how far apart each stacked badge sits, based on the corner badge's own width

        // slot 0 is the base/corner image, showing the OLDEST/bottom shield (the LAST entry in
        // 'sprites'); each higher slot steps one badge-width further left and shows the next entry
        // back through the list, ending with the highest slot showing sprites[0] - the newest/top
        // shield - furthest from the corner, exactly matching how the mockup reads left (top) to
        // right (bottom, closest to the card's edge).
        for (int slot = 0; slot < count; slot++)
        {
            Image image = GetShieldOverlaySlot(slot);
            int spriteIndex = count - 1 - slot;
            image.enabled = true;
            image.sprite = sprites[spriteIndex];
            image.rectTransform.anchoredPosition = _BaseShieldOverlayAnchoredPosition - new Vector2(slot * stepX, 0f);
        }

        // hide any further slots left over from a previously-bigger stack on this same card (e.g. a
        // shield just got destroyed and the count dropped) without destroying the cloned GameObjects,
        // since the same card might need them again later
        for (int slot = count; slot < totalExistingSlots; slot++)
        {
            GetShieldOverlaySlot(slot).enabled = false;
        }
    }

    private Image GetShieldOverlaySlot(int slot) // NEW: slot 0 is always the original _ShieldOverlayImage; slot 1+ is a cloned copy, instantiated the first time that many shields stack up and reused after that
    {
        if (slot == 0)
        {
            return _ShieldOverlayImage;
        }

        int extraIndex = slot - 1;
        while (_ExtraShieldOverlayImages.Count <= extraIndex) // NEW: only ever creates as many clones as the highest stack this card has ever actually shown
        {
            GameObject clone = Instantiate(_ShieldOverlayImage.gameObject, _ShieldOverlayImage.transform.parent);
            clone.name = "ShieldOverlay_Extra" + _ExtraShieldOverlayImages.Count;
            _ExtraShieldOverlayImages.Add(clone.GetComponent<Image>());
        }
        return _ExtraShieldOverlayImages[extraIndex];
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