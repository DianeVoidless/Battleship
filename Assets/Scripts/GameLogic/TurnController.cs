using UnityEngine;
using System.Collections.Generic; // NEW: for the List<Card> of wildcard-drawn cards passed to FinishTurnAndRefresh
using Unity.Netcode; // NEW: for NetworkManager.Singleton.IsHost, used to decide which RPC direction to relay a move through

public class TurnController : MonoBehaviour
{
    public static TurnController _Instance;
    public GameTester _GameTester;
    public HandProximityZone _HandProximityZone;
    public HandDisplay _HandDisplay;

    private Card _PendingCard;
    private System.Collections.Generic.List<Card> _SelectedHandCards = new System.Collections.Generic.List<Card>(); // NEW: which hand cards the player has picked so far for a Cleanse-style multi-select

    void Awake()
    {
        _Instance = this;
    }

    private void CancelPendingCard() // NEW: shared cleanup for whenever a pending card selection is abandoned (clicking elsewhere, clicking a different card)
    {
        if (_PendingCard != null && _PendingCard.GetTargetMode() == CardTargetMode.HandMultiSelect && _PendingCard is UtilityCard utilityCard)
        {
            utilityCard.ChooseBranch(CardBranch.NotChosen); // NEW: otherwise this exact card instance gets stuck in Cleanse mode forever
        }
        _SelectedHandCards.Clear(); // NEW
        _HandDisplay.UnpinAllCards();
        _HandProximityZone.ReleaseForceLowerHand();
        _HandProximityZone.UnpinHand(); // NEW: release the whole-hand-raised state a Cleanse selection may have set
        _PendingCard = null;
    }

    public void OnCardClicked(CardDisplay display, Card card)
    {
        GameState game = _GameTester.GetGame();
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        if (!activePlayer._Hand.Contains(card))
        {
            return;
        }

        if (_PendingCard != null && _PendingCard.GetTargetMode() == CardTargetMode.HandMultiSelect) // NEW: mid-Cleanse - clicking hand cards toggles selection; confirming happens by clicking the draw pile instead
        {
            if (card == _PendingCard)
            {
                return;
            }

            if (!_PendingCard.IsLegalHandCard(card))
            {
                return;
            }

            if (_SelectedHandCards.Contains(card))
            {
                _SelectedHandCards.Remove(card);
                _HandDisplay.UnpinCard(card);
            }
            else
            {
                _SelectedHandCards.Add(card);
                _HandDisplay.PinCard(card);
            }
            return;
        }

        if (_PendingCard != null && card != _PendingCard)
        {
            CancelPendingCard(); // CHANGED: shared cleanup, so a mid-selection wildcard's branch also gets reset
        }

        CardTargetMode mode = card.GetTargetMode();

        if (mode == CardTargetMode.HandMultiSelect)
        {
            return;
        }

        if (mode == CardTargetMode.BranchChoice)
        {
            return;
        }

        _PendingCard = card;
        _HandProximityZone.ForceLowerHand();
        _HandDisplay.PinCard(card);
    }

    public void OnDrawPileClicked(PlayerColor owner) // NEW: clicking your own draw pile confirms a Cleanse selection in progress
    {
        if (_PendingCard == null || _PendingCard.GetTargetMode() != CardTargetMode.HandMultiSelect)
        {
            return; // not mid-Cleanse - clicking the draw pile does nothing right now
        }

        GameState game = _GameTester.GetGame();

        if (owner != game._ActivePlayer)
        {
            return;
        }

        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;
        ConfirmHandMultiSelect((UtilityCard)_PendingCard, activePlayer);
    }

    private void ConfirmHandMultiSelect(UtilityCard card, PlayerState activePlayer) // NEW: player clicked their draw pile to confirm the Cleanse selection
    {
        int[] selectedIdsToRelay = new int[_SelectedHandCards.Count]; // NEW: captured now, before this method clears _SelectedHandCards below
        for (int i = 0; i < _SelectedHandCards.Count; i++)
        {
            selectedIdsToRelay[i] = _SelectedHandCards[i]._Id;
        }
        SendCleanseConfirmToNetwork(activePlayer._Color, card._Id, card._ChosenBranch, selectedIdsToRelay); // NEW: tell the other machine which cards were cleansed

        card.ResolveHandSelection(activePlayer, _SelectedHandCards); // NEW: this already drew the replacement cards into activePlayer._Hand via UtilityCard.DrawCards, which also stashed them in card._LastDrawnCards for the animation below

        List<Card> wildcardDrawnCards = new List<Card>(card._LastDrawnCards); // NEW: grabbed now, before OnDiscarded()/reuse can touch the card again
        bool wildcardReshuffled = card._LastDrawReshuffled; // NEW: whether that draw had to reshuffle the discard pile back in - grabbed now for the same reason

        activePlayer._Hand.Remove(card);
        activePlayer._DiscardPile.Add(card);

        GameState game = _GameTester.GetGame();
        game.AddMoves(card.GetBonusMoves());
        card.OnDiscarded();
        PlayerState turnEndedFor = game.SpendMove(); // CHANGED: capture whose turn just ended (if any), instead of switching view immediately - GameTester now decides when to actually swap POV, so it can play the draw-up animation first

        _HandDisplay.UnpinAllCards();
        _HandProximityZone.ReleaseForceLowerHand();
        _HandProximityZone.UnpinHand(); // NEW: release the whole-hand-raised state Cleanse selection set
        _SelectedHandCards.Clear();
        _PendingCard = null;
        _GameTester.FinishTurnAndRefresh(game, turnEndedFor, activePlayer, wildcardDrawnCards, wildcardReshuffled); // CHANGED: now also passes along Cleanse's replacement cards (and whether the draw pile had to reshuffle), so they visibly slide in from the draw pile instead of just appearing
    }

    public void OnCellClicked(CardDisplay display, GridCell cell)
    {
        GameState game = _GameTester.GetGame(); // MOVED: needed up here now, for the Healer check below
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue; // MOVED

        if (game._AwaitingHealerChoice) // NEW: the new active player's Healer needs a damaged ship picked before anything else can happen
        {
            bool isOwnDamagedCell = activePlayer._Grid.Contains(cell) && cell._Revealed && cell._DamageInstances.Count > 0 && !cell.IsSunk(); // CHANGED: exclude sunk cells - same fix as UtilityCard's Heal checks, so a sunk ship's leftover damage can't be picked here either
            if (!isOwnDamagedCell)
            {
                return;
            }

            SendCellActionToNetwork(default, -1, CardBranch.NotChosen, activePlayer._Color, activePlayer._Grid.IndexOf(cell)); // NEW: tell the other machine "the Healer picked this cell" - cardId -1 marks this as a Healer choice, not a played card, so the receiving side knows to skip straight to the healer branch below instead of expecting a pending card

            cell.RemoveHighestDamage();
            game._AwaitingHealerChoice = false;

            PlayerState healerTurnEndedFor = game.PassIfStuck(); // CHANGED: renamed from turnEndedFor - this method declares another local of that same name further down, and C# doesn't allow a nested block to reuse a name already used in the method's enclosing scope, even in a branch that returns before reaching it. If this player's hand turns out to have zero legal plays left (e.g. it's nothing but Shield cards with no revealed own ship to target), don't leave the match waiting on an action that can never come
            if (healerTurnEndedFor != null)
            {
                _GameTester.FinishTurnAndRefresh(game, healerTurnEndedFor);
            }
            else
            {
                _GameTester.RefreshBoardsAndHand();
            }
            return;
        }

        if (_PendingCard == null)
        {
            return;
        }

        PlayerState opponent = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerBlue : game._PlayerRed;

        CardTargetMode mode = _PendingCard.GetTargetMode();

        if (mode == CardTargetMode.BranchChoice)
        {
            return;
        }

        bool isOwnCell = activePlayer._Grid.Contains(cell);
        bool isEnemyCell = opponent._Grid.Contains(cell);

        if (mode == CardTargetMode.EnemyCell && !isEnemyCell)
        {
            return;
        }

        if (mode == CardTargetMode.OwnCell && !isOwnCell)
        {
            return;
        }

        if (!_PendingCard.IsLegalTarget(cell))
        {
            return;
        }

        CardBranch cardBranchToRelay = (_PendingCard is UtilityCard pendingUtilityCard) ? pendingUtilityCard._ChosenBranch : CardBranch.NotChosen; // NEW: captured now, before anything below can change it - the receiving machine needs this to put its own copy of the card into the same branch (Heal vs the OwnCell target mode wouldn't otherwise make sense) before replaying the rest of this method
        SendCellActionToNetwork(activePlayer._Color, _PendingCard._Id, cardBranchToRelay, isOwnCell ? activePlayer._Color : opponent._Color, (isOwnCell ? activePlayer : opponent)._Grid.IndexOf(cell)); // NEW: every legality check above has already passed, so this move is definitely going through - tell the other machine now, before any of the local state below starts changing

        bool wasSunkBefore = cell.IsSunk(); // NEW: snapshot so we can tell the exact moment a ship becomes captured
        bool wasRevealedBefore = cell._Revealed; // NEW: snapshot so we can tell whether this attack is the one that FIRST reveals an enemy cell, for the reveal-flip animation

        AttackCard attackCard = _PendingCard as AttackCard; // NEW: only an attack card fires a missile - Heal/Shield/wildcards resolve with no projectile
        CardDisplay missileTargetDisplay = (attackCard != null) ? display : null; // NEW: 'display' is already the exact CardDisplay the player clicked - that's the missile's destination
        TargetColor missileColor = (attackCard != null) ? attackCard._Color : default;

        // NEW: a red missile card launches one missile PER POINT of damage it deals (2, 4, or 5 with
        // Cruiser's +1 buff all fire that many missiles in a row), so a heavier hit visibly reads as
        // a bigger volley. White missiles always deal exactly 1 damage, so they always stay a single
        // missile - no need to special-case them here.
        int missileCount = (attackCard != null && attackCard._Color == TargetColor.Red)
            ? attackCard._Damage + (activePlayer.HasActiveShip(ShipType.Cruiser) ? 1 : 0)
            : 1;

        // NEW: mirrors the exact same Destroyer check AttackCard.Resolve makes internally - needed
        // here too so the impact sound (and its gating) matches what Resolve is actually about to do.
        bool canHitAnyShip = attackCard != null && attackCard._Color == TargetColor.White && activePlayer.HasActiveShip(ShipType.Destroyer);

        // NEW: a shield is only ever poppable by a red missile - while one is up, a white missile is
        // fully blocked (no shield damage, no hull damage, nothing), whether it's protecting a
        // Submarine or (via Destroyer) any other ship. Checked before Resolve() since white attacks
        // never touch a shield layer either way, so its value here is exactly what Resolve will also see.
        bool whiteBlockedByShield = attackCard != null && attackCard._Color == TargetColor.White && cell.HasShield();

        // CHANGED: whether the missile plays an impact sound depends on color AND what it's actually
        // hitting. Red missiles always launch, but their impact sound is reserved for a genuine hit
        // on a ship OTHER than a Submarine (red can't even damage a Submarine's hull - see AttackCard
        // - and an empty cell shouldn't clang either). White missiles play their impact sound on a
        // genuine hit too: a Submarine (its usual target), or - now that Destroyer can buff them to
        // hit anything - any other real ship, as long as a shield isn't blocking it entirely.
        bool missilePlaysHitSound = attackCard == null
            ? false
            : (attackCard._Color == TargetColor.Red)
                ? (cell._Ship != ShipType.None && cell._Ship != ShipType.Submarine)
                : (!whiteBlockedByShield && (cell._Ship == ShipType.Submarine || (canHitAnyShip && cell._Ship != ShipType.None)));

        // NEW: which impact SFX family actually plays - normally the same as the missile's own color,
        // EXCEPT a Destroyer-buffed white missile landing on an ordinary ship (not a Submarine), which
        // now sounds exactly like a red missile impact even though it still flies in and looks white.
        TargetColor missileHitSoundColor = (attackCard != null && attackCard._Color == TargetColor.White && canHitAnyShip && cell._Ship != ShipType.None && cell._Ship != ShipType.Submarine)
            ? TargetColor.Red
            : missileColor;

        _PendingCard.Resolve(cell, activePlayer); // CHANGED: now passes the active player as owner
        AudioManager.Instance?.PlayPlaceSFX(); // NEW: the card was just successfully committed to a board cell

        bool justSunk = !wasSunkBefore && cell.IsSunk(); // NEW: this attack just brought the ship to 0 HP
        PlayerState sunkCellOwner = isOwnCell ? activePlayer : opponent; // NEW: whoever's board this sunk cell actually lives on (same pattern as the Carrier check further below) - the shockwave plays on THIS player's board, not necessarily the attacker's
        GridCell sunkCell = justSunk ? cell : null; // NEW: passed through to GameTester so it can play the shockwave on the correct board, right after the reveal flip

        if (justSunk) // credit whoever landed the hit
        {
            activePlayer._CapturedShipCount++;
        }

        if (opponent.AreAllShipsSunk()) // NEW: every enemy ship is gone - the active player just won, stop the match right here
        {
            game._IsGameOver = true;
            game._WinningPlayer = activePlayer._Color;

            activePlayer._Hand.Remove(_PendingCard);
            activePlayer._DiscardPile.Add(_PendingCard);
            _PendingCard.OnDiscarded();

            _PendingCard = null;
            _HandProximityZone.ReleaseForceLowerHand();

            bool isNewEnemyRevealForWin = !wasRevealedBefore && isEnemyCell && cell._Revealed; // NEW: same first-reveal check as the normal path below, for the exact shot that wins the match
            _GameTester.PlayMissileAndRevealThenRefresh(missileTargetDisplay, missileColor, missileCount, missilePlaysHitSound, isNewEnemyRevealForWin ? cell : null, isNewEnemyRevealForWin ? opponent : null, sunkCell, sunkCellOwner, missileHitSoundColor, activePlayer._Color); // CHANGED: was a plain reveal-only refresh - the winning shot still gets its missile volley (and reveal flip, if this was also a first-time reveal), plus the shockwave if this exact shot is what sunk the last ship, before the Win/Lose screen appears. NEW: also passes who fired the winning shot, so the flight direction is correct on both screens
            return; // NEW: no more turn processing once the match is decided - don't switch the active player or trigger the next turn's Healer
        }

        if (cell._Ship == ShipType.Carrier && cell._Revealed) // NEW: Carrier's hand-size bonus applies the instant it's revealed, not just at a turn boundary
        {
            PlayerState carrierOwner = isOwnCell ? activePlayer : opponent; // NEW: whoever actually owns this cell, not necessarily whoever just played the card
            int handCap = carrierOwner.HasActiveShip(ShipType.Carrier) ? 7 : 5;
            carrierOwner.DrawUpToHandSize(handCap);
            _GameTester.SyncDrawPileVisibility(carrierOwner); // NEW: this instant draw isn't animated, but it can still empty (or replenish) the draw pile - keep its art in sync either way
        }

        activePlayer._Hand.Remove(_PendingCard); // MOVED: must happen before SpendMove(), since that can trigger the end-of-turn draw-up-to-5 check
        activePlayer._DiscardPile.Add(_PendingCard); // MOVED

        game.AddMoves(_PendingCard.GetBonusMoves()); // CHANGED: must read GetBonusMoves() before OnDiscarded() resets the chosen branch
        _PendingCard.OnDiscarded(); // NEW: reset any wildcard's chosen branch so it works correctly if drawn again later
        PlayerState turnEndedFor = game.SpendMove(); // CHANGED: capture whose turn just ended (if any) instead of switching view immediately - GameTester now decides when to swap POV, so it can play the draw-up animation first

        _PendingCard = null;
        _HandProximityZone.ReleaseForceLowerHand();

        bool isNewEnemyReveal = !wasRevealedBefore && isEnemyCell && cell._Revealed; // NEW: true only when this exact attack is what first revealed an enemy cell - a re-attack on an already-revealed cell (or hitting your own cell) shouldn't flip anything
        _GameTester.FinishTurnAndRefresh(game, turnEndedFor, null, null, false, isNewEnemyReveal ? cell : null, isNewEnemyReveal ? opponent : null, missileTargetDisplay, missileColor, missileCount, missilePlaysHitSound, sunkCell, sunkCellOwner, missileHitSoundColor, activePlayer._Color); // CHANGED: also passes the missile target/count/hit-sound and, separately, the sunk cell (if this move is what just sunk it) so GameTester flies the volley, then the reveal flip, then the shockwave on its board, before any draw animation. NEW: also passes who fired this shot, so the flight direction is correct on both screens
    }

    public void OnBackgroundClicked()
    {
        if (_PendingCard == null)
        {
            return;
        }

        CancelPendingCard(); // CHANGED: shared cleanup, so cancelling out of a Cleanse selection also resets its branch
    }

    public void OnWildcardBranchClicked(CardDisplay display, UtilityCard card, bool isGatedZone)
    {
        GameState game = _GameTester.GetGame();
        PlayerState activePlayer = (game._ActivePlayer == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        if (!activePlayer._Hand.Contains(card))
        {
            return;
        }

        if (_PendingCard != null && card != _PendingCard)
        {
            CancelPendingCard(); // CHANGED: shared cleanup, so a mid-selection wildcard's branch also gets reset
        }

        CardBranch branch = GetBranchForZone(card._Type, isGatedZone);

        if (isGatedZone && !card.IsBranchAvailable(branch, activePlayer))
        {
            return;
        }

        // NEW: re-picking a branch on the SAME already-pending card (e.g. correcting a misclicked
        // Heal into Draw3) skips CancelPendingCard above since card == _PendingCard, but any
        // in-progress Cleanse selection or hand-raise state from the OLD branch still needs clearing
        // before the new one takes over - otherwise it lingers (this is the other half of the "stuck"
        // fix, alongside CardDisplay now always routing here).
        if (card._ChosenBranch != branch)
        {
            _SelectedHandCards.Clear();
            _HandDisplay.UnpinAllCards();
            _HandProximityZone.ReleaseForceLowerHand();
            _HandProximityZone.UnpinHand();
        }

        card.ChooseBranch(branch);

        CardTargetMode mode = card.GetTargetMode();

        if (mode == CardTargetMode.None)
        {
            SendNoTargetResolveToNetwork(activePlayer._Color, card._Id, card._ChosenBranch); // NEW: tell the other machine which no-target branch (Draw3 or Extra Play) was just resolved
            ResolveNoTargetCard(card, activePlayer);
            return;
        }

        _PendingCard = card;
        _HandDisplay.PinCard(card);

        if (mode == CardTargetMode.HandMultiSelect)
        {
            _SelectedHandCards.Clear(); // NEW: start a fresh selection for Cleanse
            _HandProximityZone.PinHand(); // NEW: keep the whole hand raised while picking cards, instead of forcing it low like a board-target card would
            return;
        }

        _HandProximityZone.ForceLowerHand(); // MOVED: only board/cell-targeting branches should force the hand back down
    }

    private CardBranch GetBranchForZone(UtilityType type, bool isGatedZone)
    {
        if (type == UtilityType.CleanseOrExtraPlay)
        {
            return isGatedZone ? CardBranch.Cleanse : CardBranch.ExtraPlay;
        }
        return isGatedZone ? CardBranch.Heal : CardBranch.Draw3;
    }

    private void ResolveNoTargetCard(Card card, PlayerState activePlayer)
    {
        card.ResolveNoTarget(activePlayer); // NEW: for Draw3, this already drew the cards into activePlayer._Hand via UtilityCard.DrawCards, which also stashed them in card._LastDrawnCards for the animation below

        UtilityCard utilityCard = card as UtilityCard; // NEW: cast once, reused below for both the drawn cards and whether that draw had to reshuffle
        List<Card> wildcardDrawnCards = (utilityCard != null) ? new List<Card>(utilityCard._LastDrawnCards) : null; // NEW: grabbed now, before OnDiscarded()/reuse can touch the card again
        bool wildcardReshuffled = utilityCard != null && utilityCard._LastDrawReshuffled; // NEW

        activePlayer._Hand.Remove(card); // MOVED: must happen before SpendMove()
        activePlayer._DiscardPile.Add(card); // MOVED

        GameState game = _GameTester.GetGame();
        game.AddMoves(card.GetBonusMoves()); // CHANGED: must read GetBonusMoves() before OnDiscarded() resets the chosen branch
        card.OnDiscarded(); // NEW: reset any wildcard's chosen branch so it works correctly if drawn again later
        PlayerState turnEndedFor = game.SpendMove(); // CHANGED: capture whose turn just ended (if any) instead of switching view immediately - GameTester now decides when to swap POV, so it can play the draw-up animation first

        _PendingCard = null;
        _HandProximityZone.ReleaseForceLowerHand(); // NEW: defensive - if this card reached here by switching FROM a board-targeting branch (e.g. Heal corrected into Draw3), that earlier branch already forced the hand low and nothing had released it yet
        _GameTester.FinishTurnAndRefresh(game, turnEndedFor, activePlayer, wildcardDrawnCards, wildcardReshuffled); // CHANGED: now also passes along any Draw3 cards (and whether the draw pile had to reshuffle), so they visibly slide in from the draw pile instead of just appearing
    }

    public Card GetPendingCard()
    {
        return _PendingCard;
    }

    // ============================================================================================
    // NEW: networked-match move relay. Everything above this point is the same local card-game
    // logic as before, running purely on whichever machine's player clicked something - none of it
    // knows or cares whether a match is networked at all. The three Send*ToNetwork helpers hook
    // into that logic at the exact moments a move is DEFINITELY going to happen (every legality
    // check has already passed), and tell the other machine "the same thing you'd see if this had
    // been YOUR own click" - identified by stable card IDs and grid indices (see Card._Id and
    // GameSetup.BuildCardDeck) since actual object references can't cross the network. The three
    // Receive* methods are the mirror image: given those same IDs, they reconstruct the exact
    // local objects and feed them into this SAME code (OnCellClicked, ConfirmHandMultiSelect,
    // ResolveNoTargetCard) so the other machine plays out the identical move - missile flights,
    // reveal flips, card-deal animations and all - instead of just being told the final numbers.
    // This only works because GameSetup.SeedSharedRng makes both machines deal an IDENTICAL board
    // and hand from the same seed - from there, replaying the same sequence of moves in the same
    // order keeps them in perfect lockstep with zero extra state ever needing to be transmitted.
    // ============================================================================================

    private Card FindCardInHand(PlayerState owner, int cardId) // NEW: resolves a networked card ID back into THIS machine's own local Card instance - only ever needs to look in the hand, since every action a network message can describe only involves a card the active player currently holds
    {
        foreach (Card card in owner._Hand)
        {
            if (card._Id == cardId)
            {
                return card;
            }
        }
        return null; // safety - shouldn't happen if both machines are actually in sync
    }

    private void SendCellActionToNetwork(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, PlayerColor cellOwner, int cellIndex)
    {
        MatchGameRelay relay = MatchGameRelay.Instance;
        if (relay == null)
        {
            return; // NEW: not a networked match (local/offline testing) - nothing to relay
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            relay.NotifyCellActionClientRpc(cardOwner, cardId, chosenBranch, cellOwner, cellIndex);
        }
        else
        {
            relay.RequestCellActionServerRpc(cardOwner, cardId, chosenBranch, cellOwner, cellIndex);
        }
    }

    private void SendCleanseConfirmToNetwork(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, int[] selectedCardIds)
    {
        MatchGameRelay relay = MatchGameRelay.Instance;
        if (relay == null)
        {
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            relay.NotifyCleanseConfirmClientRpc(cardOwner, cardId, chosenBranch, selectedCardIds);
        }
        else
        {
            relay.RequestCleanseConfirmServerRpc(cardOwner, cardId, chosenBranch, selectedCardIds);
        }
    }

    private void SendNoTargetResolveToNetwork(PlayerColor cardOwner, int cardId, CardBranch chosenBranch)
    {
        MatchGameRelay relay = MatchGameRelay.Instance;
        if (relay == null)
        {
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            relay.NotifyNoTargetResolveClientRpc(cardOwner, cardId, chosenBranch);
        }
        else
        {
            relay.RequestNoTargetResolveServerRpc(cardOwner, cardId, chosenBranch);
        }
    }

    public void ReceiveCellAction(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, PlayerColor cellOwner, int cellIndex) // NEW: called by MatchGameRelay on whichever machine did NOT originate this move
    {
        GameState game = _GameTester.GetGame();
        PlayerState cellOwnerState = (cellOwner == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        if (cellIndex < 0 || cellIndex >= cellOwnerState._Grid.Count)
        {
            return; // safety - shouldn't happen if both machines are actually in sync
        }
        GridCell cell = cellOwnerState._Grid[cellIndex];

        CardDisplay targetDisplay = null;

        if (cardId >= 0) // a played card, not a Healer choice
        {
            PlayerState cardOwnerState = (cardOwner == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;
            Card card = FindCardInHand(cardOwnerState, cardId);
            if (card == null)
            {
                return; // safety
            }

            if (card is UtilityCard utilityCard && chosenBranch != CardBranch.NotChosen)
            {
                utilityCard.ChooseBranch(chosenBranch); // NEW: this machine never saw the branch-choice click itself - apply it directly to THIS machine's own copy of the card before replaying the rest of the move below
            }

            BoardDisplay boardForMissile = (cellOwner == PlayerColor.Red) ? _GameTester._RedBoardDisplay : _GameTester._BlueBoardDisplay;
            targetDisplay = boardForMissile.FindDisplayForCell(cell); // NEW: resolves THIS machine's own local CardDisplay for the missile animation - CardDisplay instances are per-machine UI objects and never cross the network themselves

            _PendingCard = card; // NEW: puts this machine's TurnController into the exact same state a real click would have - from here, the normal OnCellClicked logic below takes over unmodified
        }

        OnCellClicked(targetDisplay, cell); // NEW: replays the SAME method a real click would have called - covers both the Healer-choice branch (when cardId was -1, _PendingCard stays whatever it already was, normally null) and the play-a-card branch (when _PendingCard was just set above)
    }

    public void ReceiveCleanseConfirm(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, int[] selectedCardIds) // NEW: called by MatchGameRelay on whichever machine did NOT originate this move
    {
        GameState game = _GameTester.GetGame();
        PlayerState owner = (cardOwner == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        if (!(FindCardInHand(owner, cardId) is UtilityCard card))
        {
            return; // safety
        }

        card.ChooseBranch(chosenBranch); // NEW: this machine never saw the branch-choice click - apply it directly before replaying the confirm below

        List<Card> selected = new List<Card>();
        foreach (int id in selectedCardIds)
        {
            Card selectedCard = FindCardInHand(owner, id);
            if (selectedCard != null)
            {
                selected.Add(selectedCard);
            }
        }

        _SelectedHandCards = selected; // NEW: puts this machine's TurnController into the exact same state a real Cleanse selection would have
        _PendingCard = card;

        ConfirmHandMultiSelect(card, owner); // NEW: replays the SAME method a real draw-pile click (confirming Cleanse) would have called
    }

    public void ReceiveNoTargetResolve(PlayerColor cardOwner, int cardId, CardBranch chosenBranch) // NEW: called by MatchGameRelay on whichever machine did NOT originate this move
    {
        GameState game = _GameTester.GetGame();
        PlayerState owner = (cardOwner == PlayerColor.Red) ? game._PlayerRed : game._PlayerBlue;

        Card card = FindCardInHand(owner, cardId);
        if (card == null)
        {
            return; // safety
        }

        if (card is UtilityCard utilityCard)
        {
            utilityCard.ChooseBranch(chosenBranch); // NEW: this machine never saw the branch-choice click - apply it directly before replaying the resolve below
        }

        ResolveNoTargetCard(card, owner); // NEW: replays the SAME method a real wildcard-branch click (Draw3 or Extra Play) would have called
    }
}
