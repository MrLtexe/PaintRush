using UnityEngine;

public class SmokeGrenade : GrenadeBase
{
    [Header("Duman Ayarları")]
    public float smokeDuration = 8f;

    [Header("Görsel Efektler")]
    public ParticleSystem smokeFxPrefab;
    public AudioClip deploySound;

    protected override void OnExplode(Vector3 position)
    {
        // Duman yalnızca görsel — server'da uygulanacak oyun etkisi yok
    }

    protected override void OnExplodeVisual(Vector3 position)
    {
        if (smokeFxPrefab != null)
        {
            ParticleSystem fx = Instantiate(smokeFxPrefab, position, Quaternion.identity);
            // Main module duration'ı smokeDuration'a göre ayarla
            var main = fx.main;
            main.duration = smokeDuration;
            fx.Play();
            Destroy(fx.gameObject, smokeDuration + main.startLifetime.constantMax + 1f);
        }

        if (deploySound != null)
            AudioSource.PlayClipAtPoint(deploySound, position);
    }
}
