using UnityEngine;

public class Rifle : WeaponBase
{
    private void Awake()
    {
        weaponName = "Tüfek";
        damage = 30;
        range = 100f;
        fireRate = 10f; // Saniyede 10 mermi atar
        isAutomatic = true; // Basılı tutunca sürekli sıkar
        verticalRecoil = 2f;
        horizontalRecoil = 0.8f;

        // Mermi sistemi
        maxAmmoPerMag = 30;
        currentAmmo = 30;
        maxReserveAmmo = 60;
        reserveAmmo = 60; // 2 Yedek Şarjör
        reloadTime = 3f;
    }

    // İleride buraya tüfeğe özel geri tepme (Recoil) veya dürbün (ADS) özellikleri eklenebilir.
}