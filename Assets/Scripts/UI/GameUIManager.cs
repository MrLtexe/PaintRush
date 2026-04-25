using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("UI Elementleri")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text teamAScoreText;
    [SerializeField] private TMP_Text teamBScoreText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text switchesText;
    [SerializeField] private TMP_Text healthText;

    [Header("Etkileşim (İlerleme) UI")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private Image interactionProgressBar;
    [SerializeField] private TMP_Text interactionText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        // GameManager henüz sahnede yoksa bekle
        if (GameManager.Instance == null) return;

        // 1. Süreyi Güncelle (MM:SS Formatında)
        float time = GameManager.Instance.RoundTimer.Value;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        
        if (timerText) 
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        // 2. Skorları Güncelle
        if (teamAScoreText) 
            teamAScoreText.text = GameManager.Instance.TeamAScore.Value.ToString();
            
        if (teamBScoreText) 
            teamBScoreText.text = GameManager.Instance.TeamBScore.Value.ToString();

        // 3. Aşama (State) İsmini Güncelle
        if (phaseText) 
            phaseText.text = GetPhaseName(GameManager.Instance.CurrentState.Value);

        // 4. Şalter Görevi Durumunu Güncelle
        if (switchesText)
        {
            if (GameManager.Instance.CurrentState.Value == GameState.ObjectivePhase)
                switchesText.text = $"Şalterler: {GameManager.Instance.ActivatedSwitches.Value} / {GameManager.TotalSwitchesNeeded}";
            else
                switchesText.text = ""; // Sadece şalter açma evresinde ekranda göster
        }
    }

    private string GetPhaseName(GameState state)
    {
        return state switch
        {
            GameState.WaitingForPlayers => "Oyuncular Bekleniyor...",
            GameState.PreRound => "Hazırlık Evresi",
            GameState.ObjectivePhase => "Görev: Şalterleri Aç!",
            GameState.TransitionPhase => "Bölge Değişimi",
            GameState.DefusePhase => "Görev: Bombayı İmha Et!",
            GameState.RoundEnd => "Raund Bitti",
            GameState.MatchEnd => "Maç Bitti",
            _ => ""
        };
    }

    public void ShowInteraction(string message, float progress)
    {
        if (interactionPanel && !interactionPanel.activeSelf) interactionPanel.SetActive(true);
        if (interactionText) interactionText.text = message;
        if (interactionProgressBar) interactionProgressBar.fillAmount = progress;
    }

    public void HideInteraction()
    {
        if (interactionPanel && interactionPanel.activeSelf) interactionPanel.SetActive(false);
    }

    public void UpdateHealthUI(int currentHealth, int maxHealth = 100)
    {
        if (healthText) healthText.text = $"Can: {currentHealth} / {maxHealth}";
    }
}