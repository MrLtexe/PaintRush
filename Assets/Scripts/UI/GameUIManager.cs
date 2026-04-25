using UnityEngine;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("UI Elementleri")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text teamAScoreText;
    [SerializeField] private TMP_Text teamBScoreText;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text switchesText;

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
}