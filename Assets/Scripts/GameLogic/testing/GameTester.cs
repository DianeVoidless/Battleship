using UnityEngine;
using UnityEngine.UI; // NEW: for LayoutElement, used by the draw-up-to-hand-size flourish cards
using System.Collections; // NEW: for the draw-up-to-hand-size flourish coroutine
using System.Collections.Generic; // NEW: for the List<Card> of newly drawn cards
using Unity.Netcode; // NEW: for NetworkManager.Singleton.IsHost, used to work out which color THIS machine's player actually is

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

    [Header("NEW: whose-turn glow - one fade fixed at the TOP of the screen, one fixed at the BOTTOM, both tinted white by default in the Editor (the actual tint is set by script at runtime - see RefreshTurnGlow/_RedGlowColor/_BlueGlowColor below). CHANGED: named by POSITION, not color, because which color's board is actually near/bottom vs far/top flips per client (each screen pins its own color to the near/bottom board) - so it's always the object matching the active player's ACTUAL screen side that gets shown, and whichever one that is just gets tinted to that player's color.")]
    public UnityEngine.UI.Image _TopTurnGlow;
    public UnityEngine.UI.Image _BottomTurnGlow;
    [SerializeField] private bool _AutoSwitchView = true;

    [Header("NEW: turn-end draw-up-to-hand-size animation - plays on the ending player's OWN view, right before it swaps to the new active player")]
    public float _DrawCardDealDuration = 0.25f; // how long each newly drawn card takes to slide from the draw pile into the hand

    [Header("NEW: how far a face-down flourish card travels when it's the ENEMY drawing (Draw3, turn-end draw-up, Cleanse) from MY point of view - it doesn't land anywhere real (I never see the enemy's hand), it just flies this far up from their draw pile and vanishes")]
    public float _EnemyDrawFlourishDistance = 700f;

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
        if (LobbySync.Instance != null)
        {
            return; // NEW: a networked match keeps each screen pinned to ITS OWN player's color the whole time - "auto switch view" was only ever meant for local hotseat testing, where both players share one screen and it flips to whoever's turn it is
        }

        if (!_AutoSwitchView)
        {
            return;
        }

        _ViewingAs = _CurrentGame._ActivePlayer;
    }

    private PlayerColor GetLocalPlayerColor() // NEW: works out which color THIS specific machine's player actually is - the host picked a color in the lobby (LobbySync.HostIsRed), and whoever isn't the host is always the opposite color (same rule ColorChoicePanel already uses to decide who's who in the lobby)
    {
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        bool hostIsRed = LobbySync.Instance.HostIsRed.Value;
        if (isHost)
        {
            return hostIsRed ? PlayerColor.Red : PlayerColor.Blue;
        }
        return hostIsRed ? PlayerColor.Blue : PlayerColor.Red;
    }

    public void SetAutoSwitchView(bool value) // NEW: lets GameplaySettings update this live when the Gameplay tab's toggle changes, not just at launch
    {
        _AutoSwitchView = value;
    }

    void Awake()
    {
        _AutoSwitchView = PlayerPrefs.GetInt(ClonePrefs.Key(GameplaySettings.AutoSwitchViewKey), _AutoSwitchView ? 1 : 0) == 1; // CHANGED: routed through ClonePrefs - restores the saved setting on launch, falling back to this field's own Inspector default the very first time

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
        int seed = (LobbySync.Instance != null) ? LobbySync.Instance.MatchSeed.Value : System.Environment.TickCount; // NEW: in a networked match, both machines feed in the SAME host-picked seed, so GameSetup deals out the identical board/hands on both screens - falls back to an ordinary random seed for local/offline testing, same as before
        GameSetup.SeedSharedRng(seed);

        _CurrentGame = GameSetup.StartNewGame();

        _ViewingAs = (LobbySync.Instance != null) ? GetLocalPlayerColor() : _CurrentGame._ActivePlayer; // CHANGED: in a networked match, this screen is pinned to THIS machine's own player color from the very start, whether or not they happen to go first - "start by viewing whoever goes first" only made sense for local hotseat testing, where both players shared one screen

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
        _HealerChoicePrompt.SetActive(_CurrentGame._AwaitingHealerChoice && _ViewingAs == _CurrentGame._ActivePlayer); // CHANGED: _AwaitingHealerChoice is shared GameState, synced identically to both machines, so without the _ViewingAs check BOTH players saw "Select a Ship to heal" even though only the active player (whose Healer it is, and who is the only one who can actually click a cell to resolve it) can do anything about it - the other player just sees a confusing banner they can't interact with
        RefreshTurnGlow(viewingRed); // NEW

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

    public void FinishTurnAndRefresh(GameState game, PlayerState turnEndedFor, PlayerState wildcardDrawPlayer = null, List<Card> wildcardDrawnCards = null, bool wildcardReshuffled = false, GridCell revealedCell = null, PlayerState revealedCellOwner = null, CardDisplay missileTarget = null, TargetColor missileColor = default, int missileCount = 1, bool missilePlaysHitSound = false, GridCell sunkCell = null, PlayerState sunkCellOwner = null, TargetColor hitSoundColor = default, PlayerColor attackerColor = default) // CHANGED: now also takes an optional sunk cell (and whose board it's on) - if this exact move is what just sunk a ship, the shockwave plays on that board right after the reveal flip. NEW: hitSoundColor - which impact SFX family actually plays, separate from missileColor (which only drives the missile's sprite/flight) - lets a Destroyer-buffed white missile hitting an ordinary ship sound like a red impact. NEW: attackerColor - who actually fired this missile, so the flight animation launches it from the correct edge of MY OWN screen (see PlayMissileAttack) instead of always assuming "I" fired it
    {
        bool hasReveal = revealedCell != null && revealedCellOwner != null; // NEW
        bool hasMissile = missileTarget != null; // NEW
        bool hasSunk = sunkCell != null && sunkCellOwner != null; // NEW
        bool hasWildcardDraw = wildcardDrawPlayer != null && wildcardDrawnCards != null && wildcardDrawnCards.Count > 0;
        bool hasTurnEndDraw = turnEndedFor != null && game._LastDrawnCards.Count > 0; // CHANGED: used to also require "turnEndedFor._Color == _ViewingAs" - that made sense back when the view auto-followed whoever's turn it was (about to switch away from turnEndedFor), but now each screen is pinned to its own player, so that check was silently skipping this ENTIRE animation whenever the enemy's turn ended. PlayDrawAnimation already decides for itself whether to play the real hand-deal or the face-down enemy flourish based on _ViewingAs - this method doesn't need to gate on it at all


        if (hasReveal || hasMissile || hasSunk || hasWildcardDraw || hasTurnEndDraw)
        {
            // NEW: copy the lists - GameState's own _LastDrawnCards (and the card's _LastDrawnCards) could be overwritten by a later draw before this coroutine gets to them
            StartCoroutine(PlayAllDrawAnimationsThenFinish(
                hasMissile ? missileTarget : null,
                missileColor,
                hitSoundColor,
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
                hasTurnEndDraw && game._LastDrawReshuffled, // NEW
                attackerColor)); // NEW
        }
        else
        {
            SyncViewToActivePlayer();
            RefreshBoardsAndHand();
        }
    }

    private IEnumerator PlayAllDrawAnimationsThenFinish(CardDisplay missileTarget, TargetColor missileColor, TargetColor hitSoundColor, int missileCount, bool missilePlaysHitSound, GridCell revealedCell, PlayerState revealedCellOwner, GridCell sunkCell, PlayerState sunkCellOwner, PlayerState wildcardPlayer, List<Card> wildcardCards, bool wildcardReshuffled, PlayerState turnEndedFor, List<Card> turnEndCards, bool turnEndReshuffled, PlayerColor attackerColor = default) // NEW: attackerColor - threaded through to PlayMissileVolley/PlayMissileAttack so the flight direction is correct. CHANGED: plays the missile volley (if any) first - the shockwave (if any) now plays FROM WITHIN that volley, timed to the exact instant the killing missile hits (same moment as its impact sound), not afterward - then the enemy-cell reveal flip (if any), then the mid-turn wildcard draw (if any, reshuffle merge first if needed), then the turn-end draw-up-to-hand-size draw (if any, same reshuffle merge treatment), then swaps POV as usual
    {
        if (missileTarget != null)
        {
            yield return PlayMissileVolley(missileTarget, missileColor, hitSoundColor, missileCount, missilePlaysHitSound, sunkCell, sunkCellOwner, attackerColor);
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

        BoardDisplay ownBoard = (player._Color == PlayerColor.Red) ? _RedBoardDisplay : _BlueBoardDisplay;
        RectTransform drawPileRect = ownBoard._DrawPileRect;

        if (player._Color != _ViewingAs) // CHANGED: NEW branch - this draw belongs to whichever player ISN'T the one I'm currently viewing (the enemy, from my screen's point of view). I should see something happen at THEIR draw pile (so a Draw3/turn-end draw is never invisible), but I should never see what they actually drew, and it must never touch MY OWN hand display - only the drawing player's own screen (where they ARE _ViewingAs) plays the real, face-up version below.
        {
            RefreshBoardsAndHand(); // still refresh boards/piles/healer prompt as normal - there's no hand of theirs being displayed here to worry about
            foreach (Card card in drawnCards)
            {
                yield return AnimateEnemyDrawnCard(ownBoard, drawPileRect);
            }
            SyncDrawPileVisibility(player);
            yield break;
        }

        foreach (Card card in drawnCards) // NEW: temporarily pull the just-drawn cards back OUT of the hand, so the very next refresh shows the pre-draw hand, not the already-updated one
        {
            player._Hand.Remove(card);
        }

        RefreshBoardsAndHand(); // boards, piles, healer prompt etc. all update immediately - only the hand is (temporarily) missing its newest cards

        RectTransform handRect = (RectTransform)_HandDisplay.transform;

        foreach (Card card in drawnCards)
        {
            yield return AnimateOneDrawnCard(drawPileRect, handRect, player, card);
        }

        SyncDrawPileVisibility(player); // NEW: this draw may have emptied the pile down to its last card - hide its art now that the last flourish has actually landed, so it visibly reads "nothing left here" until the next reshuffle
    }

    private IEnumerator AnimateEnemyDrawnCard(BoardDisplay enemyBoard, RectTransform drawPileRect) // NEW: purely cosmetic version of AnimateOneDrawnCard, played on the OTHER player's screen when the enemy is the one drawing - flies a face-down flourish up off their own draw pile toward the top half of my screen and destroys it. Never touches any PlayerState or hand display - I'm never shown what was actually drawn, and it can't ever land in MY hand since there's nothing for it to land in
    {
        Canvas canvas = drawPileRect.GetComponentInParent<Canvas>();
        RectTransform flourishParent = (canvas != null) ? (RectTransform)canvas.transform : drawPileRect;

        GameObject cardObject = new GameObject("EnemyDrawnCardFlourish");
        cardObject.transform.SetParent(flourishParent, false);
        cardObject.transform.SetAsLastSibling();

        Image cardImage = cardObject.AddComponent<Image>();
        cardImage.sprite = enemyBoard._HandDeckBackSprite; // NEW: always face-down - the whole point is that I never find out what they drew
        cardImage.raycastTarget = false;

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = _HandDisplay._CardDisplayPrefab.GetComponent<RectTransform>().sizeDelta;

        Vector2 from = ConvertWorldCenterToLocal(drawPileRect, flourishParent);
        Vector2 to = from + new Vector2(0f, _EnemyDrawFlourishDistance); // NEW: no real destination to land in (I don't render the enemy's hand at all) - it just travels up toward the top half of the screen and gets destroyed there
        cardRect.anchoredPosition = from;

        AudioManager.Instance?.PlayShoveSFX();

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

        if (cardObject != null)
        {
            Destroy(cardObject);
        }
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

        ownBoard._DrawPileRect.gameObject.SetActive(false); // NEW: fixes "the old draw pile doesn't visibly disappear before the reshuffle" - PlayReshuffleFromDiscard assumes this was already hidden by an EARLIER SyncDrawPileVisibility call (from the pile hitting zero on some previous, separate draw), which is true most of the time. But when a single draw needs more cards than were left and reshuffles mid-draw (e.g. a Draw3 or turn-end draw-up that crosses the empty point in one go), there's no earlier moment where the pile visibly hit zero - the real (stale, technically-already-refilled-in-data) pile art is still active the whole time otherwise, so the flourish just plays on top of it with nothing ever appearing to disappear first. Explicitly hiding it here guarantees it always disappears right as the reshuffle starts, regardless of which of the two cases this is - SyncDrawPileVisibility (called right after this method returns, in PlayDrawAnimation) re-shows the real art once it's actually replenished.

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

        Sprite frontSprite = _HandDisplay._ArtDatabase.GetCardSprite(card, player); // CHANGED: now passes the whole PlayerState, so the Destroyer-enhanced white missile art can be swapped in while its passive is active
        display.SetSprite(frontSprite);
        display._RepresentedCard = card;
        display._ArtDatabase = _HandDisplay._ArtDatabase;
        display._Owner = player;
        display.RefreshWildcardSprite();
        display.SetDamageBoostOverlay(_HandDisplay._ArtDatabase.GetDamageBoostBadge(card, player)); // NEW: matches HandDisplay.ShowHand, so a newly-drawn boosted card doesn't fly in looking plain

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

    public void PlayMissileAndRevealThenRefresh(CardDisplay missileTarget, TargetColor missileColor, int missileCount, bool missilePlaysHitSound, GridCell revealedCell, PlayerState revealedCellOwner, GridCell sunkCell = null, PlayerState sunkCellOwner = null, TargetColor hitSoundColor = default, PlayerColor attackerColor = default) // CHANGED: same missile volley + reveal-flip + shockwave sequence as a normal attack, but for the exact shot that ends the match - TurnController's game-over branch calls this instead of refreshing immediately, so the missile(s), reveal flip, and shockwave (whichever apply) all still visibly play before the Win/Lose screen appears. NEW: attackerColor - who fired the winning shot, so the flight direction is correct
    {
        StartCoroutine(PlayMissileAndRevealThenRefreshRoutine(missileTarget, missileColor, hitSoundColor, missileCount, missilePlaysHitSound, revealedCell, revealedCellOwner, sunkCell, sunkCellOwner, attackerColor));
    }

    private IEnumerator PlayMissileAndRevealThenRefreshRoutine(CardDisplay missileTarget, TargetColor missileColor, TargetColor hitSoundColor, int missileCount, bool missilePlaysHitSound, GridCell revealedCell, PlayerState revealedCellOwner, GridCell sunkCell, PlayerState sunkCellOwner, PlayerColor attackerColor = default)
    {
        if (missileTarget != null)
        {
            yield return PlayMissileVolley(missileTarget, missileColor, hitSoundColor, missileCount, missilePlaysHitSound, sunkCell, sunkCellOwner, attackerColor);
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

    private IEnumerator PlayMissileVolley(CardDisplay targetDisplay, TargetColor missileColor, TargetColor hitSoundColor, int missileCount, bool missilePlaysHitSound, GridCell sunkCell, PlayerState sunkCellOwner, PlayerColor attackerColor = default) // NEW: attackerColor - threaded through to PlayMissileAttack, which is what actually decides the flight direction. CHANGED: also takes the sunk cell (if any) - the shockwave now plays timed to the LAST missile's own impact (same moment as its hit sound), fired at the SUNK ship's board, rather than waiting until after the reveal flip. Fires 'missileCount' missiles at the same target, one shortly after another (_MissileLaunchStagger apart) instead of waiting for each to land before launching the next - a multi-damage red attack (2, 4, or 5 with Cruiser's buff) reads as a volley, not a single shot. Waits for the LAST missile's own flight (plus its shake and/or shockwave, if any) to finish before returning, so whatever plays next (the reveal flip) still waits for the whole thing.
    {
        missileCount = Mathf.Max(1, missileCount);

        for (int i = 0; i < missileCount; i++)
        {
            bool isLastMissile = i == missileCount - 1; // NEW: the shockwave (if this hit sinks a ship) is tied to the LAST missile's own impact - only it gets the sunk-cell info, so a multi-missile volley doesn't retrigger the shockwave once per missile
            StartCoroutine(PlayMissileAttack(targetDisplay, missileColor, hitSoundColor, missilePlaysHitSound, isLastMissile ? sunkCell : null, isLastMissile ? sunkCellOwner : null, attackerColor)); // NEW: each missile flies independently once launched - not yielded on directly, so the next one can launch before this one lands
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

    private IEnumerator PlayMissileAttack(CardDisplay targetDisplay, TargetColor missileColor, TargetColor hitSoundColor, bool missilePlaysHitSound, GridCell sunkCell, PlayerState sunkCellOwner, PlayerColor attackerColor = default) // NEW: attackerColor - decides which edge of THIS screen the missile launches from (see spawnLocalPos below). CHANGED: also takes the sunk cell (if any) - spawns a missile at the bottom of the screen, on top of everything, and flies it in a straight line up to the clicked board cell's center, then destroys it right there - no reparenting, no tucking underneath, just launch -> fly -> arrive -> gone. The actual reveal/damage already happened the instant the card was clicked (see TurnController.OnCellClicked), this is purely the visual that "causes" it
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

        // CHANGED: which edge of THIS screen the missile launches from now depends on who actually
        // fired it, not just always "the bottom" - if I'M the one attacking, it still launches from
        // below me like before. But if my OPPONENT is attacking (their turn, their missile), it
        // should visibly come flying down from the top of my screen - their side - straight down,
        // instead of appearing to come from underneath ME, which would look like I'm attacking myself.
        bool iAmTheAttacker = attackerColor == _ViewingAs;
        float spawnY = iAmTheAttacker ? _MissileSpawnY : -_MissileSpawnY; // same distance from center, just mirrored to the opposite edge
        Vector2 spawnLocalPos = new Vector2(targetLocalPos.x, spawnY);

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
        // Submarine (or, with Destroyer active, any other ship) - any other outcome (including an
        // empty cell, or a white missile blocked by a shield) stays silent on impact. NEW: which
        // clip family actually plays is 'hitSoundColor', not 'missileColor' - a Destroyer-buffed
        // white missile landing on an ordinary ship (not a Submarine) is set by the caller to sound
        // exactly like a red impact, even though the missile itself still flies in as white.
        if (missilePlaysHitSound)
        {
            if (hitSoundColor == TargetColor.Red)
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
        if (LobbySync.Instance != null) // CHANGED: a real networked match - tell the OTHER machine over the network instead of just flipping the local view. Update()'s poll below reacts to the resulting NetworkVariable change on BOTH machines (including this one), showing the correct wait/confirm screen based on who actually asked
        {
            LobbySync.Instance.RequestRematchServerRpc(_ViewingAs);
            return;
        }

        // ORIGINAL local/offline hotseat behavior, unchanged - both players share one screen, so this just flips which one it's currently showing
        _RematchPending = true;
        _RematchRequestedBy = _ViewingAs;
        _ViewingAs = (_ViewingAs == PlayerColor.Red) ? PlayerColor.Blue : PlayerColor.Red; // pass the screen to the other player so they see the confirmation prompt
        RefreshView();
    }

    public void ConfirmRematch() // NEW: opponent agreed - start a fresh match
    {
        AudioManager.Instance?.PlayConfirmSFX(); // NEW

        if (LobbySync.Instance != null) // CHANGED: tells the host to roll a fresh seed and bump RematchStartCount - Update()'s poll then calls BeginMatch() on BOTH machines identically, same lockstep trick as the very first deal
        {
            LobbySync.Instance.ConfirmRematchServerRpc();
            return;
        }

        _RematchPending = false;
        BeginMatch();
    }

    public void DeclineRematch() // NEW: opponent said no - go back to the original result
    {
        AudioManager.Instance?.PlayDeclineSFX(); // NEW

        if (LobbySync.Instance != null) // CHANGED: tells the OTHER machine - Update()'s poll runs the same "go to main menu, or just resume" cleanup on BOTH machines once RematchRequested clears (see HandleRematchRequestCleared)
        {
            LobbySync.Instance.DeclineRematchServerRpc();
            return;
        }

        _RematchPending = false;
        _ViewingAs = _RematchRequestedBy;

        if (_CurrentGame._IsGameOver) // CHANGED: the match had already ended when the rematch was requested (from the Win/Lose screen), so there's no match to return to - send the requester to the main menu instead
        {
            HideAllOverlays(); // NEW: see this method's own comment - without this the requester's own WinScreen/LoseScreen/RematchWaitScreen/InputBlocker (all siblings of InGame, never children of it) stayed stuck on top of MainMenuRoot
            if (_InGameRoot != null) _InGameRoot.SetActive(false);
            if (_MainMenuRoot != null) _MainMenuRoot.SetActive(true);
        }
        else // a rematch requested mid-match (e.g. the top menu's Restart Match button) - just resume where things left off
        {
            RefreshView();
        }
    }

    private void HideAllOverlays() // NEW: WinScreen, LoseScreen, RematchWaitScreen, RematchConfirmPrompt and InputBlocker all live under a panel that's a SIBLING of InGame/MainMenuRoot rather than a child of either - so switching _InGameRoot off and _MainMenuRoot on (see both call sites below) never actually hides whichever one of these happened to be showing at the time. Without this, whoever was mid-prompt (either the rematch requester waiting on a decline, or - in the networked path - the confirm side) landed back on the main menu with that stale prompt still sitting on top of everything.
    {
        if (_WinScreen != null) _WinScreen.SetActive(false);
        if (_LoseScreen != null) _LoseScreen.SetActive(false);
        if (_RematchWaitScreen != null) _RematchWaitScreen.SetActive(false);
        if (_RematchConfirmPrompt != null) _RematchConfirmPrompt.SetActive(false);
        if (_InputBlocker != null) _InputBlocker.SetActive(false);
    }

    private static readonly Color32 _RedGlowColor = new Color32(0xBF, 0x00, 0x00, 220);  // NEW: hex BF0000, alpha 220 - tint applied to whichever glow object is showing when it's RED's turn
    private static readonly Color32 _BlueGlowColor = new Color32(0x00, 0x72, 0xB4, 220); // NEW: hex 0072B4, alpha 220 - tint applied to whichever glow object is showing when it's BLUE's turn

    private void RefreshTurnGlow(bool viewingRed) // CHANGED: a plain "this color = this object" mapping can't be right for both players at once - TopTurnGlow/BottomTurnGlow each sit at a FIXED physical spot on screen, but which color's board is actually near/bottom vs far/top flips depending on which machine is looking (each screen pins ITS OWN color to the near/bottom board - see the viewingRed logic just above). So this combines both: whose turn it is, AND whether that color happens to be the near or far one on THIS screen, to decide WHICH object shows - then tints that object to the active player's actual color (see _RedGlowColor/_BlueGlowColor), since the object itself is no longer color-specific.
    {
        if (_TopTurnGlow == null || _BottomTurnGlow == null)
        {
            return;
        }

        bool showGlow = !_RematchPending && !_CurrentGame._IsGameOver; // NEW: no "whose turn" glow once the match isn't actively being played - a rematch prompt or the win/lose screen already covers this same real estate, and whose turn it WAS stops being useful information at that point
        if (!showGlow)
        {
            _TopTurnGlow.gameObject.SetActive(false);
            _BottomTurnGlow.gameObject.SetActive(false);
            return;
        }

        bool activeIsRed = _CurrentGame._ActivePlayer == PlayerColor.Red;
        bool activeIsNear = activeIsRed == viewingRed; // true if the active player's board is the one sitting near/bottom on THIS screen
        Color32 activeColor = activeIsRed ? _RedGlowColor : _BlueGlowColor;

        _BottomTurnGlow.gameObject.SetActive(activeIsNear);
        _TopTurnGlow.gameObject.SetActive(!activeIsNear);

        if (activeIsNear)
        {
            _BottomTurnGlow.color = activeColor;
        }
        else
        {
            _TopTurnGlow.color = activeColor;
        }
    }

    // ============================================================================================
    // NEW: networked rematch polling. A real match has no shared screen to flip between players, so
    // "Play Again"/confirm/decline above just tell LobbySync what happened over the network - this
    // Update() loop is what actually reacts to that on EACH machine, same Update()-polling pattern
    // already used by HostOnlyButton/ColorChoicePanel/MatchStartWatcher (never a one-shot event
    // subscription) so it can't miss a change just because LobbySync.Instance didn't exist yet at
    // some earlier moment.
    // ============================================================================================

    private bool _LastSeenRematchRequested;
    private int _LastSeenRematchStartCount;
    private int _LastSeenRematchDeclineCount;

    void Update()
    {
        if (LobbySync.Instance == null)
        {
            return; // not a networked match (local/offline testing) - nothing to poll
        }

        // CHANGED: confirm and decline are now each their OWN monotonically-increasing counter,
        // checked independently - NOT inferred from RematchRequested clearing back to false. A CLIENT
        // (not the host) can receive RematchRequested's "false" and RematchStartCount's bump as two
        // separate NetworkVariable updates landing on two different frames, and for that one frame in
        // between, this used to misread an actual CONFIRM as a DECLINE (running the wrong cleanup,
        // including re-disabling the top menu right after BeginMatch had just re-enabled it) - that's
        // exactly what left the requester's screen with the top menu stuck and the wait/confirm prompt
        // in a broken state after a mid-match Restart Match. Two independent counters can't be misread
        // this way, since each only ever means one specific thing.
        int startCount = LobbySync.Instance.RematchStartCount.Value;
        bool justConfirmed = startCount != _LastSeenRematchStartCount;
        if (justConfirmed)
        {
            _LastSeenRematchStartCount = startCount;
        }

        int declineCount = LobbySync.Instance.RematchDeclineCount.Value;
        bool justDeclined = declineCount != _LastSeenRematchDeclineCount;
        if (justDeclined)
        {
            _LastSeenRematchDeclineCount = declineCount;
        }

        bool requested = LobbySync.Instance.RematchRequested.Value;
        if (requested != _LastSeenRematchRequested)
        {
            _LastSeenRematchRequested = requested;

            if (requested)
            {
                HandleRematchRequestedRemotely(LobbySync.Instance.RematchRequestedBy.Value);
            }
            // NEW: no longer reacts to RematchRequested clearing at all - justConfirmed/justDeclined below are the only authority on what happened
        }

        if (justDeclined)
        {
            HandleRematchRequestCleared();
        }

        if (justConfirmed)
        {
            _RematchPending = false;
            BeginMatch(); // NEW: fires identically on BOTH machines the instant a rematch is actually confirmed - reseeds and redeals exactly like the very first match
        }
    }

    private void HandleRematchRequestedRemotely(PlayerColor requestedBy) // NEW: someone (possibly me) just requested a rematch - RefreshView already knows how to show the correct wait/confirm screen based on _RematchRequestedBy vs _ViewingAs, unchanged from the local hotseat version
    {
        _RematchPending = true;
        _RematchRequestedBy = requestedBy;
        RefreshView();
    }

    private void HandleRematchRequestCleared() // NEW: a pending rematch request was DECLINED (see the Update() guard above) - runs identically on both machines, since RematchRequested is shared network state
    {
        _RematchPending = false;
        _ViewingAs = GetLocalPlayerColor(); // CHANGED: always back to viewing MYSELF - there's no more "whoever requested" screen-flip trick once each machine has its own pinned view

        if (_CurrentGame._IsGameOver) // the match had already ended when the rematch was requested - there's no match to return to
        {
            HideAllOverlays(); // NEW: same reasoning as DeclineRematch's local-hotseat branch above - runs on BOTH machines here, since this method fires identically for whoever requested and whoever declined
            if (_InGameRoot != null) _InGameRoot.SetActive(false);
            if (_MainMenuRoot != null) _MainMenuRoot.SetActive(true);
        }
        else // a rematch requested mid-match - just resume where things left off
        {
            RefreshView();
        }
    }
}
