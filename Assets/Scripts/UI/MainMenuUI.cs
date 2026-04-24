using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Menu - Host/Join")]
    [SerializeField] private Button hostButton;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private Button joinButton;

    [Header("Lobby")]
    [SerializeField] private TMP_Text joinCodeDisplayText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button disconnectButton;

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
        SetStatus("Relay sunucusuna bağlanıyor...");

        try
        {
            string code = await _bootstrap.StartHostAsync();
            ShowLobby();
            if (joinCodeDisplayText) joinCodeDisplayText.text = $"Kod: {code}";
            SetStatus($"Kod: {code}  —  Arkadaşına ver!");
            UpdatePlayerCount();

            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            else
                Debug.LogWarning("[Menu] SceneManager null — NetworkManager'da Enable Scene Management açık mı?");
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
            SetStatus("Bağlantı başarısız. Kodu kontrol et.");
            SetButtons(true);
        }
    }

    private void OnDisconnectClicked()
    {
        _bootstrap.Disconnect();
        ShowMenu();
        SetStatus("");
    }

    // ── Network olayları ──────────────────────────────────────────────────

    private void OnClientConnected(ulong clientId)
    {
        UpdatePlayerCount();

        if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowLobby();
            joinCodeDisplayText.text = "";
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
            SetButtons(true);
        }
    }

    // ── Yardımcı ─────────────────────────────────────────────────────────

    private void UpdatePlayerCount()
    {
        if (playerCountText == null) return;
        int count = NetworkManager.Singleton.ConnectedClients.Count;
        playerCountText.text = $"Oyuncular: {count} / 4";
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
