using UnityEngine;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float _HoverLift = 40f;

    private RectTransform _RectTransform;
    private Vector2 _RestPosition;
    private bool _HasRestPosition = false;

    void Awake()
    {
        _RectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_HasRestPosition)
        {
            _RestPosition = _RectTransform.anchoredPosition;
            _HasRestPosition = true;
        }

        _RectTransform.anchoredPosition = _RestPosition + new Vector2(0f, _HoverLift);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _RectTransform.anchoredPosition = _RestPosition;
    }
}
