using System;
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
public class YahoAttackObstacle : MonoBehaviour
{
    public static event Action<YahoAttackObstacle> OnPlayerHit;

    [Header("Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D obstacleCollider;

    [Header("Warning Phase (Optional)")]
    [SerializeField] private float warningDuration = 0.5f;
    [SerializeField] private float blinkSpeed = 12f;
    [SerializeField, Range(0f, 1f)] private float warningMinAlpha = 0.2f;
    [SerializeField, Range(0f, 1f)] private float warningMaxAlpha = 0.65f;

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
    [SerializeField] private UnityEvent onPlayerHit = new UnityEvent();

    private Rigidbody2D rb;
    private YahoSpawnType spawnType = YahoSpawnType.Left;
    private Color baseColor = Color.white;
    private float phaseTimer;
    private bool isAttacking;
    private bool hasHitPlayer;

    public YahoSpawnType SpawnType => spawnType;
    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        EnsureComponents();
        baseColor = spriteRenderer.color;
    }

    public void Initialize(YahoSpawnType type, bool skipWarning = false)
    {
        EnsureComponents();

        spawnType = type;
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

        if (skipWarning || warningDuration <= 0f)
        {
            BeginAttack();
            return;
        }

        if (obstacleCollider != null)
        {
            obstacleCollider.enabled = false;
        }

        SetSpriteAlpha(warningMaxAlpha);
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

        float t = Mathf.PingPong(phaseTimer * blinkSpeed, 1f);
        float alpha = Mathf.Lerp(warningMinAlpha, warningMaxAlpha, t);
        SetSpriteAlpha(alpha);

        if (phaseTimer >= warningDuration)
        {
            BeginAttack();
        }
    }

    private void BeginAttack()
    {
        isAttacking = true;
        SetSpriteAlpha(baseColor.a);

        if (obstacleCollider != null)
        {
            obstacleCollider.enabled = true;
        }
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
            Debug.Log($"[YahoAttackObstacle] 플레이어 피격 / 게임오버! (출현 위치: {spawnType})");
            OnPlayerHit?.Invoke(this);
            onPlayerHit?.Invoke();
        }
    }

    private void SetSpriteAlpha(float alpha)
    {
        Color color = baseColor;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private void EnsureComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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
    }
}
