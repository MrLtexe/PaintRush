using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using DG.Tweening;

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

    [Header("Etkileşim (Interact) Ayarları")]
    public InputActionReference interactInput;
    public float interactRange = 3f;
    public LayerMask interactLayer = Physics.DefaultRaycastLayers;

    [Header("Silah ve Hasar Ayarları")]
    public WeaponBase[] weapons; // 0: Tüfek, 1: Tabanca
    public LayerMask shootLayer = Physics.DefaultRaycastLayers; // Sadece oyuncuları (veya duvarları) vurması için ayarlayabilirsin

    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private float _xRotation = 0f;

    [Header("Animasyon")]
    [SerializeField] private Animator _animator;
    private static readonly int AnimVelX = Animator.StringToHash("VelocityX");
    private static readonly int AnimVelZ = Animator.StringToHash("VelocityZ");

    [Header("Ağ Senkronizasyonu")]
    public NetworkVariable<float> networkViewPitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> networkWeaponIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Geri Tepme (Recoil) Ayarları")]
    public float recoilKickDuration = 0.05f; // Sarsıntının ne kadar hızlı vuracağı
    public float recoilReturnDuration = 0.25f; // Namlunun eski yerine ne kadar hızlı döneceği
    private Vector3 _currentRecoil;

    private InteractableSwitch _currentSwitch;
    private BombController _currentBomb;
    private bool _isInteracting;
    private float _interactTimer;
    private PlayerHealth _health;

    // Silah Durumu (0: Tüfek, 1: Tabanca)
    private int _currentWeaponIndex = 0;

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<PlayerHealth>();

        // Güvenlik: Eğer Inspector'dan atanmayı unutursak alt objelerden otomatik bulmaya çalış
        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
        }
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
        
        networkWeaponIndex.OnValueChanged += OnWeaponChanged;
        ApplyWeaponVisuals(networkWeaponIndex.Value); // Herkes diğer oyuncuların güncel silahını görebilsin

        if (!IsOwner)
        {
            // Objenin kendini kapatmak yerine sadece Görüntü ve Ses dinleyiciyi kapatıyoruz.
            // Böylece objenin altındaki silah modelleri diğer oyuncularda görünmeye devam eder!
            if (playerCamera != null)
            {
                var cam = playerCamera.GetComponent<Camera>();
                if (cam != null) cam.enabled = false;
                var al = playerCamera.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }

            // DİĞER OYUNCULAR İÇİN CHARACTER CONTROLLER'I KAPAT (Çarpışma ve fiziksel itme hatalarını engeller)
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;

                // DİKKAT: CC kapandığı için düşmanlar mermi algılayamaz duruma geldi.
                // Bu yüzden mermilerin (Raycast) çarpabilmesi için yedek bir Hitbox ekliyoruz.
                CapsuleCollider hitbox = gameObject.AddComponent<CapsuleCollider>();
                hitbox.radius = cc.radius;
                hitbox.height = cc.height;
                hitbox.center = cc.center;
                // isTrigger = true yapıyoruz ki oyuncular birbirine takılıp itmesin ama mermiler vursun
                hitbox.isTrigger = true;
            }

            // Yeni açı değerini sadece değiştiğinde dinle
            networkViewPitch.OnValueChanged += OnPitchChanged;
            return;
        }

        _controller = GetComponent<CharacterController>();
        
        // Fareyi ekrana kilitle ve gizle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnableInputs();

        // Canımız azaldığında (hasar aldığımızda) etkileşimi iptal etmek için dinliyoruz
        if (_health != null)
        {
            _health.currentHealth.OnValueChanged += OnHealthChanged;
        }
    }

    private void OnPitchChanged(float previousValue, float newValue)
    {
        if (playerCamera != null)
        {
            playerCamera.DOKill(); // Çakışmaları önlemek için eski animasyonu durdur
            playerCamera.DOLocalRotate(new Vector3(newValue, 0f, 0f), 0.1f).SetEase(Ease.Linear);
        }
    }

    private void OnWeaponChanged(int previousValue, int newValue)
    {
        ApplyWeaponVisuals(newValue);
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        // Eğer can azaldıysa ve şalter açıyorsak iptal et
        if (newValue < previousValue && _isInteracting)
        {
            CancelInteraction();
        }
    }

    public override void OnNetworkDespawn()
    {
        networkWeaponIndex.OnValueChanged -= OnWeaponChanged;

        if (IsOwner)
        {
            DisableInputs();
            if (_health != null) _health.currentHealth.OnValueChanged -= OnHealthChanged;
        }
        else
        {
            networkViewPitch.OnValueChanged -= OnPitchChanged;
        }
    }

    private void Update()
    {
        if (!IsOwner) return; // Dışarıdan bakanların Update döngüsünde yapacak hiçbir işi kalmadı!

        // Eğer öldüysek hiçbir inputu kabul etme (kamera, hareket, silah hepsi kitlenir)
        if (_health != null && _health.isDead.Value) return;

        _isGrounded = _controller.isGrounded;
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Yerdeyken sürekli yapışık kalması için küçük bir aşağı kuvvet
        }

        HandleLook(); // Etrafımıza bakabilmeliyiz

        // Oyun durumunu kontrol et (Sadece Objective ve Defuse evrelerinde hareket/ateş serbest)
        bool canAct = true;
        if (GameManager.Instance != null)
        {
            GameState state = GameManager.Instance.CurrentState.Value;
            if (state != GameState.ObjectivePhase && state != GameState.DefusePhase)
            {
                canAct = false; // Hazırlık, bekleme veya bitiş ekranındayız
            }
        }

        if (_isInteracting)
        {
            HandleInteracting(); // Sadece etkileşim sürecini işlet (hareket kilitli)
        }
        else if (canAct)
        {
            HandleMovement();
            HandleJump();
            HandleWeapons();
            HandleUtilities();
            CheckInteractable(); // Şalter var mı diye kontrol et
        }
        else
        {
            // Hareket yasak olsa da yerçekimi çalışmaya devam etmeli (havada asılı kalmamak için)
            _velocity.x = 0;
            _velocity.z = 0;
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(Vector3.up * _velocity.y * Time.deltaTime);
        }
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

        // Bakış açımızı (Pitch) ağdaki diğer oyunculara bildir
        networkViewPitch.Value = _xRotation;

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(_xRotation - _currentRecoil.x, _currentRecoil.y, 0f);

        // Karakterin tüm gövdesini sağa/sola döndür
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        Vector2 moveValue = moveInput.action.ReadValue<Vector2>();
        Vector3 move = transform.right * moveValue.x + transform.forward * moveValue.y;

        bool isSprinting = sprintInput.action.IsPressed();
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;
        _controller.Move(move * currentSpeed * Time.deltaTime);

        float scale = moveValue.magnitude > 0.1f ? (isSprinting ? 1f : 0.5f) : 0f;
        float velX = moveValue.x * scale;
        float velZ = moveValue.y * scale;

        if (_animator)
        {
            _animator.SetFloat(AnimVelX, velX);
            _animator.SetFloat(AnimVelZ, velZ);
        }
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

    // ── ETKİLEŞİM (INTERACT) ─────────────────────────────────────────────

    private void CheckInteractable()
    {
        if (interactInput != null && interactInput.action.WasPressedThisFrame())
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
            {
                if (hit.collider.TryGetComponent(out InteractableSwitch sw))
                {
                    // Şalter zaten açılmadıysa ve bir başkası şu an açmıyorsa
                    if (!sw.isActivated.Value && !sw.isBeingInteracted.Value)
                    {
                        StartInteraction(sw);
                    }
                }
                else if (hit.collider.TryGetComponent(out BombController bomb))
                {
                    // Sadece B Takımı çözebilir, çözülmediyse ve Defuse evresindeysek
                    if (GetMyTeam() == 2 && !bomb.isDefused.Value && !bomb.isBeingDefused.Value && GameManager.Instance.CurrentState.Value == GameState.DefusePhase)
                    {
                        StartBombInteraction(bomb);
                    }
                }
            }
        }
    }

    public int GetMyTeam()
    {
        if (NetworkLobbyManager.Instance == null) return 1;
        foreach (var player in NetworkLobbyManager.Instance.LobbyPlayers)
        {
            if (player.ClientId == NetworkManager.Singleton.LocalClientId)
                return player.TeamId;
        }
        return 1;
    }

    private void StartInteraction(InteractableSwitch sw)
    {
        _currentSwitch = sw;
        _isInteracting = true;
        _interactTimer = 0f;
        _currentSwitch.SetInteractingRpc(true); // Animasyonu başlat
        if (GameUIManager.Instance != null) GameUIManager.Instance.ShowInteraction("Şalter Açılıyor...", 0f);
    }

    private void StartBombInteraction(BombController bomb)
    {
        _currentBomb = bomb;
        _isInteracting = true;
        _interactTimer = 0f;
        _currentBomb.SetDefusingRpc(true); 
        if (GameUIManager.Instance != null) GameUIManager.Instance.ShowInteraction("Bomba İmha Ediliyor...", 0f);
    }

    private void HandleInteracting()
    {
        // Oyuncu tuşu bırakırsa veya hedeften uzağa/başka yöne bakarsa iptal et
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        bool isLookingAtTarget = Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer);

        bool isValidTarget = false;
        if (isLookingAtTarget)
        {
            if (_currentSwitch != null && hit.collider.gameObject == _currentSwitch.gameObject) isValidTarget = true;
            if (_currentBomb != null && hit.collider.gameObject == _currentBomb.gameObject) isValidTarget = true;
        }

        if (!interactInput.action.IsPressed() || !isValidTarget)
        {
            CancelInteraction();
            return;
        }

        _interactTimer += Time.deltaTime;

        if (_currentSwitch != null)
        {
            if (GameUIManager.Instance != null) GameUIManager.Instance.ShowInteraction("Şalter Açılıyor...", _interactTimer / _currentSwitch.interactDuration);

            if (_interactTimer >= _currentSwitch.interactDuration)
            {
                _currentSwitch.TryActivateRpc(); // Başarıyla tamamlandı
                _isInteracting = false;
                _currentSwitch = null;
                if (GameUIManager.Instance != null) GameUIManager.Instance.HideInteraction();
            }
        }
        else if (_currentBomb != null)
        {
            if (GameUIManager.Instance != null) GameUIManager.Instance.ShowInteraction("Bomba İmha Ediliyor...", _interactTimer / _currentBomb.defuseDuration);

            if (_interactTimer >= _currentBomb.defuseDuration)
            {
                _currentBomb.TryDefuseRpc(); // Başarıyla imha edildi
                _isInteracting = false;
                _currentBomb = null;
                if (GameUIManager.Instance != null) GameUIManager.Instance.HideInteraction();
            }
        }
    }

    private void CancelInteraction()
    {
        if (!_isInteracting) return;

        if (_currentSwitch != null)
        {
            _currentSwitch.SetInteractingRpc(false); // Animasyonu iptal edip geri sar
            _currentSwitch = null;
        }
        else if (_currentBomb != null)
        {
            _currentBomb.SetDefusingRpc(false);
            _currentBomb = null;
        }
        
        _isInteracting = false;
        if (GameUIManager.Instance != null) GameUIManager.Instance.HideInteraction();
    }

    public void AddRecoil(float verticalRecoil, float horizontalRecoil)
    {
        DOTween.Kill("Recoil"); // Eğer önceki sarsıntı bitmediyse iptal et (Tarama yaparken üst üste binebilmesi için)

        // O anki sarsıntının üzerine yeni gücü ekle
        Vector3 targetRecoil = _currentRecoil + new Vector3(verticalRecoil, Random.Range(-horizontalRecoil, horizontalRecoil), 0f);

        // Sırayla çalışacak bir animasyon zinciri (Sequence) oluştur
        Sequence recoilSeq = DOTween.Sequence().SetId("Recoil");
        
        // 1. Kamera hızlıca yukarı seker (Kick)
        recoilSeq.Append(DOTween.To(() => _currentRecoil, x => _currentRecoil = x, targetRecoil, recoilKickDuration).SetEase(Ease.OutSine));
        // 2. Kamera yavaşça orijinal (0) konumuna döner (Return)
        recoilSeq.Append(DOTween.To(() => _currentRecoil, x => _currentRecoil = x, Vector3.zero, recoilReturnDuration).SetEase(Ease.InOutSine));
    }

    // ── SİLAH VE ÇATIŞMA ─────────────────────────────────────────────────

    private void HandleWeapons()
    {
        bool isShootingDown = attackInput.action.WasPressedThisFrame();
        bool isShootingPressed = attackInput.action.IsPressed();

        // Seçili silah varsa atış mantığını ona devret
        if (weapons != null && weapons.Length > _currentWeaponIndex && weapons[_currentWeaponIndex] != null)
        {
            weapons[_currentWeaponIndex].HandleShooting(isShootingDown, isShootingPressed, playerCamera, this);
        }

        // Scroll ile silah değiştirme (Mouse ScrollWheel)
        if (scrollWeaponInput != null && scrollWeaponInput.action.triggered)
        {
            float scrollY = scrollWeaponInput.action.ReadValue<Vector2>().y;
            if (scrollY > 0) SwitchWeapon(0);      // İleri Scroll -> Tüfek
            else if (scrollY < 0) SwitchWeapon(1); // Geri Scroll -> Tabanca
        }
    }

    [Rpc(SendTo.Server)]
    public void HitPlayerRpc(ulong targetNetworkObjectId, int damage, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetNetObj))
        {
            if (targetNetObj.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(damage);
                Debug.Log($"[Server] {targetNetworkObjectId} ID'li oyuncu vuruldu. Kalan Can: {health.currentHealth.Value}");
            }
        }
    }

    private void SwitchWeapon(int weaponIndex)
    {
        if (weapons == null || weapons.Length == 0) return;

        if (IsOwner)
        {
            networkWeaponIndex.Value = weaponIndex;
        }
    }

    private void ApplyWeaponVisuals(int weaponIndex)
    {
        if (weapons == null || weapons.Length == 0) return;

        _currentWeaponIndex = weaponIndex;
        
        // Tüm silah modellerini kapat, sadece seçileni aç
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                weapons[i].gameObject.SetActive(i == _currentWeaponIndex);
            }
        }
        
        // Sadece sahibi (Owner) konsol mesajını görsün
        if (IsOwner)
        {
            Debug.Log(weapons[_currentWeaponIndex] != null ? $"{weapons[_currentWeaponIndex].weaponName} donanıldı." : "Silah donanıldı.");
        }
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
        if (interactInput) interactInput.action.Enable();
    }

    private void DisableInputs()
    {
        moveInput.action.Disable(); lookInput.action.Disable();
        attackInput.action.Disable(); sprintInput.action.Disable(); jumpInput.action.Disable();
        if (interactInput) interactInput.action.Disable();
    }
    
}
