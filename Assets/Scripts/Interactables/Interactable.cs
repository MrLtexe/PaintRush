using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string promptMessage = "Etkileşim";
    public virtual void Interact()
    {
        Debug.Log($"{gameObject.name} ile etkileşim gerçekleşti.");
    }

    void BaseInteract()
    {
        Interact();
    }
}
