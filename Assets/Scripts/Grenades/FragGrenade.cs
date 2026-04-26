using Unity.Netcode;
using UnityEngine;

public class FragGrenade : GrenadeBase
{
    [Header("Patlama Ayarları")]
    public float explosionRadius = 7f;
    public int maxDamage = 150;
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

        // Patladığı yüzeyi (zemini) bul ve decal'ı yüzey normaline hizala
        // Bombanın havada patlama ihtimaline karşı ışın (raycast) mesafesini 3 metreye çıkarıyoruz
        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3.0f))
        {
            Vector3 spawnPos = hit.point + hit.normal * 0.001f;
            // Tıpkı mermi deliklerinde (WeaponBase) olduğu gibi: Yüzeyin içine doğru yansıtıp (-hit.normal),
            // kendi etrafında rastgele döndürüyoruz (Z ekseninde Roll) ki zemine tam otursun
            Quaternion spawnRot = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            GameObject decal = Instantiate(decalPrefab, spawnPos, spawnRot);
            Destroy(decal, decalLifetime);
        }
        else
        {
            // Zemin bulunamazsa direkt aşağı bakacak şekilde yerleştir ve rastgele döndür
            Quaternion spawnRot = Quaternion.LookRotation(Vector3.down) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            GameObject decal = Instantiate(decalPrefab, position, spawnRot);
            Destroy(decal, decalLifetime);
        }
    }
}
