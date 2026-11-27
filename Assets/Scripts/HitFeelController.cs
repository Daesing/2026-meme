using System.Collections;
using UnityEngine;

public class HitFeelController : MonoBehaviour
{
    public static HitFeelController Instance { get; private set; }

    [Header("Target Camera")]
    [Tooltip("비워 두면 Camera.main의 Transform을 자동으로 사용합니다.")]
    [SerializeField] private Transform cameraTransform;

    private Coroutine feedbackRoutine;
    private Transform activeShakeTarget;
    private Vector3 originalLocalPosition;
    private bool hasCachedOriginalPosition;
    private bool isInHitstop;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

        Instance = this;
    }

    private void OnDisable()
    {
        RestoreState();
    }

    private void OnDestroy()
    {
        RestoreState();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Coroutine PlayHitFeedback(float hitstopDuration = 0.08f, float shakeDuration = 0.25f, float shakeMagnitude = 0.15f)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning($"[HitFeelController] 오브젝트({name})가 비활성 상태여서 PlayHitFeedback 코루틴을 실행할 수 없습니다.");
            return null;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
            RestoreState();
        }

        Debug.Log($"[HitFeelController] PlayHitFeedback 시작: hitstop={hitstopDuration:F2}s, shake={shakeDuration:F2}s, magnitude={shakeMagnitude:F2}");
        feedbackRoutine = StartCoroutine(HitFeedbackRoutine(hitstopDuration, shakeDuration, shakeMagnitude));
        return feedbackRoutine;
    }

    private IEnumerator HitFeedbackRoutine(float hitstopDuration, float shakeDuration, float shakeMagnitude)
    {
        if (hitstopDuration > 0f)
        {
            isInHitstop = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitstopDuration);
            Time.timeScale = 1f;
            isInHitstop = false;
        }

        Transform target = ResolveCameraTransform();
        if (target == null)
        {
            Debug.LogWarning("[HitFeelController] 흔들 대상 카메라(Camera.main)를 찾지 못했습니다.");
        }
        else if (shakeDuration > 0f && shakeMagnitude > 0f)
        {
            activeShakeTarget = target;
            originalLocalPosition = target.localPosition;
            hasCachedOriginalPosition = true;

            Debug.Log($"[HitFeelController] 카메라 흔들림 루프 시작: target={target.name}, originalLocalPos={originalLocalPosition}, duration={shakeDuration:F2}s, magnitude={shakeMagnitude:F2}");

            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                if (activeShakeTarget == null)
                {
                    break;
                }

                Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
                activeShakeTarget.localPosition = originalLocalPosition + new Vector3(offset.x, offset.y, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (activeShakeTarget != null)
            {
                activeShakeTarget.localPosition = originalLocalPosition;
            }

            activeShakeTarget = null;
            hasCachedOriginalPosition = false;
        }

        feedbackRoutine = null;
    }

    private Transform ResolveCameraTransform()
    {
        if (cameraTransform != null)
        {
            return cameraTransform;
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            return mainCam.transform;
        }

        Camera attachedCam = GetComponent<Camera>();
        return attachedCam != null ? attachedCam.transform : null;
    }

    private void RestoreState()
    {
        if (isInHitstop)
        {
            Time.timeScale = 1f;
            isInHitstop = false;
        }

        if (hasCachedOriginalPosition && activeShakeTarget != null)
        {
            activeShakeTarget.localPosition = originalLocalPosition;
        }

        activeShakeTarget = null;
        hasCachedOriginalPosition = false;
    }
}
