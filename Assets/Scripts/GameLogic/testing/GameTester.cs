using UnityEngine;
using UnityEngine.UI; // NEW: for LayoutElement, used by the draw-up-to-hand-size flourish cards
using System.Collections; // NEW: for the draw-up-to-hand-size flourish coroutine
using System.Collections.Generic; // NEW: for the List<Card> of newly drawn cards

public class GameTester : MonoBehaviour
{
    public BoardDisplay _RedBoardDisplay;
    public BoardDisplay _BlueBoardDisplay;
    public HandDisplay _HandDisplay;

    private RectTransform _RedBoardPanel;
    private RectTransform _BlueBoardPanel;

    private GameState _CurrentGame;
    private PlayerColor _ViewingAs = PlayerColor.Red;

    private Vector2 _NearPos;
    private Vector2 _FarPos;

    public RectTransform _RedPileGroup;  // NEW
    public RectTransform _BluePileGroup; // NEW
    public GameObject _RedDiscardPile;  // NEW: the DiscardPile art sitting inside RedPileGroup - kept OFF at match start (independent of RedPileGroup's own active state) until a card actually gets discarded
    public GameObject _BlueDiscardPile; // NEW: same, inside BluePileGroup
    public CapturedShipsPileDisplay _RedCapturedPile; // NEW
    public CapturedShipsPileDisplay _BlueCapturedPile; // NEW
    public GameObject _HealerChoicePrompt; // NEW: the "Select a Ship to heal" banner, shown while _AwaitingHealerChoice is true
    public GameObject _WinScreen;  // NEW
    public GameObject _LoseScreen; // NEW
    public GameObject _TopMenuZoneObject; // CHANGED: plain GameObject reference instead of the component type directly, to work around the Inspector refusing the typed field
    private TopMenuProximityZone _TopMenuZone; // NEW: the actual component, fetched in code instead of dragged in the Inspector
    private bool _RematchPending; // NEW: true while waiting on the opposing player to confirm a rematch
    private PlayerColor _RematchRequestedBy; // NEW: who clicked Play Again
    public GameObject _RematchWaitScreen;    // NEW: "Awaiting opponent confirmation..." - shown to whoever requested it
    public GameObject _RematchConfirmPrompt; // NEW: "Your opponent is requesting a rematch" - shown to the other player
    public GameObject _InputBlocker; // NEW: full-screen invisible raycast blocker - active whenever the board shouldn't be clickable (rematch pending or game over)
    public GameObject _InGameRoot;   // NEW: dragged to the "InGame" object - deactivated when a post-match rematch request gets declined
    public GameObject _MainMenuRoot; // NEW: dragged to "MainMenuScene" - activated in that same case
    [SerializeField] private bool _AutoSwitchView = true;

    [Header("NEW: turn-end draw-up-to-hand-size animation - plays on the ending player's OWN view, right before it swaps to the new active player")]
    public float _DrawCardDealDuration = 0.25f; // how long each newly drawn card takes to slide from the draw pile into the hand

    [Header("NEW: missile attack animation - plays whenever an attack card is resolved, flying from the bottom of the screen up to the clicked board cell")]
    public Sprite _RedMissileSprite;   // drag the red-missile art here
    public Sprite _WhiteMissileSprite; // drag the white-missile art here
    public Vector2 _MissileSize = new Vector2(40f, 100f); // on-screen size of the missile while it's flying
    public float _MissileSpawnY = -1200f; // how far below this canvas's own center the missile starts - tune this so it's safely below everything visible at your canvas's reference resolution
    public float _MissileFlightDuration = 0.35f; // time to travel from spawn to the target cell's center - constant speed (not eased), since a real projectile doesn't ease in/out
    public float _MissileLaunchStagger = 0.12f; // NEW: delay between each consecutive missile in a multi-missile red-attack volley (e.g. a 2-damage or 4-damage red missile card launches that many missiles in a row, this many seconds apart)

    [Header("NEW: card shake - plays on the targeted card itself the instant a missile actually hits it")]
    public float _CardShakeDuration = 0.2f; // how long the shake lasts, fading out over this time
    public float _CardShakeMagnitude = 8f; // how far (in UI units) the card jitters from its resting position at the shake's peak


    public void SyncViewToActivePlayer()
    {
        if (!_AutoSwitchView)
        {
            return;
        }

        _ViewingAs = _CurrentGame._ActivePlayer;
    }

    public void SetAutoSwitchView(bool value) // NEW: lets GameplaySettings update this live when the Gameplay tab's toggle changes, not just at launch
    {
        _AutoSwitchView = value;
    }

    void Awake()
    {
        _AutoSwitchView = PlayerPrefs.GetInt(GameplaySettings.AutoSwitchViewKey, _AutoSwitchView ? 1 : 0) == 1; // NEW: restores the saved setting on launch, falling back to this field's own Inspector default the very first time

        _RedBoardPanel = _RedBoardDisplay.GetComponent<RectTransform>();
        _BlueBoardPanel = _BlueBoardDisplay.GetComponent<RectTransform>();

        _TopMenuZone = _TopMenuZoneObject.GetComponent<TopMenuProximityZone>(); // NEW

        Vector2 redPos = _RedBoardPanel.anchoredPosition;
        Vector2 bluePos = _BlueBoardPanel.anchoredPosition;

        if (redPos.y < bluePos.y)
        {
            _NearPos = redPos;
            _FarPos = bluePos;
        }
        else
        {
            _NearPos = bluePos;
            _FarPos = redPos;
        }

        // NEW: BoardDisplay's hand-deal animation is just a visual flourish (its cards aren't parented
        // under HandPanel, so hover-raise doesn't work on them) - once it finishes for whichever board
        // is currently the viewed one, swap in the REAL, interactive hand.
        _RedBoardDisplay.OnHandRevealed += HandleHandRevealed;
        _BlueBoardDisplay.OnHandRevealed += HandleHandRevealed;

        // NEW: same idea, one step further - once the leftover hand-deck card finishes sliding into
        // a board's draw pile spot, swap it for the REAL, pre-placed DrawPile art sitting in that
        // board's own PileGroup (currently hidden, per ClearBoard/BeginMatch below).
        _RedBoardDisplay.OnDrawPileRevealed += HandleDrawPileRevealed;
        _BlueBoardDisplay.OnDrawPileRevealed += HandleDrawPileRevealed;
    }

    private void HandleHandRevealed(PlayerState player) // NEW: called by whichever BoardDisplay just finished dealing out the viewed player's hand
    {
        _HandDisplay.ShowHand(player);
    }

    private void HandleDrawPileRevealed(PlayerState player) // NEW: called by whichever BoardDisplay just finished sliding its leftover card into the draw pile spot
    {
        RectTransform pileGroup = player._Color == PlayerColor.Red ? _RedPileGroup : _BluePileGroup;
        bool viewingThis = _ViewingAs == player._Color;
        pileGroup.localEulerAngles = new Vector3(0, 0, viewingThis ? 0f : -180f); // safety - matches whatever BeginMatch already set, in case the view was toggled mid-deal
        pileGroup.gameObject.SetActive(true);
    }

    public void BeginMatch()
    {
        _CurrentGame = GameSetup.StartNewGame();

        _ViewingAs = _CurrentGame._ActivePlayer; // start viewing whoever actually goes first, instead of always defaulting to Red

        // NEW: at the very start of a match, nothing has been dealt yet - no board cards, no
        // draw pile, and no hand cards should exist on screen. Clear them explicitly, then tell
        // RefreshView not to redraw them (a later step will animate them appearing).
        _RedBoardDisplay.ClearBoard();
        _BlueBoardDisplay.ClearBoard();
        _RedPileGroup.gameObject.SetActive(false);
        _BluePileGroup.gameObject.SetActive(false);
        _HandDisplay.ClearHand();

        // NEW: the discard piles must stay hidden at the start of a match (nothing's been discarded
        // yet) - set OFF independently of RedPileGroup/BluePileGroup itself, since that parent gets
        // switched back on later (by RefreshView and by the new draw-pile reveal), which would
        // otherwise drag DiscardPile's own active state along with it.
        if (_RedDiscardPile != null) _RedDiscardPile.SetActive(false);
        if (_BlueDiscardPile != null) _BlueDiscardPile.SetActive(false);

        // NEW: defensive reset for a rematch - a previous match may have emptied and hidden a
        // player's draw pile art (see SyncDrawPileVisibility), and that GameObject's active state
        // would otherwise persist right through into the new match even though the fresh deck is
        // obviously full again.
        if (_RedBoardDisplay._DrawPileRect != null) _RedBoardDisplay._DrawPileRect.gameObject.SetActive(true);
        if (_BlueBoardDisplay._DrawPileRect != null) _BlueBoardDisplay._DrawPileRect.gameObject.SetActive(true);

        RefreshView(false); // CHANGED: false = skip drawing the boards/piles/hand, they don't exist yet

        // NEW: slide the two face-down "table card" decks into view, one in the middle of each
        // board panel, then (inside SlideDeckIn itself) shuffle and deal that player's actual grid
        // out of it - passes this GameTester as the coroutine host since InGame's panels may still
        // be inactive at this exact moment (same reason the old DealBoard needed it too)
        bool viewingRed = _ViewingAs == PlayerColor.Red;

        // NEW: set each PileGroup's rotation now, while it's still hidden, instead of waiting for
        // RefreshView to do it later - the draw-pile deal animation below reads its target position
        // live off this (rotated) transform, so it needs to already be in its correct final
        // orientation before that animation starts.
        _RedPileGroup.localEulerAngles = new Vector3(0, 0, viewingRed ? 0f : -180f);
        _BluePileGroup.localEulerAngles = new Vector3(0, 0, viewingRed ? -180f : 0f);

        _RedBoardDisplay.SlideDeckIn(_CurrentGame._PlayerRed, !viewingRed, this);
        _BlueBoardDisplay.SlideDeckIn(_CurrentGame._PlayerBlue, viewingRed, this);
    }

    public void ToggleView()
    {
        if (_ViewingAs == PlayerColor.Red)
        {
            _ViewingAs = PlayerColor.Blue;
        }
        else
        {
            _ViewingAs = PlayerColor.Red;
        }
        RefreshView();
    }

    private void RefreshView()
    {
        RefreshView(true); // CHANGED: normal refreshes still draw the boards/piles as before
    }

    private void RefreshView(bool showBoardsAndPiles) // NEW: showBoardsAndPiles is false only right after BeginMatch(), before anything has been dealt
    {
        bool viewingRed = _ViewingAs == PlayerColor.Red;

        if (showBoardsAndPiles)
        {
            _RedBoardDisplay.ShowBoard(_CurrentGame._PlayerRed, !viewingRed);
            _BlueBoardDisplay.ShowBoard(_CurrentGame._PlayerBlue, viewingRed);

            _RedPileGroup.gameObject.SetActive(true);
            _BluePileGroup.gameObject.SetActive(true);

            // NEW: the discard pile has no animation of its own - a played card just vanishes from
            // the hand like it already does, and this simply reveals the (already-positioned) discard
            // pile art the instant that player has actually discarded something. Every RefreshView
            // call after a card is played re-checks this, so it turns on right when it should and
            // never needs to be turned off again once a match is underway.
            if (_RedDiscardPile != null) _RedDiscardPile.SetActive(_CurrentGame._PlayerRed._DiscardPile.Count > 0);
            if (_BlueDiscardPile != null) _BlueDiscardPile.SetActive(_CurrentGame._PlayerBlue._DiscardPile.Count > 0);

            if (viewingRed)
            {
                _RedPileGroup.localEulerAngles = new Vector3(0, 0, 0f);
                _BluePileGroup.localEulerAngles = new Vector3(0, 0, -180f);
            }
            else
            {
                _RedPileGroup.localEulerAngles = new Vector3(0, 0, -180f);
                _BluePileGroup.localEulerAngles = new Vector3(0, 0, 0f);
            }
        }
        if (showBoardsAndPiles) // CHANGED: hand cards don't exist yet either, right after BeginMatch()
        {
            if (viewingRed)
            {
                _HandDisplay.ShowHand(_CurrentGame._PlayerRed);
            }
            else
            {
                _HandDisplay.ShowHand(_CurrentGame._PlayerBlue);
            }
        }

        if (viewingRed)
        {
            _RedBoardPanel.anchoredPosition = _NearPos;
            _BlueBoardPanel.anchoredPosition = _FarPos;
        }
        else
        {
            _BlueBoardPanel.anchoredPosition = _NearPos;
            _RedBoardPanel.anchoredPosition = _FarPos;
        }
        _RedCapturedPile.Refresh(_CurrentGame._PlayerRed._CapturedShipCount); // NEW
        _BlueCapturedPile.Refresh(_CurrentGame._PlayerBlue._CapturedShipCount); // NEW
        _HealerChoicePrompt.SetActive(_CurrentGame._AwaitingHealerChoice); // NEW: shows/hides the banner based on whether the active player's Healer is waiting for a pick

        if (_InputBlocker != null) // NEW: block board clicks the whole time a rematch decision is pending or the match has ended, so the underlying game can't be played mid-prompt
        {
            _InputBlocker.SetActive(_RematchPending || _CurrentGame._IsGameOver);
        }

        if (_RematchPending) // CHANGED: checked first now, independent of _IsGameOver - a rematch can be requested mid-match (e.g. from the top menu's Restart Match button), not just from the Win/Lose screens
        {
            bool viewingRequester = _ViewingAs == _RematchRequestedBy;
            _WinScreen.SetActive(false);
            _LoseScreen.SetActive(false);
            _RematchWaitScreen.SetActive(viewingRequester);
            _RematchConfirmPrompt.SetActive(!viewingRequester);
            _TopMenuZone.DisableMenu();
        }
        else if (_CurrentGame._IsGameOver)
        {
            bool viewingWinner = _ViewingAs == _CurrentGame._WinningPlayer;
            _WinScreen.SetActive(viewingWinner);
            _LoseScreen.SetActive(!viewingWinner);
            _RematchWaitScreen.SetActive(false);
            _RematchConfirmPrompt.SetActive(false);
            _TopMenuZone.DisableMenu(); // NEW
        }
        else
        {
            _WinScreen.SetActive(false);
            _LoseScreen.SetActive(false);
            _RematchWaitScreen.SetActive(false); // NEW
            _RematchConfirmPrompt.SetActive(false); // NEW
            _TopMenuZone.EnableMenu(); // NEW
        }
    }

    public GameState GetGame()
    {
        return _CurrentGame;
    }

    public void RefreshBoardsAndHand()
    {
        RefreshView(); // CHANGED: RefreshView already redraws both boards and the hand together now
    }

    public void FinishTurnAndRefresh(GameState game, PlayerState turnEndedFor, PlayerState wildcardDrawPlayer = null, List<Card> wildcardDrawnCards = null, bool wildcardReshuffled = false, GridCell revealedCell = null, PlayerState revealedCellOwner = null, CardDisplay missileTarget = null, TargetColor missileColor = default, int missileCount = 1, bool missilePlaysHitSound = false, GridCell sunkCell = null, PlayerState sunkCellOwner = null) // CHANGED: now also takes an optional sunk cell (and whose board it's on) - if this exact move is what just sunk a ship, the shockwave plays on that board right after the reveal flip
    {
        bool hasReveal = revealedCell != null && revealedCellOwner != null; // NEW
        bool hasMissile = missileTarget != null; // NEW
        bool hasSunk = sunkCell != null && sunkCellOwner != null; // NEW
        bool hasWildcardDraw = wildcardDrawPlayer != null && wildcardDrawnCards != null && wildcardDrawnCards.Count > 0;
        bool hasTurnEndDraw = turnEndedFor != null && game._LastDrawnCards.Count > 0 && turnEndedFor._Color == _ViewingAs;

        if (hasReveal || hasMissile || hasSunk || hasWildcardDraw || hasTurnEndDraw)
        {
            // NEW: copy the lists - GameState's own _LastDrawnCards (and the card's _LastDrawnCards) could be overwritten by a later draw before this coroutine gets to them
            StartCoroutine(PlayAllDrawAnimationsThenFinish(
                hasMissile ? missileTarget : null,
                missileColor,
                Mathf.Max(1, missileCount),
                missilePlaysHitSound,
                hasReveal ? revealedCell : null,
                hasReveal ? revealedCellOwner : null,
                hasSunk ? sunkCell : null,
                hasSunk ? sunkCellOwner : null,
                hasWildcardDraw ? wildcardDrawPlayer : null,
                hasWildcardDraw ? new List<Card>(wildcardDrawnCards) : null,
                hasWildcardDraw && wildcardReshuffled, // NEW
                hasTurnEndDraw ? turnEndedFor : null,
                hasTurnEndDraw ? new List<Card>(game._LastDrawnCards) : null,
                hasTurnEndDraw && game._LastDrawReshuffled)); // NEW
        }
        else
        {
            SyncViewToActivePlayer();
            RefreshBoardsAndHand();
        }
    }

    private IEnumerator PlayAllDrawAnimationsThenFinish(CardDisplay missileTarget, TargetColor missileColor, int missileCount, bool missilePlaysHitSound, GridCell revealedCell, PlayerState revealedCellOwner, GridCell sunkCell, PlayerState sunkCellOwner, PlayerState wildcardPlayer, List<Card> wildcardCards, bool wildcardReshuffled, PlayerState turnEndedFor, List<Card> turnEndCards, bool turnEndReshuffled) // CHANGED: plays the missile volley (if any) first - the shockwave (if any) now plays FROM WITHIN that volley, timed to the exact instant the killing missile hits (same moment as its impact sound), not afterward - then the enemy-cell reveal flip (if any), then the mid-turn wildcard draw (if any, reshuffle merge first if needed), then the turn-end draw-up-to-hand-size draw (if any, same reshuffle merge treatment), then swaps POV as usual
    {
        if (missileTarget != null)
        {
            yield return PlayMissileVolley(missileTarget, missileColor, missileCount, missilePlaysHitSound, sunkCell, sunkCellOwner);
        }
        else if (sunkCell != null && sunkCellOwner != null) // safety fallback - a sunk cell should always come paired with an attack's missile, but just in case, still play it
        {
            BoardDisplay sunkBoard = (sunkCellOwner._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
            yield return sunkBoard.PlayShockwave(sunkCell);
        }

        if (revealedCell != null && revealedCellOwner != null)
        {
            BoardDisplay revealedBoard = (revealedCellOwner._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
            yield return revealedBoard.PlayCellRevealFlip(revealedCell, revealedCellOwner);
        }

        if (wildcardPlayer != null)
        {
            yield return PlayDrawAnimation(wildcardPlayer, wildcardCards, wildcardReshuffled);
        }

        if (turnEndedFor != null)
        {
            yield return PlayDrawAnimation(turnEndedFor, turnEndCards, turnEndReshuffled);
        }

        SyncViewToActivePlayer();
        RefreshBoardsAndHand();
    }

    private IEnumerator PlayDrawAnimation(PlayerState player, List<Card> drawnCards, bool reshuffled) // CHANGED: renamed from PlayDrawAnimationThenSwitchView - the POV switch now lives one level up, so this same routine can also be used for a mid-turn wildcard draw that shouldn't switch POV at all. Now also takes whether this draw had to reshuffle the discard pile back into the draw pile, so that merge can play BEFORE the refresh below - RefreshBoardsAndHand would otherwise just instantly hide the discard pile (its count is already 0 by this point), with no visual build-up at all. Holds back the newly drawn cards, refreshes everything else normally, then slides each drawn card in one at a time from the real draw pile
    {
        if (reshuffled)
        {
            yield return PlayReshuffleMerge(player); // NEW: discard pile art visibly slides into the draw pile spot and merges in, while it's still the active, visible discard pile from BEFORE this refresh
            SyncDrawPileVisibility(player); // NEW: the pile was hidden by a PREVIOUS depletion (see below) - now that it's genuinely been replenished, show it again before any cards start flying from it
        }

        foreach (Card card in drawnCards) // NEW: temporarily pull the just-drawn cards back OUT of the hand, so the very next refresh shows the pre-draw hand, not the already-updated one
        {
            player._Hand.Remove(card);
        }

        RefreshBoardsAndHand(); // boards, piles, healer prompt etc. all update immediately - only the hand is (temporarily) missing its newest cards

        BoardDisplay ownBoard = (player._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
        RectTransform drawPileRect = ownBoard._DrawPileRect;
        RectTransform handRect = (RectTransform)_HandDisplay.transform;

        foreach (Card card in drawnCards)
        {
            yield return AnimateOneDrawnCard(drawPileRect, handRect, player, card);
        }

        SyncDrawPileVisibility(player); // NEW: this draw may have emptied the pile down to its last card - hide its art now that the last flourish has actually landed, so it visibly reads "nothing left here" until the next reshuffle
    }

    public void SyncDrawPileVisibility(PlayerState player) // NEW: shows or hides a board's own real DrawPile art based on whether that player's draw pile currently has any cards left in it - called right after any draw (animated here, or the instant Carrier mid-turn draw in TurnController) so an emptied pile stops misleadingly looking like a full one with nothing left to give
    {
        BoardDisplay ownBoard = (player._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
        if (ownBoard._DrawPileRect != null)
        {
            ownBoard._DrawPileRect.gameObject.SetActive(player._DrawPile.Count > 0);
        }
    }

    private IEnumerator PlayReshuffleMerge(PlayerState player) // CHANGED: no longer just hops the real discard pile object over - hides it immediately (its job is done, the discard pile is genuinely empty now) and hands off to BoardDisplay.PlayReshuffleFromDiscard, which spawns a flourish that gathers at the table's center, riffle-shuffles like the very first deal, then slides into the draw pile spot - the "whole pile" look instead of a single card silently relocating
    {
        GameObject discardObject = (player._Color == PlayerColor.Red) ? _RedDiscardPile : _BlueDiscardPile;
        BoardDisplay ownBoard = (player._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;

        if (discardObject == null || !discardObject.activeSelf || ownBoard._DrawPileRect == null)
        {
            yield break; // safety - nothing visible to animate (e.g. the discard pile was somehow already hidden, or the draw pile target isn't assigned)
        }

        RectTransform discardRect = discardObject.GetComponent<RectTransform>();
        discardObject.SetActive(false); // NEW: hidden right away - a flourish card takes over from this exact spot, so the swap is invisible, and the real object no longer needs to be moved or reset afterward

        yield return ownBoard.PlayReshuffleFromDiscard(discardRect, player);
    }

    private IEnumerator AnimateOneDrawnCard(RectTransform drawPileRect, RectTransform handRect, PlayerState player, Card card) // NEW: slides one real, face-up card from the player's own draw pile into the hand panel, then adds it back to the hand and lets HandDisplay redraw the real (now-larger) hand in its correct, final layout
    {
        // CHANGED: parented under the Canvas itself (and forced to the very top of its render order)
        // instead of under the HandPanel - the draw pile sits way outside the HandPanel's own area
        // (often behind the board panels, depending on sibling order), so a card parented directly
        // under HandPanel was flying most of its route hidden behind other UI. Parenting at the
        // Canvas root and calling SetAsLastSibling() keeps it visible above everything for the
        // whole flight, the same way a UI toast or dragged item would be.
        Canvas canvas = handRect.GetComponentInParent<Canvas>();
        RectTransform flourishParent = (canvas != null) ? (RectTransform)canvas.transform : handRect;

        GameObject cardObject = Instantiate(_HandDisplay._CardDisplayPrefab, flourishParent);
        cardObject.name = "DrawnCardFlourish";
        cardObject.transform.SetAsLastSibling(); // NEW: render on top of the boards, piles, and hand panel for the entire trip
        CardDisplay display = cardObject.GetComponent<CardDisplay>();

        Sprite frontSprite = _HandDisplay._ArtDatabase.GetCardSprite(card, player._Color);
        display.SetSprite(frontSprite);
        display._RepresentedCard = card;
        display._ArtDatabase = _HandDisplay._ArtDatabase;
        display._Owner = player;
        display.RefreshWildcardSprite();

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
        cardRect.sizeDelta = _HandDisplay._CardDisplayPrefab.GetComponent<RectTransform>().sizeDelta;

        // CHANGED: both endpoints are now converted the same way (real on-screen center -> this
        // flourish's own parent space), instead of assuming the hand panel's local origin (0,0)
        // lines up with its visual center - accurate regardless of the HandPanel's own anchor setup.
        Vector2 from = ConvertWorldCenterToLocal(drawPileRect, flourishParent); // the real draw pile's on-screen spot
        Vector2 to = ConvertWorldCenterToLocal(handRect, flourishParent); // the real hand panel's on-screen spot
        cardRect.anchoredPosition = from;

        AudioManager.Instance?.PlayShoveSFX(); // NEW: one shove sound per card as it starts sliding, since DrawUpToHandSize's own single batch sound is skipped for this animated path (see GameState.SwitchActivePlayer)

        float t = 0f;
        while (t < _DrawCardDealDuration)
        {
            if (cardRect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _DrawCardDealDuration));
            cardRect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }

        player._Hand.Add(card); // NEW: officially back in the hand now that it's visually arrived
        if (cardObject != null)
        {
            Destroy(cardObject);
        }
        _HandDisplay.ShowHand(player); // redraws the real hand (now including this card) in its correct, final arrangement
    }

    private Vector2 ConvertWorldCenterToLocal(RectTransform source, RectTransform destSpace) // NEW: converts source's on-screen center into destSpace's own local anchored-position space, regardless of how many parents (and rotations - e.g. a PileGroup's 180-degree flip) sit between them
    {
        if (source == null || destSpace == null)
        {
            return Vector2.zero;
        }

        Canvas canvas = source.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector3 worldCenter = source.TransformPoint(source.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(destSpace, screenPoint, cam, out localPoint);
        return localPoint;
    }

    public void PlayMissileAndRevealThenRefresh(CardDisplay missileTarget, TargetColor missileColor, int missileCount, bool missilePlaysHitSound, GridCell revealedCell, PlayerState revealedCellOwner, GridCell sunkCell = null, PlayerState sunkCellOwner = null) // CHANGED: same missile volley + reveal-flip + shockwave sequence as a normal attack, but for the exact shot that ends the match - TurnController's game-over branch calls this instead of refreshing immediately, so the missile(s), reveal flip, and shockwave (whichever apply) all still visibly play before the Win/Lose screen appears
    {
        StartCoroutine(PlayMissileAndRevealThenRefreshRoutine(missileTarget, missileColor, missileCount, missilePlaysHitSound, revealedCell, revealedCellOwner, sunkCell, sunkCellOwner));
    }

    private IEnumerator PlayMissileAndRevealThenRefreshRoutine(CardDisplay missileTarget, TargetColor missileColor, int missileCount, bool missilePlaysHitSound, GridCell revealedCell, PlayerState revealedCellOwner, GridCell sunkCell, PlayerState sunkCellOwner)
    {
        if (missileTarget != null)
        {
            yield return PlayMissileVolley(missileTarget, missileColor, missileCount, missilePlaysHitSound, sunkCell, sunkCellOwner);
        }
        else if (sunkCell != null && sunkCellOwner != null) // safety fallback - a sunk cell should always come paired with an attack's missile, but just in case, still play it
        {
            BoardDisplay sunkBoard = (sunkCellOwner._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
            yield return sunkBoard.PlayShockwave(sunkCell);
        }

        if (revealedCell != null && revealedCellOwner != null)
        {
            BoardDisplay revealedBoard = (revealedCellOwner._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
            yield return revealedBoard.PlayCellRevealFlip(revealedCell, revealedCellOwner);
        }

        RefreshBoardsAndHand(); // no SyncViewToActivePlayer here - the match just ended, there's no next turn to switch to
    }

    private IEnumerator PlayMissileVolley(CardDisplay targetDisplay, TargetColor missileColor, int missileCount, bool missilePlaysHitSound, GridCell sunkCell, PlayerState sunkCellOwner) // CHANGED: also takes the sunk cell (if any) - the shockwave now plays timed to the LAST missile's own impact (same moment as its hit sound), fired at the SUNK ship's board, rather than waiting until after the reveal flip. Fires 'missileCount' missiles at the same target, one shortly after another (_MissileLaunchStagger apart) instead of waiting for each to land before launching the next - a multi-damage red attack (2, 4, or 5 with Cruiser's buff) reads as a volley, not a single shot. Waits for the LAST missile's own flight (plus its shake and/or shockwave, if any) to finish before returning, so whatever plays next (the reveal flip) still waits for the whole thing.
    {
        missileCount = Mathf.Max(1, missileCount);

        for (int i = 0; i < missileCount; i++)
        {
            bool isLastMissile = i == missileCount - 1; // NEW: the shockwave (if this hit sinks a ship) is tied to the LAST missile's own impact - only it gets the sunk-cell info, so a multi-missile volley doesn't retrigger the shockwave once per missile
            StartCoroutine(PlayMissileAttack(targetDisplay, missileColor, missilePlaysHitSound, isLastMissile ? sunkCell : null, isLastMissile ? sunkCellOwner : null)); // NEW: each missile flies independently once launched - not yielded on directly, so the next one can launch before this one lands
            if (i < missileCount - 1)
            {
                yield return new WaitForSeconds(_MissileLaunchStagger);
            }
        }

        // CHANGED: also waits out the shake and/or shockwave (when this hit actually plays either) -
        // otherwise whatever runs right after the volley (the reveal flip, or a plain
        // RefreshBoardsAndHand) fires while the last missile's ShakeCard/PlayShockwave coroutines are
        // still mid-animation. RefreshBoardsAndHand in particular destroys and recreates every board
        // card, which silently kills those coroutines' target RectTransforms after just a frame or
        // two - the effect never gets to actually finish playing.
        float finalWait = _MissileFlightDuration + (missilePlaysHitSound ? _CardShakeDuration : 0f);
        if (sunkCell != null && sunkCellOwner != null)
        {
            BoardDisplay sunkBoard = (sunkCellOwner._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
            finalWait += sunkBoard._ShockwaveDuration;
        }
        yield return new WaitForSeconds(finalWait); // the last missile launched still needs its own full flight time (plus its shake and/or shockwave, if any) to finish
    }

    private IEnumerator PlayMissileAttack(CardDisplay targetDisplay, TargetColor missileColor, bool missilePlaysHitSound, GridCell sunkCell, PlayerState sunkCellOwner) // CHANGED: also takes the sunk cell (if any) - spawns a missile at the bottom of the screen, on top of everything, and flies it in a straight line up to the clicked board cell's center, then destroys it right there - no reparenting, no tucking underneath, just launch -> fly -> arrive -> gone. The actual reveal/damage already happened the instant the card was clicked (see TurnController.OnCellClicked), this is purely the visual that "causes" it
    {
        if (targetDisplay == null)
        {
            yield break; // safety - the target card might already be gone by the time this runs
        }

        Sprite missileSprite = (missileColor == TargetColor.Red) ? _RedMissileSprite : _WhiteMissileSprite;
        if (missileSprite == null)
        {
            yield break; // safety - no missile art assigned yet, skip the animation rather than show a blank image
        }

        RectTransform targetRect = (RectTransform)targetDisplay.transform;
        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        RectTransform canvasRect = (canvas != null) ? (RectTransform)canvas.transform : targetRect;

        GameObject missileObject = new GameObject("MissileFlourish");
        missileObject.transform.SetParent(canvasRect, false);
        Image missileImage = missileObject.AddComponent<Image>();
        missileImage.sprite = missileSprite;
        missileImage.raycastTarget = false; // NEW: purely visual - never intercept clicks meant for the board underneath

        RectTransform missileRect = missileObject.GetComponent<RectTransform>();
        missileRect.sizeDelta = _MissileSize;
        missileRect.anchorMin = new Vector2(0.5f, 0.5f);
        missileRect.anchorMax = new Vector2(0.5f, 0.5f);
        missileRect.pivot = new Vector2(0.5f, 0.5f);
        missileRect.SetAsLastSibling(); // on top of everything on the Canvas, for the whole flight

        Vector2 targetLocalPos = ConvertWorldCenterToLocal(targetRect, canvasRect);
        Vector2 spawnLocalPos = new Vector2(targetLocalPos.x, _MissileSpawnY); // straight line up from the bottom of the screen, directly below the target

        Vector2 direction = (targetLocalPos - spawnLocalPos).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f; // NEW: -90 assumes the missile art points "up" in its own unrotated sprite - adjust this offset if your art points a different way
        missileRect.localEulerAngles = new Vector3(0f, 0f, angle);
        missileRect.anchoredPosition = spawnLocalPos;

        // CHANGED: no launch sound anymore - with a whole volley of missiles firing off in quick
        // succession (see PlayMissileVolley), a launch sound per missile piled up into too much
        // noise. Only the impact sound remains, and only when it's actually earned - see below.

        yield return LerpMissile(missileRect, spawnLocalPos, targetLocalPos, _MissileFlightDuration);

        // CHANGED: the shockwave now triggers FIRST, before the hit sound/shake below - it reads the
        // sunk card's own anchoredPosition as its "origin" to compute where its neighbors should be,
        // and ShakeCard (started right after) writes its first random jitter offset to that same
        // position SYNCHRONOUSLY the instant it's started (Unity runs a coroutine up to its first
        // yield immediately). Shockwave used to run second and would read the card's position while
        // it was already mid-jitter, throwing the whole neighbor grid off by a random few pixels each
        // time - which is why it only worked "by luck" when that random wobble happened to land inside
        // the match tolerance. Triggering it first means it always reads the card's true resting spot.
        if (sunkCell != null && sunkCellOwner != null)
        {
            BoardDisplay sunkBoard = (sunkCellOwner._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
            StartCoroutine(sunkBoard.PlayShockwave(sunkCell));
        }

        // CHANGED: the impact sound is conditional - 'missilePlaysHitSound' (computed by the
        // caller, which knows the actual cell) is true for a red missile only when it lands on a
        // real ship other than a Submarine, and for a white missile only when it lands on a
        // Submarine - any other outcome (including an empty cell) stays silent on impact.
        if (missilePlaysHitSound)
        {
            if (missileColor == TargetColor.Red)
            {
                AudioManager.Instance?.PlayRedMissileHitSFX();
            }
            else
            {
                AudioManager.Instance?.PlayWhiteMissileHitSFX();
            }

            // NEW: same "actually hit something" condition as the impact sound above - the targeted
            // card itself jitters briefly right on impact. Fire-and-forget (not yielded on) so a
            // multi-missile volley landing in quick succession can shake the same card again on each
            // hit without waiting for the previous shake to finish.
            StartCoroutine(ShakeCard(targetRect));
        }

        if (missileObject != null)
        {
            Destroy(missileObject); // arrived at the target's center - gone immediately, no lingering underneath anything
        }
    }

    private IEnumerator ShakeCard(RectTransform rect) // NEW: brief, decaying jitter on a board card's own position - used when a missile actually lands a hit on it. Restores the card's exact original position when it finishes (or if the card is destroyed/refreshed away mid-shake).
    {
        if (rect == null)
        {
            yield break;
        }

        Vector2 originalPos = rect.anchoredPosition;
        float t = 0f;

        while (t < _CardShakeDuration)
        {
            if (rect == null)
            {
                yield break; // safety - the board may have refreshed this card away mid-shake
            }
            t += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(t / _CardShakeDuration); // shake eases out instead of stopping abruptly
            Vector2 offset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _CardShakeMagnitude * damper;
            rect.anchoredPosition = originalPos + offset;
            yield return null;
        }

        if (rect != null)
        {
            rect.anchoredPosition = originalPos;
        }
    }

    private IEnumerator LerpMissile(RectTransform rect, Vector2 from, Vector2 to, float duration) // NEW: constant-speed (not eased) position lerp - a missile flies straight and fast, it doesn't ease in/out like the card flourishes elsewhere
    {
        float t = 0f;
        while (t < duration)
        {
            if (rect == null)
            {
                yield break;
            }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
            yield return null;
        }
        if (rect != null)
        {
            rect.anchoredPosition = to;
        }
    }

    public void RequestRematch() // NEW: called by "Play Again" on either the Win or Lose screen
    {
        _RematchPending = true;
        _RematchRequestedBy = _ViewingAs;
        _ViewingAs = (_ViewingAs == PlayerColor.Red) ? PlayerColor.Blue : PlayerColor.Red; // pass the screen to the other player so they see the confirmation prompt
        RefreshView();
    }

    public void ConfirmRematch() // NEW: opponent agreed - start a fresh match
    {
        AudioManager.Instance?.PlayConfirmSFX(); // NEW
        _RematchPending = false;
        BeginMatch();
    }

    public void DeclineRematch() // NEW: opponent said no - go back to the original result
    {
        AudioManager.Instance?.PlayDeclineSFX(); // NEW
        _RematchPending = false;
        _ViewingAs = _RematchRequestedBy;

        if (_CurrentGame._IsGameOver) // CHANGED: the match had already ended when the rematch was requested (from the Win/Lose screen), so there's no match to return to - send the requester to the main menu instead
        {
            if (_InGameRoot != null) _InGameRoot.SetActive(false);
            if (_MainMenuRoot != null) _MainMenuRoot.SetActive(true);
        }
        else // a rematch requested mid-match (e.g. the top menu's Restart Match button) - just resume where things left off
        {
            RefreshView();
        }
    }
}
