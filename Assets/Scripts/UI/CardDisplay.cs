using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public Image _CardImage;

    public void SetSprite(Sprite sprite)
    {
        _CardImage.sprite = sprite;
    }
}