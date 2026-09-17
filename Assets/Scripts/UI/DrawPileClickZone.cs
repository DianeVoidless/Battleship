using UnityEngine;
using UnityEngine.EventSystems;

public class DrawPileClickZone : MonoBehaviour, IPointerClickHandler // NEW: put this on each player's draw pile art so clicking it can confirm a Cleanse selection
{
    public PlayerColor _Owner; // NEW: which player's draw pile this is - set in the Inspector to Red or Blue

    public void OnPointerClick(PointerEventData eventData)
    {
        TurnController._Instance.OnDrawPileClicked(_Owner);
    }
}
