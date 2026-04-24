using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas > MainMenuPanel'a ekle.
/// Gerekli referanslar Inspector'dan bağlanır.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Host/Join")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button disconnectButton;

    [Header("Durum")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text localIPText;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Oyun sahnesi")]
    [SerializeField] private string gameSceneName = "GameScene";

    private NetworkBootstrap _bootstrap;

    private void Awake()
    {
        _bootstrap = FindFirstObjectByType<NetworkBootstrap>();
    }

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        disconnectButton.onClick.AddListener(OnDisconnectClicked);

        ShowMenu();

        // Yerel IP'yi göster (host paylaşım kolaylığı için)
        localIPText.text = $"Yerel IP: {GetLocalIP()}";
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // ── Buton işlemleri ────────────────────────────────────────────────────

    private void OnHostClicked()
    {
        SetStatus("Sunucu başlatılıyor...");
        _bootstrap.StartHost();
        ShowLobby();
        SetStatus($"Sunucu aktif!  IP: {GetLocalIP()}  Port: {NetworkBootstrap.Port}");
        UpdatePlayerCount();

        // Host oyun sahnesini yükler; diğer istemciler otomatik senkronize olur
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    private void OnJoinClicked()
    {
        string ip = ipInputField.text.Trim();
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        SetStatus($"Bağlanıyor → {ip}:{NetworkBootstrap.Port}");
        _bootstrap.StartClient(ip);
        joinButton.interactable = false;
        hostButton.interactable = false;
    }

    private void OnDisconnectClicked()
    {
        _bootstrap.Disconnect();
        ShowMenu();
        SetStatus("Bağlantı kesildi.");
    }

    // ── Network olayları ───────────────────────────────────────────────────

    private void OnClientConnected(ulong clientId)
    {
        UpdatePlayerCount();

        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowLobby();
            SetStatus("Bağlantı başarılı!");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UpdatePlayerCount();

        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowMenu();
            SetStatus("Sunucu bağlantısı kesildi.");
            joinButton.interactable = true;
            hostButton.interactable = true;
        }
    }

    private void UpdatePlayerCount()
    {
        if (playerCountText == null) return;
        int count = NetworkManager.Singleton.ConnectedClients.Count;
        playerCountText.text = $"Oyuncular: {count} / 4";
    }

    // ── Yardımcı ──────────────────────────────────────────────────────────

    private void ShowMenu()
    {
        menuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        disconnectButton.gameObject.SetActive(false);
    }

    private void ShowLobby()
    {
        menuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        disconnectButton.gameObject.SetActive(true);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"[Menu] {msg}");
    }

    private static string GetLocalIP()
    {
        foreach (var addr in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
        {
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return addr.ToString();
        }
        return "127.0.0.1";
    }
}
