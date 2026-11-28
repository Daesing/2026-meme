using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum MemeOrderMode
{
    ShuffleBag,
    Sequential,
    WeightedRandom
}

public class MemeAttackSpawner : MonoBehaviour
{
    [Header("Meme Patterns")]
    [SerializeField] private MemeOrderMode orderMode = MemeOrderMode.ShuffleBag;
    [SerializeField] private List<MemePatternData> patterns = new List<MemePatternData>();

    [Header("Meme Waves (Course Mode)")]
    [Tooltip("1개 이상의 유효한 MemeWaveData가 등록되면 웨이브(코스요리) 모드로 동작합니다. 비어 있으면 기존 단품 patterns 모드로 폴백합니다.")]
    [SerializeField] private List<MemeWaveData> waves = new List<MemeWaveData>();

    [Header("Score Difficulty Scaling")]
    [Tooltip("점수 상승에 따라 웨이브 휴식 시간 단축 및 이동 속도 배율 상승을 적용합니다.")]
    [SerializeField] private bool enableScoreDifficultyScaling = true;
    [Tooltip("최대 난이도에 도달하는 기준 점수 (0 이하이면 난이도 상승 미적용)")]
    [SerializeField, Min(0)] private int maxDifficultyScore = 5000;
    [Tooltip("점수 상승 시 postWaveDelay가 기본값 대비 줄어드는 최소 비율 (예: 0.4 = 40%까지 단축)")]
    [SerializeField, Range(0.1f, 1f)] private float minPostWaveDelayScale = 0.4f;
    [Tooltip("웨이브 간 휴식 시간(postWaveDelay)의 최소 하한선(초)")]
    [SerializeField, Min(0f)] private float minPostWaveDelay = 0.45f;
    [Tooltip("점수 상승 시 장애물 이동 속도 최대 배율 상한선")]
    [SerializeField, Min(1f)] private float maxDifficultySpeedMultiplier = 1.6f;

    [Header("Timing")]
    [SerializeField] private float initialDelay = 1.5f;
    [Tooltip("장애물 생성 전 경고 표시 지속 시간(초). 0 이하이면 프리팹(MemeAttackObstacle)의 warningDuration을 사용합니다.")]
    [SerializeField] private float warningDuration = 0.6f;
    [Tooltip("false면 1번만 출력합니다. 반복 출력이 필요할 때만 켜세요.")]
    [SerializeField] private bool repeatSpawn = false;
    [SerializeField] private float spawnInterval = 4.5f;

    [Header("Warning Indicator")]
    [Tooltip("비워 두면 기본 '!' 경고 텍스트를 런타임에 생성해 깜빡입니다.")]
    [SerializeField] private GameObject warningPrefab = null;
    [Tooltip("궤적 경고(Trajectory / Both)에 사용할 반투명 띠 프리팹")]
    [SerializeField] private GameObject trajectoryWarningPrefab = null;
    [Tooltip("Left 스폰 시 경고(!)가 표시될 X 월드 좌표")]
    [SerializeField] private float leftWarningX = -3.5f;
    [Tooltip("Right 스폰 시 경고(!)가 표시될 X 월드 좌표")]
    [SerializeField] private float rightWarningX = 3.5f;
    [Tooltip("경고(!)의 Y 좌표 오프셋 (스폰 Y + warningOffsetY)")]
    [SerializeField] private float warningOffsetY = 0.8f;
    [SerializeField, Min(1)] private int warningBlinkCount = 3;
    [SerializeField] private Color warningTextColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField, Range(0f, 1f)] private float warningTextMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float warningTextMaxAlpha = 1f;
    [SerializeField] private float warningFontSize = 26f;
    [SerializeField] private int warningSortingOrder = 16;

    [Header("Spawn Positions (Left / Right)")]
    [SerializeField] private float leftSpawnX = -7.5f;
    [SerializeField] private float rightSpawnX = 7.5f;
    [SerializeField] private float horizontalMinY = -2.6f;
    [SerializeField] private float horizontalMaxY = 1.6f;

    [Header("Spawn Position (Center)")]
    [SerializeField] private Vector2 centerSpawnPosition = new Vector2(0f, 2.2f);
    [SerializeField, Min(0f)] private float centerRandomOffsetX = 1.2f;

    private const string DefaultTrajectoryWarningPrefabPath = "Assets/Prefabs/WarningTrajectory.prefab";
    private const float TrajectoryLength = 25f;
    private const float TrajectoryDimAlpha = 0.05f;
    private const float ExclamationDimAlpha = 0.1f;
    private const int DefaultTrajectorySortingOrder = 15;
    private const float BurstRandomOffsetRange = 0.45f;

    private struct SpawnShot
    {
        public MemeSpawnType SpawnType;
        public Vector3 SpawnPos;
        public Vector3 WarningPos;
        public Vector2 MoveDirection;
        public bool UseCustomDirection;
        public bool ShowExclamation;
    }

    private readonly List<int> shuffleBag = new List<int>();
    private readonly List<int> validIndicesBuffer = new List<int>();
    private readonly List<int> waveShuffleBag = new List<int>();
    private readonly List<int> eligibleWaveIndicesBuffer = new List<int>();
    private readonly HashSet<int> knownUnlockedWaveIndices = new HashSet<int>();
    private readonly List<Coroutine> activeStepCoroutines = new List<Coroutine>();
    private readonly List<MemeSpawnType> allowedSpawnTypesBuffer = new List<MemeSpawnType>(3);
    private readonly List<SpawnShot> shotPlanBuffer = new List<SpawnShot>();
    private readonly List<GameObject> activeWarningObjects = new List<GameObject>();
    private readonly List<GameObject> activeTrajectoryWarningObjects = new List<GameObject>();

    private Coroutine spawnRoutine;
    private static Sprite fallbackSquareSprite;
    private int sequentialCursor;
    private int lastSpawnedIndex = -1;
    private int lastSpawnedWaveIndex = -1;
    private float lastWarningSfxTime = -999f;

#if UNITY_EDITOR
    private void Reset()
    {
        AutoAssignTrajectoryWarningPrefab();
    }

    private void OnValidate()
    {
        AutoAssignTrajectoryWarningPrefab();
    }

    private void AutoAssignTrajectoryWarningPrefab()
    {
        if (trajectoryWarningPrefab == null)
        {
            trajectoryWarningPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTrajectoryWarningPrefabPath);
        }
    }
#endif

    public MemeOrderMode OrderMode
    {
        get => orderMode;
        set => orderMode = value;
    }

    public IReadOnlyList<MemePatternData> Patterns => patterns;
    public IReadOnlyList<MemeWaveData> Waves => waves;
    public bool IsWaveModeActive => HasValidWave();
    public float CurrentDifficultyProgress => GetDifficultyProgress();
    public float CurrentDifficultySpeedMultiplier => GetDifficultySpeedMultiplier();

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.IsGameStarted)
            {
                StartSpawning();
            }

            return;
        }

        // GameManager 없이 스포너 단독 테스트 시 자동 스폰 fallback
        StartSpawning();
    }

    public void StartSpawning()
    {
        if (!isActiveAndEnabled || spawnRoutine != null)
        {
            return;
        }

        if (!HasValidWave() && !HasValidPattern())
        {
            Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemeWaveData 또는 MemePatternData가 없습니다. 프로젝트 창에서 MemeGame/Meme Wave Data 또는 MemeGame/Meme Pattern 에셋을 생성해 등록해 주세요.");
            return;
        }

        spawnRoutine = StartCoroutine(AutoSpawnLoop());
    }

    public void ResetOrderState()
    {
        shuffleBag.Clear();
        waveShuffleBag.Clear();
        knownUnlockedWaveIndices.Clear();
        sequentialCursor = 0;
        lastSpawnedIndex = -1;
        lastSpawnedWaveIndex = -1;
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        StopActiveStepCoroutines();
        ClearActiveWarning();
    }

    public void SpawnNextAttack()
    {
        int nextIndex = SelectNextPatternIndex();
        if (nextIndex < 0)
        {
            Debug.LogWarning("[MemeAttackSpawner] 출력할 유효한 MemePatternData가 없습니다.");
            return;
        }

        MemePatternData pattern = patterns[nextIndex];
        MemeSpawnType spawnType = PickSpawnType(pattern);
        StartSingleAttack(pattern, spawnType);
    }

    public void SpawnAttack(int index, MemeSpawnType? forcedSpawnType = null)
    {
        if (!IsValidPatternIndex(index))
        {
            Debug.LogWarning($"[MemeAttackSpawner] 인덱스 {index}에 유효한 MemePatternData(또는 obstaclePrefab)가 없습니다.");
            return;
        }

        lastSpawnedIndex = index;
        MemePatternData pattern = patterns[index];
        MemeSpawnType spawnType = forcedSpawnType ?? PickSpawnType(pattern);
        StartSingleAttack(pattern, spawnType);
    }

    private void OnDisable()
    {
        StopActiveStepCoroutines();
        ClearActiveWarning();
    }

    private void OnDestroy()
    {
        StopActiveStepCoroutines();
        ClearActiveWarning();
    }

    private void StartSingleAttack(MemePatternData pattern, MemeSpawnType spawnType)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        StopActiveStepCoroutines();
        ClearActiveWarning();
        spawnRoutine = StartCoroutine(PlayMemeAttackSequence(
            pattern,
            spawnType,
            overrideSide: false,
            overrideY: null,
            speedMultiplier: GetDifficultySpeedMultiplier(),
            clearAllWarningsOnStart: true));
    }

    private IEnumerator AutoSpawnLoop()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        do
        {
            if (HasValidWave())
            {
                MemeWaveData wave = SelectNextWave();
                if (wave == null)
                {
                    Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemeWaveData가 없어 스폰을 중단합니다.");
                    break;
                }

                yield return PlayWaveRoutine(wave);

                if (repeatSpawn)
                {
                    float postDelay = GetScaledPostWaveDelay(wave);
                    if (postDelay > 0f)
                    {
                        yield return new WaitForSeconds(postDelay);
                    }
                }
                continue;
            }

            int nextIndex = SelectNextPatternIndex();
            if (nextIndex < 0)
            {
                Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemePatternData가 없어 스폰을 중단합니다.");
                break;
            }

            MemePatternData pattern = patterns[nextIndex];
            MemeSpawnType spawnType = PickSpawnType(pattern);
            yield return PlayMemeAttackSequence(
                pattern,
                spawnType,
                overrideSide: false,
                overrideY: null,
                speedMultiplier: GetDifficultySpeedMultiplier(),
                clearAllWarningsOnStart: true);

            if (repeatSpawn && spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        while (repeatSpawn);

        spawnRoutine = null;
    }

    private IEnumerator PlayWaveRoutine(MemeWaveData wave)
    {
        if (wave == null || wave.steps == null || wave.steps.Count == 0)
        {
            yield break;
        }

        List<MemeWaveData.WaveStep> sortedSteps = new List<MemeWaveData.WaveStep>(wave.steps.Count);
        for (int i = 0; i < wave.steps.Count; i++)
        {
            MemeWaveData.WaveStep step = wave.steps[i];
            if (step != null && step.IsValid)
            {
                sortedSteps.Add(step);
            }
        }

        if (sortedSteps.Count == 0)
        {
            yield break;
        }

        if (wave.WaveBgm != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBgm(wave.WaveBgm);
        }

        sortedSteps.Sort((a, b) => Mathf.Max(0f, a.delayFromWaveStart).CompareTo(Mathf.Max(0f, b.delayFromWaveStart)));

        int pendingSteps = sortedSteps.Count;
        float waveElapsed = 0f;

        for (int i = 0; i < sortedSteps.Count; i++)
        {
            MemeWaveData.WaveStep step = sortedSteps[i];
            float targetDelay = Mathf.Max(0f, step.delayFromWaveStart);
            float waitTime = targetDelay - waveElapsed;
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
                waveElapsed = targetDelay;
            }

            Coroutine stepRoutine = null;
            stepRoutine = StartCoroutine(RunWaveStepRoutine(step, () =>
            {
                pendingSteps--;
                if (stepRoutine != null)
                {
                    activeStepCoroutines.Remove(stepRoutine);
                }
            }));

            if (stepRoutine != null)
            {
                activeStepCoroutines.Add(stepRoutine);
            }
        }

        while (pendingSteps > 0)
        {
            yield return null;
        }

        if (wave.RestoreMainBgmOnEnd && AudioManager.Instance != null)
        {
            AudioManager.Instance.RestoreMainBgm();
        }
    }

    private IEnumerator RunWaveStepRoutine(MemeWaveData.WaveStep step, System.Action onCompleted)
    {
        if (step != null && step.IsValid)
        {
            MemePatternData pattern = step.pattern;
            MemeSpawnType spawnType = ResolveWaveStepSpawnType(step, pattern);
            float? overrideY = step.overrideNormalizedHeight
                ? ResolveNormalizedHeightY(step.normalizedHeight)
                : (float?)null;
            float finalSpeedMultiplier = step.EffectiveSpeedMultiplier * GetDifficultySpeedMultiplier();

            yield return PlayMemeAttackSequence(
                pattern,
                spawnType,
                overrideSide: step.overrideSpawnSide,
                overrideY: overrideY,
                speedMultiplier: finalSpeedMultiplier,
                clearAllWarningsOnStart: false);
        }

        onCompleted?.Invoke();
    }

    private IEnumerator PlayMemeAttackSequence(
        MemePatternData pattern,
        MemeSpawnType spawnType,
        bool overrideSide = false,
        float? overrideY = null,
        float speedMultiplier = 1f,
        bool clearAllWarningsOnStart = true)
    {
        List<SpawnShot> shots = clearAllWarningsOnStart ? shotPlanBuffer : new List<SpawnShot>();
        BuildShotPlan(pattern, spawnType, shots, overrideSide, overrideY);
        if (shots.Count == 0)
        {
            yield break;
        }

        float effectiveWarningDuration = GetEffectiveWarningDuration(pattern.obstaclePrefab);
        if (effectiveWarningDuration > 0f)
        {
            yield return ShowWarningRoutine(pattern, shots, effectiveWarningDuration, clearAllWarningsOnStart);
        }

        bool isSingleBurst = pattern.Formation == MemeFormationType.Single && pattern.BurstInterval > 0f;
        if (isSingleBurst)
        {
            SpawnShot baseShot = shots[0];
            int count = pattern.ProjectileCount;
            float interval = pattern.BurstInterval;

            for (int i = 0; i < count; i++)
            {
                SpawnShot shot = baseShot;
                if (i > 0 && !pattern.useCustomPosition && !overrideY.HasValue)
                {
                    shot.SpawnPos = ApplyBurstOffset(baseShot.SpawnPos, baseShot.SpawnType);
                }

                SpawnObstacleShot(pattern, shot, playSfx: true, speedMultiplier: speedMultiplier);

                if (i < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            yield break;
        }

        bool isStaggeredFan = pattern.Formation == MemeFormationType.FanSpread && pattern.BurstInterval > 0f;
        if (isStaggeredFan)
        {
            float interval = pattern.BurstInterval;
            for (int i = 0; i < shots.Count; i++)
            {
                SpawnObstacleShot(pattern, shots[i], playSfx: true, speedMultiplier: speedMultiplier);

                if (i < shots.Count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            yield break;
        }

        for (int i = 0; i < shots.Count; i++)
        {
            SpawnObstacleShot(pattern, shots[i], playSfx: i == 0, speedMultiplier: speedMultiplier);
        }
    }

    private void BuildShotPlan(
        MemePatternData pattern,
        MemeSpawnType spawnType,
        List<SpawnShot> results,
        bool overrideSide = false,
        float? overrideY = null)
    {
        results.Clear();
        if (pattern == null || pattern.obstaclePrefab == null)
        {
            return;
        }

        switch (pattern.Formation)
        {
            case MemeFormationType.DualOpposite:
                BuildDualOppositePlan(pattern, results, overrideY);
                break;

            case MemeFormationType.DualParallel:
                BuildDualParallelPlan(pattern, spawnType, results, overrideY);
                break;

            case MemeFormationType.FanSpread:
                BuildFanSpreadPlan(pattern, spawnType, results, overrideSide, overrideY);
                break;

            case MemeFormationType.Single:
            default:
            {
                ResolvePositions(pattern, spawnType, out Vector3 spawnPos, out Vector3 warningPos, overrideSide, overrideY);
                results.Add(new SpawnShot
                {
                    SpawnType = spawnType,
                    SpawnPos = spawnPos,
                    WarningPos = warningPos,
                    MoveDirection = Vector2.zero,
                    UseCustomDirection = false,
                    ShowExclamation = true
                });
                break;
            }
        }
    }

    private void BuildDualOppositePlan(MemePatternData pattern, List<SpawnShot> results, float? overrideY = null)
    {
        float separation = Mathf.Abs(pattern.DualHeightSeparation);
        ResolveDualHeights(separation, out float lowerY, out float upperY, overrideY);

        bool leftIsLower = Random.value < 0.5f;
        float leftY = leftIsLower ? lowerY : upperY;
        float rightY = leftIsLower ? upperY : lowerY;

        results.Add(new SpawnShot
        {
            SpawnType = MemeSpawnType.Left,
            SpawnPos = new Vector3(leftSpawnX, leftY, 0f),
            WarningPos = new Vector3(leftWarningX, leftY + warningOffsetY, 0f),
            MoveDirection = Vector2.zero,
            UseCustomDirection = false,
            ShowExclamation = true
        });

        results.Add(new SpawnShot
        {
            SpawnType = MemeSpawnType.Right,
            SpawnPos = new Vector3(rightSpawnX, rightY, 0f),
            WarningPos = new Vector3(rightWarningX, rightY + warningOffsetY, 0f),
            MoveDirection = Vector2.zero,
            UseCustomDirection = false,
            ShowExclamation = true
        });
    }

    private void BuildDualParallelPlan(MemePatternData pattern, MemeSpawnType spawnType, List<SpawnShot> results, float? overrideY = null)
    {
        MemeSpawnType horizontalType = ResolveHorizontalSpawnType(pattern, spawnType);
        float separation = Mathf.Abs(pattern.DualHeightSeparation);
        ResolveDualHeights(separation, out float lowerY, out float upperY, overrideY);

        float spawnX = horizontalType == MemeSpawnType.Left ? leftSpawnX : rightSpawnX;
        float warningX = horizontalType == MemeSpawnType.Left ? leftWarningX : rightWarningX;

        results.Add(new SpawnShot
        {
            SpawnType = horizontalType,
            SpawnPos = new Vector3(spawnX, lowerY, 0f),
            WarningPos = new Vector3(warningX, lowerY + warningOffsetY, 0f),
            MoveDirection = Vector2.zero,
            UseCustomDirection = false,
            ShowExclamation = true
        });

        results.Add(new SpawnShot
        {
            SpawnType = horizontalType,
            SpawnPos = new Vector3(spawnX, upperY, 0f),
            WarningPos = new Vector3(warningX, upperY + warningOffsetY, 0f),
            MoveDirection = Vector2.zero,
            UseCustomDirection = false,
            ShowExclamation = true
        });
    }

    private void BuildFanSpreadPlan(
        MemePatternData pattern,
        MemeSpawnType spawnType,
        List<SpawnShot> results,
        bool overrideSide = false,
        float? overrideY = null)
    {
        ResolvePositions(pattern, spawnType, out Vector3 spawnPos, out Vector3 warningPos, overrideSide, overrideY);

        Vector2 baseDirection = GetBaseDirection(spawnType);
        float baseAngleDeg = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        int count = pattern.ProjectileCount;
        float halfFanAngle = pattern.FanTotalAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = count == 1
                ? 0f
                : Mathf.Lerp(-halfFanAngle, halfFanAngle, (float)i / (count - 1));
            float angleRad = (baseAngleDeg + angleOffset) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            results.Add(new SpawnShot
            {
                SpawnType = spawnType,
                SpawnPos = spawnPos,
                WarningPos = warningPos,
                MoveDirection = dir,
                UseCustomDirection = true,
                ShowExclamation = i == 0
            });
        }
    }

    private void ResolveDualHeights(float separation, out float lowerY, out float upperY, float? overrideCenterY = null)
    {
        float minY = Mathf.Min(horizontalMinY, horizontalMaxY);
        float maxY = Mathf.Max(horizontalMinY, horizontalMaxY);
        float maxLowerY = Mathf.Max(minY, maxY - separation);
        if (overrideCenterY.HasValue)
        {
            lowerY = Mathf.Clamp(overrideCenterY.Value - separation * 0.5f, minY, maxLowerY);
        }
        else
        {
            lowerY = Random.Range(minY, maxLowerY);
        }

        upperY = lowerY + separation;
    }

    private MemeSpawnType ResolveHorizontalSpawnType(MemePatternData pattern, MemeSpawnType requestedType)
    {
        if (requestedType == MemeSpawnType.Left || requestedType == MemeSpawnType.Right)
        {
            return requestedType;
        }

        if (pattern.allowLeft && !pattern.allowRight)
        {
            return MemeSpawnType.Left;
        }

        if (pattern.allowRight && !pattern.allowLeft)
        {
            return MemeSpawnType.Right;
        }

        return Random.value < 0.5f ? MemeSpawnType.Left : MemeSpawnType.Right;
    }

    private static Vector2 GetBaseDirection(MemeSpawnType spawnType)
    {
        switch (spawnType)
        {
            case MemeSpawnType.Left:
                return Vector2.right;

            case MemeSpawnType.Right:
                return Vector2.left;

            case MemeSpawnType.Center:
            default:
                return Vector2.down;
        }
    }

    private Vector3 ApplyBurstOffset(Vector3 basePos, MemeSpawnType spawnType)
    {
        if (spawnType == MemeSpawnType.Center)
        {
            float offsetX = Random.Range(-BurstRandomOffsetRange, BurstRandomOffsetRange);
            float minX = centerSpawnPosition.x - centerRandomOffsetX;
            float maxX = centerSpawnPosition.x + centerRandomOffsetX;
            float clampedX = centerRandomOffsetX > 0f
                ? Mathf.Clamp(basePos.x + offsetX, minX, maxX)
                : basePos.x + offsetX;
            return new Vector3(clampedX, basePos.y, basePos.z);
        }

        float minY = Mathf.Min(horizontalMinY, horizontalMaxY);
        float maxY = Mathf.Max(horizontalMinY, horizontalMaxY);
        float offsetY = Random.Range(-BurstRandomOffsetRange, BurstRandomOffsetRange);
        float clampedY = Mathf.Clamp(basePos.y + offsetY, minY, maxY);
        return new Vector3(basePos.x, clampedY, basePos.z);
    }

    private void SpawnObstacleShot(MemePatternData pattern, SpawnShot shot, bool playSfx, float speedMultiplier = 1f)
    {
        MemeAttackObstacle instance = Instantiate(pattern.obstaclePrefab, shot.SpawnPos, Quaternion.identity);
        instance.SetSourcePattern(pattern);
        instance.ApplyVisualOverride(pattern.overrideSprite, pattern.overrideMaterial);

        AudioClip attackSfx = pattern.attackSfx;
        float volume = pattern.soundVolume;
        System.Action onAttackBegin = playSfx
            ? () => AudioManager.Instance?.PlaySFX(attackSfx, volume)
            : null;

        instance.Initialize(
            shot.SpawnType,
            pattern.moveSpeed,
            speedMultiplier,
            shot.UseCustomDirection ? shot.MoveDirection : Vector2.zero,
            pattern.ProjectileScaleMultiplier,
            onAttackBegin: onAttackBegin,
            sourcePattern: pattern
        );
    }

    private float GetEffectiveWarningDuration(MemeAttackObstacle obstaclePrefab)
    {
        if (warningDuration > 0f)
        {
            return warningDuration;
        }

        if (obstaclePrefab != null && obstaclePrefab.WarningDuration > 0f)
        {
            return obstaclePrefab.WarningDuration;
        }

        return 0f;
    }

    private IEnumerator ShowWarningRoutine(
        MemePatternData pattern,
        List<SpawnShot> shots,
        float duration,
        bool clearAllWarningsOnStart = true)
    {
        if (clearAllWarningsOnStart)
        {
            ClearActiveWarning();
        }

        if (!Mathf.Approximately(lastWarningSfxTime, Time.time))
        {
            lastWarningSfxTime = Time.time;
            AudioManager.Instance?.PlayWarningSFX();
        }

        WarningStyle warningStyle = pattern != null ? pattern.warningStyle : WarningStyle.Trajectory;
        bool showExclamation = warningStyle == WarningStyle.Exclamation || warningStyle == WarningStyle.Both;
        bool showTrajectory = warningStyle == WarningStyle.Trajectory || warningStyle == WarningStyle.Both;

        List<GameObject> localWarningObjects = showExclamation ? new List<GameObject>(shots.Count) : null;
        List<TMP_Text[]> warningTextsList = showExclamation ? new List<TMP_Text[]>(shots.Count) : null;
        List<SpriteRenderer[]> warningSpritesList = showExclamation ? new List<SpriteRenderer[]>(shots.Count) : null;
        if (showExclamation)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                if (!shots[i].ShowExclamation)
                {
                    continue;
                }

                GameObject warningObj;
                if (warningPrefab != null)
                {
                    warningObj = Instantiate(warningPrefab, shots[i].WarningPos, Quaternion.identity);
                    if (!warningObj.activeSelf)
                    {
                        warningObj.SetActive(true);
                    }
                    warningObj.transform.position = shots[i].WarningPos;
                }
                else
                {
                    TextMeshPro runtimeWarningText = CreateRuntimeWarningMark(shots[i].WarningPos, pattern != null ? pattern.obstaclePrefab : null);
                    warningObj = runtimeWarningText.gameObject;
                }

                if (warningObj != null)
                {
                    localWarningObjects.Add(warningObj);
                    activeWarningObjects.Add(warningObj);
                    warningTextsList.Add(warningObj.GetComponentsInChildren<TMP_Text>(true));
                    warningSpritesList.Add(warningObj.GetComponentsInChildren<SpriteRenderer>(true));
                }
            }
        }

        List<GameObject> localTrajectoryObjects = showTrajectory ? new List<GameObject>(shots.Count) : null;
        List<SpriteRenderer> trajectoryRenderers = showTrajectory ? new List<SpriteRenderer>(shots.Count) : null;
        Color trajectoryBaseColor = pattern != null ? pattern.trajectoryColor : new Color(0f, 1f, 1f, 0.35f);
        int blinkCount = Mathf.Max(1, pattern != null ? pattern.blinkCount : warningBlinkCount);
        if (showTrajectory)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                SpriteRenderer sr = CreateTrajectoryWarning(pattern, shots[i], trajectoryBaseColor, out GameObject trajectoryObj);
                if (trajectoryObj != null)
                {
                    localTrajectoryObjects.Add(trajectoryObj);
                }
                if (sr != null)
                {
                    trajectoryRenderers.Add(sr);
                }
            }
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float cycleProgress = Mathf.Repeat((elapsed / duration) * blinkCount, 1f);
            bool isVisiblePhase = cycleProgress < 0.5f;

            if (trajectoryRenderers != null)
            {
                Color color = trajectoryBaseColor;
                color.a = isVisiblePhase ? trajectoryBaseColor.a : TrajectoryDimAlpha;
                for (int i = 0; i < trajectoryRenderers.Count; i++)
                {
                    if (trajectoryRenderers[i] != null)
                    {
                        trajectoryRenderers[i].color = color;
                    }
                }
            }

            if (showExclamation)
            {
                for (int i = 0; i < localWarningObjects.Count; i++)
                {
                    GameObject warningObj = localWarningObjects[i];
                    if (warningObj != null)
                    {
                        ApplyExclamationBlinkState(warningObj, warningTextsList[i], warningSpritesList[i], isVisiblePhase);
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ReleaseWarningObjects(localWarningObjects, localTrajectoryObjects);
    }

    private static void ApplyExclamationBlinkState(
        GameObject warningObject,
        TMP_Text[] warningTexts,
        SpriteRenderer[] warningSprites,
        bool isVisible)
    {
        bool hasAlphaControlledComponent = false;
        float targetAlpha = isVisible ? 1f : ExclamationDimAlpha;

        if (warningTexts != null && warningTexts.Length > 0)
        {
            hasAlphaControlledComponent = true;
            for (int i = 0; i < warningTexts.Length; i++)
            {
                TMP_Text tmp = warningTexts[i];
                if (tmp == null)
                {
                    continue;
                }

                Color color = tmp.color;
                color.a = targetAlpha;
                tmp.color = color;
            }
        }

        if (warningSprites != null && warningSprites.Length > 0)
        {
            hasAlphaControlledComponent = true;
            for (int i = 0; i < warningSprites.Length; i++)
            {
                SpriteRenderer sr = warningSprites[i];
                if (sr == null)
                {
                    continue;
                }

                Color color = sr.color;
                color.a = targetAlpha;
                sr.color = color;
            }
        }

        if (!hasAlphaControlledComponent && warningObject != null && warningObject.activeSelf != isVisible)
        {
            warningObject.SetActive(isVisible);
        }
    }

    private SpriteRenderer CreateTrajectoryWarning(
        MemePatternData pattern,
        SpawnShot shot,
        Color baseColor,
        out GameObject trajectoryObj)
    {
        float fallbackThickness = Mathf.Max(0.1f, pattern != null ? pattern.trajectoryThickness : 1.5f);
        bool hasCustomDirection = shot.UseCustomDirection && shot.MoveDirection.sqrMagnitude > 0.0001f;
        Vector3 referenceCenter = shot.SpawnPos;
        Vector3 colliderSize = Vector3.zero;
        bool hasColliderBounds = pattern != null
            && pattern.obstaclePrefab != null
            && pattern.obstaclePrefab.TryGetTrajectoryBounds(
                shot.SpawnPos,
                shot.SpawnType,
                hasCustomDirection,
                pattern.ProjectileScaleMultiplier,
                pattern.overrideSprite,
                out referenceCenter,
                out colliderSize);

        Vector3 trajectoryPos;
        Quaternion trajectoryRotation = Quaternion.identity;
        Vector3 trajectoryScale;

        if (hasCustomDirection)
        {
            Vector2 dir = shot.MoveDirection.normalized;
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float projectedThickness = hasColliderBounds
                ? Mathf.Abs(dir.y) * colliderSize.x + Mathf.Abs(dir.x) * colliderSize.y
                : fallbackThickness;
            float thickness = Mathf.Max(0.1f, projectedThickness);
            trajectoryPos = referenceCenter + new Vector3(dir.x, dir.y, 0f) * (TrajectoryLength * 0.5f);
            trajectoryRotation = Quaternion.Euler(0f, 0f, angleDeg);
            trajectoryScale = new Vector3(TrajectoryLength, thickness, 1f);
        }
        else if (shot.SpawnType == MemeSpawnType.Center)
        {
            float thickness = Mathf.Max(0.1f, hasColliderBounds && colliderSize.x > 0f ? colliderSize.x : fallbackThickness);
            trajectoryPos = new Vector3(referenceCenter.x, 0f, 0f);
            trajectoryScale = new Vector3(thickness, TrajectoryLength, 1f);
        }
        else
        {
            float thickness = Mathf.Max(0.1f, hasColliderBounds && colliderSize.y > 0f ? colliderSize.y : fallbackThickness);
            trajectoryPos = new Vector3(0f, referenceCenter.y, 0f);
            trajectoryScale = new Vector3(TrajectoryLength, thickness, 1f);
        }

        if (trajectoryWarningPrefab != null)
        {
            trajectoryObj = Instantiate(trajectoryWarningPrefab, trajectoryPos, trajectoryRotation);
            if (!trajectoryObj.activeSelf)
            {
                trajectoryObj.SetActive(true);
            }
        }
        else
        {
            trajectoryObj = new GameObject("WarningTrajectory");
            SpriteRenderer fallbackRenderer = trajectoryObj.AddComponent<SpriteRenderer>();
            fallbackRenderer.sprite = GetFallbackSquareSprite();
            fallbackRenderer.sortingOrder = DefaultTrajectorySortingOrder;
        }

        trajectoryObj.transform.position = trajectoryPos;
        trajectoryObj.transform.rotation = trajectoryRotation;
        trajectoryObj.transform.localScale = trajectoryScale;
        activeTrajectoryWarningObjects.Add(trajectoryObj);

        SpriteRenderer sr = trajectoryObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = baseColor;
        }

        return sr;
    }

    private static Sprite GetFallbackSquareSprite()
    {
        if (fallbackSquareSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            fallbackSquareSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                tex.width
            );
        }

        return fallbackSquareSprite;
    }

    private TextMeshPro CreateRuntimeWarningMark(Vector3 warningPos, MemeAttackObstacle obstaclePrefab)
    {
        GameObject warningObj = new GameObject("WarningMark");
        warningObj.transform.position = warningPos;

        TextMeshPro warningText = warningObj.AddComponent<TextMeshPro>();
        warningText.text = "!";
        warningText.fontSize = warningFontSize;
        warningText.fontStyle = FontStyles.Bold;
        warningText.color = warningTextColor;
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.textWrappingMode = TextWrappingModes.NoWrap;
        warningText.overflowMode = TextOverflowModes.Overflow;
        warningText.rectTransform.sizeDelta = new Vector2(3f, 3f);
        warningText.sortingOrder = warningSortingOrder;

        if (obstaclePrefab != null && obstaclePrefab.WarningText != null && obstaclePrefab.WarningText.font != null)
        {
            warningText.font = obstaclePrefab.WarningText.font;
        }

        return warningText;
    }

    private void ReleaseWarningObjects(List<GameObject> warningObjects, List<GameObject> trajectoryObjects)
    {
        if (warningObjects != null)
        {
            for (int i = 0; i < warningObjects.Count; i++)
            {
                GameObject obj = warningObjects[i];
                if (obj != null)
                {
                    activeWarningObjects.Remove(obj);
                    Destroy(obj);
                }
            }
        }

        if (trajectoryObjects != null)
        {
            for (int i = 0; i < trajectoryObjects.Count; i++)
            {
                GameObject obj = trajectoryObjects[i];
                if (obj != null)
                {
                    activeTrajectoryWarningObjects.Remove(obj);
                    Destroy(obj);
                }
            }
        }
    }

    private void StopActiveStepCoroutines()
    {
        for (int i = 0; i < activeStepCoroutines.Count; i++)
        {
            if (activeStepCoroutines[i] != null)
            {
                StopCoroutine(activeStepCoroutines[i]);
            }
        }

        activeStepCoroutines.Clear();
    }

    private void ClearActiveWarning()
    {
        for (int i = 0; i < activeWarningObjects.Count; i++)
        {
            if (activeWarningObjects[i] != null)
            {
                Destroy(activeWarningObjects[i]);
            }
        }
        activeWarningObjects.Clear();

        for (int i = 0; i < activeTrajectoryWarningObjects.Count; i++)
        {
            if (activeTrajectoryWarningObjects[i] != null)
            {
                Destroy(activeTrajectoryWarningObjects[i]);
            }
        }
        activeTrajectoryWarningObjects.Clear();
    }

    private int SelectNextPatternIndex()
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
            if (!IsValidPatternIndex(idx))
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
            int j = Random.Range(0, i + 1);
            int temp = shuffleBag[i];
            shuffleBag[i] = shuffleBag[j];
            shuffleBag[j] = temp;
        }

        // 가방이 리필될 때 직전에 나온 패턴이 바로 연속으로 다시 나오지 않도록 방지
        if (shuffleBag.Count > 1 && lastSpawnedIndex >= 0 && shuffleBag[0] == lastSpawnedIndex)
        {
            int swapIndex = Random.Range(1, shuffleBag.Count);
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
            totalWeight += Mathf.Max(0f, patterns[idx].spawnWeight);
        }

        int selectedIndex;
        if (totalWeight <= 0f)
        {
            selectedIndex = validIndicesBuffer[Random.Range(0, validIndicesBuffer.Count)];
        }
        else
        {
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            selectedIndex = validIndicesBuffer[validIndicesBuffer.Count - 1];

            for (int i = 0; i < validIndicesBuffer.Count; i++)
            {
                int idx = validIndicesBuffer[i];
                cumulative += Mathf.Max(0f, patterns[idx].spawnWeight);
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

    private MemeSpawnType PickSpawnType(MemePatternData pattern)
    {
        allowedSpawnTypesBuffer.Clear();
        if (pattern.allowLeft) allowedSpawnTypesBuffer.Add(MemeSpawnType.Left);
        if (pattern.allowRight) allowedSpawnTypesBuffer.Add(MemeSpawnType.Right);
        if (pattern.allowCenter) allowedSpawnTypesBuffer.Add(MemeSpawnType.Center);

        if (allowedSpawnTypesBuffer.Count == 0)
        {
            return (MemeSpawnType)Random.Range(0, 3);
        }

        int pick = Random.Range(0, allowedSpawnTypesBuffer.Count);
        return allowedSpawnTypesBuffer[pick];
    }

    private void CollectValidIndices()
    {
        validIndicesBuffer.Clear();
        if (patterns == null)
        {
            return;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            if (IsValidPatternIndex(i))
            {
                validIndicesBuffer.Add(i);
            }
        }
    }

    private bool HasValidPattern()
    {
        if (patterns == null)
        {
            return false;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            if (IsValidPatternIndex(i))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidPatternIndex(int index)
    {
        return patterns != null
            && index >= 0
            && index < patterns.Count
            && patterns[index] != null
            && patterns[index].obstaclePrefab != null;
    }

    private bool HasValidWave()
    {
        if (waves == null || waves.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < waves.Count; i++)
        {
            if (IsValidWaveIndex(i))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidWaveIndex(int index)
    {
        return waves != null
            && index >= 0
            && index < waves.Count
            && waves[index] != null
            && waves[index].HasValidStep();
    }

    private int GetCurrentDifficultyScore()
    {
        if (GameManager.Instance != null)
        {
            return Mathf.Max(0, GameManager.Instance.Score);
        }

        return 0;
    }

    private float GetDifficultyProgress()
    {
        if (!enableScoreDifficultyScaling || maxDifficultyScore <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)GetCurrentDifficultyScore() / maxDifficultyScore);
    }

    private float GetDifficultySpeedMultiplier()
    {
        if (!enableScoreDifficultyScaling || maxDifficultyScore <= 0)
        {
            return 1f;
        }

        float upperBound = Mathf.Max(1f, maxDifficultySpeedMultiplier);
        return Mathf.Lerp(1f, upperBound, GetDifficultyProgress());
    }

    private float GetScaledPostWaveDelay(MemeWaveData wave)
    {
        if (wave == null)
        {
            return 0f;
        }

        float baseDelay = Mathf.Max(0f, wave.postWaveDelay);
        if (baseDelay <= 0f)
        {
            return 0f;
        }

        if (!enableScoreDifficultyScaling || maxDifficultyScore <= 0)
        {
            return baseDelay;
        }

        float t = GetDifficultyProgress();
        float delayScale = Mathf.Lerp(1f, Mathf.Clamp(minPostWaveDelayScale, 0.1f, 1f), t);
        float scaledDelay = baseDelay * delayScale;
        float floorDelay = Mathf.Min(baseDelay, Mathf.Max(0f, minPostWaveDelay));
        return Mathf.Max(floorDelay, scaledDelay);
    }

    private MemeWaveData SelectNextWave()
    {
        int currentScore = GetCurrentDifficultyScore();
        CollectEligibleWaveIndices(currentScore);
        if (eligibleWaveIndicesBuffer.Count == 0)
        {
            return null;
        }

        int selectedIndex = SelectWaveShuffleBagIndex();
        if (!IsValidWaveIndex(selectedIndex))
        {
            return null;
        }

        return waves[selectedIndex];
    }

    private void CollectEligibleWaveIndices(int currentScore)
    {
        eligibleWaveIndicesBuffer.Clear();
        if (waves == null || waves.Count == 0)
        {
            return;
        }

        int lowestUnlockScore = int.MaxValue;
        for (int i = 0; i < waves.Count; i++)
        {
            if (!IsValidWaveIndex(i))
            {
                continue;
            }

            int unlockScore = Mathf.Max(0, waves[i].minUnlockScore);
            if (unlockScore < lowestUnlockScore)
            {
                lowestUnlockScore = unlockScore;
            }

            if (unlockScore <= currentScore)
            {
                eligibleWaveIndicesBuffer.Add(i);
            }
        }

        // 모든 유효 웨이브의 minUnlockScore가 현재 점수보다 높을 경우 가장 낮은 해금 점수의 웨이브로 폴백
        if (eligibleWaveIndicesBuffer.Count == 0 && lowestUnlockScore < int.MaxValue)
        {
            for (int i = 0; i < waves.Count; i++)
            {
                if (IsValidWaveIndex(i) && Mathf.Max(0, waves[i].minUnlockScore) == lowestUnlockScore)
                {
                    eligibleWaveIndicesBuffer.Add(i);
                }
            }
        }
    }

    private int SelectWaveShuffleBagIndex()
    {
        for (int i = waveShuffleBag.Count - 1; i >= 0; i--)
        {
            int idx = waveShuffleBag[i];
            if (!IsValidWaveIndex(idx) || !eligibleWaveIndicesBuffer.Contains(idx))
            {
                waveShuffleBag.RemoveAt(i);
            }
        }

        // 점수 상승으로 새로 해금된 웨이브가 있다면 현재 셔플백에 즉시 편입
        if (waveShuffleBag.Count > 0)
        {
            float minWeight = GetMinimumEligibleWaveWeight();
            for (int i = 0; i < eligibleWaveIndicesBuffer.Count; i++)
            {
                int idx = eligibleWaveIndicesBuffer[i];
                if (knownUnlockedWaveIndices.Add(idx))
                {
                    int tickets = CalculateWaveBagTickets(waves[idx], minWeight);
                    for (int t = 0; t < tickets; t++)
                    {
                        int insertPos = Random.Range(0, waveShuffleBag.Count + 1);
                        waveShuffleBag.Insert(insertPos, idx);
                    }
                }
            }
        }

        if (waveShuffleBag.Count == 0)
        {
            RefillWaveShuffleBag();
        }

        if (waveShuffleBag.Count == 0)
        {
            return -1;
        }

        int selectedIndex = waveShuffleBag[0];
        waveShuffleBag.RemoveAt(0);
        lastSpawnedWaveIndex = selectedIndex;
        return selectedIndex;
    }

    private void RefillWaveShuffleBag()
    {
        waveShuffleBag.Clear();
        knownUnlockedWaveIndices.Clear();

        float minWeight = GetMinimumEligibleWaveWeight();
        for (int i = 0; i < eligibleWaveIndicesBuffer.Count; i++)
        {
            int idx = eligibleWaveIndicesBuffer[i];
            knownUnlockedWaveIndices.Add(idx);
            int tickets = CalculateWaveBagTickets(waves[idx], minWeight);
            for (int t = 0; t < tickets; t++)
            {
                waveShuffleBag.Add(idx);
            }
        }

        for (int i = waveShuffleBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = waveShuffleBag[i];
            waveShuffleBag[i] = waveShuffleBag[j];
            waveShuffleBag[j] = temp;
        }

        // 웨이브 셔플백 리필 시 직전에 나온 웨이브가 바로 연속 출현하지 않도록 방지
        if (waveShuffleBag.Count > 1 && lastSpawnedWaveIndex >= 0 && waveShuffleBag[0] == lastSpawnedWaveIndex)
        {
            for (int i = 1; i < waveShuffleBag.Count; i++)
            {
                if (waveShuffleBag[i] != lastSpawnedWaveIndex)
                {
                    int temp = waveShuffleBag[0];
                    waveShuffleBag[0] = waveShuffleBag[i];
                    waveShuffleBag[i] = temp;
                    break;
                }
            }
        }
    }

    private float GetMinimumEligibleWaveWeight()
    {
        float minWeight = float.MaxValue;
        for (int i = 0; i < eligibleWaveIndicesBuffer.Count; i++)
        {
            int idx = eligibleWaveIndicesBuffer[i];
            float w = Mathf.Max(0.01f, waves[idx].weight);
            if (w < minWeight)
            {
                minWeight = w;
            }
        }

        return minWeight < float.MaxValue ? minWeight : 1f;
    }

    private static int CalculateWaveBagTickets(MemeWaveData wave, float minWeight)
    {
        if (wave == null)
        {
            return 1;
        }

        float safeWeight = Mathf.Max(0.01f, wave.weight);
        float safeMinWeight = Mathf.Max(0.01f, minWeight);
        return Mathf.Clamp(Mathf.RoundToInt(safeWeight / safeMinWeight), 1, 10);
    }

    private MemeSpawnType ResolveWaveStepSpawnType(MemeWaveData.WaveStep step, MemePatternData pattern)
    {
        if (step == null || !step.overrideSpawnSide)
        {
            return PickSpawnType(pattern);
        }

        switch (step.spawnSideOverride)
        {
            case SpawnSideOverride.Left:
                return MemeSpawnType.Left;

            case SpawnSideOverride.Right:
                return MemeSpawnType.Right;

            case SpawnSideOverride.Top:
                return MemeSpawnType.Center;

            case SpawnSideOverride.RandomHorizontal:
            default:
                return Random.value < 0.5f ? MemeSpawnType.Left : MemeSpawnType.Right;
        }
    }

    private float ResolveNormalizedHeightY(float normalizedHeight)
    {
        float minY = Mathf.Min(horizontalMinY, horizontalMaxY);
        float maxY = Mathf.Max(horizontalMinY, horizontalMaxY);
        return Mathf.Lerp(minY, maxY, Mathf.Clamp01(normalizedHeight));
    }

    private void ResolvePositions(
        MemePatternData pattern,
        MemeSpawnType spawnType,
        out Vector3 spawnPos,
        out Vector3 warningPos,
        bool overrideSide = false,
        float? overrideY = null)
    {
        if (pattern != null && pattern.useCustomPosition && !overrideSide && !overrideY.HasValue)
        {
            spawnPos = new Vector3(pattern.customObstacleSpawnPos.x, pattern.customObstacleSpawnPos.y, 0f);
            warningPos = new Vector3(pattern.customWarningSpawnPos.x, pattern.customWarningSpawnPos.y, 0f);
            return;
        }

        ResolveDefaultPositions(spawnType, out spawnPos, out warningPos, overrideY);
    }

    private void ResolveDefaultPositions(MemeSpawnType spawnType, out Vector3 spawnPos, out Vector3 warningPos, float? overrideY = null)
    {
        switch (spawnType)
        {
            case MemeSpawnType.Left:
            {
                float spawnY = overrideY ?? Random.Range(horizontalMinY, horizontalMaxY);
                spawnPos = new Vector3(leftSpawnX, spawnY, 0f);
                warningPos = new Vector3(leftWarningX, spawnY + warningOffsetY, 0f);
                break;
            }

            case MemeSpawnType.Right:
            {
                float spawnY = overrideY ?? Random.Range(horizontalMinY, horizontalMaxY);
                spawnPos = new Vector3(rightSpawnX, spawnY, 0f);
                warningPos = new Vector3(rightWarningX, spawnY + warningOffsetY, 0f);
                break;
            }

            case MemeSpawnType.Center:
            {
                float offsetX = centerRandomOffsetX > 0f
                    ? Random.Range(-centerRandomOffsetX, centerRandomOffsetX)
                    : 0f;
                spawnPos = new Vector3(centerSpawnPosition.x + offsetX, centerSpawnPosition.y, 0f);
                warningPos = new Vector3(spawnPos.x, spawnPos.y + warningOffsetY, 0f);
                break;
            }

            default:
                spawnPos = Vector3.zero;
                warningPos = Vector3.zero;
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        const float markerRadius = 0.25f;

        // Default Left / Right Spawn (Cyan) & Warning (Red)
        float midY = (horizontalMinY + horizontalMaxY) * 0.5f;
        DrawVerticalRangeGizmo(leftSpawnX, horizontalMinY, horizontalMaxY, Color.cyan, markerRadius);
        DrawVerticalRangeGizmo(rightSpawnX, horizontalMinY, horizontalMaxY, Color.cyan, markerRadius);
        DrawVerticalRangeGizmo(leftWarningX, horizontalMinY + warningOffsetY, horizontalMaxY + warningOffsetY, Color.red, markerRadius * 0.8f);
        DrawVerticalRangeGizmo(rightWarningX, horizontalMinY + warningOffsetY, horizontalMaxY + warningOffsetY, Color.red, markerRadius * 0.8f);

        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawLine(new Vector3(leftSpawnX, midY, 0f), new Vector3(leftWarningX, midY + warningOffsetY, 0f));
        Gizmos.DrawLine(new Vector3(rightSpawnX, midY, 0f), new Vector3(rightWarningX, midY + warningOffsetY, 0f));

        // Default Center Spawn (Cyan) & Warning (Red)
        DrawHorizontalRangeGizmo(centerSpawnPosition, centerRandomOffsetX, Color.cyan, markerRadius);
        DrawHorizontalRangeGizmo(new Vector2(centerSpawnPosition.x, centerSpawnPosition.y + warningOffsetY), centerRandomOffsetX, Color.red, markerRadius * 0.8f);

        // Per-pattern Custom Positions (Spawn: Green, Warning: Yellow)
        if (patterns == null)
        {
            return;
        }

        for (int i = 0; i < patterns.Count; i++)
        {
            MemePatternData pattern = patterns[i];
            if (pattern == null || !pattern.useCustomPosition)
            {
                continue;
            }

            Vector3 customSpawn = new Vector3(pattern.customObstacleSpawnPos.x, pattern.customObstacleSpawnPos.y, 0f);
            Vector3 customWarning = new Vector3(pattern.customWarningSpawnPos.x, pattern.customWarningSpawnPos.y, 0f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(customSpawn, markerRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(customWarning, markerRadius * 0.8f);
            Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.6f);
            Gizmos.DrawLine(customSpawn, customWarning);
        }
    }

    private static void DrawVerticalRangeGizmo(float x, float minY, float maxY, Color color, float radius)
    {
        Gizmos.color = color;
        Vector3 minPoint = new Vector3(x, minY, 0f);
        Vector3 maxPoint = new Vector3(x, maxY, 0f);
        Gizmos.DrawWireSphere(minPoint, radius);
        if (!Mathf.Approximately(minY, maxY))
        {
            Gizmos.DrawWireSphere(maxPoint, radius);
            Gizmos.DrawLine(minPoint, maxPoint);
        }
    }

    private static void DrawHorizontalRangeGizmo(Vector2 center, float offsetX, Color color, float radius)
    {
        Gizmos.color = color;
        Vector3 centerPoint = new Vector3(center.x, center.y, 0f);
        Gizmos.DrawWireSphere(centerPoint, radius);
        if (offsetX > 0f)
        {
            Vector3 leftPoint = new Vector3(center.x - offsetX, center.y, 0f);
            Vector3 rightPoint = new Vector3(center.x + offsetX, center.y, 0f);
            Gizmos.DrawLine(leftPoint, rightPoint);
        }
    }
}
