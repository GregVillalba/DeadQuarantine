using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 2f;

    private float timeElapsed = 1f;

    private PlayerControls controls;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion startRotation; // rotación real al momento de togglear
    private bool isOpen;
    private bool isPlayerLooking;

    private void Awake()
    {
        controls = new PlayerControls();
        closedRotation = doorLeaf.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        startRotation = closedRotation;
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();

    private void Update()
    {
        CheckIfPlayerIsLooking();

        if (controls.Player.Interact.triggered && isPlayerLooking)
        {
            isOpen = !isOpen;
            startRotation = doorLeaf.localRotation; // punto de partida REAL
            timeElapsed = 0f;
        }

        RotateDoor();
    }

    private void CheckIfPlayerIsLooking()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            isPlayerLooking = hit.transform == doorLeaf || hit.transform.IsChildOf(transform);
        }
        else
        {
            isPlayerLooking = false;
        }
    }

    private void RotateDoor()
    {
        if (timeElapsed >= 1f) return;

        timeElapsed += Time.deltaTime * openSpeed;

        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        doorLeaf.localRotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed);
    }
}