using Unity.Netcode;
using UnityEngine;
using DG.Tweening;

public class InteractableSwitch : NetworkBehaviour
{
    public NetworkVariable<bool> isActivated = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isBeingInteracted = new NetworkVariable<bool>(false);

    [Header("Animasyon Ayarları")]
    public Vector3 activeRotationOffset = new Vector3(90, 0, 0); // Şalterin açıldığında ne kadar döneceği
    public float interactDuration = 5f;
    private Vector3 _initialRotation;

    private void Start()
    {
        _initialRotation = transform.eulerAngles;
    }

    public override void OnNetworkSpawn()
    {
        isBeingInteracted.OnValueChanged += OnInteractionStateChanged;
        isActivated.OnValueChanged += OnActivationStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        isBeingInteracted.OnValueChanged -= OnInteractionStateChanged;
        isActivated.OnValueChanged -= OnActivationStateChanged;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetInteractingRpc(bool state, RpcParams rpcParams = default)
    {
        if (isActivated.Value) return;
        isBeingInteracted.Value = state;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TryActivateRpc(RpcParams rpcParams = default)
    {
        if (isActivated.Value) return;
        
        // Sadece ObjectivePhase aşamasındaysak açılabilir
        if (GameManager.Instance.CurrentState.Value == GameState.ObjectivePhase)
        {
            isActivated.Value = true;
            isBeingInteracted.Value = false;
            GameManager.Instance.SwitchActivated();
        }
    }

    public void ResetSwitch()
    {
        if (IsServer) 
        {
            isActivated.Value = false;
            isBeingInteracted.Value = false;
        }
    }

    private void OnInteractionStateChanged(bool previous, bool current)
    {
        if (isActivated.Value) return;
        transform.DOKill(); // Mevcut animasyonları durdur

        // Etkileşim başladıysa 5 saniyede yavaşça hedefe dön, iptal edildiyse 0.5 saniyede hızlıca başlangıca dön
        if (current) transform.DORotate(_initialRotation + activeRotationOffset, interactDuration).SetEase(Ease.Linear);
        else transform.DORotate(_initialRotation, 0.5f).SetEase(Ease.OutQuad);
    }

    private void OnActivationStateChanged(bool previous, bool current)
    {
        transform.DOKill();
        if (current) transform.DORotate(_initialRotation + activeRotationOffset, 0.2f);
        else transform.DORotate(_initialRotation, 0.5f);
    }
}