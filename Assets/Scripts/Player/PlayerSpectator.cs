using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.Netcode;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using TMPro;

public class PlayerSpectator : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    private readonly List<PlayerHealth> targets = new();
    [SerializeField] private Transform spectatorCamera;

    [Header("Objetos que se desactivan al espectar (solo dueño)")]
    [SerializeField] private GameObject weaponCameraObject;

    [Header("Cuerpo en tercera persona (visible para todos)")]
    [SerializeField] private GameObject polygonFPSObject;

    [Header("Cámara espectador")]
    [SerializeField] private Vector3 cameraOffset =
        new Vector3(0.8f, 1.6f, -2.5f);
        
    [Header("Órbita de cámara")]
    [SerializeField] private float orbitDistance = 3.5f;
    [SerializeField] private float orbitHeight = 1.4f;
    [SerializeField] private float orbitSensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Colisión de cámara")]
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minCollisionDistance = 0.5f;
    [SerializeField] private LayerMask collisionMask = ~0; // Configurar en el Inspector, ver nota abajo

    private float orbitYaw;
    private float orbitPitch = 15f;

    private GameObject spectatorHud;
    private TextMeshProUGUI spectatorLabel;
    private bool snapToTarget;

    public bool IsViewingLivingPlayer => isSpectating && spectatorCamera &&
        targetPlayer && targetPlayer.TryGetComponent(out PlayerHealth target) && target.IsAlive;

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
    private PauseController pauseController;

    private Transform targetPlayer;

    private bool isSpectating;
    private bool restoringFromSpectator;

    private Coroutine restoreCoroutine;

    private Vector3 normalCameraLocalPosition;
    private Quaternion normalCameraLocalRotation;

    private Transform originalCameraParent;
    private readonly List<Behaviour> suspendedCameraEffects = new(2);

    private void SuspendCameraEffect<T>() where T : Behaviour
    {
        if (!spectatorCamera.TryGetComponent(out T effect) || !effect.enabled) return;
        effect.enabled = false;
        suspendedCameraEffects.Add(effect);
    }

    private void RestoreCameraEffects()
    {
        foreach (var effect in suspendedCameraEffects)
            if (effect) effect.enabled = true;
        suspendedCameraEffects.Clear();
    }

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

        pauseController =
            GetComponent<PauseController>();

        if (pauseController == null)
            pauseController =
                GetComponentInParent<PauseController>();

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

        playerHealth.OnStateChanged += OnStateChanged;
        if (DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject)) return;

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
        if (spectatorCamera && spectatorCamera.parent == null)
        {
            if (originalCameraParent && originalCameraParent.gameObject.activeInHierarchy)
                spectatorCamera.SetParent(originalCameraParent, false);
            else Destroy(spectatorCamera.gameObject);
        }
        isSpectating = false;
        targetPlayer = null;
        RestoreCameraEffects();
        if (spectatorHud) Destroy(spectatorHud);
    }

    private void OnStateChanged(
        PlayerHealth.PlayerState previousState,
        PlayerHealth.PlayerState newState
    )
    {
        if (DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject)) return;
        if (!PvPvECombat.Active || newState != PlayerHealth.PlayerState.Dead)
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
            newState == PlayerHealth.PlayerState.Spectating ||
            (newState == PlayerHealth.PlayerState.Dead &&
             DeadQuarantine.PvPvE.Networking.PvPvECombat.Active &&
             DeadQuarantine.PvPvE.Networking.PvPvECombat.Active.IsPlaying)
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
            (previousState == PlayerHealth.PlayerState.Spectating ||
             previousState == PlayerHealth.PlayerState.Dead)
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
            // CameraBobbing writes local Y every frame. Once detached, that is world height.
            SuspendCameraEffect<CameraBobbing>();
            SuspendCameraEffect<CameraRecoil>();
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

        if (targetPlayer != null)
        {
            orbitYaw = targetPlayer.eulerAngles.y;
            orbitPitch = 15f;
        }

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

        RestoreCameraEffects();

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

    private void FindTargetPlayer(int direction = 0)
    {
        targets.Clear();
        if (NetworkManager && NetworkManager.SpawnManager != null)
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
                if (obj && obj != NetworkObject && obj.IsPlayerObject &&
                    obj.TryGetComponent(out PlayerHealth candidate) && candidate.IsAlive)
                    targets.Add(candidate);

        if (targets.Count == 0)
        {
            targetPlayer = null;
            return;
        }

        targets.Sort((a,b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        int current = targets.FindIndex(player => player.transform == targetPlayer);
        int index = current < 0 ? (direction < 0 ? targets.Count - 1 : 0) :
            (current + direction + targets.Count) % targets.Count;
        var next = targets[index].transform;
        if (next == targetPlayer) return;
        targetPlayer = next;
        snapToTarget = true;
        orbitYaw = next.eulerAngles.y;
        orbitPitch = 15f;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        bool show = isSpectating && !PlayerInputGate.MatchLost &&
            !PauseController.LocalPlayerPaused && !(PvPvECombat.Active && PvPvECombat.Active.ScoreLocked.Value);
        if (!show)
        {
            if (spectatorHud) spectatorHud.SetActive(false);
            return;
        }

        var mouse = Mouse.current;
        int direction = mouse != null && mouse.leftButton.wasPressedThisFrame ? -1 :
                        mouse != null && mouse.rightButton.wasPressedThisFrame ? 1 : 0;
        if (!IsViewingLivingPlayer || direction != 0) FindTargetPlayer(direction);
        if (PvPvECombat.Active && spectatorCamera)
        {
            bool hasTarget = IsViewingLivingPlayer;
            if (spectatorCamera.TryGetComponent(out Camera camera)) camera.enabled = hasTarget;
            if (spectatorCamera.TryGetComponent(out AudioListener listener)) listener.enabled = hasTarget;
        }
        if (IsViewingLivingPlayer && Application.isFocused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        UpdateSpectatorHud();
    }

    private void UpdateSpectatorHud()
    {
        if (!spectatorHud)
        {
            var canvas = PvPvEUIFactory.Canvas(gameObject, "HUD Espectador", 510);
            spectatorHud = canvas.gameObject;
            var panel = PvPvEUIFactory.Panel(canvas.transform, "Controles", 0, 0, 650, 64, .85f);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
            panel.pivot = new Vector2(.5f, 0);
            panel.anchoredPosition = new Vector2(0, 62);
            spectatorLabel = PvPvEUIFactory.Text(panel, "Objetivo", "", 12, 4, 626, 27, null, 19);
            spectatorLabel.alignment = TextAlignmentOptions.Center;
            var hint = PvPvEUIFactory.Text(panel, "Navegación",
                "CLIC IZQUIERDO  ANTERIOR    /    CLIC DERECHO  SIGUIENTE    /    MOUSE  GIRAR",
                12, 33, 626, 25, null, 14);
            hint.alignment = TextAlignmentOptions.Center;
        }
        spectatorHud.SetActive(true);
        string name = targetPlayer ? PlayerNameplate.GetNombreCliente(targetPlayer.GetComponent<PlayerHealth>().OwnerClientId) : null;
        spectatorLabel.text = targetPlayer ? "ESPECTANDO · "+name : "NO HAY JUGADORES VIVOS";
    }

    // =========================================================
    // LATE UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (!IsOwner || PlayerInputGate.MatchLost ||
            (PvPvECombat.Active && PvPvECombat.Active.ScoreLocked.Value))
            return;

        // No mover ni rotar la cámara mientras está pausado.
        if (pauseController != null && pauseController.EstaPausado)
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
        if (spectatorCamera == null || targetPlayer == null)
            return;

        // Input de mouse para orbitar alrededor del jugador espectado.
        Vector2 lookInput = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        orbitYaw += lookInput.x * orbitSensitivity;
        orbitPitch -= lookInput.y * orbitSensitivity;
        orbitPitch = Mathf.Clamp(orbitPitch, minPitch, maxPitch);

        Vector3 pivot = targetPlayer.position + Vector3.up * orbitHeight;

        Quaternion orbitRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        Vector3 desiredDirection = orbitRotation * Vector3.back;

        // ===================================================
        // COLISIÓN: si hay pared entre el pivote y la cámara,
        // acercamos la distancia en vez de atravesarla.
        // ===================================================

        float finalDistance = orbitDistance;

        RaycastHit[] impactos = Physics.SphereCastAll(
            pivot, collisionRadius, desiredDirection, orbitDistance,
            collisionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in impactos)
        {
            if (hit.collider.transform.IsChildOf(targetPlayer) ||
                hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<PlayerHealth>() != null ||
                hit.collider.GetComponentInParent<ZombieHealth>() != null)
                continue;

            finalDistance = Mathf.Min(finalDistance,
                Mathf.Clamp(hit.distance, minCollisionDistance, orbitDistance));
        }

        Vector3 finalPosition = pivot + desiredDirection * finalDistance;

        float blend = snapToTarget ? 1f : 1f - Mathf.Exp(-cameraFollowSpeed * Time.unscaledDeltaTime);
        snapToTarget = false;
        spectatorCamera.position = Vector3.Lerp(spectatorCamera.position, finalPosition, blend);
        spectatorCamera.rotation = Quaternion.LookRotation(pivot - spectatorCamera.position);
    }

    private void OnDisable()
    {
        if (spectatorHud) spectatorHud.SetActive(false);
    }

}
