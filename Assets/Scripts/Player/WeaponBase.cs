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

    [Header("Mermi ve Şarjör (Ammo)")]
    public int maxAmmoPerMag;    // Şarjör kapasitesi
    public int currentAmmo;      // Şarjördeki anlık mermi
    public int reserveAmmo;      // Yedekteki toplam mermi
    public float reloadTime;     // Yeniden yükleme süresi
    public bool isReloading;     // Şu an reload yapıyor mu?

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

    private Coroutine _reloadCoroutine;
    private Vector3 _initialLocalRot;

    private void Start()
    {
        _initialLocalRot = transform.localEulerAngles;
    }

    private void OnDisable()
    {
        // Silah gizlenirse (değiştirilirse) reload'u güvenli şekilde iptal et
        CancelReload();
    }

    // Oyuncu inputlarına göre ateş etme denemesi yapar
    public virtual void HandleShooting(bool wasPressed, bool isPressed, Transform cameraTransform, FPSController shooter)
    {
        if (isReloading) return; // Reload yapıyorsa ateş edemez

        bool wantToShoot = isAutomatic ? isPressed : wasPressed;

        if (wantToShoot && Time.time >= nextTimeToFire)
        {
            if (currentAmmo > 0)
            {
                nextTimeToFire = Time.time + (1f / fireRate);
                PerformShoot(cameraTransform, shooter);
            }
            else
            {
                // Şarjör boşsa tıklayınca otomatik reload yap
                StartReload();
            }
        }
    }

    public void StartReload()
    {
        // Zaten reload yapıyorsa, şarjör tam doluysa veya yedekte mermi yoksa işlemi yapma
        if (isReloading || currentAmmo >= maxAmmoPerMag || reserveAmmo <= 0) return;
        
        if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        isReloading = true;

        // Sadece lokalde çalışan basit DOTween animasyonu (Silah namlusunu yukarı 60 derece kaldırıp indirir)
        Vector3 reloadRot = _initialLocalRot + new Vector3(-60f, 0, 0);
        transform.DOLocalRotate(reloadRot, reloadTime / 2f).SetEase(Ease.InOutQuad)
            .OnComplete(() => transform.DOLocalRotate(_initialLocalRot, reloadTime / 2f).SetEase(Ease.InOutQuad));

        yield return new WaitForSeconds(reloadTime);

        // Mermi hesaplaması (Taktiksel Reload: Sadece eksik olanı tamamla)
        int bulletsNeeded = maxAmmoPerMag - currentAmmo;
        int bulletsToLoad = Mathf.Min(bulletsNeeded, reserveAmmo);

        currentAmmo += bulletsToLoad;
        reserveAmmo -= bulletsToLoad;
        
        isReloading = false;
        UpdateAmmoUI();
    }

    public void CancelReload()
    {
        if (isReloading)
        {
            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            isReloading = false;
            transform.DOKill();
            transform.localEulerAngles = _initialLocalRot; // Silah açısını sıfırla
        }
    }

    public void UpdateAmmoUI()
    {
        if (IsOwner && GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateAmmoUI(currentAmmo, reserveAmmo);
        }
    }

    // Gerçek mermi atış mantığı (Raycast)
    protected virtual void PerformShoot(Transform cameraTransform, FPSController shooter)
    {
        currentAmmo--;
        UpdateAmmoUI();

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

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
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