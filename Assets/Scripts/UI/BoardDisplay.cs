using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class BoardDisplay : MonoBehaviour
{
    public GameObject _CardDisplayPrefab;
    public CardArtDatabase _ArtDatabase;

    private const int _Columns = 4; // must match the Grid Layout Group's Fixed Column Count on this board's panel

    [Header("NEW: deck slide-in (used once at match start, before any board cards exist)")]
    public Sprite _DeckBackSprite;         // drag the face-down "BATTLESHIP" card-back art here
    public float _DeckSlideDistance = 900f; // how far below its resting spot the deck starts, before sliding up into view
    public float _DeckSlideDuration = 0.4f; // how long the slide-in takes
    public float _ShuffleDuration = 0.6f;   // CHANGED: no longer used by the slide-in shuffle - kept for WiggleDeckCard, reserved for the later table-card distribution animation

    [Header("NEW: riffle shuffle (plays once, right after the deck slides in)")]
    public int _ShuffleCardCount = 3;            // how many cards peel out from behind and land back on top
    public float _ShuffleSideOffset = 100f;      // how far sideways each card slides before coming back on top
    public float _ShuffleCardSlideDuration = 0.08f; // CHANGED: faster - how long each of the two slide legs (out, then in) takes, per card

    [Header("NEW: dealing the grid out (one card at a time, right after the shuffle finishes)")]
    public float _DealCardDuration = 0.25f; // how long each card takes to fly from the deck into its grid cell

    [Header("CHANGED: hand deck slide-in (slides in at the SAME TIME as the ship deck, settles off to the side, and shuffles continuously until the ship-card grid finishes dealing)")]
    public Sprite _HandDeckBackSprite; // drag the face-down card-back art for the hand/utility deck here (can be the same sprite as _DeckBackSprite if there's only one card back)
    public Vector2 _HandDeckRestingOffset = new Vector2(-750f, 0f); // CHANGED: per-board value - set this to -750 on RedBoardPanel (comes in from the left) and +750 on BlueBoardPanel (comes in from the right). This is the "un-rotated" side for this board; SlideHandDeckIn automatically mirrors the X when rotate180 is true (i.e. when this board is being shown as the far/opponent board), so it stays on the correct visual side no matter which player's POV you're viewing from

    [Header("NEW: dealing the hand out (5 cards, right after the hand deck's continuous shuffle stops) - spits toward the screen edge on this player's own side, down for near/bottom, up for far/top")]
    public int _HandDealCount = 5;
    public float _HandDealDistance = 700f; // how far off-screen the far/opponent player's cards travel before vanishing
    public float _HandCardRestDistance = 400f; // CHANGED: fallback only - used when _HandPanelRect isn't assigned. Prefer assigning _HandPanelRect below so the cards land exactly where the real hand sits instead of a hand-tuned guess.
    public float _HandRowWidth = 700f; // CHANGED: fallback only (same reason as above) - used when _HandPanelRect isn't assigned
    public float _HandDealDuration = 0.25f; // how long each card takes to fly

    [Header("NEW: where the viewed player's revealed hand cards should visually land - drag the actual HandPanel's RectTransform here so the flourish cards land EXACTLY where HandDisplay's real cards will appear, eliminating the visual jump when they're swapped in")]
    public RectTransform _HandPanelRect;

    [Header("NEW: draw pile - after the 5 hand cards are dealt, one last face-down card slides from the hand deck into THIS board's own real DrawPile object (the static, pre-placed card art already in the scene) - drag that board's own DrawPile RectTransform here (the Red one for RedBoardPanel, the Blue one for BlueBoardPanel)")]
    public RectTransform _DrawPileRect;

    [Header("NEW: enemy board reveal flip - plays on a board cell the instant an attack card reveals it for the first time")]
    public float _RevealFlipDuration = 0.25f; // total time for the flip (half shrinking down to edge-on, half growing back out) - the sprite swap happens right at the midpoint, while the card is edge-on and invisible
    public float _SubmarineDiscoveredSoundDelay = 0.35f; // NEW: how long to hold the Submarine-discovered sting back from the flip's midpoint, so it doesn't get buried under the missile impact sound's own tail - tweak this if it still feels too close together or starts feeling laggy

    [Header("NEW: ship-sunk shockwave - plays on the (up to) four cards directly adjacent to a ship the instant it's sunk, each rising slightly outward in its own direction")]
    public float _ShockwaveRiseDistance = 14f; // how far the neighboring card rises, in its own outward direction, at the shake's peak
    public float _ShockwaveDuration = 0.25f; // how long the whole rise-and-settle takes, per neighboring card

    [Header("NEW: discard pile reshuffle - when the draw pile empties, the WHOLE discard pile visibly gathers at the center of this board (same spot the original deck slide-in used), riffle-shuffles just like the very first deal, then slides into the draw pile spot to become the new draw pile")]
    public float _ReshuffleTravelDuration = 0.3f; // how long each of the two travel legs takes (discard spot -> center, then center -> draw pile spot)

    public event System.Action<PlayerState> OnHandRevealed; // NEW: fired once, right when the viewed player's 5-card hand-deal flourish finishes
    public event System.Action<PlayerState> OnDrawPileRevealed; // NEW: fired once, right when the leftover hand-deck card finishes sliding into the draw pile spot - GameTester listens for this and activates the REAL, pre-placed DrawPile art at that moment

    private GameObject _DeckCardObject; // NEW: the single face-down card representing this board's deck, kept around so a later step (shuffle + deal) can reuse it
    private GameObject _HandDeckCardObject; // NEW: the face-down hand deck card that slides in alongside the ship deck and sits to the side, shuffling until the grid finishes dealing
    private MonoBehaviour _CoroutineRunner; // NEW: kept so ClearBoard can stop an in-flight slide/shuffle if a match resets mid-animation
    private Coroutine _ActiveDeckAnimation;
    private Coroutine _ActiveHandDeckAnimation; // NEW
    private bool _KeepShufflingHandDeck; // NEW: true while the hand deck should keep looping its riffle shuffle - set false once the ship-card grid finishes dealing, which lets the CURRENT riffle cycle finish before stopping (no abrupt cut)

    public void ClearBoard() // NEW: removes any dealt cards (and the deck, if present) without drawing new ones - used right when a match begins, before any cards exist yet
    {
        if (_ActiveDeckAnimation != null && _CoroutineRunner != null)
        {
            _CoroutineRunner.StopCoroutine(_ActiveDeckAnimation);
            _ActiveDeckAnimation = null;
        }
        if (_ActiveHandDeckAnimation != null && _CoroutineRunner != null) // NEW
        {
            _CoroutineRunner.StopCoroutine(_ActiveHandDeckAnimation);
            _ActiveHandDeckAnimation = null;
        }
        _KeepShufflingHandDeck = false; // NEW

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        _DeckCardObject = null;
        _HandDeckCardObject = null; // NEW
    }

    public void SlideDeckIn(PlayerState player, bool rotate180, MonoBehaviour coroutineRunner) // CHANGED: now also deals the grid out (one card at a time, shove SFX) right after the shuffle finishes
    {
        _CoroutineRunner = coroutineRunner;

        if (_ActiveDeckAnimation != null)
        {
            _CoroutineRunner.StopCoroutine(_ActiveDeckAnimation); // safety - don't let two slide-ins overlap (e.g. a very quick rematch)
            _ActiveDeckAnimation = null;
        }

        if (_DeckCardObject != null)
        {
            Destroy(_DeckCardObject);
        }

        GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
        cardObject.name = "ShipDeckCard"; // NEW: easier to find in the Hierarchy than an unlabeled "CardDisplay(Clone)"
        CardDisplay display = cardObject.GetComponent<CardDisplay>();
        display.SetSprite(_DeckBackSprite);
        display.SetShieldOverlays(null);

        // NEW: this panel has a Grid Layout Group on it, which auto-slots any child into its own
        // cells (a single child lands in cell 0, near the top-left) - ignoring anchoredPosition
        // entirely. Pull this one card out of the Grid Layout Group's control and center it
        // manually instead, so it actually sits in the middle of the panel.
        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = cardObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        RectTransform rect = cardObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // NEW: ignoreLayout also means the Grid Layout Group no longer sizes this card either, so
        // it was falling back to the prefab's own default (portrait) size instead of the board's
        // actual landscape cell shape. Match this panel's own cell size explicitly instead.
        GridLayoutGroup grid = GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            rect.sizeDelta = grid.cellSize;
        }

        Vector2 restingPos = Vector2.zero; // now genuinely the panel's own center
        Vector2 startPos = restingPos + new Vector2(0f, -_DeckSlideDistance);
        rect.anchoredPosition = startPos;

        _DeckCardObject = cardObject;
        _ActiveDeckAnimation = coroutineRunner.StartCoroutine(SlideShuffleThenDeal(rect, startPos, restingPos, player, rotate180));

        SlideHandDeckIn(coroutineRunner, rotate180, player); // CHANGED: the hand deck now slides in at the SAME TIME as the ship deck, instead of waiting for it to finish dealing - rotate180 is passed through so its side mirrors correctly when the POV is switched (see SlideHandDeckIn); player is passed through so the viewed player's hand-deal can show their ACTUAL hand cards face-up
    }

    private IEnumerator SlideShuffleThenDeal(RectTransform rect, Vector2 from, Vector2 to, PlayerState player, bool rotate180)
    {
        AudioManager.Instance?.PlaySlideSFX();

        float t = 0f;
        while (t < _DeckSlideDuration)
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _DeckSlideDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        if (rect == null)
        {
            yield break;
        }
        rect.anchoredPosition = to;

        yield return RiffleShuffleDeck(to, rect.sizeDelta, _DeckBackSprite); // CHANGED: riffle cards now match this deck's own current size and sprite (landscape ship-deck back)

        yield return DealFromDeck(player, rotate180, to); // once the shuffle settles, deal the actual grid out of the same deck spot

        _ActiveDeckAnimation = null;

        _KeepShufflingHandDeck = false; // CHANGED: the grid is fully dealt now, so let the hand deck's current riffle cycle finish, then stop and settle
    }

    public void SlideHandDeckIn(MonoBehaviour coroutineRunner, bool rotate180, PlayerState player) // CHANGED: slides a face-down hand deck into view (at the same time as the ship deck), settling off to the side, then shuffles continuously until told to stop - rotate180 mirrors which side it settles on; player is threaded through so the eventual hand-deal can show real card faces for the viewed player
    {
        _CoroutineRunner = coroutineRunner;

        if (_ActiveHandDeckAnimation != null)
        {
            _CoroutineRunner.StopCoroutine(_ActiveHandDeckAnimation);
            _ActiveHandDeckAnimation = null;
        }

        if (_HandDeckCardObject != null)
        {
            Destroy(_HandDeckCardObject);
        }

        GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
        cardObject.name = "HandDeckCard"; // NEW: so it's easy to spot in the Hierarchy instead of blending in as just another identical "CardDisplay(Clone)" among the 12 grid cards
        CardDisplay display = cardObject.GetComponent<CardDisplay>();
        display.SetSprite(_HandDeckBackSprite);
        display.SetShieldOverlays(null);

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = cardObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        RectTransform rect = cardObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // CHANGED: unlike the ship deck (which matches the board's landscape grid cells), the hand
        // deck art is a normal portrait card - so use the CardDisplayPrefab's own natural (portrait)
        // size here instead of stretching it to the grid's landscape cellSize.
        rect.sizeDelta = _CardDisplayPrefab.GetComponent<RectTransform>().sizeDelta;

        // CHANGED: mirror the side (flip X) when this board is being shown rotate180 (the "far"/opponent
        // board from the current viewer's POV) - so switching which player you're viewing as doesn't
        // leave a hand deck sitting on the visually wrong side.
        Vector2 restingPos = rotate180 ? new Vector2(-_HandDeckRestingOffset.x, _HandDeckRestingOffset.y) : _HandDeckRestingOffset;
        Vector2 startPos = restingPos + new Vector2(0f, -_DeckSlideDistance);
        rect.anchoredPosition = startPos;
        rect.SetAsLastSibling(); // render on top of the already-dealt grid cards underneath it

        _HandDeckCardObject = cardObject;
        _ActiveHandDeckAnimation = coroutineRunner.StartCoroutine(SlideThenShuffleHandDeck(rect, startPos, restingPos, rotate180, player));
    }

    private IEnumerator SlideThenShuffleHandDeck(RectTransform rect, Vector2 from, Vector2 to, bool rotate180, PlayerState player) // CHANGED: same slide-in as the ship deck, then loops the riffle shuffle continuously until told to stop, then spits 5 cards out toward this player's hand
    {
        AudioManager.Instance?.PlaySlideSFX();

        float t = 0f;
        while (t < _DeckSlideDuration)
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _DeckSlideDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        if (rect == null)
        {
            yield break;
        }
        rect.anchoredPosition = to;

        _KeepShufflingHandDeck = true; // NEW: keeps looping the riffle below until the ship-card grid finishes dealing (or ClearBoard cancels it outright)
        while (_KeepShufflingHandDeck)
        {
            yield return RiffleShuffleDeck(to, rect.sizeDelta, _HandDeckBackSprite); // shuffles in place (riffle cards match this deck's own portrait size and sprite) - looped continuously, one full cycle at a time
            if (rect == null)
            {
                yield break;
            }
        }

        yield return DealHandCards(to, rotate180, player); // NEW: once the shuffle actually stops, spit 5 cards out toward this player's hand edge

        _ActiveHandDeckAnimation = null;
    }

    private IEnumerator DealHandCards(Vector2 deckPos, bool rotate180, PlayerState player) // CHANGED: for the near/viewed player, cards now enter FROM below the screen, sliding UP into an on-screen resting spot, showing that player's ACTUAL hand card face-up (not the generic face-down back) - looking like a normal hand instead of a magic trick. The far/opponent player keeps the original "shoot toward the edge, face-down, and disappear" look, since their hand should stay hidden. Either way, the very last card of the 5 stays instantiated instead of being destroyed.
    {
        bool isViewedPlayer = !rotate180; // NEW: this is the board currently shown as "near" (bottom of screen) - i.e. the player whose POV we're watching

        // CHANGED: aim for the horizontal CENTER of that edge (X = 0, the panel's own center line),
        // not straight down/up from wherever the hand deck sits off to the side - so the cards
        // visually converge toward the middle of the screen edge instead of just dropping/rising
        // in a straight line from the deck's own off-center X.
        float offscreenY = deckPos.y + (rotate180 ? _HandDealDistance : -_HandDealDistance); // far board (top) spits upward (+Y), near board (bottom) spits downward (-Y)
        Vector2 offscreenPoint = new Vector2(0f, offscreenY);

        // CHANGED: the viewed player's cards now land wherever the REAL HandPanel actually sits on
        // screen (converted into this board panel's own local space), instead of a hand-tuned offset
        // from the deck - this both fixes "a bit too low" and, more importantly, means there's no
        // jump when these flourish cards get swapped out for HandDisplay's real ones afterward.
        Vector2 handCenter = GetLocalCenterOf(_HandPanelRect);
        bool haveRealHandPanel = _HandPanelRect != null;
        float rowCenterX = haveRealHandPanel ? handCenter.x : 0f;
        float restY = haveRealHandPanel ? handCenter.y : deckPos.y + (rotate180 ? _HandCardRestDistance : -_HandCardRestDistance);
        float rowWidth = haveRealHandPanel ? Mathf.Min(_HandRowWidth, _HandPanelRect.rect.width) : _HandRowWidth;

        List<GameObject> viewedCardObjects = isViewedPlayer ? new List<GameObject>() : null; // NEW: tracks the viewed player's flourish cards so they can be swapped out for the real hand once dealt

        for (int i = 0; i < _HandDealCount; i++)
        {
            // CHANGED: fan the viewed player's cards out into a row across rowWidth, centered on
            // rowCenterX (the real HandPanel's own center when assigned), same idea as a real hand
            // of cards laid out side by side, instead of every card converging on the exact same
            // point and stacking on top of each other.
            float cardOffset = _HandDealCount > 1 ? Mathf.Lerp(-rowWidth / 2f, rowWidth / 2f, (float)i / (_HandDealCount - 1)) : 0f;
            Vector2 restPoint = new Vector2(rowCenterX + cardOffset, restY);

            bool isLastCard = i == _HandDealCount - 1;
            if (isLastCard && _HandDeckCardObject != null) // NEW: same treatment as the ship deck - stays visible as the "pile" until the very last card leaves it
            {
                Destroy(_HandDeckCardObject);
                _HandDeckCardObject = null;
            }

            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            cardObject.name = "HandDealCard";
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            // CHANGED: the viewed player's own cards are face-up, showing their ACTUAL hand card art
            // (same as HandDisplay.ShowHand would) - only the far/opponent player still gets the
            // generic face-down back, since their hand needs to stay hidden.
            if (isViewedPlayer && player != null && player._Hand != null && i < player._Hand.Count)
            {
                Card handCard = player._Hand[i];
                Sprite frontSprite = _ArtDatabase.GetCardSprite(handCard, player); // CHANGED: now passes the whole PlayerState, so the Destroyer-enhanced white missile art can be swapped in while its passive is active
                display.SetSprite(frontSprite);
                display._RepresentedCard = handCard;
                display._ArtDatabase = _ArtDatabase;
                display._Owner = player;
                display.RefreshWildcardSprite();
                display.SetDamageBoostOverlay(_ArtDatabase.GetDamageBoostBadge(handCard, player)); // NEW: matches HandDisplay.ShowHand, so the flourish deal doesn't briefly show a plain card where the real hand would show a boosted one
            }
            else
            {
                display.SetSprite(_HandDeckBackSprite);
                display.SetShieldOverlays(null);
                display.SetDamageBoostOverlay(null);
            }

            LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = cardObject.AddComponent<LayoutElement>();
            }
            layoutElement.ignoreLayout = true;

            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = _CardDisplayPrefab.GetComponent<RectTransform>().sizeDelta;

            // CHANGED: both players' cards now visibly spit OUT of the deck itself (same origin point
            // for all 5) - the opponent's fly on past the edge and vanish (offscreenPoint), while the
            // viewed player's land in their fanned spot in the real hand's position (restPoint). This
            // gives the viewed player the same "thrown from the deck" flourish as the opponent, instead
            // of silently popping up from below the screen.
            Vector2 from = deckPos;
            Vector2 to = isViewedPlayer ? restPoint : offscreenPoint;
            cardRect.anchoredPosition = from;

            if (_HandDeckCardObject != null)
            {
                _HandDeckCardObject.transform.SetAsLastSibling(); // keeps the hand deck rendered ON TOP of every newly-spawned card, same as the ship deck
            }

            if (isViewedPlayer)
            {
                viewedCardObjects.Add(cardObject); // NEW: remembered so it can be cleaned up once the real hand takes over
            }

            // CHANGED: the viewed player's cards ALL stay (they're the actual arranged hand now) -
            // the opponent's fly past the edge and vanish, none kept - the leftover draw-pile card
            // dealt below now covers the "one card survives" flourish instead.
            bool keepThisCard = isViewedPlayer;
            yield return DealOneHandCard(cardRect, from, to, keepThisCard); // one at a time - waits for this card to land before the next one spawns
        }

        if (isViewedPlayer)
        {
            // NEW: these animated cards are just a visual flourish - they're parented under this
            // board panel, not under HandPanel, so they don't have working hover-raise or fit into
            // HandDisplay's own layout/overlap logic. Once the flourish finishes, swap them out for
            // the REAL hand (GameTester listens for OnHandRevealed and calls HandDisplay.ShowHand).
            foreach (GameObject cardObject in viewedCardObjects)
            {
                if (cardObject != null)
                {
                    Destroy(cardObject);
                }
            }
            OnHandRevealed?.Invoke(player);
        }

        // NEW: once the hand is dealt, one last face-down card slides from the same deck spot into
        // this board's own real draw pile position - the leftover of the shuffled deck, becoming the
        // pile players draw from during the match.
        yield return DealDrawPileCard(deckPos, player);
    }

    private IEnumerator DealDrawPileCard(Vector2 deckPos, PlayerState player) // NEW: spits one final face-down card from the hand deck's spot into this board's real DrawPile position, then hands off to the actual pre-placed DrawPile art
    {
        Vector2 pileTarget = GetLocalCenterOf(_DrawPileRect);

        GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
        cardObject.name = "DrawPileCard";
        CardDisplay display = cardObject.GetComponent<CardDisplay>();
        display.SetSprite(_HandDeckBackSprite);
        display.SetShieldOverlays(null);

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = cardObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = _CardDisplayPrefab.GetComponent<RectTransform>().sizeDelta;
        cardRect.anchoredPosition = deckPos;

        yield return DealOneHandCard(cardRect, deckPos, pileTarget, true); // keepAfterArrival: true - stays until swapped for the real DrawPile art right below

        OnDrawPileRevealed?.Invoke(player);

        if (cardObject != null)
        {
            Destroy(cardObject);
        }
    }

    private Vector2 GetLocalCenterOf(RectTransform target) // CHANGED: generalized from GetHandPanelCenterLocal - converts ANY target RectTransform's on-screen center into THIS board panel's own local anchored-position space, regardless of how many parents (and rotations, e.g. a PileGroup's 180-degree flip) sit between them. Used so animated flourish cards can land exactly where a real, already-placed UI element (the HandPanel, a DrawPile) will appear.
    {
        if (target == null)
        {
            return Vector2.zero;
        }

        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

        RectTransform boardRect = (RectTransform)transform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPoint, cam, out localPoint);
        return localPoint;
    }

    private IEnumerator DealOneHandCard(RectTransform card, Vector2 from, Vector2 to, bool keepAfterArrival) // CHANGED: now takes whether to keep this card around once it lands - the viewed player keeps all 5 (they ARE the arranged hand), the opponent still only keeps its last one
    {
        AudioManager.Instance?.PlayShoveSFX();

        float t = 0f;
        while (t < _HandDealDuration)
        {
            if (card == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _HandDealDuration));
            card.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        if (card == null)
        {
            yield break;
        }
        card.anchoredPosition = to; // NEW: snap precisely to the resting spot - matters more now that a kept card might linger there
        if (!keepAfterArrival)
        {
            Destroy(card.gameObject);
        }
    }

    private IEnumerator DealFromDeck(PlayerState player, bool rotate180, Vector2 deckPos) // CHANGED: the deck card now stays visible - on top of the pile - the whole time cards are being dealt out from underneath it, and only disappears once the LAST card is dealt (an "empty deck" look), instead of vanishing upfront
    {
        List<GridCell> cellsToShow = rotate180 ? FlipRowsVertically(player._Grid) : player._Grid;
        GridLayoutGroup grid = GetComponent<GridLayoutGroup>();

        for (int i = 0; i < cellsToShow.Count; i++)
        {
            GridCell cell = cellsToShow[i];

            bool isLastCard = i == cellsToShow.Count - 1;
            if (isLastCard && _DeckCardObject != null) // NEW: the deck's job is done right as the last card is dealt - not before
            {
                Destroy(_DeckCardObject);
                _DeckCardObject = null;
            }

            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetShipSprite(cell, player._Color);
            display.SetSprite(sprite);
            display._RepresentedCell = cell;
            display._Owner = player; // NEW: BoardCardHoverEffect needs to know which player this cell belongs to, to tell an enemy cell from an own cell
            cardObject.AddComponent<BoardCardHoverEffect>(); // NEW: hover highlight for this board cell

            List<Sprite> shieldSprites = _ArtDatabase.GetShieldSprites(cell, player._Color);
            display.SetShieldOverlays(shieldSprites);

            // NEW: same treatment as the deck/riffle cards - pulled out of the Grid Layout Group's
            // control and centered manually, so its position is fully deterministic instead of
            // depending on the layout system resolving a brand-new child in time.
            LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = cardObject.AddComponent<LayoutElement>();
            }
            layoutElement.ignoreLayout = true;

            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            if (grid != null)
            {
                cardRect.sizeDelta = grid.cellSize;
            }

            if (_DeckCardObject != null)
            {
                _DeckCardObject.transform.SetAsLastSibling(); // NEW: keeps the deck rendered ON TOP of every newly-instantiated card, so it visually stays the "pile" that cards emerge from underneath
            }

            Vector2 target = ComputeCellAnchoredPosition(i, grid);
            cardRect.anchoredPosition = deckPos;

            yield return DealOneCard(cardRect, deckPos, target); // one at a time - waits for this card to land before the next is even instantiated
        }
    }

    private Vector2 ComputeCellAnchoredPosition(int index, GridLayoutGroup grid) // NEW: computes cell i's true center directly from the Grid Layout Group's own settings, in the same center-anchored coordinate space the deck card uses - assumes the default Upper Left corner / Horizontal start axis
    {
        RectTransform panelRect = (RectTransform)transform;
        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;

        float cellWidth = grid != null ? grid.cellSize.x : panelWidth / _Columns;
        float cellHeight = grid != null ? grid.cellSize.y : panelHeight / 3f;
        float spacingX = grid != null ? grid.spacing.x : 0f;
        float spacingY = grid != null ? grid.spacing.y : 0f;
        float paddingLeft = grid != null ? grid.padding.left : 0f;
        float paddingTop = grid != null ? grid.padding.top : 0f;

        int row = index / _Columns;
        int col = index % _Columns;

        float xTopLeft = paddingLeft + col * (cellWidth + spacingX);
        float yTopLeft = paddingTop + row * (cellHeight + spacingY);

        float centerX = (xTopLeft + cellWidth / 2f) - panelWidth / 2f;
        float centerY = panelHeight / 2f - (yTopLeft + cellHeight / 2f);

        return new Vector2(centerX, centerY);
    }

    private IEnumerator DealOneCard(RectTransform card, Vector2 from, Vector2 to) // CHANGED: back to anchoredPosition
    {
        AudioManager.Instance?.PlayShoveSFX();

        float t = 0f;
        while (t < _DealCardDuration)
        {
            if (card == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _DealCardDuration));
            card.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        if (card != null)
        {
            card.anchoredPosition = to;
        }
    }

    private IEnumerator RiffleShuffleDeck(Vector2 restPos, Vector2 cardSize, Sprite backSprite) // CHANGED: now takes the shuffling deck's own size and back sprite instead of always assuming the ship deck's landscape cellSize/sprite - so this same routine looks right whether it's shuffling the ship deck or the hand deck
    {
        float totalDuration = _ShuffleCardCount * _ShuffleCardSlideDuration * 2f; // NEW: matches the actual visual length (out + in, per card) so the sound doesn't outlast it
        AudioManager.Instance?.PlayShuffleSFX(totalDuration);

        for (int i = 0; i < _ShuffleCardCount; i++)
        {
            GameObject riffleCard = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay riffleDisplay = riffleCard.GetComponent<CardDisplay>();
            riffleDisplay.SetSprite(backSprite);
            riffleDisplay.SetShieldOverlays(null);

            LayoutElement riffleLayout = riffleCard.GetComponent<LayoutElement>();
            if (riffleLayout == null)
            {
                riffleLayout = riffleCard.AddComponent<LayoutElement>();
            }
            riffleLayout.ignoreLayout = true;

            RectTransform riffleRect = riffleCard.GetComponent<RectTransform>();
            riffleRect.anchorMin = new Vector2(0.5f, 0.5f);
            riffleRect.anchorMax = new Vector2(0.5f, 0.5f);
            riffleRect.pivot = new Vector2(0.5f, 0.5f);
            riffleRect.sizeDelta = cardSize;

            riffleRect.anchoredPosition = restPos;
            riffleRect.SetSiblingIndex(0); // NEW: starts BEHIND the currently-visible deck card

            Vector2 sideOffset = restPos + new Vector2(i % 2 == 0 ? -_ShuffleSideOffset : _ShuffleSideOffset, 0f); // alternate left/right so it doesn't look mechanical

            yield return SlideRect(riffleRect, restPos, sideOffset, _ShuffleCardSlideDuration);
            if (riffleRect == null)
            {
                continue;
            }
            riffleRect.SetAsLastSibling(); // NEW: now slides back ON TOP of the pile
            yield return SlideRect(riffleRect, sideOffset, restPos, _ShuffleCardSlideDuration);

            Destroy(riffleCard); // visually identical to the persistent deck card underneath, safe to discard once it lands
        }
    }

    private IEnumerator SlideRect(RectTransform rect, Vector2 from, Vector2 to, float duration) // NEW: small shared position-lerp helper used by the riffle shuffle
    {
        float t = 0f;
        while (t < duration)
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        if (rect != null)
        {
            rect.anchoredPosition = to;
        }
    }

    private IEnumerator WiggleDeckCard(RectTransform card, Vector2 restPos, float duration) // KEPT: not called by the slide-in anymore - reserved as the animation for distributing the table cards later
    {
        AudioManager.Instance?.PlayShuffleSFX();

        float t = 0f;
        while (t < duration)
        {
            if (card == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float wobble = Mathf.Sin(t * 18f) * 6f; // degrees
            card.localEulerAngles = new Vector3(0f, 0f, wobble);
            card.anchoredPosition = restPos + new Vector2(Mathf.Sin(t * 22f) * 4f, 0f);
            yield return null;
        }
        if (card == null)
        {
            yield break;
        }
        card.localEulerAngles = Vector3.zero;
        card.anchoredPosition = restPos;
    }

    public void ShowBoard(PlayerState player, bool rotate180) // rotate180 means "flip vertically" (row order reversed, column order kept)
    {
        ClearBoard(); // CHANGED: reuses ClearBoard instead of repeating the same loop

        List<GridCell> cellsToShow = rotate180 ? FlipRowsVertically(player._Grid) : player._Grid;

        foreach (GridCell cell in cellsToShow)
        {
            GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
            CardDisplay display = cardObject.GetComponent<CardDisplay>();

            Sprite sprite = _ArtDatabase.GetShipSprite(cell, player._Color);
            display.SetSprite(sprite);
            display._RepresentedCell = cell;
            display._Owner = player; // NEW: BoardCardHoverEffect needs to know which player this cell belongs to, to tell an enemy cell from an own cell
            cardObject.AddComponent<BoardCardHoverEffect>(); // NEW: hover highlight for this board cell

            List<Sprite> shieldSprites = _ArtDatabase.GetShieldSprites(cell, player._Color);
            display.SetShieldOverlays(shieldSprites);
        }
    }

    private CardDisplay FindDisplayForCell(GridCell cell) // NEW: shared lookup - the same "find the existing CardDisplay by its represented cell" search PlayCellRevealFlip already did inline, factored out so PlayShockwave can reuse it (once for the sunk cell itself, then again per neighbor cell it finds geometrically)
    {
        foreach (Transform child in transform)
        {
            CardDisplay display = child.GetComponent<CardDisplay>();
            if (display != null && display._RepresentedCell == cell)
            {
                return display;
            }
        }
        return null;
    }

    public IEnumerator PlayCellRevealFlip(GridCell cell, PlayerState owner) // NEW: finds the already-existing CardDisplay for this cell (the same one DealFromDeck/ShowBoard placed), flips it edge-on and back while swapping in the now-revealed sprite at the midpoint - reuses the existing GameObject directly, so no grid-position math is needed, and the normal ShowBoard() refresh that follows will destroy/recreate it identically once this finishes
    {
        CardDisplay target = FindDisplayForCell(cell);

        if (target == null)
        {
            yield break; // safety - the board may already have been refreshed/cleared by the time this runs
        }

        RectTransform cardRect = target.GetComponent<RectTransform>();
        float halfDuration = _RevealFlipDuration / 2f;

        yield return ScaleCardX(cardRect, 1f, 0f, halfDuration); // shrink to edge-on (still showing the old face, but invisibly thin)

        if (cardRect == null)
        {
            yield break;
        }

        Sprite revealedSprite = _ArtDatabase.GetShipSprite(cell, owner._Color);
        target.SetSprite(revealedSprite);
        List<Sprite> shieldSprites = _ArtDatabase.GetShieldSprites(cell, owner._Color);
        target.SetShieldOverlays(shieldSprites);

        if (cell._Ship == ShipType.Submarine) // a distinct sting shortly after a Submarine's sprite is what actually gets revealed, at the flip's midpoint
        {
            // CHANGED: this used to fire immediately at the flip's midpoint, which lands it right on top
            // of (or a hair after) the missile's own impact sound - close enough that the two blend
            // together and the discovery sting gets buried under the impact clip's tail. Delaying the
            // SOUND ONLY (via its own short-lived coroutine, not a yield in this one) gives the impact
            // sound room to die down first without holding up the visual flip itself, which keeps
            // playing out on its own normal timing.
            StartCoroutine(PlaySubmarineDiscoveredSFXDelayed(_SubmarineDiscoveredSoundDelay));
        }

        yield return ScaleCardX(cardRect, 0f, 1f, halfDuration); // grow back out, now showing the revealed face
    }

    private IEnumerator PlaySubmarineDiscoveredSFXDelayed(float delay) // NEW: small helper so the discovery sting can be pushed back a beat from the missile impact sound without blocking PlayCellRevealFlip's own visual timing
    {
        yield return new WaitForSeconds(delay);
        AudioManager.Instance?.PlaySubmarineDiscoveredSFX();
    }

    public IEnumerator PlayShockwave(GridCell sunkCell) // NEW: plays right after a ship is sunk - finds the (up to) four cards immediately above/below/left/right of the sunk cell ON THIS BOARD, and briefly rises each one outward in its own direction (up rises up, down rises down, left rises left, right rises right). Works purely off each existing card's own on-screen anchoredPosition rather than the underlying data list's index order, so it's correct regardless of how DealFromDeck may have flipped the row order for a rotated/far-side board.
    {
        CardDisplay sunkDisplay = FindDisplayForCell(sunkCell);
        GridLayoutGroup grid = GetComponent<GridLayoutGroup>();

        if (sunkDisplay == null || grid == null)
        {
            yield break; // safety - the board may already have been refreshed/cleared, or this board has no grid layout to measure cell spacing from
        }

        RectTransform sunkRect = (RectTransform)sunkDisplay.transform;
        Vector2 origin = sunkRect.anchoredPosition;

        float stepX = grid.cellSize.x + grid.spacing.x;
        float stepY = grid.cellSize.y + grid.spacing.y;

        // NEW: anchoredPosition's Y increases upward in this panel's local space (see
        // ComputeCellAnchoredPosition above) - so the neighbor one row UP the screen sits at a
        // HIGHER Y (origin + stepY), and rises further in that same +Y direction; the neighbor one
        // row DOWN sits at a LOWER Y and rises further down; left/right work the same way on X.
        TryRiseNeighbor(origin + new Vector2(0f, stepY), Vector2.up);
        TryRiseNeighbor(origin - new Vector2(0f, stepY), Vector2.down);
        TryRiseNeighbor(origin - new Vector2(stepX, 0f), Vector2.left);
        TryRiseNeighbor(origin + new Vector2(stepX, 0f), Vector2.right);

        yield return new WaitForSeconds(_ShockwaveDuration); // lets the whole shockwave finish playing before whatever runs next (typically a full board refresh)
    }

    private void TryRiseNeighbor(Vector2 expectedPos, Vector2 direction) // NEW: finds whichever of this board's OTHER cards is actually sitting at expectedPos (within a small tolerance, to allow for float rounding) and, if one exists there, starts it rising in 'direction'. A missing neighbor (the sunk cell was on an edge/corner) is simply skipped.
    {
        const float tolerance = 4f;

        foreach (Transform child in transform)
        {
            CardDisplay display = child.GetComponent<CardDisplay>();
            if (display == null)
            {
                continue;
            }

            RectTransform rect = (RectTransform)child;
            if (Vector2.Distance(rect.anchoredPosition, expectedPos) <= tolerance)
            {
                StartCoroutine(RiseCard(rect, direction));
                return;
            }
        }
    }

    private IEnumerator RiseCard(RectTransform rect, Vector2 direction) // NEW: rises a card a short distance in 'direction' and eases back to its exact original position - the shockwave's per-neighbor animation
    {
        if (rect == null)
        {
            yield break;
        }

        Vector2 originalPos = rect.anchoredPosition;
        Vector2 peakPos = originalPos + direction.normalized * _ShockwaveRiseDistance;
        float halfDuration = _ShockwaveDuration / 2f;

        float t = 0f;
        while (t < halfDuration) // rise out
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / halfDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(originalPos, peakPos, p);
            yield return null;
        }

        t = 0f;
        while (t < halfDuration) // settle back
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / halfDuration));
            rect.anchoredPosition = Vector2.LerpUnclamped(peakPos, originalPos, p);
            yield return null;
        }

        if (rect != null)
        {
            rect.anchoredPosition = originalPos;
        }
    }

    private IEnumerator ScaleCardX(RectTransform rect, float from, float to, float duration) // NEW: shared helper - eases a card's local X scale between from and to, used for the reveal flip's two halves
    {
        float t = 0f;
        while (t < duration)
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            float x = Mathf.LerpUnclamped(from, to, p);
            rect.localScale = new Vector3(x, 1f, 1f);
            yield return null;
        }
        if (rect != null)
        {
            rect.localScale = new Vector3(to, 1f, 1f);
        }
    }

    public IEnumerator PlayReshuffleFromDiscard(RectTransform discardRect, PlayerState player) // NEW: called right after the real discard pile art has already been hidden (its job is done - the discard pile is genuinely empty now) - spawns a flourish card at that same spot, slides it to this board's own center (the exact spot the original deck slide-in/shuffle used), riffle-shuffles it there just like the very first deal, then slides it into the draw pile spot and disappears, leaving the already-present, never-hidden real DrawPile art as the (now replenished) pile
    {
        if (discardRect == null || _DrawPileRect == null)
        {
            yield break; // safety - nothing to animate without both ends of the trip
        }

        Vector2 discardLocalPos = GetLocalCenterOf(discardRect); // where the real discard pile was sitting, converted into this board panel's own local space
        Vector2 centerPos = Vector2.zero; // this board panel's own center - same spot SlideDeckIn/SlideHandDeckIn already shuffle at
        Vector2 drawPileLocalPos = GetLocalCenterOf(_DrawPileRect);

        GameObject cardObject = Instantiate(_CardDisplayPrefab, transform);
        cardObject.name = "ReshuffleCard";
        CardDisplay display = cardObject.GetComponent<CardDisplay>();
        display.SetSprite(_HandDeckBackSprite);
        display.SetShieldOverlays(null);

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = cardObject.AddComponent<LayoutElement>();
        }
        layoutElement.ignoreLayout = true;

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = _CardDisplayPrefab.GetComponent<RectTransform>().sizeDelta;
        cardRect.anchoredPosition = discardLocalPos;
        cardRect.SetAsLastSibling(); // render on top of the board's own dealt grid cards during the whole trip

        AudioManager.Instance?.PlaySlideSFX();
        yield return SlideRect(cardRect, discardLocalPos, centerPos, _ReshuffleTravelDuration); // whole discard pile gathers at the table's center

        if (cardRect == null)
        {
            yield break;
        }

        yield return RiffleShuffleDeck(centerPos, cardRect.sizeDelta, _HandDeckBackSprite); // same riffle-shuffle look as the very first deal

        if (cardRect == null)
        {
            yield break;
        }

        AudioManager.Instance?.PlaySlideSFX();
        yield return SlideRect(cardRect, centerPos, drawPileLocalPos, _ReshuffleTravelDuration); // slides into the draw pile spot and settles in as the new pile

        if (cardObject != null)
        {
            Destroy(cardObject); // the real DrawPile art was never hidden - it's already sitting right there to take over
        }
    }

    private List<GridCell> FlipRowsVertically(List<GridCell> grid) // reverses the ORDER of rows top-to-bottom, but keeps each row's own left-to-right order
    {
        List<GridCell> flipped = new List<GridCell>(grid.Count);
        int rowCount = grid.Count / _Columns;

        for (int row = rowCount - 1; row >= 0; row--)
        {
            for (int col = 0; col < _Columns; col++)
            {
                flipped.Add(grid[row * _Columns + col]);
            }
        }

        return flipped;
    }
}
