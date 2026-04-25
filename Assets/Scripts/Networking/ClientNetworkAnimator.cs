using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkAnimator : NetworkAnimator
{
    // Bu fonksiyon false döndürdüğünde, animasyonları Sunucu değil, objenin Sahibi (Owner) yönetir.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}