using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public static event Action<int, int> OnHpChanged;
    public static event Action OnGameOver;

    [Header("Player Status")]
    [SerializeField, Min(1)] private int maxHp = 3;
    [SerializeField] private int currentHp;
    [SerializeField] private float survivalTime;
    [SerializeField] private bool isGameOver;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public float SurvivalTime => survivalTime;
    public bool IsGameOver => isGameOver;

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
        isGameOver = false;
    }

    private void OnEnable()
    {
        SubtitleObstacle.OnPlayerHit += HandleSubtitleHit;
        MemeAttackObstacle.OnPlayerHit += HandleYahoHit;
    }

    private void OnDisable()
    {
        SubtitleObstacle.OnPlayerHit -= HandleSubtitleHit;
        MemeAttackObstacle.OnPlayerHit -= HandleYahoHit;
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
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    private void Update()
    {
        if (isGameOver)
        {
            return;
        }

        survivalTime += Time.deltaTime;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void HandleSubtitleHit(SubtitleObstacle obstacle)
    {
        ApplyPlayerDamage(1);
    }

    private void HandleYahoHit(MemeAttackObstacle obstacle)
    {
        ApplyPlayerDamage(1);
    }

    private void ApplyPlayerDamage(int amount)
    {
        if (Instance != this || isGameOver)
        {
            return;
        }

        currentHp = Mathf.Max(0, currentHp - amount);
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            isGameOver = true;
            Time.timeScale = 0f;
            Debug.Log($"[GameManager] 게임 오버! 생존 시간: {survivalTime:F2}초");
            OnGameOver?.Invoke();
        }
    }
}
