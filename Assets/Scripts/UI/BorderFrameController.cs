using UnityEngine;

// NEW: a single reusable border-frame overlay. One instance is used for hover, a second (different color)
// for the persistent "selected" frame. Both work the same way: SetParent onto whatever RectTransform
// should be framed, stretch to fill it exactly via anchors, and show/hide.
public class BorderFrameController : MonoBehaviour
{
    private RectTransform _RectTransform;

    void Awake()
    {
        _RectTransform = GetComponent<RectTransform>();
        // CHANGED: no longer force-hides here. If another object's Awake() (e.g. BackgroundSettings, restoring
        // the saved background on launch) calls ShowOn() before THIS Awake() has run, Unity doesn't guarantee
        // which one fires first - and hiding here unconditionally could wipe out a ShowOn() that already happened.
        // If you want this hidden by default until first used, just uncheck the GameObject's active checkbox in the Editor.
    }

    private RectTransform GetRectTransform() // NEW: safe to call even if this object's own Awake() hasn't run yet
    {
        if (_RectTransform == null)
        {
            _RectTransform = GetComponent<RectTransform>();
        }
        return _RectTransform;
    }

    public void ShowOn(RectTransform target)
    {
        _RectTransform = GetRectTransform();
        _RectTransform.SetParent(target, false);
        _RectTransform.anchorMin = Vector2.zero;
        _RectTransform.anchorMax = Vector2.one;
        _RectTransform.offsetMin = Vector2.zero;
        _RectTransform.offsetMax = Vector2.zero;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
