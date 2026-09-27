using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HostOnlyButton : MonoBehaviour // NEW: disables the Button on this same GameObject for anyone who isn't the host - drop this on the Start Match button so only the player who actually created the lobby can press it, while the joining player sees it grayed out. Re-checks every time this button is shown, not just once, so it doesn't matter whether the host's connection finishes starting slightly before or after this button becomes active.
{
    private Button _Button;

    void Awake()
    {
        _Button = GetComponent<Button>();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Update() // CHANGED: not just OnEnable - StartHost()/StartClient() can finish a frame or two after this button becomes active depending on OnClick listener order, so this keeps checking rather than risk locking in a stale "not host yet" snapshot the one time OnEnable happens to fire first
    {
        Refresh();
    }

    private void Refresh()
    {
        _Button.interactable = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
}
