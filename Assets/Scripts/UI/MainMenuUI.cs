using System.Collections;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Menu")]
    [SerializeField] private Button hostButton;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private Button joinButton;

    [Header("Lobby")]
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button startGameButton;

    [Header("Oyun sahnesi")]
    [SerializeField] private string gameSceneName = "HelloWorld";

    private NetworkBootstrap _bootstrap;

    private void Awake()
    {
        // Artık Singleton olduğu için doğrudan Instance üzerinden alabiliriz (veya Find kullanmaya devam edebiliriz).
        _bootstrap = NetworkBootstrap.Instance ?? FindFirstObjectByType<NetworkBootstrap>();
    }

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        disconnectButton.onClick.AddListener(OnDisconnectClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        ShowMenu();
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

    // ── Buton işlemleri ───────────────────────────────────────────────────

    private async void OnHostClicked()
    {
        SetButtons(false);
        SetStatus("Bağlanıyor...");

        try
        {
            string code = await _bootstrap.StartHostAsync();
            
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Host başlatılamadı. Lütfen tekrar deneyin.");
                SetButtons(true);
                return;
            }

            ShowLobby();
            SetStatus($"Kod: {code}");
            UpdatePlayerCount();
            startGameButton.gameObject.SetActive(true);
        }
        catch (System.Exception e)
        {
            SetStatus($"Hata: {e.Message}");
            SetButtons(true);
        }
    }

    private async void OnJoinClicked()
    {
        string code = joinCodeInputField.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Join kodu gir!");
            return;
        }

        SetButtons(false);
        SetStatus($"Bağlanıyor... ({code})");

        try
        {
            await _bootstrap.StartClientAsync(code);
            
            // Bağlantı başladığında (CustomMessagingManager aktifleştiğinde) mesajları dinlemeye başla
            if (NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("PlayerCountUpdate", ReceivePlayerCountUpdate);
            }

            StartCoroutine(ConnectionTimeout(8f));
        }
        catch (System.Exception e)
        {
            SetStatus($"Hata: {e.Message}");
            SetButtons(true);
        }
    }

    private IEnumerator ConnectionTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            _bootstrap.Disconnect();
            yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
            SetStatus("Bağlantı başarısız. Kodu kontrol et.");
            SetButtons(true);
        }
    }

    private void OnStartGameClicked()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        
        if (NetworkManager.Singleton.SceneManager != null)
        {
            // Karmaşık index hesaplaması yerine yukarıda tanımlanan Inspector değişkenini kullanıyoruz.
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[Menu] NetworkManager üzerinde 'Enable Scene Management' aktif değil!");
        }
    }

    private void OnDisconnectClicked()
    {
        // Eğer Client isek dinlemeyi bırak
        if (!NetworkManager.Singleton.IsHost && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("PlayerCountUpdate");
        }
        _bootstrap.Disconnect();
        StartCoroutine(WaitForShutdownThenMenu(""));
    }

    // ── Network olayları ──────────────────────────────────────────────────

    private void OnClientConnected(ulong clientId)
    {
        // Eğer Host isek, yeni gelen kişiyi say ve herkese yeni sayıyı bildir
        if (NetworkManager.Singleton.IsServer)
        {
            UpdatePlayerCount();
        }

        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowLobby();
            startGameButton.gameObject.SetActive(false);
            SetStatus("Host oyunu başlatana kadar bekle...");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Biri koptuğunda (Host isek) güncel sayıyı herkese yeniden gönder
        if (NetworkManager.Singleton.IsServer)
        {
            UpdatePlayerCount();
        }

        // Biz (Client) koptuysak
        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("PlayerCountUpdate");
            }
            _bootstrap.Disconnect();
            StartCoroutine(WaitForShutdownThenMenu("Sunucu bağlantısı kesildi."));
        }
    }

    // Shutdown tamamlanmadan menüye dönme — re-host sorununu çözer
    private IEnumerator WaitForShutdownThenMenu(string msg)
    {
        yield return new WaitUntil(() => !NetworkManager.Singleton.IsListening);
        ShowMenu();
        SetStatus(msg);
        SetButtons(true);
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────

    private void UpdatePlayerCount()
    {
        if (!playerCountText) return;
        
        if (NetworkManager.Singleton.IsServer)
        {
            int count = NetworkManager.Singleton.ConnectedClients.Count;
            SetPlayerCountUI(count);

            // İstemcilere (Client) güncel sayıyı bildir
            if (NetworkManager.Singleton.CustomMessagingManager != null)
            {
                using var writer = new FastBufferWriter(4, Allocator.Temp);
                writer.WriteValueSafe(count);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("PlayerCountUpdate", writer);
            }
        }
    }

    private void ReceivePlayerCountUpdate(ulong senderClientId, FastBufferReader messagePayload)
    {
        messagePayload.ReadValueSafe(out int count);
        SetPlayerCountUI(count);
    }

    private void SetPlayerCountUI(int count)
    {
        if (playerCountText)
        {
            playerCountText.text = $"Oyuncular: {count} / {NetworkBootstrap.MaxConnections}";
        }
    }

    private void ShowMenu()
    {
        menuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    private void ShowLobby()
    {
        menuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log($"[Menu] {msg}");
    }

    private void SetButtons(bool interactable)
    {
        hostButton.interactable = interactable;
        joinButton.interactable = interactable;
    }
}
