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
    private YahoSpawnType spawnType = YahoSpawnType.Left;
    private float activeMoveSpeed = 10f;
    private Color baseColor = Color.white;
    private bool baseColorCached;
    private Action onAttackBeginCallback;
    private bool isAttacking;
    private bool hasHitPlayer;

    public YahoSpawnType SpawnType => spawnType;
    public float ActiveMoveSpeed => activeMoveSpeed;
    public bool IsAttacking => isAttacking;
    public float WarningDuration => warningDuration;
    public int WarningBlinkCount => warningBlinkCount;
    public TextMeshPro WarningText => warningText;

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

    public void Initialize(YahoSpawnType type, Action onAttackBegin = null)
    {
        float defaultSpeed = type == YahoSpawnType.Center ? centerDownSpeed : horizontalDashSpeed;
        Initialize(type, defaultSpeed, onAttackBegin);
    }

    public void Initialize(YahoSpawnType type, float moveSpeed, Action onAttackBegin = null)
    {
        EnsureComponents();

        spawnType = type;
        activeMoveSpeed = Mathf.Max(0f, moveSpeed);
        onAttackBeginCallback = onAttackBegin;
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

        switch (spawnType)
        {
            case YahoSpawnType.Left:
                pos.x += activeMoveSpeed * Time.deltaTime;
                transform.position = pos;
                if (pos.x > offScreenLimitX)
                {
                    Destroy(gameObject);
                }
                break;

            case YahoSpawnType.Right:
                pos.x -= activeMoveSpeed * Time.deltaTime;
                transform.position = pos;
                if (pos.x < -offScreenLimitX)
                {
                    Destroy(gameObject);
                }
                break;

            case YahoSpawnType.Center:
                pos.y -= activeMoveSpeed * Time.deltaTime;
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
