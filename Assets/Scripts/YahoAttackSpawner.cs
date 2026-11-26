using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class YahoAttackSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private YahoAttackObstacle yahoObstaclePrefab = null;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource = null;
    [SerializeField] private AudioClip yahoSound = null;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Timing")]
    [SerializeField] private float initialDelay = 1.5f;
    [Tooltip("'거제~' 재생 동안 대기한 뒤 '야호!' 타이밍에 이미지를 출력합니다.")]
    [SerializeField] private float geojeWaitDuration = 1.0f;
    [Tooltip("false면 1번만 출력합니다. 반복 출력이 필요할 때만 켜세요.")]
    [SerializeField] private bool repeatSpawn = false;
    [SerializeField] private float spawnInterval = 4.5f;

    [Header("Spawn Positions (Left / Right)")]
    [SerializeField] private float leftSpawnX = -7.5f;
    [SerializeField] private float rightSpawnX = 7.5f;
    [SerializeField] private float horizontalMinY = -2.6f;
    [SerializeField] private float horizontalMaxY = 1.6f;

    [Header("Spawn Position (Center)")]
    [SerializeField] private Vector2 centerSpawnPosition = new Vector2(0f, 2.2f);
    [SerializeField] private float centerRandomOffsetX = 1.2f;

    private Coroutine spawnRoutine;

    private void Awake()
    {
        EnsureAudioSource();
    }

    private void Start()
    {
        if (yahoObstaclePrefab == null)
        {
            Debug.LogWarning("[YahoAttackSpawner] yahoObstaclePrefab이 비어 있습니다. 상단 메뉴 'Tools/Setup Yaho Attack'을 실행해 주세요.");
            return;
        }

        spawnRoutine = StartCoroutine(AutoSpawnLoop());
    }

    private IEnumerator AutoSpawnLoop()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        do
        {
            YahoSpawnType randomType = (YahoSpawnType)Random.Range(0, 3);
            yield return PlayYahoAttackSequence(randomType);

            if (repeatSpawn && spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        while (repeatSpawn);

        spawnRoutine = null;
    }

    public void SpawnRandomAttack()
    {
        YahoSpawnType randomType = (YahoSpawnType)Random.Range(0, 3);
        SpawnAttack(randomType);
    }

    public void SpawnAttack(YahoSpawnType spawnType)
    {
        if (yahoObstaclePrefab == null)
        {
            Debug.LogWarning("[YahoAttackSpawner] yahoObstaclePrefab이 비어 있습니다. 상단 메뉴 'Tools/Setup Yaho Attack'을 실행해 주세요.");
            return;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(PlayYahoAttackSequence(spawnType));
    }

    private IEnumerator PlayYahoAttackSequence(YahoSpawnType spawnType)
    {
        EnsureAudioSource();

        // 1단계: "거제~" 효과음 재생 시작 후 '야호!' 직전까지 대기
        if (audioSource != null && yahoSound != null)
        {
            audioSource.PlayOneShot(yahoSound, soundVolume);
        }

        if (geojeWaitDuration > 0f)
        {
            yield return new WaitForSeconds(geojeWaitDuration);
        }

        // 2단계: "야호!" 타이밍에 맞춰 Yaho 이미지 1회 출력 및 즉시 돌진
        Vector3 spawnPos = GetSpawnPosition(spawnType);
        YahoAttackObstacle instance = Instantiate(yahoObstaclePrefab, spawnPos, Quaternion.identity);
        instance.Initialize(spawnType, skipWarning: true);
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private Vector3 GetSpawnPosition(YahoSpawnType spawnType)
    {
        switch (spawnType)
        {
            case YahoSpawnType.Left:
                return new Vector3(leftSpawnX, Random.Range(horizontalMinY, horizontalMaxY), 0f);

            case YahoSpawnType.Right:
                return new Vector3(rightSpawnX, Random.Range(horizontalMinY, horizontalMaxY), 0f);

            case YahoSpawnType.Center:
                float randomX = centerSpawnPosition.x + Random.Range(-centerRandomOffsetX, centerRandomOffsetX);
                return new Vector3(randomX, centerSpawnPosition.y, 0f);

            default:
                return Vector3.zero;
        }
    }
}
