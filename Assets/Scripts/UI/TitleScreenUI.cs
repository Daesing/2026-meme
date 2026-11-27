using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TitleScreenUI : MonoBehaviour
{
    private const string BestScorePrefsKey = "MemeLicense_BestScore";

    [Header("UI References")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private Button startExamButton;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private string bestScoreFormat = "역대 최고 기록 : {0:N0}점";
    [SerializeField] private TMP_Text examCodeText;

    [Header("Input Options")]
    [SerializeField] private bool allowKeyboardStart = true;

    private bool isStartButtonBound;
    private bool isTitleVisible;
    private bool hasTriggeredStart;
    private GameManager subscribedGameManager;

    private void Awake()
    {
        BindStartButton();
    }

    private void Start()
    {
        BindStartButton();

        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            SubscribeExamStarted(manager);

            if (manager.IsGameStarted || !manager.ShouldWaitForTitle)
            {
                hasTriggeredStart = true;
                SetTitleVisible(false);
                return;
            }
        }

        Time.timeScale = 1f;
        UpdateBestScoreDisplay();
        EnsureDefaultExamCodeText();
        SetTitleVisible(true);
    }

    private void Update()
    {
        if (!allowKeyboardStart || !isTitleVisible || hasTriggeredStart)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameStarted)
        {
            SetTitleVisible(false);
            return;
        }

        if (IsKeyboardStartPressed())
        {
            HandleStartExamClicked();
        }
    }

    private void OnDestroy()
    {
        UnbindStartButton();
        UnsubscribeExamStarted();
    }

    public void HandleStartExamClicked()
    {
        if (hasTriggeredStart)
        {
            return;
        }

        hasTriggeredStart = true;
        Time.timeScale = 1f;
        SetTitleVisible(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartExam();
        }
    }

    private void HandleExamStarted()
    {
        hasTriggeredStart = true;
        SetTitleVisible(false);
    }

    private void UpdateBestScoreDisplay()
    {
        if (bestScoreText == null)
        {
            return;
        }

        float savedBestScore = Mathf.Max(0f, PlayerPrefs.GetFloat(BestScorePrefsKey, 0f));
        int displayBestScore = Mathf.FloorToInt(savedBestScore);
        string format = string.IsNullOrEmpty(bestScoreFormat) ? "{0:N0}" : bestScoreFormat;
        bestScoreText.text = string.Format(format, displayBestScore);
    }

    private void EnsureDefaultExamCodeText()
    {
        if (examCodeText != null && string.IsNullOrWhiteSpace(examCodeText.text))
        {
            examCodeText.text = "수험번호 : 2026-MEME-0001";
        }
    }

    private void SetTitleVisible(bool visible)
    {
        isTitleVisible = visible;
        GameObject targetPanel = titlePanel != null ? titlePanel : gameObject;
        if (targetPanel != null && targetPanel.activeSelf != visible)
        {
            targetPanel.SetActive(visible);
        }
    }

    private void BindStartButton()
    {
        if (isStartButtonBound || startExamButton == null)
        {
            return;
        }

        startExamButton.onClick.AddListener(HandleStartExamClicked);
        isStartButtonBound = true;
    }

    private void UnbindStartButton()
    {
        if (!isStartButtonBound || startExamButton == null)
        {
            return;
        }

        startExamButton.onClick.RemoveListener(HandleStartExamClicked);
        isStartButtonBound = false;
    }

    private void SubscribeExamStarted(GameManager manager)
    {
        if (manager == null || subscribedGameManager == manager)
        {
            return;
        }

        UnsubscribeExamStarted();
        manager.OnExamStarted += HandleExamStarted;
        subscribedGameManager = manager;
    }

    private void UnsubscribeExamStarted()
    {
        if (subscribedGameManager != null)
        {
            subscribedGameManager.OnExamStarted -= HandleExamStarted;
            subscribedGameManager = null;
        }
    }

    private static bool IsKeyboardStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame;
        }

        return false;
#else
        return Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space);
#endif
    }
}
