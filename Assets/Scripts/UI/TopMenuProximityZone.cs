using UnityEngine;

public class TopMenuProximityZone : MonoBehaviour // NEW: same pattern as HandProximityZone, but reveals the in-game menu when the cursor hovers near the top edge of the screen
{
    public RectTransform _MenuPanel;
    public RectTransform _ZoneArea;
    public float _RestHeight = 20f;    // how tall the hover-trigger strip is at the very top of the screen while the menu is hidden
    public float _RaisedHeight = 150f; // how tall the zone becomes once the menu is showing (roughly the menu panel's own height), so moving onto the buttons doesn't retract it - also how far down the panel slides

    private Vector2 _HiddenPosition;
    private bool _IsRaised = false;
    private bool _PinnedRaised;
    public float _EaseSpeed = 10f;   // NEW: higher = snappier ease, lower = slower/floatier slide
    private Vector2 _TargetPosition; // NEW: where the panel is currently trying to reach

    void Start()
    {
        _HiddenPosition = _MenuPanel.anchoredPosition;
        _TargetPosition = _HiddenPosition; // NEW: start already aiming at hidden
        _ZoneArea.sizeDelta = new Vector2(_ZoneArea.sizeDelta.x, _RestHeight);
    }

    private bool _Disabled; // NEW: true while a win/lose screen is showing - keeps the menu from opening at all

    void Update()
    {
        bool wantRaised;

        if (_Disabled) // NEW: never show while disabled, regardless of pinning or hover
        {
            wantRaised = false;
        }
        else if (_PinnedRaised)
        {
            wantRaised = true;
        }
        else
        {
            wantRaised = RectTransformUtility.RectangleContainsScreenPoint(_ZoneArea, Input.mousePosition);
        }

        SetRaised(wantRaised);

        if (PlayerPrefs.GetInt(GameplaySettings.ReduceMotionKey, 0) == 1) // NEW: Reduce Motion - snap straight to the target instead of easing into it
        {
            _MenuPanel.anchoredPosition = _TargetPosition;
        }
        else
        {
            float t = 1f - Mathf.Exp(-_EaseSpeed * Time.deltaTime);
            _MenuPanel.anchoredPosition = Vector2.Lerp(_MenuPanel.anchoredPosition, _TargetPosition, t);
        }
    }

    public void DisableMenu() // NEW
    {
        _Disabled = true;
    }

    public void EnableMenu() // NEW
    {
        _Disabled = false;
    }

    private void SetRaised(bool raised)
    {
        if (raised == _IsRaised)
        {
            return;
        }
        _IsRaised = raised;

        float targetHeight = raised ? _RaisedHeight : _RestHeight;
        _ZoneArea.sizeDelta = new Vector2(_ZoneArea.sizeDelta.x, targetHeight);

        float shownOffset = raised ? _RaisedHeight : 0f;
        _TargetPosition = _HiddenPosition - new Vector2(0f, shownOffset); // CHANGED: sets the target instead of moving the panel directly, so Update()'s ease actually has something correct to ease toward
    }

    public void PinMenu() // NEW: lets other code force the menu to stay open if needed later
    {
        _PinnedRaised = true;
    }

    public void UnpinMenu()
    {
        _PinnedRaised = false;
    }
}