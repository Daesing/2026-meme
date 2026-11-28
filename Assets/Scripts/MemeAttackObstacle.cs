using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public enum MemeSpawnType
{
    Left,
    Right,
    Center
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class MemeAttackObstacle : MonoBehaviour
{
    public static event Action<MemeAttackObstacle> OnAttackStart;
    public static event Action<MemeAttackObstacle> OnPlayerHit;

    [Header("Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D obstacleCollider;
    [SerializeField] private TextMeshPro warningText;

    [Header("Warning Reference (Used by Spawner)")]
    [SerializeField] private float warningDuration = 0.6f;
    [SerializeField, Min(1)] private int warningBlinkCount = 3;

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
    private MemeSpawnType spawnType = MemeSpawnType.Left;
    private float activeMoveSpeed = 10f;
    private bool useCustomDirection;
    private Vector2 customMoveDirection = Vector2.right;
    private float activeScaleMultiplier = 1f;
    private Color baseColor = Color.white;
    private bool baseColorCached;
    private Action onAttackBeginCallback;
    private bool isAttacking;
    private bool hasHitPlayer;

    public MemePatternData SourcePattern { get; private set; }
    public MemeSpawnType SpawnType => spawnType;
    public float ActiveMoveSpeed => activeMoveSpeed;
    public bool IsAttacking => isAttacking;
    public float WarningDuration => warningDuration;
    public int WarningBlinkCount => warningBlinkCount;
    public TextMeshPro WarningText => warningText;

    public void SetSourcePattern(MemePatternData pattern)
    {
        SourcePattern = pattern;
    }

    private void Awake()
    {
        EnsureComponents();
        HideWarningText();
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

    public void Initialize(MemeSpawnType type, Action onAttackBegin = null)
    {
        float defaultSpeed = type == MemeSpawnType.Center ? centerDownSpeed : horizontalDashSpeed;
        Initialize(type, defaultSpeed, onAttackBegin);
    }

    public void Initialize(MemeSpawnType type, float moveSpeed, Action onAttackBegin = null, MemePatternData sourcePattern = null)
    {
        Initialize(type, moveSpeed, Vector2.zero, 1f, onAttackBegin, sourcePattern);
    }

    public void Initialize(
        MemeSpawnType type,
        float moveSpeed,
        Vector2 moveDirection,
        float scaleMultiplier = 1f,
        Action onAttackBegin = null,
        MemePatternData sourcePattern = null)
    {
        EnsureComponents();

        if (sourcePattern != null)
        {
            SourcePattern = sourcePattern;
        }

        spawnType = type;
        activeMoveSpeed = Mathf.Max(0f, moveSpeed);
        activeScaleMultiplier = scaleMultiplier > 0f ? scaleMultiplier : 1f;
        useCustomDirection = moveDirection.sqrMagnitude > 0.0001f;
        customMoveDirection = useCustomDirection ? moveDirection.normalized : Vector2.zero;
        onAttackBeginCallback = onAttackBegin;
        isAttacking = false;
        hasHitPlayer = false;

        if (useCustomDirection)
        {
            spriteRenderer.flipX = customMoveDirection.x < -0.01f;
            transform.localScale = Vector3.one * (horizontalScale * activeScaleMultiplier);
        }
        else
        {
            spriteRenderer.flipX = spawnType == MemeSpawnType.Right;

            if (spawnType == MemeSpawnType.Center)
            {
                transform.localScale = Vector3.one * (centerStartScale * activeScaleMultiplier);
            }
            else
            {
                transform.localScale = Vector3.one * (horizontalScale * activeScaleMultiplier);
            }
        }

        BeginAttack();
    }

    private void Update()
    {
        if (!isAttacking)
        {
            return;
        }

        UpdateAttackPhase();
    }

    private void BeginAttack()
    {
        if (isAttacking)
        {
            return;
        }

        isAttacking = true;
        HideWarningText();
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

        if (useCustomDirection)
        {
            pos.x += customMoveDirection.x * activeMoveSpeed * Time.deltaTime;
            pos.y += customMoveDirection.y * activeMoveSpeed * Time.deltaTime;
            transform.position = pos;

            float upperLimitY = Mathf.Abs(offScreenMinY);
            if (Mathf.Abs(pos.x) > offScreenLimitX || pos.y < offScreenMinY || pos.y > upperLimitY)
            {
                Destroy(gameObject);
            }
            return;
        }

        switch (spawnType)
        {
            case MemeSpawnType.Left:
                pos.x += activeMoveSpeed * Time.deltaTime;
                transform.position = pos;
                if (pos.x > offScreenLimitX)
                {
                    Destroy(gameObject);
                }
                break;

            case MemeSpawnType.Right:
                pos.x -= activeMoveSpeed * Time.deltaTime;
                transform.position = pos;
                if (pos.x < -offScreenLimitX)
                {
                    Destroy(gameObject);
                }
                break;

            case MemeSpawnType.Center:
                pos.y -= activeMoveSpeed * Time.deltaTime;
                transform.position = pos;

                float currentScale = transform.localScale.x;
                float effectiveMaxScale = centerMaxScale * activeScaleMultiplier;
                float nextScale = Mathf.Min(effectiveMaxScale, currentScale + centerScaleGrowSpeed * activeScaleMultiplier * Time.deltaTime);
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
        TryHandlePlayerHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryHandlePlayerHit(collision.collider);
        }
    }

    private void TryHandlePlayerHit(Collider2D other)
    {
        if (!isAttacking || hasHitPlayer || other == null)
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        hasHitPlayer = true;
        string patternName = SourcePattern != null ? SourcePattern.name : "None";
        int listenerCount = OnPlayerHit != null ? OnPlayerHit.GetInvocationList().Length : 0;
        Debug.Log($"[MemeAttackObstacle] 플레이어 충돌 감지! 대상={other.name}(Tag={other.tag}), 출현={spawnType}, 패턴={patternName}, OnPlayerHit구독자={listenerCount}");

        OnPlayerHit?.Invoke(this);
        onPlayerHit?.Invoke();
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            return true;
        }

        return other.GetComponent<PlayerBoxController>() != null
            || other.GetComponentInParent<PlayerBoxController>() != null;
    }

    private void HideWarningText()
    {
        if (warningText != null)
        {
            warningText.gameObject.SetActive(false);
        }
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

        if (warningText == null)
        {
            warningText = GetComponentInChildren<TextMeshPro>(true);
        }
    }
}
