using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    // Server-side takım tablosu: clientId → teamId (1=A, 2=B)
    private readonly Dictionary<ulong, int> _playerTeams = new();

    // Client UI için NetworkList
    public NetworkList<PlayerLobbyData> LobbyPlayers { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LobbyPlayers = new NetworkList<PlayerLobbyData>();
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += ServerOnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += ServerOnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= ServerOnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= ServerOnClientDisconnected;
    }

    private void ServerOnClientConnected(ulong clientId)
    {
        _playerTeams[clientId] = 0;
        LobbyPlayers.Add(new PlayerLobbyData { ClientId = clientId, TeamId = 0 });
    }

    private void ServerOnClientDisconnected(ulong clientId)
    {
        _playerTeams.Remove(clientId);
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId != clientId) continue;
            LobbyPlayers.RemoveAt(i);
            break;
        }
    }

    // MainMenuUI'dan çağrılır
    public void SelectTeamRpc(int teamId) => SelectTeamServerRpc(teamId);

    [ServerRpc(RequireOwnership = false)]
    private void SelectTeamServerRpc(int teamId, ServerRpcParams rpc = default)
    {
        ulong clientId = rpc.Receive.SenderClientId;
        _playerTeams[clientId] = teamId;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId != clientId) continue;
            LobbyPlayers[i] = new PlayerLobbyData { ClientId = clientId, TeamId = teamId };
            break;
        }
    }

    public int GetPlayerTeam(ulong clientId) =>
        _playerTeams.TryGetValue(clientId, out int team) ? team : 1;

    public bool CanStartGame(out string errorMessage)
    {
        int a = 0, b = 0;
        foreach (var kv in _playerTeams)
        {
            if (kv.Value == 1) a++;
            else if (kv.Value == 2) b++;
        }
        if (a == 0 || b == 0) { errorMessage = "Her takımda en az 1 oyuncu olmalı!"; return false; }
        errorMessage = "";
        return true;
    }
}
