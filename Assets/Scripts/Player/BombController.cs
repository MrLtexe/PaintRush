using Unity.Netcode;
using UnityEngine;

public class BombController : NetworkBehaviour
{
    public NetworkVariable<bool> isDefused = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isBeingDefused = new NetworkVariable<bool>(false);
    
    [Header("Etkileşim Ayarları")]
    public float defuseDuration = 7f; // İmha süresi daha uzun (örneğin 7 saniye) olabilir

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetDefusingRpc(bool state, RpcParams rpcParams = default)
    {
        if (isDefused.Value) return;
        isBeingDefused.Value = state;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TryDefuseRpc(RpcParams rpcParams = default)
    {
        if (isDefused.Value) return;

        // Sadece DefusePhase aşamasındaysak çözülebilir
        if (GameManager.Instance.CurrentState.Value == GameState.DefusePhase)
        {
            isDefused.Value = true;
            isBeingDefused.Value = false;
            
            GameManager.Instance.EndRound(2); // 2: B Takımı kazanır
            Debug.Log("Bomba imha edildi! B Takımı raundu kazanıyor.");
        }
    }

    public void ResetBomb()
    {
        if (IsServer) { isDefused.Value = false; isBeingDefused.Value = false; }
    }
}