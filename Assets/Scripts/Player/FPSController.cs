using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class FPSController : NetworkBehaviour
{
    [Header("Kamera ve Hareket Ayarları")]
    public Transform playerCamera;
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -15f;
    public float mouseSensitivity = 15f;

    [Header("Mevcut Input Referansları")]
    public InputActionReference moveInput;
    public InputActionReference lookInput;
    public InputActionReference attackInput;
    public InputActionReference sprintInput;
    public InputActionReference jumpInput;

    [Header("Yeni Eklenecek Input Referansları")]
    [Tooltip("Input Asset'te Vector2 (Pass Through) tipinde bir Scroll aksiyonu oluşturun")]
    public InputActionReference scrollWeaponInput;
    [Tooltip("Input Asset'te Button tipinde '1' tuşuna atanmış aksiyon")]
    public InputActionReference grenadeInput;
    [Tooltip("Input Asset'te Button tipinde '2' tuşuna atanmış aksiyon")]
    public InputActionReference smokeInput;
    [Tooltip("Input Asset'te Button tipinde '3' tuşuna atanmış aksiyon")]
    public InputActionReference flashbangInput;

    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private float _xRotation = 0f;

    // Silah Durumu (0: Tüfek, 1: Tabanca)
    private int _currentWeaponIndex = 0;

    public override void OnNetworkSpawn()
    {
        // Eğer bu karakter bizim değilse kamerasını kapatıyoruz ki kendi kameramızla çakışmasın.
        if (!IsOwner)
        {
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
            return;
        }

        _controller = GetComponent<CharacterController>();
        
        // Fareyi ekrana kilitle ve gizle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnableInputs();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            DisableInputs();
        }
    }

    private void Update()
    {
        // Sadece kendi karakterimizi kontrol edebiliriz
        if (!IsOwner) return;

        _isGrounded = _controller.isGrounded;
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Yerdeyken sürekli yapışık kalması için küçük bir aşağı kuvvet
        }

        HandleLook();
        HandleMovement();
        HandleJump();
        HandleWeapons();
        HandleUtilities();
    }

    // ── HAREKET VE KAMERA ────────────────────────────────────────────────

    private void HandleLook()
    {
        Vector2 lookValue = lookInput.action.ReadValue<Vector2>();
        float mouseX = lookValue.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookValue.y * mouseSensitivity * Time.deltaTime;

        // Sadece kamerayı yukarı/aşağı döndür
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Karakterin tüm gövdesini sağa/sola döndür
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        Vector2 moveValue = moveInput.action.ReadValue<Vector2>();
        Vector3 move = transform.right * moveValue.x + transform.forward * moveValue.y;

        // Shift'e basılıysa koşma hızını al, değilse yürüme hızı
        float currentSpeed = sprintInput.action.IsPressed() ? sprintSpeed : walkSpeed;

        _controller.Move(move * currentSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        // Space tuşuna basıldığında ve yerdeysek zıpla
        if (jumpInput.action.WasPressedThisFrame() && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Yerçekimini uygula
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    // ── SİLAH VE ÇATIŞMA ─────────────────────────────────────────────────

    private void HandleWeapons()
    {
        // Sol tık (Ateş etme)
        if (attackInput.action.WasPressedThisFrame())
        {
            // TODO: Ateş etme mantığı buraya eklenecek
            Debug.Log(_currentWeaponIndex == 0 ? "Tüfek ile ateş edildi!" : "Tabanca ile ateş edildi!");
        }

        // Scroll ile silah değiştirme (Mouse ScrollWheel)
        if (scrollWeaponInput != null && scrollWeaponInput.action.triggered)
        {
            float scrollY = scrollWeaponInput.action.ReadValue<Vector2>().y;
            if (scrollY > 0) SwitchWeapon(0);      // İleri Scroll -> Tüfek
            else if (scrollY < 0) SwitchWeapon(1); // Geri Scroll -> Tabanca
        }
    }

    private void SwitchWeapon(int weaponIndex)
    {
        _currentWeaponIndex = weaponIndex;
        Debug.Log(_currentWeaponIndex == 0 ? "Tüfek donanıldı." : "Tabanca donanıldı.");
        // TODO: Silah modellerini (Mesh) aç/kapa yapma kodları buraya eklenecek
    }

    // ── BOMBALAR VE EKSTRALAR ────────────────────────────────────────────

    private void HandleUtilities()
    {
        if (grenadeInput != null && grenadeInput.action.WasPressedThisFrame())
            Debug.Log("Grenade (1) fırlatıldı!"); // TODO: Grenade mantığı
            
        if (smokeInput != null && smokeInput.action.WasPressedThisFrame())
            Debug.Log("Smoke (2) fırlatıldı!"); // TODO: Smoke mantığı
            
        if (flashbangInput != null && flashbangInput.action.WasPressedThisFrame())
            Debug.Log("Flashbang (3) fırlatıldı!"); // TODO: Flashbang mantığı
    }

    // ── INPUT KONTROLLERİ ────────────────────────────────────────────────

    private void EnableInputs()
    {
        moveInput.action.Enable(); lookInput.action.Enable();
        attackInput.action.Enable(); sprintInput.action.Enable(); jumpInput.action.Enable();
        
        if (scrollWeaponInput) scrollWeaponInput.action.Enable();
        if (grenadeInput) grenadeInput.action.Enable();
        if (smokeInput) smokeInput.action.Enable();
        if (flashbangInput) flashbangInput.action.Enable();
    }

    private void DisableInputs()
    {
        moveInput.action.Disable(); lookInput.action.Disable();
        attackInput.action.Disable(); sprintInput.action.Disable(); jumpInput.action.Disable();
    }
    
}
