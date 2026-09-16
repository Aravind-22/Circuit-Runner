using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 12f;      // how fast forward speed ramps up/down

    [SerializeField] private float turnSpeed = 180f;        // max turn rate, degrees/sec
    [SerializeField] private float turnAcceleration = 10f;  // how fast the turn rate itself ramps up/down (the "steering" feel)

    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;

    private float currentSpeed;
    private float currentTurnRate;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!GameManager.Instance.GameRunning)
            return;

        float turnInput = Input.GetAxisRaw("Horizontal");           // A/D — steering
        float moveInput = Mathf.Max(0f, Input.GetAxisRaw("Vertical")); // W only — no reverse

        // Steering: ease the turn rate toward the input's target rate,
        // then rotate around local up by that rate. This is what makes
        // it feel like steering instead of an instant-snap turn.
        float targetTurnRate = turnInput * turnSpeed;

        currentTurnRate = Mathf.Lerp(
            currentTurnRate,
            targetTurnRate,
            1f - Mathf.Exp(-turnAcceleration * Time.deltaTime));

        transform.Rotate(Vector3.up, currentTurnRate * Time.deltaTime);

        // Forward speed, eased the same way.
        float targetSpeed = moveInput * moveSpeed;

        currentSpeed = Mathf.Lerp(
            currentSpeed,
            targetSpeed,
            1f - Mathf.Exp(-acceleration * Time.deltaTime));

        verticalVelocity = controller.isGrounded ? -0.5f : verticalVelocity + gravity * Time.deltaTime;

        // transform.forward IS local Z — moving along it after rotating
        // is what makes W always mean "forward from here", steered by A/D.
        Vector3 motion = transform.forward * currentSpeed;
        motion.y = verticalVelocity;

        controller.Move(motion * Time.deltaTime);
    }

    public void ResetPosition(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        currentSpeed = 0f;
        currentTurnRate = 0f;
        verticalVelocity = 0f;
    }
}