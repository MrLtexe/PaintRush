using System.Threading.Tasks;
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

    private UnityTransport _transport;

    private void Awake()
    {
        _transport = GetComponent<UnityTransport>();
        if (_transport == null)
            _transport = gameObject.AddComponent<UnityTransport>();

        DontDestroyOnLoad(gameObject);
    }

    public async Task InitServicesAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log("[Relay] Servisler hazır.");
    }

    // Host: Relay allocation oluştur, join kodu döndür
    public async Task<string> StartHostAsync()
    {
        await InitServicesAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections - 1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        _transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
        NetworkManager.Singleton.StartHost();

        Debug.Log($"[Relay] Host başlatıldı. Kod: {joinCode}");
        return joinCode;
    }

    // Client: Join koduyla Relay üzerinden bağlan
    public async Task StartClientAsync(string joinCode)
    {
        await InitServicesAsync();

        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpper());
        _transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
        NetworkManager.Singleton.StartClient();

        Debug.Log($"[Relay] Client bağlanıyor. Kod: {joinCode}");
    }

    public void Disconnect()
    {
        NetworkManager.Singleton.Shutdown();
        Debug.Log("[Relay] Bağlantı kesildi.");
    }
}
