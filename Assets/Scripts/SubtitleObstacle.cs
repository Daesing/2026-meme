using System;
using TMPro;
using UnityEngine;

public enum SubtitleTag
{
    Normal,
    Fast,
    Wall,
    Zigzag,
    Heavy
}

[RequireComponent(typeof(TextMeshPro))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class SubtitleObstacle : MonoBehaviour
{
    public static event Action<SubtitleObstacle> OnPlayerHit;

    [Header("Components")]
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField] private BoxCollider2D boxCollider;

    [Header("Size & Font")]
    [SerializeField] private Vector2 colliderPadding = new Vector2(0.15f, 0.1f);
    [SerializeField] private Vector2 minColliderSize = new Vector2(1.0f, 0.6f);
    [SerializeField] private float baseFontSize = 12f;
    [SerializeField] private float fastFontSize = 13f;
    [SerializeField] private float wallFontSize = 14f;
    [SerializeField] private float heavyFontSize = 13.5f;

    [Header("Tag Colors")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color fastTextColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color wallTextColor = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField] private Color zigzagTextColor = new Color(0.4f, 0.85f, 1f, 1f);
    [SerializeField] private Color heavyTextColor = new Color(1f, 0.65f, 0.2f, 1f);

    [Header("Movement")]
    [SerializeField] private float fallSpeed = 3.5f;
    [SerializeField] private float minFallSpeed = 0.5f;
    [SerializeField] private float zigzagAmplitude = 1.8f;
    [SerializeField] private float zigzagFrequency = 4.2f;
    [SerializeField] private float destroyY = -7.5f;

    [Header("Default Speed Scales (Fallback)")]
    [SerializeField] private float fastSpeedScale = 1.8f;
    [SerializeField] private float wallSpeedScale = 0.65f;
    [SerializeField] private float heavySpeedScale = 0.8f;

    private Rigidbody2D rb;
    private SubtitleTag tagType = SubtitleTag.Normal;
    private string subtitleText = string.Empty;
    private float startX;
    private float elapsedTime;
    private float zigzagPhase;

    public SubtitleTag TagType => tagType;
    public string SubtitleText => subtitleText;
    public float FallSpeed => fallSpeed;

    private void Awake()
    {
        EnsureComponents();
    }

    public void Initialize(
        string text,
        SubtitleTag tag,
        float baseFallSpeed,
        float speedScale = -1f,
        float randomSpeedMultiplier = 1f,
        TMP_FontAsset fontAsset = null)
    {
        EnsureComponents();

        subtitleText = text;
        tagType = tag;
        startX = transform.position.x;
        elapsedTime = 0f;
        zigzagPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        if (fontAsset != null)
        {
            textMesh.font = fontAsset;
        }

        ApplyTagStyleAndSpeed(baseFallSpeed, speedScale, randomSpeedMultiplier);
        textMesh.text = subtitleText;
        UpdateSizeToFitText();
    }

    public void UpdateSizeToFitText()
    {
        EnsureComponents();

        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        textMesh.overflowMode = TextOverflowModes.Overflow;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.ForceMeshUpdate(true, true);

        Vector2 renderedSize = textMesh.GetRenderedValues(false);
        if (!IsValidSize(renderedSize))
        {
            renderedSize = textMesh.GetPreferredValues(textMesh.text);
        }

        textMesh.rectTransform.sizeDelta = renderedSize;
        textMesh.ForceMeshUpdate(true, true);

        Vector2 refinedSize = textMesh.GetRenderedValues(false);
        if (IsValidSize(refinedSize))
        {
            renderedSize = refinedSize;
        }

        boxCollider.size = new Vector2(
            Mathf.Max(minColliderSize.x, renderedSize.x + colliderPadding.x),
            Mathf.Max(minColliderSize.y, renderedSize.y + colliderPadding.y)
        );

        Vector2 offset = (Vector2)textMesh.textBounds.center;
        if (textMesh.transform != transform)
        {
            offset += (Vector2)textMesh.transform.localPosition;
        }

        boxCollider.offset = IsFiniteVector(offset) ? offset : Vector2.zero;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        Vector3 pos = transform.position;
        pos.y -= fallSpeed * Time.deltaTime;

        if (tagType == SubtitleTag.Zigzag)
        {
            pos.x = startX + Mathf.Sin(elapsedTime * zigzagFrequency + zigzagPhase) * zigzagAmplitude;
        }

        transform.position = pos;

        if (pos.y < destroyY)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[SubtitleObstacle] 플레이어 피격! [{tagType}] (속도: {fallSpeed:F2}) {subtitleText}");
            OnPlayerHit?.Invoke(this);
            Destroy(gameObject);
        }
    }

    private void ApplyTagStyleAndSpeed(float baseFallSpeed, float speedScale, float randomSpeedMultiplier)
    {
        float resolvedSpeedScale;

        switch (tagType)
        {
            case SubtitleTag.Fast:
                resolvedSpeedScale = speedScale > 0f ? speedScale : fastSpeedScale;
                textMesh.color = fastTextColor;
                textMesh.fontStyle = FontStyles.Bold;
                textMesh.fontSize = fastFontSize;
                break;

            case SubtitleTag.Wall:
                resolvedSpeedScale = speedScale > 0f ? speedScale : wallSpeedScale;
                textMesh.color = wallTextColor;
                textMesh.fontStyle = FontStyles.Bold;
                textMesh.fontSize = wallFontSize;
                break;

            case SubtitleTag.Zigzag:
                resolvedSpeedScale = speedScale > 0f ? speedScale : 1f;
                textMesh.color = zigzagTextColor;
                textMesh.fontStyle = FontStyles.Normal;
                textMesh.fontSize = baseFontSize;
                break;

            case SubtitleTag.Heavy:
                resolvedSpeedScale = speedScale > 0f ? speedScale : heavySpeedScale;
                textMesh.color = heavyTextColor;
                textMesh.fontStyle = FontStyles.Bold;
                textMesh.fontSize = heavyFontSize;
                break;

            default:
                resolvedSpeedScale = speedScale > 0f ? speedScale : 1f;
                textMesh.color = normalTextColor;
                textMesh.fontStyle = FontStyles.Normal;
                textMesh.fontSize = baseFontSize;
                break;
        }

        float safeRandomMultiplier = Mathf.Max(0.1f, randomSpeedMultiplier);
        fallSpeed = Mathf.Max(minFallSpeed, baseFallSpeed * resolvedSpeedScale * safeRandomMultiplier);
    }

    private void EnsureComponents()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }
        boxCollider.isTrigger = true;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMeshPro>();
            }
            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMeshPro>();
            }
        }
        textMesh.sortingOrder = 5;
    }

    private static bool IsValidSize(Vector2 size)
    {
        return size.x > 0f && size.y > 0f && IsFiniteVector(size);
    }

    private static bool IsFiniteVector(Vector2 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y);
    }
}
