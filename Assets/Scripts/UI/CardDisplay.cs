using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardDisplay : MonoBehaviour, IPointerClickHandler
{
    public Image _CardImage;
    public Card _RepresentedCard;
    public GridCell _RepresentedCell;

    public void SetSprite(Sprite sprite)
    {
        _CardImage.sprite = sprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(_RepresentedCard != null)
        {
            TurnController._Instance.OnCardClicked(this, _RepresentedCard);
        }
        else if(_RepresentedCell  != null)
        {
            TurnController._Instance.OnCellClicked(this, _RepresentedCell);
        }
    }
}