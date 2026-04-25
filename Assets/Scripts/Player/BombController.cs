using Unity.Netcode;
using UnityEngine;

public class BombController : NetworkBehaviour
{
    public NetworkVariable<bool> isDefused = new NetworkVariable<bool>(false);

    [Rpc(SendTo.Server)]
    public void TryDefuseRpc(RpcParams rpcParams = default)
    {
        if (isDefused.Value) return;

        // Sadece DefusePhase aşamasındaysak çözülebilir
        if (GameManager.Instance.CurrentState.Value == GameState.DefusePhase)
        {
            // TODO: B Takımı imha etme süreci (Hemen mi çözülecek, 5 saniye basılı mı tutulacak?)
            
            isDefused.Value = true;
            
            // TODO: GameManager'a haber ver (B Takımı kazandı)
            Debug.Log("Bomba imha edildi! B Takımı raundu kazanıyor.");
        }
    }
}