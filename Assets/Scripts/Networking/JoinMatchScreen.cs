using Unity.Netcode;
using UnityEngine;

public class JoinMatchScreen : MonoBehaviour // CHANGED: no longer shows a separate "connected, waiting" message on this screen - the moment MatchNetworking.JoinMatch() actually connects, this pulls the joining player straight into the SAME CreateMatchScene lobby the host is already looking at. Both players end up on one shared screen; what keeps the host special is the Start Match button being locked to host-only (see HostOnlyButton on that button).
{
    [Header("Drag this JoinMatchScene's own root GameObject, and CreateMatchScene's root, here")]
    public GameObject _Self;
    public GameObject _CreateMatchScene;

    void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            return; // NEW: this callback also fires on the HOST's side once per client that connects TO them - only react when it's OUR OWN connection succeeding, not some other client joining a match we happen to be hosting
        }

        _Self.SetActive(false);
        _CreateMatchScene.SetActive(true);
    }
}
