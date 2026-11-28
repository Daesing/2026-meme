using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static event Action<int, int> OnHpChanged;
    public static event Action<int> OnScoreChanged;
    public static event Action OnGameOver;
    public static event Action<MemePatternData, float, int> OnGameOverResultReady;
    public event Action OnExamStarted;

    [Header("Title Screen")]
    [SerializeField] private bool useTitleScreen = true;

    [Header("Player Status")]
    [SerializeField, Min(1)] private int maxHp = 3;
    [SerializeField] private int currentHp;
    [SerializeField] private float survivalTime;
    [SerializeField] private bool isGameOver;

    [Header("Score")]
    [SerializeField, Min(1f)] private float scorePerSecond = 100f;
    [SerializeField] private int score;

    [Header("References (Optional)")]
    [SerializeField] private PlayerBoxController playerController;
    [SerializeField] private MemeAttackSpawner spawner;
    [SerializeField] private HitFeelController hitFeelController;
    [SerializeField] private ResultReportUI resultReportUI;

    [Header("Hit Feel & Game Over Sequence")]
    [SerializeField, Min(0f)] private float hitstopDuration = 0.08f;
    [SerializeField, Min(0f)] private float shakeDuration = 0.25f;
    [SerializeField, Min(0f)] private float shakeMagnitude = 0.15f;
    [SerializeField, Min(0f)] private float gameOverResultDelay = 0.4f;

    [Header("Invincibility")]
    [SerializeField, Min(0f)] private float invincibleDuration = 1.7f;

    private static bool skipTitleOnNextLoad = false;

    private MemePatternData lastKillerPattern;
    private Coroutine gameOverRoutine;
    private Coroutine invincibilityRoutine;
    private bool isInvincible;
    private bool isGameStarted;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public float SurvivalTime => survivalTime;
    public int Score => score;
    public bool IsGameStarted => isGameStarted;
    public bool IsGameOver => isGameOver;
    public bool IsInvincible => isInvincible;
    public bool ShouldWaitForTitle => useTitleScreen && !skipTitleOnNextLoad;
    public MemePatternData LastKillerPattern => lastKillerPattern;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        skipTitleOnNextLoad = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        maxHp = Mathf.Max(1, maxHp);
        currentHp = maxHp;
        survivalTime = 0f;
        score = 0;
        isGameStarted = false;
        isGameOver = false;
        lastKillerPattern = null;
        ResetInvincibility();
    }

    private void OnEnable()
    {
        MemeAttackObstacle.OnPlayerHit += HandleMemeAttackHit;
    }

    private void OnDisable()
    {
        MemeAttackObstacle.OnPlayerHit -= HandleMemeAttackHit;
        ResetInvincibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (useTitleScreen && !skipTitleOnNextLoad)
        {
            isGameStarted = false;
            DisablePlayerInput();
            StopSpawners();
            OnHpChanged?.Invoke(currentHp, maxHp);
            OnScoreChanged?.Invoke(score);
        }
        else
        {
            skipTitleOnNextLoad = false;
            StartExam();
        }
    }

    private void Update()
    {
        if (!isGameStarted || isGameOver)
        {
            return;
        }

        survivalTime += Time.deltaTime;
        int updatedScore = CalculateScore(survivalTime);
        if (updatedScore != score)
        {
            score = updatedScore;
            OnScoreChanged?.Invoke(score);
        }
    }

    public void StartExam()
    {
        if (Instance != this || isGameStarted || isGameOver)
        {
            return;
        }

        isGameStarted = true;
        Time.timeScale = 1f;

        EnablePlayerInput();
        StartSpawners();

        OnHpChanged?.Invoke(currentHp, maxHp);
        OnScoreChanged?.Invoke(score);
        OnExamStarted?.Invoke();
    }

    public void RestartGame()
    {
        ResetInvincibility();
        PlayerBoxController player = ResolvePlayerController();
        if (player != null)
        {
            player.ResetHitFlash();
        }

        isGameOver = false;
        isGameStarted = false;
        currentHp = maxHp;
        survivalTime = 0f;
        score = 0;
        OnHpChanged?.Invoke(currentHp, maxHp);
        OnScoreChanged?.Invoke(score);

        skipTitleOnNextLoad = true;
        Time.timeScale = 1f;
        AudioManager.Instance?.ResetBgmOnRestart();
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    public void TriggerGameOver(MemePatternData killerPattern)
    {
        if (Instance != this || isGameOver)
        {
            return;
        }

        isGameOver = true;
        ResetInvincibility();
        lastKillerPattern = killerPattern;

        int finalScore = CalculateScore(survivalTime);
        if (finalScore != score)
        {
            score = finalScore;
            OnScoreChanged?.Invoke(score);
        }

        PlayerBoxController player = ResolvePlayerController();
        if (player != null)
        {
            player.ResetHitFlash();
        }

        DisablePlayerInput();
        StopSpawners();

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning($"[GameManager] 오브젝트({name})가 비활성 상태여서 GameOverSequenceRoutine 코루틴을 시작할 수 없습니다.");
            return;
        }

        if (gameOverRoutine != null)
        {
            StopCoroutine(gameOverRoutine);
        }

        gameOverRoutine = StartCoroutine(GameOverSequenceRoutine());
    }

    private void HandleMemeAttackHit(MemeAttackObstacle obstacle)
    {
        if (!isGameStarted || isGameOver || isInvincible)
        {
            return;
        }

        MemePatternData killerPattern = obstacle != null ? obstacle.SourcePattern : null;
        string obstacleName = obstacle != null ? obstacle.name : "Null";
        string patternName = killerPattern != null ? killerPattern.name : "Null";
        Debug.Log($"[GameManager] 피격 메서드 진입 (HandleMemeAttackHit): 장애물={obstacleName}, 패턴={patternName}, 현재HP={currentHp}/{maxHp}, isGameOver={isGameOver}");
        ApplyPlayerDamage(1, killerPattern);
    }

    private void ApplyPlayerDamage(int amount, MemePatternData sourcePattern)
    {
        if (Instance != this || !isGameStarted || isGameOver || isInvincible)
        {
            Debug.LogWarning($"[GameManager] ApplyPlayerDamage 무시됨: isInstance={Instance == this}, isGameStarted={isGameStarted}, isGameOver={isGameOver}, isInvincible={isInvincible}");
            return;
        }

        currentHp = Mathf.Max(0, currentHp - amount);
        OnHpChanged?.Invoke(currentHp, maxHp);
        AudioManager.Instance?.PlayHitSFX();
        Debug.Log($"[GameManager] 데미지 적용 완료: 남은HP={currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            TriggerGameOver(sourcePattern);
            return;
        }

        StartInvincibility(invincibleDuration);

        PlayerBoxController player = ResolvePlayerController();
        if (player != null)
        {
            player.PlayHitFlash(invincibleDuration);
        }

        HitFeelController feelController = ResolveHitFeelController();
        if (feelController != null)
        {
            Debug.Log($"[GameManager] 일반 피격 HitFeelController.PlayHitFeedback 호출: 대상={feelController.name}");
            feelController.PlayHitFeedback(hitstopDuration, shakeDuration, shakeMagnitude);
        }
        else
        {
            Debug.LogWarning("[GameManager] HitFeelController를 찾거나 생성하지 못했습니다.");
        }
    }

    private void StartInvincibility(float duration)
    {
        ResetInvincibility();

        if (duration <= 0f || !isActiveAndEnabled)
        {
            return;
        }

        isInvincible = true;
        invincibilityRoutine = StartCoroutine(InvincibilityRoutine(duration));
    }

    private IEnumerator InvincibilityRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        isInvincible = false;
        invincibilityRoutine = null;
    }

    private void ResetInvincibility()
    {
        if (invincibilityRoutine != null)
        {
            StopCoroutine(invincibilityRoutine);
            invincibilityRoutine = null;
        }

        isInvincible = false;
    }

    private IEnumerator GameOverSequenceRoutine()
    {
        HitFeelController feelController = ResolveHitFeelController();
        if (feelController != null)
        {
            Debug.Log($"[GameManager] 게임오버 HitFeelController.PlayHitFeedback 호출: 대상={feelController.name}");
            yield return feelController.PlayHitFeedback(hitstopDuration, shakeDuration, shakeMagnitude);
        }
        else
        {
            Debug.LogWarning("[GameManager] 게임오버 시 HitFeelController를 찾거나 생성하지 못했습니다.");
        }

        if (gameOverResultDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(gameOverResultDelay);
        }

        Time.timeScale = 0f;

        string killerName = lastKillerPattern != null && !string.IsNullOrWhiteSpace(lastKillerPattern.memeTitle)
            ? lastKillerPattern.memeTitle
            : (lastKillerPattern != null ? lastKillerPattern.name : "Unknown");

        Debug.Log($"[GameManager] 게임 오버! 탈락 주범: {killerName}, 생존 시간: {survivalTime:F2}초, 점수: {score}");

        ResultReportUI reportUI = ResolveResultReportUI();
        if (reportUI != null)
        {
            float bestScore = Mathf.Max(ResultReportUI.GetSavedBestScore(), score);
            reportUI.ShowReport(score, bestScore, lastKillerPattern);
        }
        else
        {
            Debug.LogWarning("[GameManager] ResultReportUI를 찾지 못했습니다. Tools > Meme Game > Create Result Report UI로 생성해 주세요.");
        }

        OnGameOver?.Invoke();
        OnGameOverResultReady?.Invoke(lastKillerPattern, survivalTime, score);

        gameOverRoutine = null;
    }

    private void EnablePlayerInput()
    {
        PlayerBoxController player = ResolvePlayerController();
        if (player != null)
        {
            player.SetInputEnabled(true);
        }
    }

    private void DisablePlayerInput()
    {
        PlayerBoxController player = ResolvePlayerController();
        if (player != null)
        {
            player.SetInputEnabled(false);
        }
    }

    private PlayerBoxController ResolvePlayerController()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerBoxController>();
        }

        return playerController;
    }

    private void StartSpawners()
    {
        if (spawner != null)
        {
            spawner.StartSpawning();
            return;
        }

        MemeAttackSpawner[] spawners = FindObjectsByType<MemeAttackSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                spawners[i].StartSpawning();
            }
        }
    }

    private void StopSpawners()
    {
        if (spawner != null)
        {
            spawner.StopSpawning();
            return;
        }

        MemeAttackSpawner[] spawners = FindObjectsByType<MemeAttackSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                spawners[i].StopSpawning();
            }
        }
    }

    private HitFeelController ResolveHitFeelController()
    {
        if (hitFeelController != null && hitFeelController.isActiveAndEnabled)
        {
            return hitFeelController;
        }

        hitFeelController = HitFeelController.Instance != null && HitFeelController.Instance.isActiveAndEnabled
            ? HitFeelController.Instance
            : FindFirstObjectByType<HitFeelController>();

        if (hitFeelController == null || !hitFeelController.isActiveAndEnabled)
        {
            GameObject host = Camera.main != null ? Camera.main.gameObject : gameObject;
            hitFeelController = host.GetComponent<HitFeelController>();
            if (hitFeelController == null)
            {
                hitFeelController = host.AddComponent<HitFeelController>();
            }
            else if (!hitFeelController.enabled)
            {
                hitFeelController.enabled = true;
            }
        }

        return hitFeelController;
    }

    private ResultReportUI ResolveResultReportUI()
    {
        if (resultReportUI != null)
        {
            return resultReportUI;
        }

        resultReportUI = FindFirstObjectByType<ResultReportUI>(FindObjectsInactive.Include);
        return resultReportUI;
    }

    private int CalculateScore(float elapsedSeconds)
    {
        return Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds * scorePerSecond));
    }
}
