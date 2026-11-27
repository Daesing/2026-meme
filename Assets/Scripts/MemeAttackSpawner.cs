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

    private readonly List<int> shuffleBag = new List<int>();
    private readonly List<int> validIndicesBuffer = new List<int>();
    private readonly List<MemeSpawnType> allowedSpawnTypesBuffer = new List<MemeSpawnType>(3);

    private Coroutine spawnRoutine;
    private GameObject activeWarningObject;
    private int sequentialCursor;
    private int lastSpawnedIndex = -1;

    public MemeOrderMode OrderMode
    {
        get => orderMode;
        set => orderMode = value;
    }

    public IReadOnlyList<MemePatternData> Patterns => patterns;

    private void Start()
    {
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
        ResolvePositions(pattern, spawnType, out Vector3 spawnPos, out Vector3 warningPos);
        float effectiveWarningDuration = GetEffectiveWarningDuration(pattern.obstaclePrefab);

        if (effectiveWarningDuration > 0f)
        {
            yield return ShowWarningRoutine(warningPos, effectiveWarningDuration, pattern.obstaclePrefab);
        }

        MemeAttackObstacle instance = Instantiate(pattern.obstaclePrefab, spawnPos, Quaternion.identity);
        instance.ApplyVisualOverride(pattern.overrideSprite, pattern.overrideMaterial);

        AudioClip attackSfx = pattern.attackSfx;
        float volume = pattern.soundVolume;

        instance.Initialize(
            spawnType,
            pattern.moveSpeed,
            onAttackBegin: () => AudioManager.Instance?.PlaySFX(attackSfx, volume)
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

    private IEnumerator ShowWarningRoutine(Vector3 warningPos, float duration, MemeAttackObstacle obstaclePrefab)
    {
        ClearActiveWarning();
        AudioManager.Instance?.PlayWarningSFX();

        if (warningPrefab != null)
        {
            activeWarningObject = Instantiate(warningPrefab, warningPos, Quaternion.identity);
            if (!activeWarningObject.activeSelf)
            {
                activeWarningObject.SetActive(true);
            }
            activeWarningObject.transform.position = warningPos;
            yield return new WaitForSeconds(duration);
            ClearActiveWarning();
            yield break;
        }

        TextMeshPro warningText = CreateRuntimeWarningMark(warningPos, obstaclePrefab);
        activeWarningObject = warningText.gameObject;

        int blinkCount = Mathf.Max(1, warningBlinkCount);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float blinkT = 1f - Mathf.PingPong(normalizedTime * blinkCount * 2f, 1f);

            if (warningText != null)
            {
                Color color = warningTextColor;
                color.a = Mathf.Lerp(warningTextMinAlpha, warningTextMaxAlpha, blinkT);
                warningText.color = color;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearActiveWarning();
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
        if (activeWarningObject != null)
        {
            Destroy(activeWarningObject);
            activeWarningObject = null;
        }
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
