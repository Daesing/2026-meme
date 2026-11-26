using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MemeOrderMode
{
    ShuffleBag,
    Sequential,
    WeightedRandom
}

[Serializable]
public class MemeAttackData
{
    public MemeAttackObstacle obstaclePrefab;
    public Sprite overrideSprite;
    public Material overrideMaterial;
    public AudioClip attackSound;
    [Range(0f, 1f)] public float soundVolume = 1f;
    [Min(0.01f)] public float spawnWeight = 1f;
    public bool allowLeft = true;
    public bool allowRight = true;
    public bool allowCenter = true;
}

[RequireComponent(typeof(AudioSource))]
public class MemeAttackSpawner : MonoBehaviour
{
    [Header("Meme Attacks")]
    [SerializeField] private MemeOrderMode orderMode = MemeOrderMode.ShuffleBag;
    [SerializeField] private List<MemeAttackData> memeAttacks = new List<MemeAttackData>();

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource = null;

    [Header("Timing")]
    [SerializeField] private float initialDelay = 1.5f;
    [Tooltip("돌진 전 경고 깜빡임 지속 시간(초). 0 이하이면 프리팹(MemeAttackObstacle)의 warningDuration을 사용합니다.")]
    [SerializeField] private float warningDuration = 0.6f;
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

    private readonly List<int> shuffleBag = new List<int>();
    private readonly List<int> validIndicesBuffer = new List<int>();
    private readonly List<YahoSpawnType> allowedSpawnTypesBuffer = new List<YahoSpawnType>(3);

    private Coroutine spawnRoutine;
    private int sequentialCursor;
    private int lastSpawnedIndex = -1;

    public MemeOrderMode OrderMode
    {
        get => orderMode;
        set => orderMode = value;
    }

    public IReadOnlyList<MemeAttackData> MemeAttacks => memeAttacks;

    private void Awake()
    {
        EnsureAudioSource();
    }

    private void Start()
    {
        if (!HasValidAttackData())
        {
            Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemeAttackData가 없습니다. 상단 메뉴 'Tools/Setup Yaho Attack'을 실행해 주세요.");
            return;
        }

        spawnRoutine = StartCoroutine(AutoSpawnLoop());
    }

    public void ResetOrderState()
    {
        shuffleBag.Clear();
        sequentialCursor = 0;
        lastSpawnedIndex = -1;
    }

    public void SpawnNextAttack()
    {
        int nextIndex = SelectNextAttackIndex();
        if (nextIndex < 0)
        {
            Debug.LogWarning("[MemeAttackSpawner] 출력할 유효한 MemeAttackData가 없습니다.");
            return;
        }

        MemeAttackData data = memeAttacks[nextIndex];
        YahoSpawnType spawnType = PickSpawnType(data);
        StartSingleAttack(data, spawnType);
    }

    public void SpawnAttack(int index, YahoSpawnType? forcedSpawnType = null)
    {
        if (index < 0 || index >= memeAttacks.Count || memeAttacks[index] == null || memeAttacks[index].obstaclePrefab == null)
        {
            Debug.LogWarning($"[MemeAttackSpawner] 인덱스 {index}에 유효한 MemeAttackData(또는 obstaclePrefab)가 없습니다.");
            return;
        }

        lastSpawnedIndex = index;
        MemeAttackData data = memeAttacks[index];
        YahoSpawnType spawnType = forcedSpawnType ?? PickSpawnType(data);
        StartSingleAttack(data, spawnType);
    }

    private void StartSingleAttack(MemeAttackData data, YahoSpawnType spawnType)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(PlayMemeAttackSequence(data, spawnType));
    }

    private IEnumerator AutoSpawnLoop()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        do
        {
            int nextIndex = SelectNextAttackIndex();
            if (nextIndex < 0)
            {
                Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemeAttackData가 없어 스폰을 중단합니다.");
                break;
            }

            MemeAttackData data = memeAttacks[nextIndex];
            YahoSpawnType spawnType = PickSpawnType(data);
            yield return PlayMemeAttackSequence(data, spawnType);

            if (repeatSpawn && spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        while (repeatSpawn);

        spawnRoutine = null;
    }

    private IEnumerator PlayMemeAttackSequence(MemeAttackData data, YahoSpawnType spawnType)
    {
        EnsureAudioSource();

        Vector3 spawnPos = GetSpawnPosition(spawnType);
        MemeAttackObstacle instance = Instantiate(data.obstaclePrefab, spawnPos, Quaternion.identity);
        instance.ApplyVisualOverride(data.overrideSprite, data.overrideMaterial);

        AudioClip clip = data.attackSound;
        float volume = data.soundVolume;

        instance.Initialize(
            spawnType,
            skipWarning: false,
            onAttackBegin: () => PlayAttackSound(clip, volume),
            warningDurationOverride: warningDuration
        );

        while (instance != null && !instance.IsAttacking)
        {
            yield return null;
        }
    }

    private int SelectNextAttackIndex()
    {
        CollectValidIndices();
        if (validIndicesBuffer.Count == 0)
        {
            return -1;
        }

        switch (orderMode)
        {
            case MemeOrderMode.Sequential:
                return SelectSequentialIndex();

            case MemeOrderMode.WeightedRandom:
                return SelectWeightedRandomIndex();

            case MemeOrderMode.ShuffleBag:
            default:
                return SelectShuffleBagIndex();
        }
    }

    private int SelectShuffleBagIndex()
    {
        for (int i = shuffleBag.Count - 1; i >= 0; i--)
        {
            int idx = shuffleBag[i];
            if (!IsValidAttackIndex(idx))
            {
                shuffleBag.RemoveAt(i);
            }
        }

        if (shuffleBag.Count == 0)
        {
            RefillShuffleBag();
        }

        if (shuffleBag.Count == 0)
        {
            return -1;
        }

        int selectedIndex = shuffleBag[0];
        shuffleBag.RemoveAt(0);
        lastSpawnedIndex = selectedIndex;
        return selectedIndex;
    }

    private void RefillShuffleBag()
    {
        shuffleBag.Clear();
        for (int i = 0; i < validIndicesBuffer.Count; i++)
        {
            shuffleBag.Add(validIndicesBuffer[i]);
        }

        for (int i = shuffleBag.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = shuffleBag[i];
            shuffleBag[i] = shuffleBag[j];
            shuffleBag[j] = temp;
        }

        // 가방이 리필될 때 직전에 나온 장애물이 바로 연속으로 다시 나오지 않도록 방지
        if (shuffleBag.Count > 1 && lastSpawnedIndex >= 0 && shuffleBag[0] == lastSpawnedIndex)
        {
            int swapIndex = UnityEngine.Random.Range(1, shuffleBag.Count);
            int temp = shuffleBag[0];
            shuffleBag[0] = shuffleBag[swapIndex];
            shuffleBag[swapIndex] = temp;
        }
    }

    private int SelectSequentialIndex()
    {
        int count = validIndicesBuffer.Count;
        int selectedIndex = validIndicesBuffer[sequentialCursor % count];
        sequentialCursor = (sequentialCursor + 1) % count;
        lastSpawnedIndex = selectedIndex;
        return selectedIndex;
    }

    private int SelectWeightedRandomIndex()
    {
        float totalWeight = 0f;
        for (int i = 0; i < validIndicesBuffer.Count; i++)
        {
            int idx = validIndicesBuffer[i];
            totalWeight += Mathf.Max(0f, memeAttacks[idx].spawnWeight);
        }

        int selectedIndex;
        if (totalWeight <= 0f)
        {
            selectedIndex = validIndicesBuffer[UnityEngine.Random.Range(0, validIndicesBuffer.Count)];
        }
        else
        {
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            selectedIndex = validIndicesBuffer[validIndicesBuffer.Count - 1];

            for (int i = 0; i < validIndicesBuffer.Count; i++)
            {
                int idx = validIndicesBuffer[i];
                cumulative += Mathf.Max(0f, memeAttacks[idx].spawnWeight);
                if (roll <= cumulative)
                {
                    selectedIndex = idx;
                    break;
                }
            }
        }

        lastSpawnedIndex = selectedIndex;
        return selectedIndex;
    }

    private YahoSpawnType PickSpawnType(MemeAttackData data)
    {
        allowedSpawnTypesBuffer.Clear();
        if (data.allowLeft) allowedSpawnTypesBuffer.Add(YahoSpawnType.Left);
        if (data.allowRight) allowedSpawnTypesBuffer.Add(YahoSpawnType.Right);
        if (data.allowCenter) allowedSpawnTypesBuffer.Add(YahoSpawnType.Center);

        if (allowedSpawnTypesBuffer.Count == 0)
        {
            return (YahoSpawnType)UnityEngine.Random.Range(0, 3);
        }

        int pick = UnityEngine.Random.Range(0, allowedSpawnTypesBuffer.Count);
        return allowedSpawnTypesBuffer[pick];
    }

    private void CollectValidIndices()
    {
        validIndicesBuffer.Clear();
        for (int i = 0; i < memeAttacks.Count; i++)
        {
            if (IsValidAttackIndex(i))
            {
                validIndicesBuffer.Add(i);
            }
        }
    }

    private bool HasValidAttackData()
    {
        for (int i = 0; i < memeAttacks.Count; i++)
        {
            if (IsValidAttackIndex(i))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidAttackIndex(int index)
    {
        return index >= 0
            && index < memeAttacks.Count
            && memeAttacks[index] != null
            && memeAttacks[index].obstaclePrefab != null;
    }

    private void PlayAttackSound(AudioClip clip, float volume)
    {
        if (this == null || clip == null)
        {
            return;
        }

        EnsureAudioSource();

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
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
                return new Vector3(leftSpawnX, UnityEngine.Random.Range(horizontalMinY, horizontalMaxY), 0f);

            case YahoSpawnType.Right:
                return new Vector3(rightSpawnX, UnityEngine.Random.Range(horizontalMinY, horizontalMaxY), 0f);

            case YahoSpawnType.Center:
                float randomX = centerSpawnPosition.x + UnityEngine.Random.Range(-centerRandomOffsetX, centerRandomOffsetX);
                return new Vector3(randomX, centerSpawnPosition.y, 0f);

            default:
                return Vector3.zero;
        }
    }
}
