using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(100);
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = 100;
            isDead.Value = false;
        }

        // Sadece kendi karakterimizin can değişimlerini dinliyoruz
        if (IsOwner)
        {
            currentHealth.OnValueChanged += OnHealthChanged;
            if (GameUIManager.Instance != null) GameUIManager.Instance.UpdateHealthUI(currentHealth.Value);
            isDead.OnValueChanged += OnDeathStateChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            currentHealth.OnValueChanged -= OnHealthChanged;
            isDead.OnValueChanged -= OnDeathStateChanged;
        }
    }

    private void OnDeathStateChanged(bool previous, bool current)
    {
        if (current)
        {
            // Görev aşamasındaysak 5 saniyelik geri sayım ekranını, değilse kalıcı ölüm ekranını göster
            if (GameManager.Instance != null && GameManager.Instance.CurrentState.Value == GameState.ObjectivePhase)
            {
                GameUIManager.Instance.ShowRespawnScreen(GameManager.Instance.respawnCooldown);
            }
            else
            {
                GameUIManager.Instance.ShowDeadScreen();
            }
        }
        else
        {
            if (GameUIManager.Instance != null) GameUIManager.Instance.HideRespawnScreen();
        }
    }

    private void OnHealthChanged(int previous, int current)
    {
        if (GameUIManager.Instance != null) GameUIManager.Instance.UpdateHealthUI(current);
    }

    // Sadece sunucu hasar verebilir
    public void TakeDamage(int damage)
    {
        if (!IsServer || isDead.Value) return;

        currentHealth.Value -= damage;

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead.Value = true;
        // TODO: Karakteri gizle / Ragdoll yarat
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied(this);
        }
    }

    public void Respawn(Vector3 spawnPos, Quaternion spawnRot)
    {
        if (!IsServer) return;

        isDead.Value = false;
        currentHealth.Value = 100;

        // İstemci (Client) hareket yetkisine sahip olduğu için doğrudan sunucudan pozisyonunu değiştiremeyiz.
        // Sahibine "Kendini buraya ışınla" demek için RPC kullanıyoruz.
        TeleportClientRpc(spawnPos, spawnRot);
    }

    [Rpc(SendTo.Owner)]
    private void TeleportClientRpc(Vector3 pos, Quaternion rot, RpcParams rpcParams = default)
    {
        // Unity'de CharacterController aktifken transform.position değiştirmek hatalara yol açar
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = pos;
        transform.rotation = rot;
        if (cc != null) cc.enabled = true;
    }

    public int GetTeam()
    {
        if (NetworkLobbyManager.Instance == null) return 1;
        foreach (var player in NetworkLobbyManager.Instance.LobbyPlayers)
        {
            if (player.ClientId == OwnerClientId) return player.TeamId;
        }
        return 1;
    }
}