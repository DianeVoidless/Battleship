using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections; // NEW: for the ease-in/out lift coroutine

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float _HoverLift = 40f;
    public float _LiftDuration = 0.15f; // NEW: how long the rise/lower takes to ease through, instead of snapping instantly

    private RectTransform _RectTransform;
    private Vector2 _RestPosition;
    private bool _HasRestPosition = false;
    private bool _Pinned = false;
    private Coroutine _ActiveLiftAnimation; // NEW: so a quick in/out hover doesn't fight itself with two overlapping animations

    void Awake()
    {
        _RectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureRestPosition();
        AnimateTo(_RestPosition + new Vector2(0f, _HoverLift));

        AudioManager.Instance?.PlaySlideSFX(); // NEW: light hover feedback when a hand card lifts
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_Pinned)
        {
            return;
        }
        AnimateTo(_RestPosition);
    }

    public void Pin()
    {
        EnsureRestPosition();
        _Pinned = true;
        AnimateTo(_RestPosition + new Vector2(0f, _HoverLift));
    }

    public void Unpin()
    {
        _Pinned = false;
        if (_HasRestPosition)
        {
            AnimateTo(_RestPosition);
        }
    }

    private void EnsureRestPosition() // NEW: shared - was duplicated inline in OnPointerEnter and Pin
    {
        if (!_HasRestPosition)
        {
            _RestPosition = _RectTransform.anchoredPosition;
            _HasRestPosition = true;
        }
    }

    private void AnimateTo(Vector2 target) // NEW: eases the card's position toward target over _LiftDuration instead of snapping instantly, restarting cleanly if a rapid hover in/out interrupts a lift already in progress
    {
        if (_ActiveLiftAnimation != null)
        {
            StopCoroutine(_ActiveLiftAnimation);
        }
        _ActiveLiftAnimation = StartCoroutine(LiftRoutine(target));
    }

    private IEnumerator LiftRoutine(Vector2 target)
    {
        Vector2 start = _RectTransform.anchoredPosition;
        float t = 0f;
        while (t < _LiftDuration)
        {
            if (_RectTransform == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _LiftDuration));
            _RectTransform.anchoredPosition = Vector2.LerpUnclamped(start, target, p);
            yield return null;
        }
        if (_RectTransform != null)
        {
            _RectTransform.anchoredPosition = target;
        }
        _ActiveLiftAnimation = null;
    }
}