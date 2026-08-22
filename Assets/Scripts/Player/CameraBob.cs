using UnityEngine;

public class CameraBob : MonoBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Weapon weapon;
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float smoothSpeed = 10f;

    private Vector3 initialPosition;
    private float bobTimer;

    private void Awake()
    {
        initialPosition = transform.localPosition;
    }

    private void Update()
    {
        bool isMoving = IsMovingOnGround();
        bool isAiming = weapon != null && weapon.IsAiming;

        if (isMoving && !isAiming)
        {
            bobTimer += Time.deltaTime * bobFrequency;

            float verticalOffset = Mathf.Sin(bobTimer) * bobAmplitude;
            float horizontalOffset = Mathf.Cos(bobTimer * 0.5f) * bobAmplitude * 0.5f;

            Vector3 targetPosition = initialPosition + new Vector3(horizontalOffset, verticalOffset, 0f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, smoothSpeed * Time.deltaTime);
        }
        else
        {
            bobTimer = 0f;
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialPosition, smoothSpeed * Time.deltaTime);
        }
    }

    private bool IsMovingOnGround()
    {
        if (!characterController.isGrounded) return false;

        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        return horizontalVelocity.magnitude > 0.1f;
    }
}