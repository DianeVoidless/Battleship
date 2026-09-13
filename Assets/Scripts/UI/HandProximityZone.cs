using UnityEngine;

public class HandProximityZone : MonoBehaviour
{
    public RectTransform _HandPanel;
    public RectTransform _ZoneArea;
    public float _RestHeight = 91.432f;   
    public float _RaisedHeight = 317f;    

    private Vector2 _RestPosition;
    private bool _IsRaised = false;
    private bool _ForceLowered;
    private bool _PinnedRaised; 

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
        _ZoneArea.sizeDelta = new Vector2(_ZoneArea.sizeDelta.x, targetHeight);

        float heightDelta = targetHeight - _RestHeight;
        _HandPanel.anchoredPosition = _RestPosition + new Vector2(0f, heightDelta);
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