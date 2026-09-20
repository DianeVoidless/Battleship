using UnityEngine;
using UnityEngine.EventSystems;

// NEW: drop this on any button that should show the hover border. Add it to all 21 wood swatch
// buttons at once (multi-select them all, Add Component, drag the shared HoverFrame in once).
public class HoverFrameTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public BorderFrameController _HoverFrame; // drag the shared HoverFrame object here (same one on every button)

    private RectTransform _RectTransform;

    void Awake()
    {
        _RectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _HoverFrame.ShowOn(_RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _HoverFrame.Hide();
    }
}
