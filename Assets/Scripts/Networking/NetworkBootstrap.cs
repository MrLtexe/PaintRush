using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// NetworkManager'a ekle. UnityTransport ayarlarını runtime'da yönetir.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class NetworkBootstrap : MonoBehaviour
{
    public const ushort Port = 7777;
    public const int MaxConnections = 4;

    private UnityTransport _transport;

    private void Awake()
    {
        _transport = GetComponent<UnityTransport>();
        if (_transport == null)
            _transport = gameObject.AddComponent<UnityTransport>();

        DontDestroyOnLoad(gameObject);
    }

    public void StartHost()
    {
        ConfigureTransport("0.0.0.0");
        NetworkManager.Singleton.StartHost();
        Debug.Log($"[Host] Başlatıldı. Port: {Port}");
    }

    public void StartClient(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";
        ConfigureTransport(ip);
        NetworkManager.Singleton.StartClient();
        Debug.Log($"[Client] Bağlanıyor → {ip}:{Port}");
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton.IsHost)
            NetworkManager.Singleton.Shutdown();
        else if (NetworkManager.Singleton.IsClient)
            NetworkManager.Singleton.Shutdown();

        Debug.Log("[Network] Bağlantı kesildi.");
    }

    private void ConfigureTransport(string address)
    {
        _transport.SetConnectionData(address, Port);
    }
}
