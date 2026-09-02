using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpectator : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform spectatorCamera;

    [Header("Objetos que se desactivan al espectar (solo dueño)")]
    [SerializeField] private GameObject weaponCameraObject;

    [Header("Cuerpo en tercera persona (visible para todos)")]
    [SerializeField] private GameObject polygonFPSObject;

    [Header("Cámara espectador")]
    [SerializeField] private Vector3 cameraOffset =
        new Vector3(0.8f, 1.6f, -2.5f);

    [SerializeField] private float cameraFollowSpeed = 8f;

    [SerializeField] private float lookHeight = 1.2f;

    [Header("Respawn")]
    [SerializeField] private float respawnPositionTolerance = 0.5f;
    [SerializeField] private float maxRespawnWaitTime = 3f;

    private PlayerMovement playerMovement;
    private PlayerLook playerLook;
    private Weapon weapon;
    private CharacterController characterController;

    private MultiplayerPlayerSpawnAssigner spawnAssigner;

    private Transform targetPlayer;

    private bool isSpectating;
    private bool restoringFromSpectator;

    private Coroutine restoreCoroutine;

    private Vector3 normalCameraLocalPosition;
    private Quaternion normalCameraLocalRotation;

    private Transform originalCameraParent;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth =
                GetComponent<PlayerHealth>();
        }

        playerMovement =
            GetComponent<PlayerMovement>();

        playerLook =
            GetComponentInChildren<PlayerLook>(true);

        weapon =
            GetComponentInChildren<Weapon>(true);

        characterController =
            GetComponent<CharacterController>();

        spawnAssigner =
            GetComponent<MultiplayerPlayerSpawnAssigner>();

        if (spectatorCamera == null)
        {
            Camera cam =
                GetComponentInChildren<Camera>(true);

            if (cam != null)
            {
                spectatorCamera =
                    cam.transform;
            }
        }

        if (spectatorCamera != null)
        {
            normalCameraLocalPosition =
                spectatorCamera.localPosition;

            normalCameraLocalRotation =
                spectatorCamera.localRotation;

            originalCameraParent =
                spectatorCamera.parent;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnStateChanged +=
            OnStateChanged;

        if (spectatorCamera != null)
        {
            normalCameraLocalPosition =
                spectatorCamera.localPosition;

            normalCameraLocalRotation =
                spectatorCamera.localRotation;

            if (originalCameraParent == null)
            {
                originalCameraParent =
                    spectatorCamera.parent;
            }
        }

        ApplyBodyVisibility(
            playerHealth.State.Value
        );

        if (IsOwner)
        {
            ApplyOwnerState(
                playerHealth.State.Value,
                playerHealth.State.Value
            );
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.OnStateChanged -=
                OnStateChanged;
        }

        if (restoreCoroutine != null)
        {
            StopCoroutine(restoreCoroutine);
            restoreCoroutine = null;
        }
    }

    private void OnStateChanged(
        PlayerHealth.PlayerState previousState,
        PlayerHealth.PlayerState newState
    )
    {
        ApplyBodyVisibility(newState);

        if (!IsOwner)
            return;

        ApplyOwnerState(
            previousState,
            newState
        );
    }

    // =========================================================
    // VISIBILIDAD DEL CUERPO
    // =========================================================

    private void ApplyBodyVisibility(
        PlayerHealth.PlayerState state
    )
    {
        if (polygonFPSObject == null)
            return;

        if (
            state ==
            PlayerHealth.PlayerState.Alive
        )
        {
            polygonFPSObject.SetActive(true);
        }
        else if (
            state ==
                PlayerHealth.PlayerState.Spectating ||
            state ==
                PlayerHealth.PlayerState.Dead
        )
        {
            polygonFPSObject.SetActive(false);
        }

        // Downed mantiene el cuerpo visible.
    }

    // =========================================================
    // ESTADO DEL DUEÑO
    // =========================================================

    private void ApplyOwnerState(
        PlayerHealth.PlayerState previousState,
        PlayerHealth.PlayerState newState
    )
    {
        if (
            newState ==
            PlayerHealth.PlayerState.Spectating
        )
        {
            StartSpectating();
        }
        else if (
            newState ==
            PlayerHealth.PlayerState.Dead
        )
        {
            FreezeAsDead();
        }
        else if (
            newState ==
                PlayerHealth.PlayerState.Alive &&
            previousState ==
                PlayerHealth.PlayerState.Spectating
        )
        {
            StartRestoringFromRespawn();
        }
    }

    // =========================================================
    // COMENZAR ESPECTADOR
    // =========================================================

    private void StartSpectating()
    {
        if (isSpectating)
            return;

        if (restoreCoroutine != null)
        {
            StopCoroutine(
                restoreCoroutine
            );

            restoreCoroutine = null;
        }

        restoringFromSpectator = false;
        isSpectating = true;

        targetPlayer = null;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerLook != null)
        {
            playerLook.enabled = false;
        }

        if (weapon != null)
        {
            weapon.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (weaponCameraObject != null)
        {
            weaponCameraObject.SetActive(false);
        }

        // Sacamos la cámara de la jerarquía del player.
        if (spectatorCamera != null)
        {
            spectatorCamera.SetParent(
                null,
                true
            );

            spectatorCamera.gameObject.SetActive(true);

            Camera cam =
                spectatorCamera.GetComponent<Camera>();

            if (cam != null)
            {
                cam.enabled = true;
            }
        }

        FindTargetPlayer();

        Debug.Log(
            "[PlayerSpectator] " +
            gameObject.name +
            " comenzó a espectar."
        );
    }

    // =========================================================
    // MUERTO
    // =========================================================

    private void FreezeAsDead()
    {
        if (restoreCoroutine != null)
        {
            StopCoroutine(
                restoreCoroutine
            );

            restoreCoroutine = null;
        }

        restoringFromSpectator = false;
        isSpectating = false;
        targetPlayer = null;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerLook != null)
        {
            playerLook.enabled = false;
        }

        if (weapon != null)
        {
            weapon.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        Debug.Log(
            "[PlayerSpectator] " +
            gameObject.name +
            " quedó ELIMINADO."
        );
    }

    // =========================================================
    // RESTAURACIÓN
    // =========================================================

    private void StartRestoringFromRespawn()
    {
        if (restoreCoroutine != null)
        {
            StopCoroutine(
                restoreCoroutine
            );
        }

        restoreCoroutine =
            StartCoroutine(
                RestoreAfterRespawn()
            );
    }

    private IEnumerator RestoreAfterRespawn()
    {
        restoringFromSpectator = true;
        isSpectating = false;
        targetPlayer = null;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerLook != null)
        {
            playerLook.enabled = false;
        }

        if (weapon != null)
        {
            weapon.enabled = false;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (weaponCameraObject != null)
        {
            weaponCameraObject.SetActive(false);
        }

        if (spectatorCamera != null)
        {
            spectatorCamera.gameObject.SetActive(true);

            Camera cam =
                spectatorCamera.GetComponent<Camera>();

            if (cam != null)
            {
                cam.enabled = true;
            }
        }

        // -----------------------------------------------------
        // ESPERAR AL SPAWN
        // -----------------------------------------------------

        float elapsed = 0f;

        while (
            elapsed <
            maxRespawnWaitTime
        )
        {
            if (spawnAssigner == null)
                break;

            if (
                spawnAssigner.IsAtAssignedSpawn(
                    respawnPositionTolerance
                )
            )
            {
                break;
            }

            elapsed +=
                Time.deltaTime;

            yield return null;
        }

        yield return null;

        // -----------------------------------------------------
        // VOLVER A PONER LA CÁMARA EN SU PADRE
        // -----------------------------------------------------

        if (
            spectatorCamera != null &&
            originalCameraParent != null
        )
        {
            spectatorCamera.SetParent(
                originalCameraParent,
                false
            );

            spectatorCamera.localPosition =
                normalCameraLocalPosition;

            spectatorCamera.localRotation =
                normalCameraLocalRotation;
        }

        // -----------------------------------------------------
        // CHARACTER CONTROLLER
        // -----------------------------------------------------

        if (characterController != null)
        {
            characterController.enabled =
                true;
        }

        // -----------------------------------------------------
        // MOVIMIENTO
        // -----------------------------------------------------

        if (playerMovement != null)
        {
            playerMovement.enabled = true;

            playerMovement.ResetMovementState();
        }

        // -----------------------------------------------------
        // LOOK
        // -----------------------------------------------------

        if (playerLook != null)
        {
            playerLook.enabled = true;
        }

        // -----------------------------------------------------
        // WEAPON
        // -----------------------------------------------------

        if (weapon != null)
        {
            weapon.enabled = true;
        }

        // -----------------------------------------------------
        // WEAPON CAMERA
        // -----------------------------------------------------

        if (weaponCameraObject != null)
        {
            weaponCameraObject.SetActive(true);
        }

        restoringFromSpectator = false;

        restoreCoroutine = null;

        Debug.Log(
            "[PlayerSpectator] " +
            gameObject.name +
            " terminó el respawn."
        );
    }

    // =========================================================
    // BUSCAR JUGADOR VIVO
    // =========================================================

    private void FindTargetPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        float closestDistance =
            Mathf.Infinity;

        Transform closestPlayer = null;

        foreach (
            NetworkClient client
            in NetworkManager.Singleton
                .ConnectedClientsList
        )
        {
            if (client.PlayerObject == null)
                continue;

            if (
                client.PlayerObject ==
                NetworkObject
            )
            {
                continue;
            }

            PlayerHealth otherHealth =
                client.PlayerObject
                    .GetComponent<PlayerHealth>();

            if (otherHealth == null)
                continue;

            if (!otherHealth.IsAlive)
                continue;

            Transform otherPlayer =
                client.PlayerObject.transform;

            float distance =
                Vector3.Distance(
                    transform.position,
                    otherPlayer.position
                );

            if (
                distance <
                closestDistance
            )
            {
                closestDistance =
                    distance;

                closestPlayer =
                    otherPlayer;
            }
        }

        targetPlayer =
            closestPlayer;

        if (targetPlayer != null)
        {
            Debug.Log(
                "[PlayerSpectator] " +
                "Espectando a: " +
                targetPlayer.name
            );
        }
        else
        {
            Debug.LogWarning(
                "[PlayerSpectator] " +
                "No hay jugadores vivos para espectar."
            );
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!IsOwner)
            return;

        if (!isSpectating)
            return;

        if (targetPlayer == null)
        {
            FindTargetPlayer();
            return;
        }

        PlayerHealth targetHealth =
            targetPlayer.GetComponent<PlayerHealth>();

        if (
            targetHealth == null ||
            !targetHealth.IsAlive
        )
        {
            targetPlayer = null;

            FindTargetPlayer();
        }
    }

    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (!IsOwner)
            return;

        if (isSpectating)
        {
            if (targetPlayer == null)
            {
                FindTargetPlayer();
            }

            if (targetPlayer != null)
            {
                FollowTarget();
            }

            return;
        }

        if (restoringFromSpectator)
        {
            if (
                spectatorCamera != null &&
                originalCameraParent != null
            )
            {
                spectatorCamera.localPosition =
                    normalCameraLocalPosition;

                spectatorCamera.localRotation =
                    normalCameraLocalRotation;
            }
        }
    }

    // =========================================================
    // SEGUIR JUGADOR
    // =========================================================

    private void FollowTarget()
    {
        if (
            spectatorCamera == null ||
            targetPlayer == null
        )
        {
            return;
        }

        Vector3 desiredPosition =
            targetPlayer.position +
            targetPlayer.TransformDirection(
                cameraOffset
            );

        spectatorCamera.position =
            Vector3.Lerp(
                spectatorCamera.position,
                desiredPosition,
                cameraFollowSpeed *
                Time.deltaTime
            );

        Vector3 lookTarget =
            targetPlayer.position +
            Vector3.up *
            lookHeight;

        Vector3 direction =
            lookTarget -
            spectatorCamera.position;

        if (
            direction.sqrMagnitude >
            0.001f
        )
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(
                    direction
                );

            spectatorCamera.rotation =
                Quaternion.Lerp(
                    spectatorCamera.rotation,
                    desiredRotation,
                    cameraFollowSpeed *
                    Time.deltaTime
                );
        }
    }
}