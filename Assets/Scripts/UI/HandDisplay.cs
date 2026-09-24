using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.VisualScripting;

public class HandDisplay : MonoBehaviour
{
    public GameObject _CardDisplayPrefab;
    public CardArtDatabase _ArtDatabase;
    public HorizontalLayoutGroup _LayoutGroup; // NEW: the Horizontal Layout Group on this same HandPanel object - drag it in

    public float _MaxCardSpacing = 20f; // NEW: the normal gap between cards when they all fit comfortably
    [Range(0f, 1f)] public float _MaxOverlapFraction = 0.5f; // NEW: how far cards are allowed to overlap at most, as a fraction of one card's width

    private List<CardDisplay> _CurrentDisplays = new List<CardDisplay>();

    public void ClearHand() // NEW: removes any dealt hand cards without drawing new ones - used right when a match begins, before any cards exist yet
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        _CurrentDisplays.Clear();
    }

    public void ShowHand(PlayerState owner)
    {
        ClearHand(); // CHANGED: reuses ClearHand instead of repeating the same loop

        UpdateCardSpacing(owner._Hand.Count); // NEW: shrink the gap between cards (and let them overlap) once they no longer fit at the normal spacing

        // CHANGED: renders a freshly-sorted COPY of the hand every time, rather than iterating
        // owner._Hand directly - cards get appended to _Hand in whatever order they're drawn or
        // returned in (a mid-turn draw animation adds each new card back one at a time, for
        // instance), so re-sorting here is what keeps the on-screen hand in its fixed order
        // (White missiles, Red missiles by damage, Cleanse/ExtraPlay, Heal/Draw3, Shield) no matter
        // how the underlying list ended up ordered.
        List<Card> sortedHand = new List<Card>(owner._Hand);
        sortedHand.Sort((a, b) => PlayerState.GetHandSortKey(a).CompareTo(PlayerState.GetHandSortKey(b)));

        foreach (Card card in sortedHand)
        {
            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetCardSprite(card, owner); // CHANGED: now passes the whole PlayerState, so the Destroyer-enhanced white missile art can be swapped in while its passive is active
            display.SetSprite(sprite);
            display._RepresentedCard = card;
            display._ArtDatabase = _ArtDatabase;
            display._Owner = owner;
            display.RefreshWildcardSprite();
            display.SetDamageBoostOverlay(_ArtDatabase.GetDamageBoostBadge(card, owner)); // NEW: shows the Cruiser "+1" badge on red damage cards while its passive is active

            cardObject.AddComponent<CardHoverEffect>();

            _CurrentDisplays.Add(display);
        }
    }

    private void UpdateCardSpacing(int cardCount) // NEW: computes how much gap to leave between cards so the whole hand always fits inside the panel, centered
    {
        if (_LayoutGroup == null || cardCount <= 1)
        {
            return;
        }

        float panelWidth = ((RectTransform)transform).rect.width;
        float cardWidth = _CardDisplayPrefab.GetComponent<RectTransform>().rect.width;

        float widthAtMaxSpacing = (cardCount * cardWidth) + ((cardCount - 1) * _MaxCardSpacing);

        if (widthAtMaxSpacing <= panelWidth)
        {
            _LayoutGroup.spacing = _MaxCardSpacing; // NEW: plenty of room - use the normal, comfortable spacing
            return;
        }

        float neededSpacing = (panelWidth - (cardCount * cardWidth)) / (cardCount - 1); // NEW: the gap that makes everything exactly fit (can go negative, meaning overlap)
        float minSpacing = -(cardWidth * _MaxOverlapFraction); // NEW: don't let cards overlap more than this, even if there isn't enough room

        _LayoutGroup.spacing = Mathf.Max(neededSpacing, minSpacing);
    }

    public void PinCard(Card card)
    {
        foreach (CardDisplay display in _CurrentDisplays)
        {
            if (display._RepresentedCard == card)
            {
                display.GetComponent<CardHoverEffect>().Pin();
                return;
            }
        }
    }

    public void UnpinAllCards()
    {
        foreach (CardDisplay display in _CurrentDisplays)
        {
            display.GetComponent<CardHoverEffect>().Unpin();
        }
    }

    public void UnpinCard(Card card) // NEW: un-highlight a single card without touching any other pinned/selected cards - used to toggle one Cleanse selection off
    {
        foreach (CardDisplay display in _CurrentDisplays)
        {
            if (display._RepresentedCard == card)
            {
                display.GetComponent<CardHoverEffect>().Unpin();
                return;
            }
        }
    }
}