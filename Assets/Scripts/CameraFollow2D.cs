using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target = null;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private void Awake()
    {
        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position;
        transform.position = new Vector3(
            targetPosition.x + offset.x,
            targetPosition.y + offset.y,
            offset.z
        );
    }
}
