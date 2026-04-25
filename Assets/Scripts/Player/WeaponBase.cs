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
        
        // Bütün çarpan objeleri al (Kendi karakterimize çarpıp merminin durmasını engellemek için)
        RaycastHit[] hits = Physics.RaycastAll(ray, range, shooter.shootLayer);
        
        // Çarpan objeleri mesafeye göre yakından uzağa sırala
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();

            // Eğer mermi KENDİMİZE çarptıysa, görmezden gel ve arkaya gitmeye devam et
            if (targetHealth != null && targetHealth.OwnerClientId == shooter.OwnerClientId)
                continue; 

            // Kendimiz haricinde İLK geçerli objeye çarptık (Duvar veya Düşman)
            hitPoint = hit.point;
            hitNormal = hit.normal;
            spawnDecal = true;

            if (targetHealth != null)
            {
                spawnDecal = false; // Vurduğumuz şey oyuncuysa duvar deliği çıkartma

                // Kendi takım arkadaşımızı vurmayı engelliyoruz
                if (targetHealth.GetTeam() != shooter.GetMyTeam())
                {
                    var targetNetObj = targetHealth.GetComponent<NetworkObject>();
                    if (targetNetObj != null) shooter.HitPlayerRpc(targetNetObj.NetworkObjectId, damage);
                }
            }
            break; // İlk hedefe (duvar veya düşman) çarptıktan sonra mermiyi durdur
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