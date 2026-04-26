using Unity.Netcode;
using UnityEngine;

public class FlashGrenade : GrenadeBase
{
    [Header("Flash Ayarları")]
    public float flashRadius = 100f;
    public float flashDuration = 3f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer = Physics.DefaultRaycastLayers; // Duvar arkasını algılamak için

    [Header("Ses Efektleri")]
    public AudioClip flashSound;

    protected override void OnExplode(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, flashRadius, playerLayer);

        foreach (var col in colliders)
        {
            PlayerHealth health = col.GetComponentInParent<PlayerHealth>();
            if (health == null || health.isDead.Value) continue;

            // Yerden seken bombanın ışınının anında zemine çarpmasını önlemek için hafif yukarıdan başlatıyoruz
            Vector3 startPos = position + Vector3.up * 0.1f;
            Vector3 targetPos = col.bounds.center; // Doğrudan oyuncunun merkezini hedef al
            Vector3 dir = targetPos - startPos;
            float distance = dir.magnitude;

            // Bombadan oyuncuya giden ışın bir engele (duvara) çarpıyor mu?
            if (Physics.Raycast(startPos, dir.normalized, out RaycastHit hit, distance, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                // Eğer ışın oyuncuya değil de başka bir şeye çarptıysa, oyuncu duvar arkasındadır
                if (hitHealth != health)
                {
                    continue; 
                }
            }

            // Sadece etkilenen oyuncunun client'ına flash gönder
            ulong clientId = health.OwnerClientId;
            ApplyFlashToClientRpc(flashDuration, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ApplyFlashToClientRpc(float duration, RpcParams rpcParams = default)
    {
        if (GameUIManager.Instance != null)
            GameUIManager.Instance.ShowFlash(duration);
    }

    protected override void OnExplodeVisual(Vector3 position)
    {
        if (flashSound != null)
            AudioSource.PlayClipAtPoint(flashSound, position);
    }
}
