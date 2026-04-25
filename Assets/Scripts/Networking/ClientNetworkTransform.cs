using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // Bu fonksiyon false döndürdüğünde, objeyi Sunucu değil, objenin Sahibi (Owner) kimse o yönetir.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}