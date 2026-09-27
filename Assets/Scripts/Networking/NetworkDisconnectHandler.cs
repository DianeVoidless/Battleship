using Unity.Netcode;
using UnityEngine;

public class NetworkDisconnectHandler : MonoBehaviour // NEW: watches for the LOCAL player getting disconnected - whether they clicked Back/Leave themselves (see MatchNetworking.LeaveMatch) or the host left and Netcode kicked them - and snaps their screen back to GameChoiceScene automatically. Without this, a client whose host left would just sit frozen wherever they were forever, even though the actual network session is already gone. CHANGED: now also recovers from a disconnect that happens once the match itself has started (InGame), not just one that happens in the lobby (CreateMatchScene) - a disconnect mid-match used to leave the InGame scene sitting there dead, with no way back to the menu at all.
{
    [Header("Drag CreateMatchScene's and GameChoiceScene's root GameObjects here")]
    public GameObject _CreateMatchScene;
    public GameObject _GameChoiceScene;

    [Header("NEW: drag InGame's root GameObject here too - lets this same handler recover from a disconnect that happens mid-match (host left via LeaveMatch, or I did), not just one in the lobby")]
    public GameObject _InGame;

    [Header("NEW: drag the NetworkManager's own MatchNetworking component here - lets the HOST automatically re-host a fresh match (new Relay allocation, new room code) the instant the OTHER player disconnects mid-match, instead of being dumped all the way out to GameChoiceScene for something they didn't choose to do")]
    public MatchNetworking _MatchNetworking;

    [Header("NEW: drag the same WinScreen/LoseScreen/InputBlocker root GameObjects GameTester uses - all three live under a separate OutputPanels object, a SIBLING of InGame rather than a child of it, so deactivating InGame alone (see below) never actually hides them. Without this, a player recovered by this script after the OTHER player ends the match from a Win/Lose screen gets dropped back at the menu with their own stale Win/Lose screen (and its invisible full-screen InputBlocker, silently eating all future clicks) still stuck on top of everything.")]
    public GameObject _WinScreen;
    public GameObject _LoseScreen;
    public GameObject _InputBlocker;

    private bool _PendingRehost; // NEW: true from the moment the host's own Shutdown() (triggered because the OTHER player left) is called, until NetworkManager confirms that old session has actually finished tearing down - Shutdown() isn't necessarily instant, and calling HostMatch() while it's still IsListening would just bail out immediately (HostMatch refuses to start a second session on top of one still shutting down)

    private int _LastSeenClientLeftSignal; // NEW: tracks LobbySync.ClientLeftMidSessionSignal (see that field's own comment for the full story) - this is how the HOST reacts to a CLIENT deliberately clicking Leave Match, since testing proved OnClientDisconnectCallback never fires locally for a machine's OWN self-initiated Shutdown() call. HandleDisconnected below still exists and still matters for the OTHER case: the other player's connection dropping unexpectedly (crash, lost connection, alt-F4) WITHOUT going through that deliberate leave flow - that genuinely is an external event from the host's point of view, and Netcode does raise the callback for it.

    void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleDisconnected;
        }

        _LastSeenClientLeftSignal = LobbySync.ClientLeftMidSessionSignal; // NEW: baseline against the CURRENT value - a static field can already be nonzero from an earlier match this same app run, and we only want to react to it changing FROM HERE ON, not fire immediately on scene start
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleDisconnected;
        }
    }

    void Update() // NEW
    {
        int signal = LobbySync.ClientLeftMidSessionSignal; // NEW: poll the static signal every frame (same Update()-polling pattern used everywhere else in this project) - can't rely on OnClientDisconnectCallback for this specific case (see _LastSeenClientLeftSignal's comment), and can't read it off LobbySync.Instance either since that object is destroyed as part of the very Shutdown() that raises this signal
        if (signal != _LastSeenClientLeftSignal)
        {
            _LastSeenClientLeftSignal = signal;
            if (_InGame != null && _InGame.activeSelf) // only matters if I was actually in a match when this happened - if I was still sitting in the lobby (match hadn't started), CreateMatchScene is already showing, nothing to snap back from
            {
                RecoverHostToFreshLobby();
            }
        }

        if (_PendingRehost && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            _PendingRehost = false;
            Debug.Log("[NetworkDisconnectHandler] TEMP: rehost condition met, _MatchNetworking is " + (_MatchNetworking != null ? "assigned" : "NULL") + " - calling HostMatch() now");
            if (_MatchNetworking != null)
            {
                _MatchNetworking.HostMatch();
            }
        }
    }

    private void HandleDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SnapBackToMenu(); // I'm the one who just disconnected - either I left myself, or the host left and Netcode kicked me
            return;
        }

        // The OTHER player's connection dropped unexpectedly (crash, lost connection, alt-F4) rather
        // than going through the deliberate Leave Match flow (that case is handled by the
        // ClientLeftMidSessionSignal poll in Update() above instead, since this callback never fires
        // for a machine's own self-initiated Shutdown()). A 2-player match can't continue with only one
        // player left, so if I'M the host, end my own hosting too and recover the same way.
        bool iAmTheHost = NetworkManager.Singleton.IsHost;
        if (iAmTheHost && _InGame != null && _InGame.activeSelf)
        {
            NetworkManager.Singleton.Shutdown();
            RecoverHostToFreshLobby();
        }
    }

    private void RecoverHostToFreshLobby() // NEW: shared by both ways the host can end up here (the other player deliberately leaving, or their connection dropping unexpectedly) - sends the host straight back to CreateMatchScene and lines up a fresh re-host, instead of dumping them all the way out to GameChoiceScene for something they never asked for
    {
        SnapBackToCreateMatch();
        _PendingRehost = true; // actual re-hosting is deferred to Update() above, once the old session is confirmed fully torn down
    }

    private void SnapBackToMenu() // NEW: shared cleanup - whichever screen was showing when the disconnect happened, drop back to GameChoiceScene
    {
        HideEndScreens(); // CHANGED: WinScreen/LoseScreen are siblings of InGame, not children of it - clearing them explicitly here is the only way to guarantee they're actually gone, regardless of which branch below runs

        if (_CreateMatchScene.activeSelf) // NEW: only snap back if we were actually looking at the lobby - avoids fighting with whatever screen we're already on if the disconnect happens at some other point
        {
            _CreateMatchScene.SetActive(false);
            _GameChoiceScene.SetActive(true);
        }
        else if (_InGame != null && _InGame.activeSelf) // NEW: same recovery, but for a disconnect that happens once the match itself has actually started
        {
            _InGame.SetActive(false);
            _GameChoiceScene.SetActive(true);
        }
    }

    private void SnapBackToCreateMatch() // NEW: used only for the HOST when the OTHER player disconnects mid-match (see HandleDisconnected above) - goes straight back to CreateMatchScene (about to be re-hosted fresh in Update() once the old session finishes tearing down) instead of all the way out to GameChoiceScene, since the host never asked to leave
    {
        HideEndScreens(); // CHANGED: same reasoning as SnapBackToMenu above - WinScreen/LoseScreen won't hide just because InGame does

        if (_InGame != null)
        {
            _InGame.SetActive(false);
        }
        if (_CreateMatchScene != null)
        {
            _CreateMatchScene.SetActive(true);
        }
    }

    private void HideEndScreens() // NEW: WinScreen, LoseScreen and InputBlocker all live under OutputPanels, a sibling of InGame in the hierarchy - not a child of it - so nothing here ever gets hidden as a side effect of InGame.SetActive(false). Whoever's game is being ended by the OTHER player leaving needs this called explicitly, or their stale Win/Lose screen (and its InputBlocker) stays stuck on screen even after they're dropped back to a menu underneath it - InputBlocker in particular is invisible, so it silently eats every click on whatever menu they land on next until the game is restarted.
    {
        if (_WinScreen != null)
        {
            _WinScreen.SetActive(false);
        }
        if (_LoseScreen != null)
        {
            _LoseScreen.SetActive(false);
        }
        if (_InputBlocker != null)
        {
            _InputBlocker.SetActive(false);
        }
    }
}
