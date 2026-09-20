using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

// NEW: hover highlight for board cells - scales the card up slightly in place while the mouse is
// over it, instead of lifting it like a hand card (board cards sit in a tight grid with spacing,
// so a position lift risks visually overlapping the row above it - scaling in place doesn't).
public class BoardCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float _HoverScale = 1.08f;
    public float _ScaleDuration = 0.12f;

    private RectTransform _RectTransform;
    private Vector3 _RestScale = Vector3.one;
    private Coroutine _ActiveScaleAnimation;
    private CardDisplay _CardDisplay; // NEW: read _RepresentedCell/_Owner off this to decide, live, whether hovering this exact cell should currently be allowed

    void Awake()
    {
        _RectTransform = GetComponent<RectTransform>();
        _RestScale = _RectTransform.localScale; // NEW: captured once at spawn - each board card is a freshly instantiated object every refresh, so this is always its true starting scale
        _CardDisplay = GetComponent<CardDisplay>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsHoverAllowed()) // NEW: only enemy cells get the hover highlight by default - an own cell only gets it while it's actually a legal Heal or Shield target the player currently has the means to use
        {
            return;
        }
        AnimateTo(_RestScale * _HoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(_RestScale); // safe even if hover was never allowed in the first place - the card never scaled up, so this is a harmless no-op lerp back to the same rest scale
    }

    private bool IsHoverAllowed() // NEW: re-checked on every hover (not cached at spawn) since which cells are legal Heal/Shield targets can change turn to turn, or the instant a wildcard branch is chosen
    {
        GridCell cell = _CardDisplay != null ? _CardDisplay._RepresentedCell : null;
        PlayerState cellOwner = _CardDisplay != null ? _CardDisplay._Owner : null;
        GameTester gameTester = (TurnController._Instance != null) ? TurnController._Instance._GameTester : null;

        if (cell == null || cellOwner == null || gameTester == null)
        {
            return true; // safety fallback - can't determine ownership, so don't suppress the effect entirely
        }

        GameState game = gameTester.GetGame();
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;
        Card pendingCard = TurnController._Instance.GetPendingCard(); // NEW: fetched up front now - also needed by the enemy-cell check below, not just the own-cell one

        // NEW: Heal and Shield only ever target the player's OWN board - while either is actually
        // in progress (a Heal-branch wildcard or a Shield card is pending, or the forced
        // turn-start Healer choice is up), enemy cells aren't legal targets for anything right now,
        // so they shouldn't hover either.
        bool usingHealOrShieldRightNow = game._AwaitingHealerChoice
            || (pendingCard is UtilityCard activeUtility && (activeUtility._Type == UtilityType.Shield || activeUtility._ChosenBranch == CardBranch.Heal));

        if (cellOwner != activePlayer)
        {
            return !usingHealOrShieldRightNow; // default: any enemy cell (relative to whoever's turn it is) is hoverable, UNLESS a Heal/Shield action is currently in progress
        }

        // own cell - only hoverable while it's a legal Heal target the player currently has the
        // means to use (an active Healer ship, or a Heal-branch wildcard actually chosen right now),
        // or a legal Shield target with a Shield card actually chosen right now
        bool isLegalHealTarget = cell._Revealed && cell._DamageInstances.Count > 0 && !cell.IsSunk();
        bool canHealRightNow = activePlayer.HasActiveShip(ShipType.PatrolBoat) || (pendingCard is UtilityCard healCard && healCard._ChosenBranch == CardBranch.Heal);
        if (isLegalHealTarget && canHealRightNow)
        {
            return true;
        }

        bool isLegalShieldTarget = cell._Revealed && cell._Ship != ShipType.None;
        bool isShieldCardPending = pendingCard is UtilityCard shieldCard && shieldCard._Type == UtilityType.Shield;
        if (isLegalShieldTarget && isShieldCardPending)
        {
            return true;
        }

        return false;
    }

    private void AnimateTo(Vector3 target)
    {
        if (_ActiveScaleAnimation != null)
        {
            StopCoroutine(_ActiveScaleAnimation);
        }
        _ActiveScaleAnimation = StartCoroutine(ScaleRoutine(target));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = _RectTransform.localScale;
        float t = 0f;
        while (t < _ScaleDuration)
        {
            if (_RectTransform == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _ScaleDuration));
            _RectTransform.localScale = Vector3.LerpUnclamped(start, target, p);
            yield return null;
        }
        if (_RectTransform != null)
        {
            _RectTransform.localScale = target;
        }
        _ActiveScaleAnimation = null;
    }
}
