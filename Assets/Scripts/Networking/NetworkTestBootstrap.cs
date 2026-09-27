using Unity.Netcode;
using UnityEngine;

public class NetworkTestBootstrap : MonoBehaviour // TEMPORARY, throwaway script - just proves two Editor instances (the original project and its ParrelSync clone) can actually find and connect to each other over Netcode for GameObjects, before any real UI gets wired to it. Draws two on-screen buttons (Host / Join) so no Canvas/UI wiring is needed to try this - drop this on the same GameObject as the NetworkManager and press Play. Delete this once Create/Join Match drive things for real.
{
    void OnGUI()
    {
        NetworkManager net = NetworkManager.Singleton;
        if (net == null)
        {
            GUI.Label(new Rect(10, 10, 400, 20), "No NetworkManager.Singleton found - is the NetworkManager component in the scene?");
            return;
        }

        string status = !net.IsListening
            ? "not connected"
            : net.IsHost
                ? "HOSTING (" + net.ConnectedClients.Count + " client(s) connected)"
                : net.IsConnectedClient
                    ? "CONNECTED AS CLIENT"
                    : "connecting...";

        GUI.Label(new Rect(10, 10, 400, 20), status);

        if (!net.IsListening)
        {
            if (GUI.Button(new Rect(10, 40, 100, 30), "Host"))
            {
                net.StartHost();
            }
            if (GUI.Button(new Rect(120, 40, 100, 30), "Join"))
            {
                net.StartClient();
            }
        }
        else
        {
            if (GUI.Button(new Rect(10, 40, 100, 30), "Disconnect"))
            {
                net.Shutdown();
            }
        }
    }
}
