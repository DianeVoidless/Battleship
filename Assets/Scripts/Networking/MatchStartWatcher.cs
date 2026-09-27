using Unity.Netcode;
using UnityEngine;

public class MatchStartWatcher : MonoBehaviour // NEW: watches LobbySync.MatchStarted every frame and, the moment it flips true (host pressed Start Match), switches BOTH players' screens from CreateMatchScene to InGame at the same time. Uses Update()-polling instead of subscribing to the NetworkVariable's OnValueChanged event, on purpose - same lesson as ColorChoicePanel/HostOnlyButton: LobbySync.Instance might not exist yet at the moment this component enables, so a one-shot subscription attempt could silently miss it. Polling every frame means it always catches the flip eventually, regardless of timing. Drop this on the same GameObject as the NetworkManager.
{
    [Header("Drag CreateMatchScene's and InGame's root GameObjects here")]
    public GameObject _CreateMatchScene;
    public GameObject _InGame;

    [Header("CHANGED: the host already deals its own board via TestMatchStartButton's own On Click list (GameTester.BeginMatch + HandPanel activation happen there). The CLIENT never clicks that button, so nothing ever told its own local GameTester to deal - drag the same GameTester and HandPanel here so the client gets its own local deal too, instead of sitting on an empty board forever. NOTE: this just stops the client's screen from looking frozen - it does NOT make the two players share the same game state yet (each still gets its own random deal, cards, and turn order) - that's a separate, bigger networking project for later.")]
    public GameTester _GameTester;
    public GameObject _HandPanel;

    void Update()
    {
        if (LobbySync.Instance == null)
        {
            return; // NEW: no match in progress (or LobbySync hasn't spawned yet) - nothing to watch
        }

        if (LobbySync.Instance.MatchStarted.Value && _CreateMatchScene.activeSelf)
        {
            _CreateMatchScene.SetActive(false);
            _InGame.SetActive(true);

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost; // NEW: the host already ran BeginMatch()/HandPanel activation itself via the Start Match button's own On Click list - only the client needs this watcher to do it on their behalf
            if (!isHost)
            {
                if (_GameTester != null) _GameTester.BeginMatch();
                if (_HandPanel != null) _HandPanel.SetActive(true);
            }
        }
    }
}
