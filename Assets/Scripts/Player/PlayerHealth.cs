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
        
        // TODO: GameManager'a sor -> Hangi state'deyiz? Cooldown ile mi doğacağım yoksa tamamen mi öldüm?
    }
}