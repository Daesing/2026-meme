using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHudUI : MonoBehaviour
{
    [Header("HUD Root")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private bool hideOnGameOver = true;

    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string scoreFormat = "점수 : {0:N0}";

    [Header("HP")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image[] hpIcons;
    [SerializeField] private Color activeHpColor = Color.white;
    [SerializeField] private Color emptyHpColor = new Color(1f, 1f, 1f, 0.2f);

    private bool isSubscribed;
    private bool hasSyncedInitialState;
    private bool hasDisplayedScore;
    private int lastDisplayedScore = -1;

    private void OnEnable()
    {
        SubscribeEvents();
        TrySyncWithGameManager();
    }

    private void Start()
    {
        if (!hasSyncedInitialState)
        {
            TrySyncWithGameManager();
        }
    }

    private void Update()
    {
        if (!hasSyncedInitialState)
        {
            TrySyncWithGameManager();
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        hasSyncedInitialState = false;
    }

    private void SubscribeEvents()
    {
        if (isSubscribed)
        {
            return;
        }

        GameManager.OnHpChanged += HandleHpChanged;
        GameManager.OnScoreChanged += HandleScoreChanged;
        GameManager.OnGameOver += HandleGameOver;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed)
        {
            return;
        }

        GameManager.OnHpChanged -= HandleHpChanged;
        GameManager.OnScoreChanged -= HandleScoreChanged;
        GameManager.OnGameOver -= HandleGameOver;
        isSubscribed = false;
    }

    private void TrySyncWithGameManager()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        hasSyncedInitialState = true;
        hasDisplayedScore = false;

        SetHudVisible(!hideOnGameOver || !manager.IsGameOver);
        HandleHpChanged(manager.CurrentHp, manager.MaxHp);
        HandleScoreChanged(manager.Score);
    }

    private void HandleHpChanged(int currentHp, int maxHp)
    {
        int clampedMax = Mathf.Max(1, maxHp);
        int clampedCurrent = Mathf.Clamp(currentHp, 0, clampedMax);

        if (hpIcons != null)
        {
            for (int i = 0; i < hpIcons.Length; i++)
            {
                if (hpIcons[i] != null)
                {
                    hpIcons[i].color = i < clampedCurrent ? activeHpColor : emptyHpColor;
                }
            }
        }

        if (hpText != null)
        {
            hpText.text = $"기회 : {clampedCurrent} / {clampedMax}";
        }

        if (hideOnGameOver && GameManager.Instance != null && !GameManager.Instance.IsGameOver)
        {
            SetHudVisible(true);
        }
    }

    private void HandleScoreChanged(int currentScore)
    {
        int clampedScore = Mathf.Max(0, currentScore);
        if (hasDisplayedScore && clampedScore == lastDisplayedScore)
        {
            return;
        }

        hasDisplayedScore = true;
        lastDisplayedScore = clampedScore;

        if (scoreText == null)
        {
            return;
        }

        string format = string.IsNullOrEmpty(scoreFormat) ? "{0:N0}" : scoreFormat;
        scoreText.text = string.Format(format, clampedScore);
    }

    private void HandleGameOver()
    {
        if (hideOnGameOver)
        {
            SetHudVisible(false);
        }
    }

    private void SetHudVisible(bool visible)
    {
        GameObject targetRoot = hudRoot != null ? hudRoot : gameObject;
        if (targetRoot != null && targetRoot.activeSelf != visible)
        {
            targetRoot.SetActive(visible);
        }
    }
}
