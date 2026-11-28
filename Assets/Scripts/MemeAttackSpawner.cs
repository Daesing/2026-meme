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
    private readonly List<MemeSpawnType> allowedSpawnTypesBuffer = new List<MemeSpawnType>(3);
    private readonly List<SpawnShot> shotPlanBuffer = new List<SpawnShot>();
    private readonly List<GameObject> activeWarningObjects = new List<GameObject>();
    private readonly List<GameObject> activeTrajectoryWarningObjects = new List<GameObject>();

    private Coroutine spawnRoutine;
    private static Sprite fallbackSquareSprite;
    private int sequentialCursor;
    private int lastSpawnedIndex = -1;

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

        if (!HasValidPattern())
        {
            Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemePatternData가 없습니다. 프로젝트 창에서 MemeGame/Meme Pattern 에셋을 생성해 등록하거나 'Tools/Meme Game/Setup Meme Attack'을 실행해 주세요.");
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

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

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
        ClearActiveWarning();
    }

    private void OnDestroy()
    {
        ClearActiveWarning();
    }

    private void StartSingleAttack(MemePatternData pattern, MemeSpawnType spawnType)
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        ClearActiveWarning();
        spawnRoutine = StartCoroutine(PlayMemeAttackSequence(pattern, spawnType));
    }

    private IEnumerator AutoSpawnLoop()
    {
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        do
        {
            int nextIndex = SelectNextPatternIndex();
            if (nextIndex < 0)
            {
                Debug.LogWarning("[MemeAttackSpawner] 등록된 유효한 MemePatternData가 없어 스폰을 중단합니다.");
                break;
            }

            MemePatternData pattern = patterns[nextIndex];
            MemeSpawnType spawnType = PickSpawnType(pattern);
            yield return PlayMemeAttackSequence(pattern, spawnType);

            if (repeatSpawn && spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
        while (repeatSpawn);

        spawnRoutine = null;
    }

    private IEnumerator PlayMemeAttackSequence(MemePatternData pattern, MemeSpawnType spawnType)
    {
        BuildShotPlan(pattern, spawnType, shotPlanBuffer);
        if (shotPlanBuffer.Count == 0)
        {
            yield break;
        }

        float effectiveWarningDuration = GetEffectiveWarningDuration(pattern.obstaclePrefab);
        if (effectiveWarningDuration > 0f)
        {
            yield return ShowWarningRoutine(pattern, shotPlanBuffer, effectiveWarningDuration);
        }

        bool isSingleBurst = pattern.Formation == MemeFormationType.Single && pattern.BurstInterval > 0f;
        if (isSingleBurst)
        {
            SpawnShot baseShot = shotPlanBuffer[0];
            int count = pattern.ProjectileCount;
            float interval = pattern.BurstInterval;

            for (int i = 0; i < count; i++)
            {
                SpawnShot shot = baseShot;
                if (i > 0 && !pattern.useCustomPosition)
                {
                    shot.SpawnPos = ApplyBurstOffset(baseShot.SpawnPos, baseShot.SpawnType);
                }

                SpawnObstacleShot(pattern, shot, playSfx: true);

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
            for (int i = 0; i < shotPlanBuffer.Count; i++)
            {
                SpawnObstacleShot(pattern, shotPlanBuffer[i], playSfx: true);

                if (i < shotPlanBuffer.Count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            yield break;
        }

        for (int i = 0; i < shotPlanBuffer.Count; i++)
        {
            SpawnObstacleShot(pattern, shotPlanBuffer[i], playSfx: i == 0);
        }
    }

    private void BuildShotPlan(MemePatternData pattern, MemeSpawnType spawnType, List<SpawnShot> results)
    {
        results.Clear();
        if (pattern == null || pattern.obstaclePrefab == null)
        {
            return;
        }

        switch (pattern.Formation)
        {
            case MemeFormationType.DualOpposite:
                BuildDualOppositePlan(pattern, results);
                break;

            case MemeFormationType.DualParallel:
                BuildDualParallelPlan(pattern, spawnType, results);
                break;

            case MemeFormationType.FanSpread:
                BuildFanSpreadPlan(pattern, spawnType, results);
                break;

            case MemeFormationType.Single:
            default:
            {
                ResolvePositions(pattern, spawnType, out Vector3 spawnPos, out Vector3 warningPos);
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

    private void BuildDualOppositePlan(MemePatternData pattern, List<SpawnShot> results)
    {
        float separation = Mathf.Abs(pattern.DualHeightSeparation);
        ResolveDualHeights(separation, out float lowerY, out float upperY);

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

    private void BuildDualParallelPlan(MemePatternData pattern, MemeSpawnType spawnType, List<SpawnShot> results)
    {
        MemeSpawnType horizontalType = ResolveHorizontalSpawnType(pattern, spawnType);
        float separation = Mathf.Abs(pattern.DualHeightSeparation);
        ResolveDualHeights(separation, out float lowerY, out float upperY);

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

    private void BuildFanSpreadPlan(MemePatternData pattern, MemeSpawnType spawnType, List<SpawnShot> results)
    {
        ResolvePositions(pattern, spawnType, out Vector3 spawnPos, out Vector3 warningPos);

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

    private void ResolveDualHeights(float separation, out float lowerY, out float upperY)
    {
        float minY = Mathf.Min(horizontalMinY, horizontalMaxY);
        float maxY = Mathf.Max(horizontalMinY, horizontalMaxY);
        float maxLowerY = Mathf.Max(minY, maxY - separation);
        lowerY = Random.Range(minY, maxLowerY);
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

    private void SpawnObstacleShot(MemePatternData pattern, SpawnShot shot, bool playSfx)
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
        float duration)
    {
        ClearActiveWarning();
        AudioManager.Instance?.PlayWarningSFX();

        WarningStyle warningStyle = pattern != null ? pattern.warningStyle : WarningStyle.Trajectory;
        bool showExclamation = warningStyle == WarningStyle.Exclamation || warningStyle == WarningStyle.Both;
        bool showTrajectory = warningStyle == WarningStyle.Trajectory || warningStyle == WarningStyle.Both;

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
                    activeWarningObjects.Add(warningObj);
                    warningTextsList.Add(warningObj.GetComponentsInChildren<TMP_Text>(true));
                    warningSpritesList.Add(warningObj.GetComponentsInChildren<SpriteRenderer>(true));
                }
            }
        }

        List<SpriteRenderer> trajectoryRenderers = showTrajectory ? new List<SpriteRenderer>(shots.Count) : null;
        Color trajectoryBaseColor = pattern != null ? pattern.trajectoryColor : new Color(0f, 1f, 1f, 0.35f);
        int blinkCount = Mathf.Max(1, pattern != null ? pattern.blinkCount : warningBlinkCount);
        if (showTrajectory)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                SpriteRenderer sr = CreateTrajectoryWarning(pattern, shots[i], trajectoryBaseColor);
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
                for (int i = 0; i < activeWarningObjects.Count; i++)
                {
                    GameObject warningObj = activeWarningObjects[i];
                    if (warningObj != null)
                    {
                        ApplyExclamationBlinkState(warningObj, warningTextsList[i], warningSpritesList[i], isVisiblePhase);
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearActiveWarning();
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
        Color baseColor)
    {
        float thickness = Mathf.Max(0.1f, pattern != null ? pattern.trajectoryThickness : 1.5f);
        Vector3 trajectoryPos;
        Quaternion trajectoryRotation = Quaternion.identity;
        Vector3 trajectoryScale;

        if (shot.UseCustomDirection && shot.MoveDirection.sqrMagnitude > 0.0001f)
        {
            Vector2 dir = shot.MoveDirection.normalized;
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            trajectoryPos = shot.SpawnPos + new Vector3(dir.x, dir.y, 0f) * (TrajectoryLength * 0.5f);
            trajectoryRotation = Quaternion.Euler(0f, 0f, angleDeg);
            trajectoryScale = new Vector3(TrajectoryLength, thickness, 1f);
        }
        else if (shot.SpawnType == MemeSpawnType.Center)
        {
            trajectoryPos = new Vector3(shot.SpawnPos.x, 0f, 0f);
            trajectoryScale = new Vector3(thickness, TrajectoryLength, 1f);
        }
        else
        {
            trajectoryPos = new Vector3(0f, shot.SpawnPos.y, 0f);
            trajectoryScale = new Vector3(TrajectoryLength, thickness, 1f);
        }

        GameObject trajectoryObj;
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

    private void ResolvePositions(MemePatternData pattern, MemeSpawnType spawnType, out Vector3 spawnPos, out Vector3 warningPos)
    {
        if (pattern != null && pattern.useCustomPosition)
        {
            spawnPos = new Vector3(pattern.customObstacleSpawnPos.x, pattern.customObstacleSpawnPos.y, 0f);
            warningPos = new Vector3(pattern.customWarningSpawnPos.x, pattern.customWarningSpawnPos.y, 0f);
            return;
        }

        ResolveDefaultPositions(spawnType, out spawnPos, out warningPos);
    }

    private void ResolveDefaultPositions(MemeSpawnType spawnType, out Vector3 spawnPos, out Vector3 warningPos)
    {
        switch (spawnType)
        {
            case MemeSpawnType.Left:
            {
                float spawnY = Random.Range(horizontalMinY, horizontalMaxY);
                spawnPos = new Vector3(leftSpawnX, spawnY, 0f);
                warningPos = new Vector3(leftWarningX, spawnY + warningOffsetY, 0f);
                break;
            }

            case MemeSpawnType.Right:
            {
                float spawnY = Random.Range(horizontalMinY, horizontalMaxY);
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
