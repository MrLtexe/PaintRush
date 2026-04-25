using UnityEngine;
using Unity.Netcode;
using TMPro;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("UI Ayarları")]
    public TMP_Text nameText;
    
    private PlayerHealth _health;

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<PlayerHealth>();

        // Kendi karakterimizin ismini kendi ekranımızda görmemize gerek yok
        if (IsOwner)
        {
            if (nameText != null) nameText.gameObject.SetActive(false);
            return;
        }

        SetupNameTag();

        // Oyuncu öldüğünde ismini gizlemek, dirildiğinde tekrar açmak için dinliyoruz
        if (_health != null)
        {
            _health.isDead.OnValueChanged += OnDeathStateChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner && _health != null)
        {
            _health.isDead.OnValueChanged -= OnDeathStateChanged;
        }
    }

    private void OnDeathStateChanged(bool previous, bool current)
    {
        if (nameText == null) return;
        
        if (current) 
            nameText.gameObject.SetActive(false); // Öldüyse gizle
        else 
            SetupNameTag(); // Dirildiyse takım kontrolünü tekrar yap ve gerekiyorsa aç
    }

    private void SetupNameTag()
    {
        if (nameText == null || NetworkLobbyManager.Instance == null) return;

        int myTeam = 0;
        int localPlayerTeam = 0;
        string myName = "Player";

        // Lobi listesini tarayarak bu objenin sahibini ve bizim (yerel oyuncu) takımımızı buluyoruz
        foreach (var player in NetworkLobbyManager.Instance.LobbyPlayers)
        {
            if (player.ClientId == OwnerClientId)
            {
                myTeam = player.TeamId;
                myName = player.PlayerName.ToString();
            }
            if (player.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                localPlayerTeam = player.TeamId;
            }
        }

        // Sadece aynı takımdaysak ve oyuncu ölü değilse ismi göster
        bool isAlive = _health == null || !_health.isDead.Value;
        nameText.gameObject.SetActive(myTeam == localPlayerTeam && isAlive);
        nameText.text = myName;
    }

    private void LateUpdate()
    {
        // İsim etiketi aktifse, her zaman yerel oyuncunun kamerasına (MainCamera) doğru baksın (Billboard Efekti)
        if (nameText != null && nameText.gameObject.activeSelf && Camera.main != null)
        {
            nameText.transform.LookAt(nameText.transform.position + Camera.main.transform.rotation * Vector3.forward, Camera.main.transform.rotation * Vector3.up);
        }
    }
}