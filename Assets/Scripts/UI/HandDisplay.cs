using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class HandDisplay : MonoBehaviour
{
    public GameObject _CardDisplayPrefab;
    public CardArtDatabase _ArtDatabase;

    private List<CardDisplay> _CurrentDisplays = new List<CardDisplay>();

    public void ShowHand(PlayerState owner) 
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        _CurrentDisplays.Clear();

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