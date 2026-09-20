using UnityEngine;
using System.Collections; // NEW: for the hand-panel ease in/out coroutine

public class HandProximityZone : MonoBehaviour
{
    public RectTransform _HandPanel;
    public RectTransform _ZoneArea;
    public float _RestHeight = 91.432f;
    public float _RaisedHeight = 317f;
    public float _RaiseDuration = 0.2f; // NEW: how long the whole hand takes to ease up/down, instead of popping instantly

    private Vector2 _RestPosition;
    private bool _IsRaised = false;
    private bool _ForceLowered;
    private bool _PinnedRaised;
    private Coroutine _ActiveRaiseAnimation; // NEW: so rapid raise/lower toggling (e.g. mouse skimming the zone edge) doesn't leave two animations fighting each other

    void Start()
    {
        _RestPosition = _HandPanel.anchoredPosition;
        _ZoneArea.sizeDelta = new Vector2(_ZoneArea.sizeDelta.x, _RestHeight); 
    }

    void Update()
    {
        bool wantRaised; 

        if (_PinnedRaised)
        {
            wantRaised = true;
        }
        else if (_ForceLowered)
        {
            wantRaised = false;
        }
        else
        {
            wantRaised = RectTransformUtility.RectangleContainsScreenPoint(_ZoneArea, Input.mousePosition);
        }

        SetRaised(wantRaised); 
    }

    private void SetRaised(bool raised)
    {
        if (raised == _IsRaised)
        {
            return;
        }
        _IsRaised = raised;

        float targetHeight = raised ? _RaisedHeight : _RestHeight;
        _ZoneArea.sizeDelta = new Vector2(_ZoneArea.sizeDelta.x, targetHeight); // CHANGED: kept instant - this is just the hit-test area the mouse check uses, not something the player actually sees, so it should snap to its new bounds right away rather than easing in sync with the visual panel

        float heightDelta = targetHeight - _RestHeight;
        Vector2 target = _RestPosition + new Vector2(0f, heightDelta);

        if (_ActiveRaiseAnimation != null) // CHANGED: was an instant assignment - now eases the whole hand panel toward its new position instead of popping
        {
            StopCoroutine(_ActiveRaiseAnimation);
        }
        _ActiveRaiseAnimation = StartCoroutine(AnimateHandPanel(target));
    }

    private IEnumerator AnimateHandPanel(Vector2 target) // NEW: eases the hand panel's Y position toward target over _RaiseDuration
    {
        Vector2 start = _HandPanel.anchoredPosition;
        float t = 0f;
        while (t < _RaiseDuration)
        {
            if (_HandPanel == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _RaiseDuration));
            _HandPanel.anchoredPosition = Vector2.LerpUnclamped(start, target, p);
            yield return null;
        }
        if (_HandPanel != null)
        {
            _HandPanel.anchoredPosition = target;
        }
        _ActiveRaiseAnimation = null;
    }

    public void PinHand()
    {
        _PinnedRaised = true;
    }

    public void UnpinHand()
    {
        _PinnedRaised = false;
    }

    public void ForceLowerHand()
    {
        _ForceLowered = true;
    }

    public void ReleaseForceLowerHand()
    {
        _ForceLowered = false;
    }
}