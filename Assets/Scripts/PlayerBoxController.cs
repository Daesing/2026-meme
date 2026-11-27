using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerBoxController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Header("Hit Flash")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 0.65f);
    [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.12f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;
    private bool hasOriginalColor;
    private Coroutine hitFlashRoutine;
    private float moveInputX;
    private bool jumpRequested;
    private int jumpsRemaining;
    private bool inputEnabled = true;

    public bool InputEnabled => inputEnabled;

    public void SetInputEnabled(bool isEnabled)
    {
        inputEnabled = isEnabled;

        if (!inputEnabled)
        {
            moveInputX = 0f;
            jumpRequested = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void PlayHitFlash(float duration)
    {
        ResetHitFlash();
        CacheSpriteRenderer();

        if (spriteRenderer == null || duration <= 0f || !isActiveAndEnabled)
        {
            return;
        }

        hitFlashRoutine = StartCoroutine(HitFlashRoutine(duration));
    }

    public void ResetHitFlash()
    {
        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }

        RestoreOriginalColor();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        CacheSpriteRenderer();

        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        jumpsRemaining = maxJumps;
    }

    private void OnDisable()
    {
        ResetHitFlash();
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            moveInputX = 0f;
            jumpRequested = false;
            return;
        }

        float horizontal = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.dKey.isPressed) horizontal += 1f;
            if (keyboard.aKey.isPressed) horizontal -= 1f;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }
#else
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }
#endif

        moveInputX = horizontal;
    }

    private void FixedUpdate()
    {
        if (!inputEnabled)
        {
            return;
        }

        bool isGrounded = CheckGrounded();

        if (isGrounded && rb.linearVelocity.y <= 0.05f)
        {
            jumpsRemaining = maxJumps;
        }
        else if (!isGrounded && jumpsRemaining == maxJumps)
        {
            jumpsRemaining = Mathf.Max(0, maxJumps - 1);
        }

        Vector2 velocity = rb.linearVelocity;
        velocity.x = moveInputX * moveSpeed;

        if (jumpRequested)
        {
            jumpRequested = false;

            if (jumpsRemaining > 0)
            {
                velocity.y = jumpForce;
                jumpsRemaining--;
            }
        }

        rb.linearVelocity = velocity;
    }

    private bool CheckGrounded()
    {
        Bounds bounds = boxCollider.bounds;
        Vector2 castSize = new Vector2(bounds.size.x * 0.9f, bounds.size.y * 0.9f);
        RaycastHit2D[] hits = Physics2D.BoxCastAll(bounds.center, castSize, 0f, Vector2.down, groundCheckDistance);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider == boxCollider || hitCollider.isTrigger)
            {
                continue;
            }

            if (hits[i].distance <= 0f)
            {
                continue;
            }

            if (hits[i].normal.y > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheSpriteRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (spriteRenderer != null && !hasOriginalColor)
        {
            originalColor = spriteRenderer.color;
            hasOriginalColor = true;
        }
    }

    private void RestoreOriginalColor()
    {
        if (spriteRenderer != null && hasOriginalColor)
        {
            spriteRenderer.color = originalColor;
        }
    }

    private IEnumerator HitFlashRoutine(float duration)
    {
        float interval = Mathf.Max(0.01f, hitFlashInterval);
        float elapsed = 0f;
        bool useFlashColor = true;

        while (elapsed < duration)
        {
            if (spriteRenderer == null)
            {
                break;
            }

            spriteRenderer.color = useFlashColor ? hitFlashColor : originalColor;
            useFlashColor = !useFlashColor;

            float remaining = duration - elapsed;
            float waitTime = Mathf.Min(interval, remaining);
            if (waitTime <= 0f)
            {
                break;
            }

            float stepStart = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(waitTime);
            elapsed += Mathf.Max(Time.unscaledTime - stepStart, Time.unscaledDeltaTime);
        }

        RestoreOriginalColor();
        hitFlashRoutine = null;
    }
}
