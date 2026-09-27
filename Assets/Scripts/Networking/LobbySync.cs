using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbySync : NetworkBehaviour // NEW: the first real piece of synced network state - lets both players' CreateMatchScene screens show the ACTUAL other player's username and the host's ACTUAL chosen color, instead of each screen only ever knowing about its own local player. Lives on a dedicated NetworkObject the host spawns the moment hosting starts (see MatchNetworking.HostMatch) - Netcode automatically creates a matching copy of this object on every client that connects afterward.
{
    public static LobbySync Instance; // NEW: simple singleton so ColorChoicePanel can read this without extra scene wiring - there's only ever one of these per match

    // NEW: Server-only write permission (the default) means only the host can ever set these directly -
    // a joining client can READ them (that's what syncs the values down), but has to go through the
    // ServerRpc below to ask the host to change ClientUsername on its behalf.
    public NetworkVariable<FixedString32Bytes> HostUsername = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<FixedString32Bytes> ClientUsername = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<bool> HostIsRed = new NetworkVariable<bool>();
    public NetworkVariable<bool> MatchStarted = new NetworkVariable<bool>(); // NEW: flips true once the host presses Start Match - both screens watch this (see MatchStartWatcher) to move into InGame together at the same moment, instead of each player's own button just switching their own screen locally
    public NetworkVariable<int> MatchSeed = new NetworkVariable<int>(); // NEW: the host picks one random number the instant this object spawns and both machines feed it into GameSetup.SeedSharedRng before dealing (see GameTester.BeginMatch) - since the rest of the card game never calls System.Random anywhere else, this one shared seed is enough to make both machines deal out the SAME board and hands, with no need to actually transmit the board/hand data itself
    public NetworkVariable<bool> RematchRequested = new NetworkVariable<bool>(); // NEW: true while a "Play Again" (or mid-match "Restart Match") request is awaiting the other player's answer
    public NetworkVariable<PlayerColor> RematchRequestedBy = new NetworkVariable<PlayerColor>(); // NEW: who asked - lets each screen tell "I'm the one waiting" apart from "I'm the one being asked"
    public NetworkVariable<int> RematchStartCount = new NetworkVariable<int>(); // NEW: bumped by exactly 1 every time a rematch is actually confirmed - GameTester polls this (same Update()-polling pattern as MatchStartWatcher/ColorChoicePanel) and calls BeginMatch() on BOTH machines the instant it changes, reusing MatchSeed (re-rolled below) for the new match
    public NetworkVariable<int> RematchDeclineCount = new NetworkVariable<int>(); // NEW: bumped by exactly 1 every time a rematch is actually declined - kept as its own separate counter (rather than just watching RematchRequested clear back to false) because a CLIENT can receive RematchRequested's "false" and RematchStartCount's bump on two different frames, and briefly misreading a CONFIRM as a DECLINE (or the reverse) was exactly what left the requester's screen stuck with the top menu disabled and the wait/confirm prompt in a wrong state

    public override void OnNetworkSpawn()
    {
        Instance = this;

        if (IsServer) // NEW: the host IS the server here - set their own username the moment this object exists, no RPC needed since only the server can write these anyway
        {
            HostUsername.Value = new FixedString32Bytes(PlayerPrefs.GetString(ClonePrefs.Key(GameplaySettings.UsernameKey), "Player"));
            MatchSeed.Value = new System.Random().Next(); // NEW: one shared deal seed for the whole match, picked once, the moment hosting starts - both machines already agree on this number by the time Start Match is pressed, since NetworkVariables replicate their current value to a client the instant it connects
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleAnyClientDisconnected; // NEW: only the HOST needs to react to some OTHER client leaving - clears their synced username so the lobby stops showing a player who's actually gone
        }
        else // NEW: a joining client can't write NetworkVariables directly (Server-only write permission) - so it asks the server to do it via RPC
        {
            SubmitClientUsernameServerRpc(PlayerPrefs.GetString(ClonePrefs.Key(GameplaySettings.UsernameKey), "Player"));
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleAnyClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleAnyClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId)
        {
            return; // NEW: that's the HOST's own id - the host leaving on purpose is already handled by MatchNetworking.LeaveMatch, nothing to clear here
        }

        ClientUsername.Value = default; // NEW: the joining player actually disconnected - clear their name so the lobby correctly falls back to "Waiting for opponent..." instead of showing someone who's no longer there
    }

    [ServerRpc(RequireOwnership = false)] // NEW: RequireOwnership false - this object is owned by the host, but any connected CLIENT needs to be able to call this to report their own name
    private void SubmitClientUsernameServerRpc(string username)
    {
        ClientUsername.Value = new FixedString32Bytes(username);
    }

    public void SetHostIsRed(bool isRed) // NEW: called locally by ColorChoicePanel.ToggleColor, but only ever on the host's own screen (the color-swap button is host-only) - safe to set directly since the host IS the server, no RPC needed
    {
        if (IsServer)
        {
            HostIsRed.Value = isRed;
        }
    }

    public void TriggerMatchStart() // NEW: called locally by MatchNetworking.StartMatch, but only ever on the host's own screen (Start Match is host-only) - safe to set directly since the host IS the server, no RPC needed
    {
        if (IsServer)
        {
            MatchStarted.Value = true;
        }
    }

    // NEW: rematch flow - unlike the fields above, EITHER player can click "Play Again"/confirm/decline,
    // not just the host, so these always go through a ServerRpc (RequireOwnership false lets any
    // connected client call it) rather than a direct set - a host calling its own ServerRpc runs the
    // body directly with no extra network hop, so this works identically no matter who's hosting.

    [ServerRpc(RequireOwnership = false)]
    public void RequestRematchServerRpc(PlayerColor by)
    {
        RematchRequestedBy.Value = by;
        RematchRequested.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ConfirmRematchServerRpc()
    {
        RematchRequested.Value = false;
        MatchSeed.Value = new System.Random().Next(); // NEW: a fresh deal for the new match - reusing the old seed would deal out the exact same board and hands all over again
        RematchStartCount.Value++;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DeclineRematchServerRpc()
    {
        RematchRequested.Value = false;
        RematchDeclineCount.Value++;
    }

    private bool _PendingHostShutdown; // NEW: set by NotifyClientLeavingServerRpc below, actually acted on during the NEXT Update() frame - calling NetworkManager.Singleton.Shutdown() directly from INSIDE an incoming RPC's own handling (i.e. while Netcode is still mid-receive, processing that very message) turned out to be unreliable and could silently do nothing, unlike a host calling Shutdown() from a normal UI button click in an ordinary Update loop. Deferring it to the next frame's Update() reuses the same "never act synchronously from inside network message processing - poll for it instead" pattern already used elsewhere in this file (RematchStartCount/RematchDeclineCount polling in GameTester).

    public static int ClientLeftMidSessionSignal; // NEW: bumped by the HOST the instant it shuts down because the OTHER player left (see Update() below), so NetworkDisconnectHandler can react and put the host's own screen back in CreateMatchScene (see that script). CONFIRMED BY TESTING: OnClientDisconnectCallback (what NetworkDisconnectHandler originally relied on) never fires locally for a machine's OWN self-initiated Shutdown() call - it only fires for a genuinely external/unexpected disconnect, which is why the client leaving successfully kicks itself and the host off the SAME session, yet the host's own NetworkDisconnectHandler never saw a single callback fire when ITS OWN Shutdown() (below) ran. This field is deliberately STATIC rather than an instance field on LobbySync - the LobbySync GameObject itself gets destroyed as PART OF that very Shutdown() call (NetworkManager despawns every spawned NetworkObject during shutdown), so by the time anything else could poll it next frame, LobbySync.Instance may already be null. A static field lives on the type itself and survives that.

    void Update() // NEW
    {
        if (_PendingHostShutdown)
        {
            _PendingHostShutdown = false;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                ClientLeftMidSessionSignal++; // NEW: bump BEFORE Shutdown() below - Shutdown() may destroy this very object synchronously, so nothing after that call is guaranteed to still run
                NetworkManager.Singleton.Shutdown();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)] // NEW: called by the CLIENT right before they leave, instead of just quietly disconnecting themselves and hoping the host's own disconnect-timeout detection notices - over Relay in particular, that detection isn't instant, so a client just closing their own connection could leave the host sitting in a "still connected" match for a noticeable while. Asking the host to shut down directly reuses the ALREADY-working "host leaves -> client gets kicked too" path, just triggered from the other direction. CHANGED: no longer shuts down immediately inline - just raises the flag Update() above acts on next frame (see that field's comment for why).
    public void NotifyClientLeavingServerRpc()
    {
        _PendingHostShutdown = true;
    }
}
