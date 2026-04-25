using Unity.Netcode;
using UnityEngine;
using DG.Tweening;

public abstract class WeaponBase : NetworkBehaviour
{
    [Header("Temel Silah Ayarları")]
    public string weaponName;
    public int damage;
    public float range;
    public float fireRate; // Saniyede kaç mermi atılabileceği
    public bool isAutomatic; // Otomatik (basılı tutunca sıkan) mi, yoksa tekli mi?

    [Header("Geri Tepme (Recoil)")]
    public float verticalRecoil = 2f;
    public float horizontalRecoil = 0.5f;

    [Header("Görsel Efektler")]
    public Transform barrelPoint; // Namlu ucu
    public ParticleSystem muzzleFlash; // Namlu ateşi
    public TrailRenderer bulletTrailPrefab; // Mermi izi (Trail)
    public GameObject bulletHolePrefab; // Duvarlardaki mermi deliği (Decal)

    [Header("Ses Efektleri")]
    public AudioSource weaponAudioSource; // Sesi çalacak kaynak
    public AudioClip shootSound; // Patlama sesi

    protected float nextTimeToFire = 0f;

    // Oyuncu inputlarına göre ateş etme denemesi yapar
    public virtual void HandleShooting(bool wasPressed, bool isPressed, Transform cameraTransform, FPSController shooter)
    {
        bool wantToShoot = isAutomatic ? isPressed : wasPressed;

        if (wantToShoot && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + (1f / fireRate);
            PerformShoot(cameraTransform, shooter);
        }
    }

    // Gerçek mermi atış mantığı (Raycast)
    protected virtual void PerformShoot(Transform cameraTransform, FPSController shooter)
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        
        Vector3 hitPoint = ray.GetPoint(range); // Default olarak menzil sonunu hedefle (havaya sıkılırsa)
        Vector3 hitNormal = -ray.direction;
        bool spawnDecal = false;
        
        if (Physics.Raycast(ray, out RaycastHit hit, range, shooter.shootLayer))
        {
            hitPoint = hit.point; // Gerçekte vurulan nokta
            hitNormal = hit.normal; // Vurulan yüzeyin baktığı yön
            spawnDecal = true; // Bir yüzeye çarptık, mermi izi çıkabilir

            // Çarptığımız objede veya onun ebeveyn objelerinde (parent) PlayerHealth var mı?
            PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (targetHealth != null)
            {
                spawnDecal = false; // Vurduğumuz şey oyuncuysa duvar deliği çıkartma

                // Kendi takım arkadaşımızı ve kendimizi vurmayı engelliyoruz
                if (targetHealth.OwnerClientId != shooter.OwnerClientId && targetHealth.GetTeam() != shooter.GetMyTeam())
                {
                    var targetNetObj = targetHealth.GetComponent<NetworkObject>();
                    if (targetNetObj != null)
                    {
                        // Hasarı FPSController üzerinden sunucuya iletiyoruz
                        shooter.HitPlayerRpc(targetNetObj.NetworkObjectId, damage);
                    }
                }
            }
        }

        // Editor üzerinde merminin nereye gittiğini görmek için Debug çizgisi (2 saniye ekranda kalır)
        Debug.DrawLine(ray.origin, hitPoint, Color.red, 2f);

        // Görsel efektleri (Muzzle Flash ve Trail) ağdaki herkese göster
        PlayShootVisualsRpc(hitPoint, hitNormal, spawnDecal);

        // Kamerayı sars (Recoil)
        shooter.AddRecoil(verticalRecoil, horizontalRecoil);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Owner)]
    private void PlayShootVisualsRpc(Vector3 endPoint, Vector3 hitNormal, bool spawnDecal, RpcParams rpcParams = default)
    {
        if (weaponAudioSource != null && shootSound != null) weaponAudioSource.PlayOneShot(shootSound);

        if (muzzleFlash != null) muzzleFlash.Play();

        if (bulletTrailPrefab != null && barrelPoint != null)
        {
            TrailRenderer trail = Instantiate(bulletTrailPrefab, barrelPoint.position, Quaternion.identity);
            trail.transform.DOMove(endPoint, 0.05f).SetEase(Ease.Linear).OnComplete(() => {
                Destroy(trail.gameObject, trail.time);
            });
        }

        // Mermi deliği (Decal) oluşturma
        if (spawnDecal && bulletHolePrefab != null)
        {
            // Titremeyi (Z-Fighting) önlemek için yüzeyden çok az (0.001f) öne alıyoruz
            Vector3 spawnPos = endPoint + hitNormal * 0.001f;
            Quaternion spawnRot = Quaternion.LookRotation(hitNormal);
            
            GameObject decal = Instantiate(bulletHolePrefab, spawnPos, spawnRot);
            Destroy(decal, 10f); // Performans için 10 saniye sonra sil
        }
    }
}