using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// 1. Oyuncu Verisi (Ağ üzerinden taşınacak paket)
public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
{
    public ulong ClientId;
    public FixedString32Bytes PlayerName; // Ağ dostu string tipi
    public int TeamId; // 0: Tarafsız, 1: A Takımı, 2: B Takımı

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref TeamId);
    }

    public bool Equals(LobbyPlayerState other)
    {
        return ClientId == other.ClientId && 
               PlayerName == other.PlayerName && 
               TeamId == other.TeamId;
    }
}

public class NetworkLobbyManager : NetworkBehaviour
{
    public static NetworkLobbyManager Instance { get; private set; }

    // 2. Senkronize Liste (İçinde bir değişiklik olduğunda otomatik olarak tüm Client'lara yansır)
    public NetworkList<LobbyPlayerState> LobbyPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Awake içinde listeyi mutlaka oluşturmalıyız
        LobbyPlayers = new NetworkList<LobbyPlayerState>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Yeni oyuncular katıldıkça veya koptukça listeyi güncellemek için dinleyiciler
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId) => AddPlayerToList(clientId);

    private void OnClientDisconnected(ulong clientId)
    {
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                LobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayerToList(ulong clientId)
    {
        // 3. Sıraya göre isimlendirme (Player 1, Player 2 vb.)
        int playerIndex = LobbyPlayers.Count + 1;
            
        LobbyPlayers.Add(new LobbyPlayerState
        {
            ClientId = clientId,
            PlayerName = $"Player {playerIndex}",
            TeamId = 0 // Başlangıçta tarafsız
        });
    }

    // 4. Takım Seçimi (Client'lar Host'a takım seçtiklerini bu metodla bildirir)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SelectTeamRpc(int teamId, RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == senderClientId)
            {
                var updatedPlayer = LobbyPlayers[i];
                updatedPlayer.TeamId = teamId;
                LobbyPlayers[i] = updatedPlayer; // Struct'ı güncelle
                break;
            }
        }
    }

    // 5. Başlatma Kontrolü (Herkes takım seçti mi?)
    public bool CanStartGame()
    {
        if (LobbyPlayers.Count == 0) return false;

        foreach (var player in LobbyPlayers)
        {
            if (player.TeamId == 0)
            {
                Debug.Log("Tüm oyuncular takım seçimi yapmalı!");
                return false;
            }
        }
        return true;
    }
}
