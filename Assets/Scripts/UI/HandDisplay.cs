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

    public void ShowHand(PlayerState owner)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        _CurrentDisplays.Clear();

        UpdateCardSpacing(owner._Hand.Count); // NEW: shrink the gap between cards (and let them overlap) once they no longer fit at the normal spacing

        foreach (Card card in owner._Hand)
        {
            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetCardSprite(card, owner._Color); 
            display.SetSprite(sprite);
            display._RepresentedCard = card;
            display._ArtDatabase = _ArtDatabase;
            display._Owner = owner;
            display.RefreshWildcardSprite();

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
}