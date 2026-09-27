using Unity.Netcode;
using UnityEngine;

public class MatchGameRelay : NetworkBehaviour // NEW: carries actual gameplay moves (not just lobby info) between the two machines, so both players end up looking at the SAME match instead of two independently-dealt local ones. Lives on the same LobbySync prefab/NetworkObject - add this component to Assets/Prefabs/LobbySync.prefab. TurnController is what actually decides WHEN to relay a move (see its Send*ToNetwork helpers) - this class only carries the message across and hands it back to TurnController on the other side.
{
    public static MatchGameRelay Instance;

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // --- Cell action: either a Healer's damaged-ship choice (cardId == -1), or playing a card onto a cell ---

    [ServerRpc(RequireOwnership = false)] // NEW: called BY the client, when the CLIENT is the one who made this move - RequireOwnership false because this object is owned by the host, but the client still needs to be able to call this
    public void RequestCellActionServerRpc(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, PlayerColor cellOwner, int cellIndex)
    {
        TurnController._Instance?.ReceiveCellAction(cardOwner, cardId, chosenBranch, cellOwner, cellIndex); // mirrors the client's move onto the HOST's own local game
    }

    [ClientRpc] // NEW: called BY the host, when the HOST is the one who made this move
    public void NotifyCellActionClientRpc(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, PlayerColor cellOwner, int cellIndex)
    {
        if (IsHost)
        {
            return; // NEW: the host is also technically "a client" as far as Netcode's ClientRpc delivery goes - it already applied this move to its own local game the moment it made it, so it must not re-apply its own broadcast
        }
        TurnController._Instance?.ReceiveCellAction(cardOwner, cardId, chosenBranch, cellOwner, cellIndex);
    }

    // --- Confirming a Cleanse hand-multi-select ---

    [ServerRpc(RequireOwnership = false)]
    public void RequestCleanseConfirmServerRpc(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, int[] selectedCardIds)
    {
        TurnController._Instance?.ReceiveCleanseConfirm(cardOwner, cardId, chosenBranch, selectedCardIds);
    }

    [ClientRpc]
    public void NotifyCleanseConfirmClientRpc(PlayerColor cardOwner, int cardId, CardBranch chosenBranch, int[] selectedCardIds)
    {
        if (IsHost)
        {
            return;
        }
        TurnController._Instance?.ReceiveCleanseConfirm(cardOwner, cardId, chosenBranch, selectedCardIds);
    }

    // --- Resolving a no-target wildcard branch (Draw3 or Extra Play) ---

    [ServerRpc(RequireOwnership = false)]
    public void RequestNoTargetResolveServerRpc(PlayerColor cardOwner, int cardId, CardBranch chosenBranch)
    {
        TurnController._Instance?.ReceiveNoTargetResolve(cardOwner, cardId, chosenBranch);
    }

    [ClientRpc]
    public void NotifyNoTargetResolveClientRpc(PlayerColor cardOwner, int cardId, CardBranch chosenBranch)
    {
        if (IsHost)
        {
            return;
        }
        TurnController._Instance?.ReceiveNoTargetResolve(cardOwner, cardId, chosenBranch);
    }
}
