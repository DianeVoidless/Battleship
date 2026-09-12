using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class HandDisplay : MonoBehaviour
{
    public GameObject _CardDisplayPrefab;
    public CardArtDatabase _ArtDatabase;

    public void ShowHand(List<Card> hand, PlayerColor owner)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        foreach (Card card in hand)
        {
            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetCardSprite(card, owner);
            display.SetSprite(sprite);

            cardObject.AddComponent<CardHoverEffect>();
        }
    }
}
