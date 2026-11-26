using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public enum YahoSpawnType
{
    Left,
    Right,
    Center
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class MemeAttackObstacle : MonoBehaviour
{
    public static event Action<MemeAttackObstacle> OnAttackStart;
    public static event Action<MemeAttackObstacle> OnPlayerHit;

    [Header("Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D obstacleCollider;
    [SerializeField] private TextMeshPro warningText;

    [Header("Warning Phase")]
    [SerializeField] private float warningDuration = 0.6f;
    [SerializeField, Min(1)] private int warningBlinkCount = 3;
    [SerializeField] private Color warningSpriteColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField, Range(0f, 1f)] private float warningMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float warningMaxAlpha = 0.65f;
    [SerializeField] private Color warningTextColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField, Range(0f, 1f)] private float warningTextMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float warningTextMaxAlpha = 1f;
    [SerializeField] private float defaultWarningFontSize = 26f;

    [Header("Horizontal Dash (Left / Right)")]
    [SerializeField] private float horizontalDashSpeed = 18f;
    [SerializeField] private float horizontalScale = 0.85f;
    [SerializeField] private float offScreenLimitX = 15f;

    [Header("Center Lunge (Center)")]
    [SerializeField] private float centerDownSpeed = 9f;
    [SerializeField] private float centerStartScale = 0.35f;
    [SerializeField] private float centerMaxScale = 1.65f;
    [SerializeField] private float centerScaleGrowSpeed = 1.4f;
    [SerializeField] private float offScreenMinY = -10f;

    [Header("Events")]
    [SerializeField] private UnityEvent onAttackStart = new UnityEvent();
    [SerializeField] private UnityEvent onPlayerHit = new UnityEvent();

    private Rigidbody2D rb;
    private YahoSpawnType spawnType = YahoSpawnType.Left;
    private Color baseColor = Color.white;
    private bool baseColorCached;
    private Action onAttackBeginCallback;
    private float activeWarningDuration;
    private float phaseTimer;
    private bool isAttacking;
    private bool hasHitPlayer;

    public YahoSpawnType SpawnType => spawnType;
    public bool IsAttacking => isAttacking;
    public float WarningDuration => warningDuration;

    private void Awake()
    {
        EnsureComponents();
        activeWarningDuration = warningDuration;
    }

    public void ApplyVisualOverride(Sprite overrideSprite, Material overrideMaterial)
    {
        EnsureComponents();

        if (overrideSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = overrideSprite;

            if (obstacleCollider is BoxCollider2D boxCollider)
            {
                Vector2 spriteSize = overrideSprite.bounds.size;
                boxCollider.size = new Vector2(
                    Mathf.Max(0.5f, spriteSize.x * 0.75f),
                    Mathf.Max(0.5f, spriteSize.y * 0.75f)
                );
                boxCollider.offset = overrideSprite.bounds.center;
            }
        }

        if (overrideMaterial != null && spriteRenderer != null)
        {
            spriteRenderer.sharedMaterial = overrideMaterial;
        }
    }

    public void Initialize(
        YahoSpawnType type,
        bool skipWarning = false,
        Action onAttackBegin = null,
        float warningDurationOverride = -1f)
    {
        EnsureComponents();

        spawnType = type;
        onAttackBeginCallback = onAttackBegin;
        activeWarningDuration = warningDurationOverride > 0f ? warningDurationOverride : warningDuration;
        phaseTimer = 0f;
        isAttacking = false;
        hasHitPlayer = false;

        spriteRenderer.flipX = spawnType == YahoSpawnType.Right;

        if (spawnType == YahoSpawnType.Center)
        {
            transform.localScale = Vector3.one * centerStartScale;
        }
        else
        {
            transform.localScale = Vector3.one * horizontalScale;
        }

        if (skipWarning || activeWarningDuration <= 0f)
        {
            BeginAttack();
            return;
        }

        if (obstacleCollider != null)
        {
            obstacleCollider.enabled = false;
        }

        SetWarningTextVisible(true);
        ApplyWarningVisual(1f);
    }

    private void Update()
    {
        if (!isAttacking)
        {
            UpdateWarningPhase();
            return;
        }

        UpdateAttackPhase();
    }

    private void UpdateWarningPhase()
    {
        phaseTimer += Time.deltaTime;

        if (phaseTimer >= activeWarningDuration)
        {
            BeginAttack();
            return;
        }

        int blinkCount = Mathf.Max(1, warningBlinkCount);
        float normalizedTime = Mathf.Clamp01(phaseTimer / activeWarningDuration);
        float blinkT = 1f - Mathf.PingPong(normalizedTime * blinkCount * 2f, 1f);
        ApplyWarningVisual(blinkT);
    }

    private void BeginAttack()
    {
        if (isAttacking)
        {
            return;
        }

        isAttacking = true;
        SetWarningTextVisible(false);
        spriteRenderer.color = baseColor;

        if (obstacleCollider != null)
        {
            obstacleCollider.enabled = true;
        }

        Action callback = onAttackBeginCallback;
        onAttackBeginCallback = null;
        callback?.Invoke();

        OnAttackStart?.Invoke(this);
        onAttackStart?.Invoke();
    }

    private void UpdateAttackPhase()
    {
        Vector3 pos = transform.position;

        switch (spawnType)
        {
            case YahoSpawnType.Left:
                pos.x += horizontalDashSpeed * Time.deltaTime;
                transform.position = pos;
                if (pos.x > offScreenLimitX)
                {
                    Destroy(gameObject);
                }
                break;

            case YahoSpawnType.Right:
                pos.x -= horizontalDashSpeed * Time.deltaTime;
                transform.position = pos;
                if (pos.x < -offScreenLimitX)
                {
                    Destroy(gameObject);
                }
                break;

            case YahoSpawnType.Center:
                pos.y -= centerDownSpeed * Time.deltaTime;
                transform.position = pos;

                float currentScale = transform.localScale.x;
                float nextScale = Mathf.Min(centerMaxScale, currentScale + centerScaleGrowSpeed * Time.deltaTime);
                transform.localScale = Vector3.one * nextScale;

                if (pos.y < offScreenMinY)
                {
                    Destroy(gameObject);
                }
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAttacking || hasHitPlayer)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            hasHitPlayer = true;
            Debug.Log($"[MemeAttackObstacle] 플레이어 피격 / 게임오버! (출현 위치: {spawnType})");
            OnPlayerHit?.Invoke(this);
            onPlayerHit?.Invoke();
        }
    }

    private void ApplyWarningVisual(float blinkT)
    {
        Color tintedSpriteColor = warningSpriteColor;
        tintedSpriteColor.a = Mathf.Lerp(warningMinAlpha, warningMaxAlpha, blinkT);
        spriteRenderer.color = tintedSpriteColor;

        if (warningText != null)
        {
            Color exclamationColor = warningTextColor;
            exclamationColor.a = Mathf.Lerp(warningTextMinAlpha, warningTextMaxAlpha, blinkT);
            warningText.color = exclamationColor;
        }
    }

    private void SetWarningTextVisible(bool visible)
    {
        if (warningText == null)
        {
            return;
        }

        if (visible)
        {
            warningText.text = "!";
            warningText.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 16;
        }

        warningText.gameObject.SetActive(visible);
    }

    private void EnsureComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (!baseColorCached && spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            baseColorCached = true;
        }

        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<Collider2D>();
        }
        if (obstacleCollider != null)
        {
            obstacleCollider.isTrigger = true;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        EnsureWarningText();
    }

    private void EnsureWarningText()
    {
        if (warningText == null)
        {
            warningText = GetComponentInChildren<TextMeshPro>(true);
        }

        if (warningText == null)
        {
            GameObject warningObj = new GameObject("WarningMark");
            warningObj.transform.SetParent(transform, false);
            warningObj.transform.localPosition = Vector3.zero;

            warningText = warningObj.AddComponent<TextMeshPro>();
            warningText.text = "!";
            warningText.fontSize = defaultWarningFontSize;
            warningText.fontStyle = FontStyles.Bold;
            warningText.color = warningTextColor;
            warningText.alignment = TextAlignmentOptions.Center;
            warningText.textWrappingMode = TextWrappingModes.NoWrap;
            warningText.overflowMode = TextOverflowModes.Overflow;
            warningText.rectTransform.sizeDelta = new Vector2(3f, 3f);
            warningText.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 16;
        }
    }
}
