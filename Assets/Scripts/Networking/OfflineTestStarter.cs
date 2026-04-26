using Unity.Netcode;
using UnityEngine;

public class OfflineTestStarter : MonoBehaviour
{
    private void Start()
    {
        // Eğer sahnede bir NetworkManager varsa ve henüz çalışmıyorsa
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            // Doğrudan Host (Sunucu + Oyuncu) olarak başlat
            NetworkManager.Singleton.StartHost();
            
            Debug.Log("[OfflineTest] Yerel test sunucusu başlatıldı. Karakter kontrolleri aktif.");
        }
    }
}