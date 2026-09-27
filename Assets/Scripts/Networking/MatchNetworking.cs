using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class MatchNetworking : MonoBehaviour // CHANGED: HostMatch/JoinMatch now go through Unity Relay instead of always dialing 127.0.0.1 - this is what lets two players actually connect over the real internet using a short room code, instead of only ever working between two windows on the same machine. Drop this on the same GameObject as the NetworkManager.
{
    [Header("Drag the LobbySync prefab here (Assets/Prefabs/LobbySync.prefab) - the host spawns one of these the moment hosting starts, so there's a shared networked object for usernames/color choice to live on")]
    public GameObject _LobbySyncPrefab;

    [Header("Drag the Room Code TEXT (the one that shows XXXXXX) from CreateMatchScene here - filled in with the real code once hosting starts")]
    public TMP_Text _RoomCodeText;

    [Header("Drag JoinMatchScene's room-code InputField here - read when the player clicks Join")]
    public TMP_InputField _RoomCodeInputField;

    private const int MaxOtherPlayers = 1; // NEW: a 2-player game means 1 other connection besides the host - this is Relay's "how many guests can join" number, not the total headcount

    public async void HostMatch() // wire this as CreateMatchScene's confirmation button's OnClick - CHANGED to async: creating a Relay allocation and getting a join code are real network calls to Unity's cloud, they don't finish instantly like StartHost() used to on its own
    {
        Debug.Log("[MatchNetworking] TEMP: HostMatch() called, IsListening=" + NetworkManager.Singleton.IsListening);
        if (NetworkManager.Singleton.IsListening)
        {
            return; // NEW: already hosting or already connected - clicking again shouldn't try to start a second session
        }

        try
        {
            await EnsureSignedInAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[MatchNetworking] TEMP: EnsureSignedInAsync threw: " + e); // TEMP: this call wasn't wrapped before - if sign-in itself throws (e.g. on a re-host), the whole method used to abort silently with no log at all, which would exactly explain a stale room code with zero error output
            return;
        }
        Debug.Log("[MatchNetworking] TEMP: signed in OK, IsSignedIn=" + AuthenticationService.Instance.IsSignedIn + ", creating Relay allocation...");

        Allocation allocation;
        string joinCode;
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(MaxOtherPlayers);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to create Relay allocation: " + e.Message); // NEW: a real failure case now exists (no internet, Relay service hiccup, etc.) that couldn't happen with the old localhost-only version - logged rather than silently doing nothing, so it's visible instead of just leaving the player stuck on the button
            return;
        }
        Debug.Log("[MatchNetworking] TEMP: got new join code '" + joinCode + "', starting host and setting room code text...");

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartHost();

        GameObject lobbySyncInstance = Instantiate(_LobbySyncPrefab); // NEW: only the host ever runs this - creates the one shared LobbySync object for this match and hands it to Netcode, which automatically creates a matching copy on every client that connects afterward
        lobbySyncInstance.GetComponent<NetworkObject>().Spawn();

        if (_RoomCodeText != null)
        {
            _RoomCodeText.text = joinCode; // CHANGED: the real Relay join code, replacing the old "XXXXXX" placeholder
        }
        Debug.Log("[MatchNetworking] TEMP: _RoomCodeText now reads '" + (_RoomCodeText != null ? _RoomCodeText.text : "NULL FIELD") + "'");
    }

    public async void JoinMatch() // wire this as JoinMatchScene's confirmation button's OnClick - CHANGED to async, same reasoning as HostMatch
    {
        if (NetworkManager.Singleton.IsListening)
        {
            return; // NEW: same guard as HostMatch - already connecting/connected, ignore a second click
        }

        string code = _RoomCodeInputField != null ? _RoomCodeInputField.text.Trim() : string.Empty; // CHANGED: the room code field is no longer a placeholder - this is what actually gets used now
        if (string.IsNullOrEmpty(code))
        {
            return; // NEW: nothing typed - nothing to join
        }

        await EnsureSignedInAsync();

        JoinAllocation joinAllocation;
        try
        {
            joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to join Relay allocation with code '" + code + "': " + e.Message); // NEW: covers a bad/expired code, or the host's session having already ended
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

        NetworkManager.Singleton.StartClient();

        if (_RoomCodeText != null)
        {
            _RoomCodeText.text = code; // NEW: the client already knows this code (they just typed it) - this just makes their own CreateMatchScene view show it too, instead of leaving the "XXXXXX" placeholder behind
        }
    }

    public void StartMatch() // NEW: wire this as TestMatchStartButton's OnClick - that button already has HostOnlyButton on it, so only the host can ever actually trigger this. Just tells the shared LobbySync that the match has started; MatchStartWatcher (on the NetworkManager) is what actually moves both screens into InGame once it sees that flip.
    {
        if (LobbySync.Instance != null)
        {
            LobbySync.Instance.TriggerMatchStart();
        }
    }

    public void LeaveMatch() // NEW: wire this to CreateMatchScene's BackButton, IN ADDITION to whatever it already does - this actually shuts down the network session instead of just hiding the panel. CHANGED: if the CLIENT leaves, this now asks the HOST to shut down too (see LobbySync.NotifyClientLeavingServerRpc) rather than just disconnecting the client's own connection and leaving the host's match running - a match can't continue with only one player anyway, and this reuses the already-working "host leaves -> client gets kicked" path from the other direction, instead of waiting on Netcode's own disconnect-timeout detection to eventually notice the client is gone.
    {
        if (!NetworkManager.Singleton.IsListening)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsHost && LobbySync.Instance != null) // NEW: I'm the client - ask the host to end the match instead of just quietly disconnecting myself
        {
            LobbySync.Instance.NotifyClientLeavingServerRpc();
            return;
        }

        NetworkManager.Singleton.Shutdown(); // I'm the host (or LobbySync doesn't exist for some reason) - shut down directly, same as before
    }

    public void CopyRoomCode() // NEW: wire this to the little copy-icon button next to the Room Code text
    {
        if (_RoomCodeText != null)
        {
            GUIUtility.systemCopyBuffer = _RoomCodeText.text;
        }
    }

    private static async Task EnsureSignedInAsync() // NEW: Relay calls need a signed-in Unity Authentication session first - this sets one up the first time it's needed (anonymous sign-in, no login screen or credentials involved) rather than requiring a separate startup step elsewhere
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
