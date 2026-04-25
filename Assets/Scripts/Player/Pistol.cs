using UnityEngine;

public class Pistol : WeaponBase
{
    private void Awake()
    {
        weaponName = "Tabanca";
        damage = 15;
        range = 50f;
        fireRate = 5f; // Saniyede 5 mermi (oyuncu hızlı tıklarsa)
        isAutomatic = false; // Sadece her tıklamada 1 kez sıkar
        verticalRecoil = 1.5f;
        horizontalRecoil = 0.3f;
    }

    // İleride buraya tabancaya özel hızlı çekme veya koşarken sekme özellikleri eklenebilir.
}