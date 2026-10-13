using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static event Action<int> ScoreEvent;
    public static event Action<int> LevelEvent;
    public static event Action<int> ComboEvent;

    [Header("UI Text")]
    public Text ScoreText;
    public Text LevelText;
    public Text ComboText;

    public TMP_Text ScoreTMP;
    public TMP_Text LevelTMP;
    public TMP_Text ComboTMP;

    [Header("Settings")]
    public int baseLineScore = 100;
    public int scorePerLevel = 1000;
    public float comboTimeout = 2.5f;

    private int Score = 0;
    private int Level = 1;
    private int Combo = 0;

    private float scoreMultiplier = 1.0f;
    private float comboTimer = 0f;
    private bool isComboTimerActive = false;
    private int scoreAccumulatorForLevel = 0;
    private int nextLevelThreshold = 1000;

    public int score
    {
        get { return Score; }
        set
        {
            Score = value;
            ScoreEvent?.Invoke(Score);
        }
    }

    public int level
    {
        get { return Level; }
        set
        {
            Level = value;
            LevelEvent?.Invoke(Level);
        }
    }

    public int combo
    {
        get { return Combo; }
        set
        {
            Combo = value;
            ComboEvent?.Invoke(Combo);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ResetGame();
    }

    private void OnEnable()
    {
        ScoreEvent += UpdateScoreText;
        LevelEvent += UpdateLevelText;
        ComboEvent += UpdateComboText;
    }

    private void OnDisable()
    {
        ScoreEvent -= UpdateScoreText;
        LevelEvent -= UpdateLevelText;
        ComboEvent -= UpdateComboText;
    }

    private void UpdateScoreText(int newScore)
    {
        string text = newScore.ToString();
        if (ScoreText != null) ScoreText.text = text;
        if (ScoreTMP != null) ScoreTMP.text = text;
    }

    private void UpdateLevelText(int newLevel)
    {
        string text = newLevel.ToString();
        if (LevelText != null) LevelText.text = text;
        if (LevelTMP != null) LevelTMP.text = text;
    }

    private void UpdateComboText(int newCombo)
    {
        string text = newCombo > 1 ? $"x{newCombo}" : (newCombo == 1 ? "1" : "0");
        if (ComboText != null) ComboText.text = text;
        if (ComboTMP != null) ComboTMP.text = text;
    }

    private void Update()
    {
        if (isComboTimerActive)
        {
            comboTimer += Time.deltaTime;
            if (comboTimer >= comboTimeout)
            {
                ResetCombo();
            }
        }
    }

    public void ScoreSet(int linesCleared = 1)
    {
        if (linesCleared <= 0) return;

        int lineBonus;
        switch (linesCleared)
        {
            case 1: lineBonus = 100; break;
            case 2: lineBonus = 300; break;
            case 3: lineBonus = 500; break;
            case 4: lineBonus = 800; break;
            default: lineBonus = linesCleared * 250; break;
        }

        if (isComboTimerActive)
        {
            combo += 1;
            scoreMultiplier += 0.5f;
        }
        else
        {
            combo = 1;
            scoreMultiplier = 1.0f;
            isComboTimerActive = true;
        }

        comboTimer = 0f;

        int gainedScore = Mathf.RoundToInt(lineBonus * Mathf.Max(1, Level) * scoreMultiplier);
        score += gainedScore;

        scoreAccumulatorForLevel += gainedScore;
        while (scoreAccumulatorForLevel >= nextLevelThreshold)
        {
            scoreAccumulatorForLevel -= nextLevelThreshold;
            level += 1;
            nextLevelThreshold = Mathf.RoundToInt(nextLevelThreshold * 1.5f);
            Debug.Log($"Seviye {level} oldu. Sonraki hedef: {nextLevelThreshold}");
        }
    }

    private void ResetCombo()
    {
        combo = 0;
        scoreMultiplier = 1.0f;
        comboTimer = 0f;
        isComboTimerActive = false;
    }

    public void ResetGame()
    {
        Score = 0;
        Level = 1;
        scoreAccumulatorForLevel = 0;
        nextLevelThreshold = scorePerLevel;

        ResetCombo();

        UpdateScoreText(Score);
        UpdateLevelText(Level);
        UpdateComboText(Combo);
    }
}
