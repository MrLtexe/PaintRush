using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum GameState
{
    WaitingForPlayers, // Lobi/Yükleme ekranı
    PreRound,          // 5 sn hazırlık (Hareket kilitli)
    ObjectivePhase,    // 90 sn Switch açma (Ölünce 5 sn sonra doğma)
    TransitionPhase,   // 3 sn ikincil spawnlara ışınlanma (Hareket kilitli)
    DefusePhase,       // 40 sn Bomba patlama süresi (Ölüm kalıcı)
    RoundEnd,          // Raund bitti, puanlar dağıtıldı
    MatchEnd           // Bir takım 3 puan yaptı, maç bitti
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Oyun Durumu Senkronizasyonu")]
    public NetworkVariable<GameState> CurrentState = new NetworkVariable<GameState>(GameState.WaitingForPlayers);
    public NetworkVariable<float> RoundTimer = new NetworkVariable<float>(0f);
    
    [Header("Süre Ayarları")]
    public float preRoundDuration = 5f;
    public float objectiveDuration = 90f;
    public float transitionDuration = 3f;
    public float defuseDuration = 40f;
    public float roundEndDuration = 5f;
    public float respawnCooldown = 5f;

    [Header("Görev Ayarları")]
    public NetworkVariable<int> ActivatedSwitches = new NetworkVariable<int>(0);
    public const int TotalSwitchesNeeded = 3;

    public const int ScoreToWin = 3;

    [Header("Skor Tablosu")]
    public NetworkVariable<int> TeamAScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> TeamBScore = new NetworkVariable<int>(0);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Oyun sahnesi yüklendiğinde hazırlık evresini başlat
            StartPreRound();
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        // Sadece süreli aşamalarda sayacı çalıştır
        if (CurrentState.Value == GameState.WaitingForPlayers || CurrentState.Value == GameState.MatchEnd)
            return;

        if (RoundTimer.Value > 0)
        {
            RoundTimer.Value -= Time.deltaTime;
            
            // Süre bittiğinde ne olacağına karar ver
            if (RoundTimer.Value <= 0)
            {
                RoundTimer.Value = 0;
                HandleTimerZero();
            }
        }
    }
    
    private void HandleTimerZero()
    {
        switch (CurrentState.Value)
        {
            case GameState.PreRound:
                // 5 sn bitti -> Hareket serbest, Switchleri açma süresi başlar
                ChangeState(GameState.ObjectivePhase, objectiveDuration);
                break;
                
            case GameState.ObjectivePhase:
                // 90 sn bitti ve A takımı switchleri açamadı -> B takımı kazandı
                EndRound(2);
                break;
                
            case GameState.TransitionPhase:
                // 3 sn ışınlanma arası bitti -> Bomba sayacı başlar
                ChangeState(GameState.DefusePhase, defuseDuration);
                break;
                
            case GameState.DefusePhase:
                // 40 sn bitti ve B takımı bombayı imha edemedi -> Bomba patlar, A takımı kazandı
                EndRound(1);
                break;
                
            case GameState.RoundEnd:
                // Raund arası bitti -> Yeni raunda geç
                StartPreRound();
                break;
        }
    }

    // ── Aşama Kontrolleri ────────────────────────────────────────────────

    private void ChangeState(GameState newState, float duration)
    {
        CurrentState.Value = newState;
        RoundTimer.Value = duration;
        Debug.Log($"[GameManager] Yeni Aşama: {newState} | Süre: {duration} sn");
    }

    private void StartPreRound()
    {
        ActivatedSwitches.Value = 0;
        
        // Yeni Raund Başlangıcı: Bütün oyuncuları başlangıç noktalarına ışınla (Ölüyse dirilir, canları dolar)
        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player != null)
            {
                var (pos, rot) = PlayerSpawnManager.Instance.GetRandomSpawnPoint(player.GetTeam());
                player.Respawn(pos, rot); 
            }
        }

        // TODO: Şalterleri sıfırla
        ChangeState(GameState.PreRound, preRoundDuration);
    }

    public void SwitchActivated()
    {
        if (!IsServer) return;
        
        ActivatedSwitches.Value++;
        Debug.Log($"[GameManager] Şalter açıldı! ({ActivatedSwitches.Value}/{TotalSwitchesNeeded})");

        if (ActivatedSwitches.Value >= TotalSwitchesNeeded)
        {
            TriggerTransitionPhase();
        }
    }

    // Dışarıdan (örneğin InteractableSwitch veya BombController) çağrılacak fonksiyonlar
    public void TriggerTransitionPhase()
    {
        if (CurrentState.Value == GameState.ObjectivePhase)
        {
            // Yeni evreye (Defuse) geçmeden hemen önce, ölü olan oyuncuları bekletmeden anında dirilt
            PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player != null && player.isDead.Value)
                {
                    var (pos, rot) = PlayerSpawnManager.Instance.GetRandomSpawnPoint(player.GetTeam());
                    player.Respawn(pos, rot);
                }
            }

            // TODO: İkincil noktalara ışınla
            ChangeState(GameState.TransitionPhase, transitionDuration);
        }
    }

    public void EndRound(int winnerTeamId)
    {
        Debug.Log($"[GameManager] Raund bitti. Kazanan Takım: {(winnerTeamId == 1 ? "A Takımı" : "B Takımı")}");
        
        if (winnerTeamId == 1) TeamAScore.Value++;
        else if (winnerTeamId == 2) TeamBScore.Value++;

        if (TeamAScore.Value >= ScoreToWin || TeamBScore.Value >= ScoreToWin)
        {
            CurrentState.Value = GameState.MatchEnd;
            Debug.Log("[GameManager] MAÇ BİTTİ!");
        }
        else
        {
            ChangeState(GameState.RoundEnd, roundEndDuration);
        }
    }

    // ── Ölüm ve Doğma Sistemi ────────────────────────────────────────────

    public void OnPlayerDied(PlayerHealth victim)
    {
        if (!IsServer) return;

        int teamId = victim.GetTeam();
        Debug.Log($"[GameManager] Oyuncu öldü. Takım: {(teamId == 1 ? "A" : "B")} | Aşama: {CurrentState.Value}");

        if (CurrentState.Value == GameState.ObjectivePhase)
        {
            // Deathmatch evresi: 5 saniye sonra doğur
            StartCoroutine(RespawnRoutine(victim, respawnCooldown));
        }
        else if (CurrentState.Value == GameState.DefusePhase)
        {
            // Kalıcı ölüm evresi: B takımı tamamen öldü mü kontrol et
            if (teamId == 2) CheckTeamBWipeout();
        }
    }

    private IEnumerator RespawnRoutine(PlayerHealth victim, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (victim == null || !victim.IsSpawned) yield break;

        // Geçiş aşamasında oyuncu topluca (otomatik) diriltildiyse (isDead = false) tekrar ışınlama yapma
        if (!victim.isDead.Value) yield break;

        // Objective veya Transition evresindeysek normal bir şekilde doğmaya izin ver
        if (CurrentState.Value == GameState.ObjectivePhase || CurrentState.Value == GameState.TransitionPhase)
        {
            var (pos, rot) = PlayerSpawnManager.Instance.GetRandomSpawnPoint(victim.GetTeam());
            victim.Respawn(pos, rot);
        }
    }

    private void CheckTeamBWipeout()
    {
        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        bool isAnyBAlive = false;

        foreach (var player in allPlayers)
        {
            if (player.GetTeam() == 2 && !player.isDead.Value)
            {
                isAnyBAlive = true;
                break;
            }
        }

        if (!isAnyBAlive)
        {
            Debug.Log("[GameManager] B Takımının tamamı öldü! A Takımı kazanıyor.");
            EndRound(1); // 1: A Takımı
        }
    }
}