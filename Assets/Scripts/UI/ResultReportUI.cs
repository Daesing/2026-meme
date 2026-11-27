using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultReportUI : MonoBehaviour
{
    private const string BestScorePrefsKey = "MemeLicense_BestScore";

    [Header("UI References")]
    [SerializeField] private GameObject reportPanel;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image killerMemeImage;
    [SerializeField] private TMP_Text killerTitleText;
    [SerializeField] private TMP_Text killerCommentText;
    [SerializeField] private Button retryButton;

    [Header("Grade Score Thresholds")]
    [SerializeField, Min(0f)] private float grade3Score = 1000f;
    [SerializeField, Min(0f)] private float grade2Score = 2500f;
    [SerializeField, Min(0f)] private float grade1Score = 4500f;
    [SerializeField, Min(0f)] private float specialGradeScore = 7000f;

    [Header("Grade Colors")]
    [SerializeField] private Color failGradeColor = new Color(1f, 0.3f, 0.45f, 1f);
    [SerializeField] private Color grade3Color = new Color(0.55f, 0.95f, 1f, 1f);
    [SerializeField] private Color grade2Color = new Color(0f, 0.96f, 1f, 1f);
    [SerializeField] private Color grade1Color = new Color(0.35f, 1f, 0.65f, 1f);
    [SerializeField] private Color specialGradeColor = new Color(1f, 0.9f, 0.2f, 1f);

    private bool isReportShown;
    private bool isRetryBound;

    public static float GetSavedBestScore()
    {
        return PlayerPrefs.GetFloat(BestScorePrefsKey, 0f);
    }

    private void Awake()
    {
        BindRetryButton();

        if (!isReportShown && reportPanel != null && reportPanel != gameObject)
        {
            reportPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        UnbindRetryButton();
    }

    public void ShowReport(float score, float bestScore, MemePatternData killerPattern)
    {
        isReportShown = true;
        BindRetryButton();

        float clampedScore = Mathf.Max(0f, score);
        float storedBest = GetSavedBestScore();
        float resolvedBest = Mathf.Max(storedBest, Mathf.Max(0f, bestScore), clampedScore);
        bool isNewRecord = clampedScore > storedBest && clampedScore > 0f;

        if (resolvedBest > storedBest || !PlayerPrefs.HasKey(BestScorePrefsKey))
        {
            PlayerPrefs.SetFloat(BestScorePrefsKey, resolvedBest);
            PlayerPrefs.Save();
        }

        UpdateGradeText(clampedScore);
        UpdateScoreText(clampedScore, resolvedBest, isNewRecord);
        UpdateKillerInfo(killerPattern);

        if (reportPanel != null)
        {
            reportPanel.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void HideReport()
    {
        isReportShown = false;

        if (reportPanel != null)
        {
            reportPanel.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateGradeText(float score)
    {
        if (gradeText == null)
        {
            return;
        }

        string gradeLabel = EvaluateGrade(score, out Color gradeColor);
        gradeText.text = $"취득 급수 : {gradeLabel}";
        gradeText.color = gradeColor;
    }

    private string EvaluateGrade(float score, out Color gradeColor)
    {
        if (score >= specialGradeScore)
        {
            gradeColor = specialGradeColor;
            return "특급";
        }

        if (score >= grade1Score)
        {
            gradeColor = grade1Color;
            return "1급";
        }

        if (score >= grade2Score)
        {
            gradeColor = grade2Color;
            return "2급";
        }

        if (score >= grade3Score)
        {
            gradeColor = grade3Color;
            return "3급";
        }

        gradeColor = failGradeColor;
        return "불합격";
    }

    private void UpdateScoreText(float score, float bestScore, bool isNewRecord)
    {
        if (scoreText == null)
        {
            return;
        }

        int displayScore = Mathf.FloorToInt(score);
        int displayBest = Mathf.FloorToInt(bestScore);
        string newRecordTag = isNewRecord ? "  <color=#FFE600>[NEW!]</color>" : string.Empty;

        scoreText.text = $"최종 점수 : {displayScore:N0}점\n최고 기록 : {displayBest:N0}점{newRecordTag}";
    }

    private void UpdateKillerInfo(MemePatternData killerPattern)
    {
        if (killerPattern == null)
        {
            if (killerMemeImage != null)
            {
                killerMemeImage.sprite = null;
                killerMemeImage.enabled = false;
            }

            if (killerTitleText != null)
            {
                killerTitleText.text = "정체불명의 밈";
            }

            if (killerCommentText != null)
            {
                killerCommentText.text = "알 수 없는 밈 공격으로 탈락했습니다.";
            }

            return;
        }

        Sprite killerSprite = ResolveKillerSprite(killerPattern);
        if (killerMemeImage != null)
        {
            killerMemeImage.sprite = killerSprite;
            killerMemeImage.preserveAspect = true;
            killerMemeImage.color = Color.white;
            killerMemeImage.enabled = killerSprite != null;
        }

        if (killerTitleText != null)
        {
            killerTitleText.text = !string.IsNullOrWhiteSpace(killerPattern.memeTitle)
                ? killerPattern.memeTitle
                : killerPattern.name;
        }

        if (killerCommentText != null)
        {
            killerCommentText.text = !string.IsNullOrWhiteSpace(killerPattern.failComment)
                ? killerPattern.failComment
                : "이 밈을 피하지 못하고 장렬히 탈락했습니다.";
        }
    }

    private static Sprite ResolveKillerSprite(MemePatternData killerPattern)
    {
        if (killerPattern == null)
        {
            return null;
        }

        if (killerPattern.overrideSprite != null)
        {
            return killerPattern.overrideSprite;
        }

        if (killerPattern.obstaclePrefab == null)
        {
            return null;
        }

        SpriteRenderer sr = killerPattern.obstaclePrefab.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = killerPattern.obstaclePrefab.GetComponentInChildren<SpriteRenderer>(true);
        }

        return sr != null ? sr.sprite : null;
    }

    private void BindRetryButton()
    {
        if (isRetryBound || retryButton == null)
        {
            return;
        }

        retryButton.onClick.AddListener(HandleRetryClicked);
        isRetryBound = true;
    }

    private void UnbindRetryButton()
    {
        if (!isRetryBound || retryButton == null)
        {
            return;
        }

        retryButton.onClick.RemoveListener(HandleRetryClicked);
        isRetryBound = false;
    }

    private void HandleRetryClicked()
    {
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }
}
