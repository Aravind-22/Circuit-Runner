using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 15f;
    [SerializeField] private float pitch = 30f;   // tilt down from horizontal
    [SerializeField] private float yaw = 45f;     // rotation around Y — classic isometric look
    [SerializeField] private float followSmoothTime = 0.15f;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1f, 0f); // point on the player to frame (roughly chest height)
 
    private Vector3 velocity;
    private Camera cam;
 
    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
 
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
 
    private void LateUpdate()
    {
        if (target == null)
            return;
 
        // Rotation never changes — that's what makes it read as isometric
        // instead of a chase camera. Only position translates.
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = rotation * Vector3.back;
 
        Vector3 desiredPosition = target.position + pivotOffset + direction * distance;
 
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            followSmoothTime);
    }
}