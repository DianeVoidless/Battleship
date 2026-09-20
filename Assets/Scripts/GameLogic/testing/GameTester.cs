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

    public void FinishTurnAndRefresh(GameState game, PlayerState turnEndedFor, PlayerState wildcardDrawPlayer = null, List<Card> wildcardDrawnCards = null) // CHANGED: now also takes an optional mid-turn wildcard draw (Draw3/Cleanse's replacement cards) to animate FIRST - called after every spent move. If that move also ended the turn (turnEndedFor != null) and drew that player's hand back up, this then briefly holds the view on THEM so they watch those new cards slide in too, before finally swapping POV to the new active player. If neither kind of draw happened, this behaves exactly like the old immediate SyncViewToActivePlayer() + RefreshBoardsAndHand().
    {
        bool hasWildcardDraw = wildcardDrawPlayer != null && wildcardDrawnCards != null && wildcardDrawnCards.Count > 0;
        bool hasTurnEndDraw = turnEndedFor != null && game._LastDrawnCards.Count > 0 && turnEndedFor._Color == _ViewingAs;

        if (hasWildcardDraw || hasTurnEndDraw)
        {
            // NEW: copy the lists - GameState's own _LastDrawnCards (and the card's _LastDrawnCards) could be overwritten by a later draw before this coroutine gets to them
            StartCoroutine(PlayAllDrawAnimationsThenFinish(
                hasWildcardDraw ? wildcardDrawPlayer : null,
                hasWildcardDraw ? new List<Card>(wildcardDrawnCards) : null,
                hasTurnEndDraw ? turnEndedFor : null,
                hasTurnEndDraw ? new List<Card>(game._LastDrawnCards) : null));
        }
        else
        {
            SyncViewToActivePlayer();
            RefreshBoardsAndHand();
        }
    }

    private IEnumerator PlayAllDrawAnimationsThenFinish(PlayerState wildcardPlayer, List<Card> wildcardCards, PlayerState turnEndedFor, List<Card> turnEndCards) // NEW: plays the mid-turn wildcard draw (if any) first, then the turn-end draw-up-to-hand-size draw (if any), then swaps POV as usual
    {
        if (wildcardPlayer != null)
        {
            yield return PlayDrawAnimation(wildcardPlayer, wildcardCards);
        }

        if (turnEndedFor != null)
        {
            yield return PlayDrawAnimation(turnEndedFor, turnEndCards);
        }

        SyncViewToActivePlayer();
        RefreshBoardsAndHand();
    }

    private IEnumerator PlayDrawAnimation(PlayerState player, List<Card> drawnCards) // CHANGED: renamed from PlayDrawAnimationThenSwitchView - the POV switch now lives one level up, so this same routine can also be used for a mid-turn wildcard draw that shouldn't switch POV at all. Holds back the newly drawn cards, refreshes everything else normally, then slides each drawn card in one at a time from the real draw pile
    {
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
