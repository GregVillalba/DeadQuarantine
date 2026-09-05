using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSway : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Weapon weapon; // el arma actualmente activa (usa SetWeapon como HUDController)

    [Header("Sway por mouse")]
    [SerializeField] private float swayAmount = 0.015f;
    [SerializeField] private float maxSwayAmount = 0.05f;
    [SerializeField] private float rotationSwayAmount = 3f;
    [SerializeField] private float maxRotationSway = 5f;
    [SerializeField] private float swaySmooth = 8f;

    [Header("Bobbing al caminar")]
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmount = 0.012f;

    [Header("Recoil")]
    [SerializeField] private float recoilKickBack = 0.04f;   // el arma retrocede en Z
    [SerializeField] private float recoilKickUp = 2.5f;      // el arma "levanta la boca" en X (grados)
    [SerializeField] private float recoilRandomSide = 1f;    // pequeño giro random en Y (grados)
    [SerializeField] private float recoilRecoverySpeed = 10f;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private float bobTimer;

    private Vector3 recoilPositionOffset;
    private Vector3 recoilRotationOffset;

    private int lastKnownAmmo = -1;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        DetectShotForRecoil();

        Vector3 swayOffset = CalculateMouseSway(out Vector3 rotSway);
        Vector3 bobOffset = CalculateWalkBob();

        // Recupera el recoil hacia 0 con el tiempo.
        recoilPositionOffset = Vector3.Lerp(recoilPositionOffset, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);
        recoilRotationOffset = Vector3.Lerp(recoilRotationOffset, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);

        Vector3 targetPosition = initialLocalPosition + swayOffset + bobOffset + recoilPositionOffset;
        Quaternion targetRotation = initialLocalRotation * Quaternion.Euler(rotSway + recoilRotationOffset);

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, swaySmooth * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, swaySmooth * Time.deltaTime);
    }

    // =========================================================
    // SWAY POR MOUSE
    // =========================================================

    private Vector3 CalculateMouseSway(out Vector3 rotationOffset)
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        float swayX = Mathf.Clamp(-mouseDelta.x * swayAmount * 0.01f, -maxSwayAmount, maxSwayAmount);
        float swayY = Mathf.Clamp(-mouseDelta.y * swayAmount * 0.01f, -maxSwayAmount, maxSwayAmount);

        float rotY = Mathf.Clamp(mouseDelta.x * rotationSwayAmount * 0.01f, -maxRotationSway, maxRotationSway);
        float rotX = Mathf.Clamp(-mouseDelta.y * rotationSwayAmount * 0.01f, -maxRotationSway, maxRotationSway);

        rotationOffset = new Vector3(rotX, rotY, 0f);

        return new Vector3(swayX, swayY, 0f);
    }

    // =========================================================
    // BOBBING AL CAMINAR
    // =========================================================

    private Vector3 CalculateWalkBob()
    {
        if (characterController == null)
            return Vector3.zero;

        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);

        if (characterController.isGrounded && horizontalVelocity.magnitude > 0.1f)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            float offset = Mathf.Sin(bobTimer) * bobAmount;
            return new Vector3(0f, offset, 0f);
        }

        bobTimer = 0f;
        return Vector3.zero;
    }

    // =========================================================
    // RECOIL (detecta disparo comparando munición)
    // =========================================================

    private void DetectShotForRecoil()
    {
        if (weapon == null)
            return;

        if (lastKnownAmmo == -1)
        {
            lastKnownAmmo = weapon.CurrentAmmo;
            return;
        }

        if (weapon.CurrentAmmo < lastKnownAmmo)
            ApplyRecoilKick();

        lastKnownAmmo = weapon.CurrentAmmo;
    }

    private void ApplyRecoilKick()
    {
        recoilPositionOffset += new Vector3(0f, 0f, -recoilKickBack);

        float randomSide = Random.Range(-recoilRandomSide, recoilRandomSide);
        recoilRotationOffset += new Vector3(-recoilKickUp, randomSide, 0f);
    }

    // Llamado por WeaponSwitcher al cambiar de arma (igual que HUDController/WeaponAnimationEvents).
    public void SetWeapon(Weapon newWeapon)
    {
        weapon = newWeapon;
        lastKnownAmmo = newWeapon != null ? newWeapon.CurrentAmmo : -1;
    }
}