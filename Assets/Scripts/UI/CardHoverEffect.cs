using UnityEngine;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float _HoverLift = 40f;

    private RectTransform _RectTransform;
    private Vector2 _RestPosition;
    private bool _HasRestPosition = false;
    private bool _Pinned = false; 

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

        AudioManager.Instance?.PlaySlideSFX(); // NEW: light hover feedback when a hand card lifts
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_Pinned)
        {
            return;
        }
        _RectTransform.anchoredPosition = _RestPosition;
    }

    public void Pin() 
    {
        if (!_HasRestPosition)
        {
            _RestPosition = _RectTransform.anchoredPosition;
            _HasRestPosition = true;
        }
        _Pinned = true;
        _RectTransform.anchoredPosition = _RestPosition + new Vector2(0f, _HoverLift);
    }

    public void Unpin()
    {
        _Pinned = false;
        if (_HasRestPosition) 
        {
            _RectTransform.anchoredPosition = _RestPosition;
        }
    }
}