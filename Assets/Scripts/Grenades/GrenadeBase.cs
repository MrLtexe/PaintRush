using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Grenade prefab'ına NetworkObject + NetworkTransform component'larının eklenmesi gereklidir.
// NetworkTransform varsayılan olarak server-authoritative çalışır (ClientNetworkTransform değil).
// Prefab'ı ayrıca DefaultNetworkPrefabs listesine eklemeyi unutmayın.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
public abstract class GrenadeBase : NetworkBehaviour
{
    [Header("Fırlatma Ayarları")]
    public float fuseTime = 3f;
    public float throwForce = 15f;

    protected Rigidbody _rb;
    private float _fuseTimer;
    private bool _hasExploded;

    public override void OnNetworkSpawn()
    {
        _rb = GetComponent<Rigidbody>();

        if (!IsServer && _rb != null)
            _rb.isKinematic = true; // Fizik simülasyonu yalnızca server'da çalışır

        if (IsServer)
            _fuseTimer = fuseTime;
    }

    // Server tarafından spawn sonrası çağrılır
    public void Launch(Vector3 direction)
    {
        if (!IsServer) return;
        _rb.AddForce(direction * throwForce, ForceMode.Impulse);
        _rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
    }

    private void Update()
    {
        if (!IsServer || _hasExploded) return;
        _fuseTimer -= Time.deltaTime;
        if (_fuseTimer <= 0f) TriggerExplosion();
    }

    private void TriggerExplosion()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Vector3 pos = transform.position;

        // Önce görsel efekti herkese gönder, sonra hasar uygula, sonra despawn et
        ExplodeVisualRpc(pos);
        OnExplode(pos);
        GetComponent<NetworkObject>().Despawn(true);
    }

    [Rpc(SendTo.Everyone)]
    private void ExplodeVisualRpc(Vector3 position)
    {
        OnExplodeVisual(position);
    }

    // Server-only: hasar ve oyun etkilerini uygula
    protected abstract void OnExplode(Vector3 position);

    // Tüm clientlarda: görsel ve ses efektleri
    protected virtual void OnExplodeVisual(Vector3 position) { }
}
