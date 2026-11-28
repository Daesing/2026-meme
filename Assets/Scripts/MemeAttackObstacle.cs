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
    public Collider2D ObstacleCollider => obstacleCollider != null ? obstacleCollider : GetComponent<Collider2D>();

    public void SetSourcePattern(MemePatternData pattern)
    {
        SourcePattern = pattern;
    }

    public void SetSpawnPosition(Vector3 position)
    {
        transform.position = position;
    }

    public void SetSpawnCoordinates(float? x = null, float? y = null)
    {
        Vector3 pos = transform.position;
        if (x.HasValue)
        {
            pos.x = x.Value;
        }

        if (y.HasValue)
        {
            pos.y = y.Value;
        }

        transform.position = pos;
    }

    public void ApplySpeedMultiplier(float speedMultiplier)
    {
        float effectiveMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
        activeMoveSpeed = Mathf.Max(0f, activeMoveSpeed * effectiveMultiplier);
    }

    public bool TryGetCurrentColliderBounds(out Vector3 boundsCenter, out Vector3 boundsSize)
    {
        Collider2D col = ObstacleCollider;
        if (col != null && gameObject.scene.IsValid() && gameObject.activeInHierarchy && col.enabled)
        {
            Physics2D.SyncTransforms();
            Bounds colliderBounds = col.bounds;
            if (colliderBounds.size.x > 0f || colliderBounds.size.y > 0f)
            {
                boundsCenter = colliderBounds.center;
                boundsSize = colliderBounds.size;
                return true;
            }
        }

        boundsCenter = transform.position;
        boundsSize = Vector3.zero;
        return false;
    }

    public bool TryGetTrajectoryBounds(
        Vector3 spawnPos,
        MemeSpawnType type,
        bool customDirection,
        float scaleMultiplier,
        Sprite overrideSprite,
        out Vector3 boundsCenter,
        out Vector3 boundsSize)
    {
        Collider2D col = ObstacleCollider;
        if (col == null)
        {
            boundsCenter = spawnPos;
            boundsSize = Vector3.zero;
            return false;
        }

        if (gameObject.scene.IsValid() && gameObject.activeInHierarchy && col.enabled)
        {
            Physics2D.SyncTransforms();
            Bounds colliderBounds = col.bounds;
            if (colliderBounds.size.x > 0f || colliderBounds.size.y > 0f)
            {
                Vector3 centerOffset = colliderBounds.center - transform.position;
                boundsCenter = spawnPos + centerOffset;
                boundsSize = colliderBounds.size;
                return true;
            }
        }

        if (!TryGetLocalColliderBounds(col, overrideSprite, out Vector2 localCenter, out Vector2 localSize))
        {
            boundsCenter = spawnPos;
            boundsSize = Vector3.zero;
            return false;
        }

        float scale = GetInitialScale(type, customDirection, scaleMultiplier);
        boundsCenter = new Vector3(
            spawnPos.x + localCenter.x * scale,
            spawnPos.y + localCenter.y * scale,
            spawnPos.z
        );
        boundsSize = new Vector3(
            Mathf.Abs(localSize.x * scale),
            Mathf.Abs(localSize.y * scale),
            0f
        );
        return boundsSize.x > 0f || boundsSize.y > 0f;
    }

    private float GetInitialScale(MemeSpawnType type, bool customDirection, float scaleMultiplier)
    {
        float effectiveScaleMultiplier = scaleMultiplier > 0f ? scaleMultiplier : 1f;
        if (customDirection || type != MemeSpawnType.Center)
        {
            return horizontalScale * effectiveScaleMultiplier;
        }

        return centerStartScale * effectiveScaleMultiplier;
    }

    private static bool TryGetLocalColliderBounds(
        Collider2D col,
        Sprite overrideSprite,
        out Vector2 localCenter,
        out Vector2 localSize)
    {
        if (overrideSprite != null && col is BoxCollider2D)
        {
            Vector2 spriteSize = overrideSprite.bounds.size;
            localSize = new Vector2(
                Mathf.Max(0.5f, spriteSize.x * 0.75f),
                Mathf.Max(0.5f, spriteSize.y * 0.75f)
            );
            localCenter = overrideSprite.bounds.center;
            return true;
        }

        if (col is BoxCollider2D box)
        {
            localCenter = box.offset;
            localSize = box.size;
            return true;
        }

        if (col is CapsuleCollider2D capsule)
        {
            localCenter = capsule.offset;
            float sizeX = Mathf.Abs(capsule.size.x);
            float sizeY = Mathf.Abs(capsule.size.y);
            localSize = capsule.direction == CapsuleDirection2D.Vertical
                ? new Vector2(sizeX, Mathf.Max(sizeX, sizeY))
                : new Vector2(Mathf.Max(sizeX, sizeY), sizeY);
            return true;
        }

        if (col is CircleCollider2D circle)
        {
            localCenter = circle.offset;
            float diameter = Mathf.Abs(circle.radius) * 2f;
            localSize = new Vector2(diameter, diameter);
            return true;
        }

        if (col is PolygonCollider2D polygon && polygon.pathCount > 0)
        {
            bool hasPoint = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;

            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                Vector2[] points = polygon.GetPath(pathIndex);
                if (points == null)
                {
                    continue;
                }

                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 point = points[i];
                    if (!hasPoint)
                    {
                        min = point;
                        max = point;
                        hasPoint = true;
                    }
                    else
                    {
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }
            }

            if (hasPoint)
            {
                localCenter = polygon.offset + (min + max) * 0.5f;
                localSize = max - min;
                return true;
            }
        }

        if (col is EdgeCollider2D edge && edge.pointCount > 0)
        {
            Vector2[] points = edge.points;
            Vector2 min = points[0];
            Vector2 max = points[0];
            for (int i = 1; i < points.Length; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            localCenter = edge.offset + (min + max) * 0.5f;
            localSize = max - min;
            return true;
        }

        localCenter = Vector2.zero;
        localSize = Vector2.zero;
        return false;
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
        float baseMoveSpeed,
        float speedMultiplier,
        Vector2 moveDirection,
        float scaleMultiplier = 1f,
        Action onAttackBegin = null,
        MemePatternData sourcePattern = null)
    {
        float effectiveSpeedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
        float finalMoveSpeed = Mathf.Max(0f, baseMoveSpeed) * effectiveSpeedMultiplier;
        Initialize(type, finalMoveSpeed, moveDirection, scaleMultiplier, onAttackBegin, sourcePattern);
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
