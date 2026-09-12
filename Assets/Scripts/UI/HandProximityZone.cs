using UnityEngine;

public class HandProximityZone : MonoBehaviour
{
    public RectTransform _HandPanel;
    public RectTransform _ZoneArea;
    public float _LiftAmount = 120f;

    private Vector2 _RestPosition;
    private bool _IsRaised = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _RestPosition = _HandPanel.anchoredPosition;
    }

    // Update is called once per frame
    void Update()
    {
        bool mouseInZone = RectTransformUtility.RectangleContainsScreenPoint(_ZoneArea, Input.mousePosition);

        if(mouseInZone && !_IsRaised)
        {
            _HandPanel.anchoredPosition = _RestPosition + new Vector2(0f, _LiftAmount);
            _IsRaised = true;
        }
        else if (!mouseInZone && _IsRaised)
        {
            _HandPanel.anchoredPosition = _RestPosition;
            _IsRaised = false;
        }
    }
}
