using Unity.Netcode;
using UnityEngine;

public class FragGrenade : GrenadeBase
{
    [Header("Patlama Ayarları")]
    public float explosionRadius = 7f;
    public int maxDamage = 100;
    public LayerMask playerLayer;

    [Header("Görsel Efektler")]
    public GameObject decalPrefab;
    public float decalLifetime = 10f;
    public AudioClip explosionSound;

    protected override void OnExplode(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, explosionRadius, playerLayer);

        foreach (var col in colliders)
        {
            PlayerHealth health = col.GetComponentInParent<PlayerHealth>();
            if (health == null || health.isDead.Value) continue;

            float distance = Vector3.Distance(position, col.transform.position);
            float damageFactor = 1f - Mathf.Clamp01(distance / explosionRadius);
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(maxDamage * damageFactor));
            health.TakeDamage(finalDamage);
        }
    }

    protected override void OnExplodeVisual(Vector3 position)
    {
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, position);

        if (decalPrefab == null) return;

        // Patladığı yüzeyi bul ve decal'ı yüzey normaline hizala
        if (Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 0.5f))
        {
            Vector3 spawnPos = hit.point + hit.normal * 0.001f;
            Quaternion spawnRot = Quaternion.LookRotation(hit.normal);
            GameObject decal = Instantiate(decalPrefab, spawnPos, spawnRot);
            Destroy(decal, decalLifetime);
        }
        else
        {
            // Zemin bulunamazsa direkt patlama noktasına yerleştir (yukarı bakan)
            GameObject decal = Instantiate(decalPrefab, position, Quaternion.LookRotation(Vector3.up));
            Destroy(decal, decalLifetime);
        }
    }
}
