using Unity.Netcode;
using UnityEngine;

public class FlashGrenade : GrenadeBase
{
    [Header("Flash Ayarları")]
    public float flashRadius = 12f;
    public float maxFlashDuration = 2.5f;
    public LayerMask playerLayer;

    [Header("Ses Efektleri")]
    public AudioClip flashSound;

    protected override void OnExplode(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, flashRadius, playerLayer);

        foreach (var col in colliders)
        {
            PlayerHealth health = col.GetComponentInParent<PlayerHealth>();
            if (health == null || health.isDead.Value) continue;

            float distance = Vector3.Distance(position, col.transform.position);
            float intensity = 1f - Mathf.Clamp01(distance / flashRadius);
            float duration = maxFlashDuration * intensity;

            if (duration <= 0.05f) continue;

            // Sadece etkilenen oyuncunun client'ına flash gönder
            ulong clientId = health.OwnerClientId;
            ApplyFlashToClientRpc(duration, RpcTarget.Single(clientId, RpcTargetUse.Temp));
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

        // Tüm oyuncularda kısa bir beyaz flash (görsel ipucu)
        GameUIManager.Instance?.ShowFlash(0.2f);
    }
}
