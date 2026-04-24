using System.Threading.Tasks;
using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class NetworkBootstrap : MonoBehaviour
{
    public const int MaxConnections = 4;

    public static NetworkBootstrap Instance { get; private set; }

    private UnityTransport _transport;

    private void Awake()
    {
        // Singleton pattern ile duplicate objeleri engelliyoruz
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _transport = GetComponent<UnityTransport>();
        if (_transport == null)
        {
            _transport = gameObject.AddComponent<UnityTransport>();
            // Kod ile eklendiğinde NetworkManager'a bu transportu kullanmasını söylemeliyiz
            GetComponent<NetworkManager>().NetworkConfig.NetworkTransport = _transport;
        }

        DontDestroyOnLoad(gameObject);
    }

    public async Task InitServicesAsync()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("[Relay] Servisler hazır.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Servisler başlatılamadı: {e.Message}");
        }
    }

    // Host: Relay allocation oluştur, join kodu döndür
    public async Task<string> StartHostAsync()
    {
        try
        {
            await InitServicesAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            _transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartHost();

            Debug.Log($"[Relay] Host başlatıldı. Kod: {joinCode}");
            return joinCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Host başlatılırken hata oluştu: {e.Message}");
            return null;
        }
    }

    // Client: Join koduyla Relay üzerinden bağlan
    public async Task StartClientAsync(string joinCode)
    {
        try
        {
            await InitServicesAsync();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpper());
            _transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
            NetworkManager.Singleton.StartClient();

            Debug.Log($"[Relay] Client bağlanıyor. Kod: {joinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Client bağlanırken hata oluştu (Kod yanlış olabilir): {e.Message}");
        }
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[Relay] Bağlantı kesildi.");
        }
    }
}
