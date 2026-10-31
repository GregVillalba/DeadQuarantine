using UnityEngine;
using Unity.Netcode;

public class Door : NetworkBehaviour
{
    [Header("Puerta")]
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private float openAngle = 90f;

    [Tooltip("Velocidad de transición. 2 significa aproximadamente medio segundo para completar la apertura.")]
    [SerializeField] private float openSpeed = 2f;

    [Header("Dirección")]
    [SerializeField] private bool invertOpenDirection = false;

    [Header("Física")]
    [Tooltip("Si DoorLeaf no tiene Rigidbody, se crea automáticamente.")]
    [SerializeField] private bool crearRigidbodyAutomaticamente = true;

    [Tooltip("Configura automáticamente el Rigidbody como cinemático.")]
    [SerializeField] private bool configurarRigidbodyAutomaticamente = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    [Header("Seguridad del giro")]
    [Tooltip("Detiene la hoja antes de empujar al jugador. Activo en las puertas de Escenario 3.")]
    [SerializeField] private bool detenerAnteJugadores;

    private Collider[] collidersHoja;
    private readonly NetworkVariable<Quaternion> rotacionSeguraNetwork = new NetworkVariable<Quaternion>(Quaternion.identity);

    private PlayerControls controls;

    private Rigidbody doorRigidbody;

    private Quaternion closedLocalRotation;
    private Quaternion startLocalRotation;

    private float movementProgress = 1f;

    private Camera localPlayerCamera;

    // =========================================================
    // SINGLEPLAYER
    // =========================================================

    private bool isOpenLocal = false;
    private float openDirectionLocal = 1f;

    // =========================================================
    // MULTIPLAYER
    // =========================================================

    private readonly NetworkVariable<bool> isOpenNetwork =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<float> openDirectionNetwork =
        new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // =========================================================
    // PROPIEDADES
    // =========================================================

    public bool IsOpen => ObtenerEstadoAbierto();

    private bool UsarRed()
    {
        return
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            IsSpawned;
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        controls = new PlayerControls();

        ConfiguracionesJuego.CargarRebinds(
            controls.asset
        );

        if (doorLeaf == null)
        {
            Debug.LogError(
                $"[Door] {name}: Door Leaf no está asignado.",
                this
            );

            return;
        }

        /*
         * Guardamos la rotación cerrada original.
         *
         * IMPORTANTE:
         * trabajamos internamente con rotación LOCAL porque
         * la puerta puede estar dentro de un prefab/escenario
         * rotado.
         */
        closedLocalRotation =
            doorLeaf.localRotation;

        startLocalRotation =
            closedLocalRotation;

        PrepararRigidbody();
        collidersHoja = doorLeaf.GetComponentsInChildren<Collider>(true);
    }

    // =========================================================
    // RIGIDBODY
    // =========================================================

    private void PrepararRigidbody()
    {
        if (doorLeaf == null)
            return;

        doorRigidbody =
            doorLeaf.GetComponent<Rigidbody>();

        if (
            doorRigidbody == null &&
            crearRigidbodyAutomaticamente
        )
        {
            doorRigidbody =
                doorLeaf.gameObject.AddComponent<Rigidbody>();

            Debug.Log(
                $"[Door] {name}: Rigidbody agregado automáticamente a {doorLeaf.name}.",
                doorLeaf
            );
        }

        if (doorRigidbody == null)
        {
            Debug.LogWarning(
                $"[Door] {name}: Door Leaf no tiene Rigidbody. " +
                "La puerta usará Transform como fallback.",
                this
            );

            return;
        }

        if (!configurarRigidbodyAutomaticamente)
            return;

        /*
         * Una puerta controlada por código NO debe ser un
         * Rigidbody dinámico.
         */
        doorRigidbody.useGravity = false;
        doorRigidbody.isKinematic = true;

        /*
         * Interpolate suaviza visualmente el movimiento entre
         * pasos de física.
         */
        doorRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        /*
         * Para objetos cinemáticos que se mueven/rotan,
         * ContinuousSpeculative es la opción apropiada.
         */
        doorRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        doorRigidbody.detectCollisions = true;

        /*
         * No queremos que restricciones heredadas de un prefab
         * impidan el giro.
         */
        doorRigidbody.constraints =
            RigidbodyConstraints.None;
    }

    // =========================================================
    // ENABLE / DISABLE
    // =========================================================

    private void OnEnable()
    {
        if (controls != null)
            controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (controls != null)
            controls.Player.Disable();
    }

    // =========================================================
    // NETWORK
    // =========================================================

    public override void OnNetworkSpawn()
    {
        /*
         * En tu Door anterior estas suscripciones aparecían
         * duplicadas. Acá se hacen UNA sola vez.
         */
        isOpenNetwork.OnValueChanged +=
            OnDoorStateChanged;

        openDirectionNetwork.OnValueChanged +=
            OnOpenDirectionChanged;

        if (detenerAnteJugadores && IsServer)
            rotacionSeguraNetwork.Value = doorLeaf.localRotation;
        if (detenerAnteJugadores && !IsServer)
            AplicarRotacionInmediata(rotacionSeguraNetwork.Value);
        else
            AplicarEstadoActual();
    }

    public override void OnNetworkDespawn()
    {
        isOpenNetwork.OnValueChanged -=
            OnDoorStateChanged;

        openDirectionNetwork.OnValueChanged -=
            OnOpenDirectionChanged;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        /*
         * INPUT únicamente.
         *
         * La física de la puerta NO se mueve acá.
         */
        BuscarCamaraJugadorLocal();

        if (localPlayerCamera == null)
            return;

        if (
            controls.Player.Interact.triggered &&
            EstaMirandoLaPuerta()
        )
        {
            ToggleDoor();
        }
    }

    // =========================================================
    // FIXED UPDATE
    // =========================================================

    private void FixedUpdate()
    {
        /*
         * Toda la rotación física ocurre sincronizada con
         * el timestep de Physics.
         */
        RotateDoorPhysics();
    }

    // =========================================================
    // CÁMARA LOCAL
    // =========================================================

    private void BuscarCamaraJugadorLocal()
    {
        if (
            localPlayerCamera != null &&
            localPlayerCamera.isActiveAndEnabled
        )
        {
            return;
        }

        localPlayerCamera = null;

#if UNITY_2023_1_OR_NEWER

        Camera[] cameras =
            FindObjectsByType<Camera>(
                FindObjectsSortMode.None
            );

#else

        Camera[] cameras =
            FindObjectsOfType<Camera>();

#endif

        foreach (Camera cam in cameras)
        {
            if (cam == null)
                continue;

            if (!cam.isActiveAndEnabled)
                continue;

            NetworkObject networkObject =
                cam.GetComponentInParent<NetworkObject>();

            // =================================================
            // MULTIPLAYER
            // =================================================

            if (
                networkObject != null &&
                networkObject.IsSpawned &&
                networkObject.IsOwner
            )
            {
                localPlayerCamera = cam;
                return;
            }

            // =================================================
            // SINGLEPLAYER
            // =================================================

            if (
                networkObject == null &&
                cam == Camera.main
            )
            {
                localPlayerCamera = cam;
                return;
            }
        }

        /*
         * Fallback.
         */
        if (
            Camera.main != null &&
            Camera.main.isActiveAndEnabled
        )
        {
            NetworkObject networkObject =
                Camera.main
                    .GetComponentInParent<NetworkObject>();

            if (
                networkObject == null ||
                networkObject.IsOwner
            )
            {
                localPlayerCamera =
                    Camera.main;
            }
        }
    }

    // =========================================================
    // MIRAR PUERTA
    // =========================================================

    private bool EstaMirandoLaPuerta()
    {
        if (
            localPlayerCamera == null ||
            doorLeaf == null
        )
        {
            return false;
        }

        Ray ray =
            new Ray(
                localPlayerCamera.transform.position,
                localPlayerCamera.transform.forward
            );

        if (
            !Physics.Raycast(
                ray,
                out RaycastHit hit,
                3f,
                ~0,
                QueryTriggerInteraction.Ignore
            )
        )
        {
            return false;
        }

        Transform hitTransform =
            hit.transform;

        if (hitTransform == null)
            return false;

        return
            hitTransform == doorLeaf ||
            hitTransform.IsChildOf(doorLeaf) ||
            hitTransform.IsChildOf(transform);
    }

    // =========================================================
    // ESTADO
    // =========================================================

    private bool ObtenerEstadoAbierto()
    {
        if (UsarRed())
            return isOpenNetwork.Value;

        return isOpenLocal;
    }

    private float ObtenerDireccionApertura()
    {
        if (UsarRed())
            return openDirectionNetwork.Value;

        return openDirectionLocal;
    }

    // =========================================================
    // TOGGLE
    // =========================================================

    private void ToggleDoor()
    {
        if (doorLeaf == null)
            return;

        bool currentlyOpen =
            ObtenerEstadoAbierto();

        // =====================================================
        // MULTIPLAYER
        // =====================================================

        if (UsarRed())
        {
            if (!currentlyOpen)
            {
                float direction =
                    CalcularDireccionApertura();

                ToggleDoorServerRpc(
                    direction
                );
            }
            else
            {
                ToggleDoorServerRpc(
                    openDirectionNetwork.Value
                );
            }

            return;
        }

        // =====================================================
        // SINGLEPLAYER
        // =====================================================

        if (!currentlyOpen)
        {
            openDirectionLocal =
                CalcularDireccionApertura();
        }

        bool wasOpen =
            isOpenLocal;

        isOpenLocal =
            !isOpenLocal;

        IniciarMovimiento();

        ReproducirSonidoLocal(
            !wasOpen
        );
    }

    // =========================================================
    // SERVER
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc(
        float requestedDirection
    )
    {
        bool wasOpen =
            isOpenNetwork.Value;

        if (!wasOpen)
        {
            openDirectionNetwork.Value =
                requestedDirection >= 0f
                    ? 1f
                    : -1f;
        }

        isOpenNetwork.Value =
            !isOpenNetwork.Value;

        PlayDoorSoundClientRpc(
            !wasOpen
        );
    }

    // =========================================================
    // INICIAR MOVIMIENTO
    // =========================================================

    private void IniciarMovimiento()
    {
        if (doorLeaf == null)
            return;

        /*
         * Utilizamos la rotación física actual como origen.
         *
         * Esto permite invertir la puerta a mitad de movimiento
         * sin que haga un salto.
         */
        startLocalRotation =
            ObtenerRotacionLocalActual();

        movementProgress = 0f;
    }

    // =========================================================
    // DIRECCIÓN
    // =========================================================

    private float CalcularDireccionApertura()
    {
        if (
            localPlayerCamera == null ||
            doorLeaf == null
        )
        {
            return 1f;
        }

        Vector3 directionFromDoor =
            localPlayerCamera.transform.position -
            doorLeaf.position;

        /*
         * Determinamos de qué lado de la puerta está el jugador.
         */
        float side =
            Vector3.Dot(
                directionFromDoor,
                doorLeaf.forward
            );

        /*
         * La puerta abre alejándose del jugador.
         */
        float direction =
            side > 0f
                ? 1f
                : -1f;

        if (invertOpenDirection)
            direction *= -1f;

        return direction;
    }

    // =========================================================
    // NETWORK CALLBACKS
    // =========================================================

    private void OnDoorStateChanged(
        bool previousValue,
        bool newValue
    )
    {
        IniciarMovimiento();
    }

    private void OnOpenDirectionChanged(
        float previousValue,
        float newValue
    )
    {
        /*
         * Cuando cambia la dirección y la puerta está abriendo,
         * recalculamos desde la posición actual.
         */
        if (ObtenerEstadoAbierto())
        {
            IniciarMovimiento();
        }
    }

    // =========================================================
    // ESTADO INICIAL
    // =========================================================

    private void AplicarEstadoActual()
    {
        if (doorLeaf == null)
            return;

        Quaternion targetLocalRotation =
            ObtenerRotacionObjetivoLocal();

        movementProgress = 1f;

        startLocalRotation =
            targetLocalRotation;

        AplicarRotacionInmediata(
            targetLocalRotation
        );
    }

    // =========================================================
    // OBJETIVO
    // =========================================================

    private Quaternion ObtenerRotacionObjetivoLocal()
    {
        float direction =
            ObtenerDireccionApertura();

        Quaternion openLocalRotation =
            closedLocalRotation *
            Quaternion.Euler(
                0f,
                openAngle * direction,
                0f
            );

        return ObtenerEstadoAbierto()
            ? openLocalRotation
            : closedLocalRotation;
    }

    // =========================================================
    // ROTACIÓN FÍSICA
    // =========================================================

    private void RotateDoorPhysics()
    {
        if (doorLeaf == null) return;
        if (detenerAnteJugadores && UsarRed() && !IsServer)
        {
            MoverFisicamente(rotacionSeguraNetwork.Value);
            return;
        }
        if (movementProgress >= 1f) return;

        float nextProgress = Mathf.Clamp01(movementProgress + Time.fixedDeltaTime * Mathf.Max(.01f, openSpeed));
        Quaternion desired = Quaternion.Slerp(startLocalRotation, ObtenerRotacionObjetivoLocal(), Mathf.SmoothStep(0, 1, nextProgress));
        if (detenerAnteJugadores && HayJugadorEnBarrido(desired))
        {
            // No avanza el reloj: reanuda desde esta misma pose cuando se despeja el giro.
            MoverFisicamente(ObtenerRotacionLocalActual());
            return;
        }
        movementProgress = nextProgress;
        MoverFisicamente(desired);
        if (detenerAnteJugadores && UsarRed()) rotacionSeguraNetwork.Value = desired;
    }

    private bool HayJugadorEnBarrido(Quaternion destino)
    {
        Quaternion actual = ObtenerRotacionLocalActual();
        int pasos = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(actual, destino) / 2f));
        Quaternion padre = doorLeaf.parent != null ? doorLeaf.parent.rotation : Quaternion.identity;
        for (int paso = 1; paso <= pasos; paso++)
        {
            Quaternion giro = padre * Quaternion.Slerp(actual, destino, (float)paso / pasos);
            Quaternion delta = giro * Quaternion.Inverse(doorLeaf.rotation);
            foreach (Collider hoja in collidersHoja)
            {
                if (!hoja || !hoja.enabled || hoja.isTrigger || !hoja.gameObject.activeInHierarchy) continue;
                // La caja mundial es conservadora para hojas compuestas o mallas no convexas.
                Bounds caja = hoja.bounds;
                Quaternion orientacion = delta;
                Vector3 centroActual = caja.center;
                Vector3 semiejes = caja.extents;
                Bounds local;
                if (hoja is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (hoja is MeshCollider mesh && mesh.sharedMesh) local = mesh.sharedMesh.bounds;
                else local = default;
                if (local.size != Vector3.zero)
                {
                    Vector3 escala = hoja.transform.lossyScale;
                    escala = new Vector3(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z));
                    centroActual = hoja.transform.TransformPoint(local.center);
                    semiejes = Vector3.Scale(local.extents, escala);
                    orientacion = delta * hoja.transform.rotation;
                }
                Vector3 centro = doorLeaf.position + delta * (centroActual - doorLeaf.position);
                Vector3 margen = Vector3.one * .025f;
                foreach (Collider obstaculo in Physics.OverlapBox(centro, semiejes + margen, orientacion, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (obstaculo.transform.IsChildOf(doorLeaf)) continue;
                    if (obstaculo is CharacterController || obstaculo.GetComponentInParent<PlayerHealth>() || obstaculo.GetComponentInParent<PlayerMovement>())
                        return true;
                }
            }
        }
        return false;
    }

    // =========================================================
    // MOVIMIENTO DEL RIGIDBODY
    // =========================================================

    private void MoverFisicamente(
        Quaternion desiredLocalRotation
    )
    {
        if (doorLeaf == null)
            return;

        /*
         * Si no tenemos Rigidbody hacemos fallback.
         *
         * No es la opción recomendada, pero evita romper una
         * puerta si por alguna razón no pudo crearse.
         */
        if (doorRigidbody == null)
        {
            doorLeaf.localRotation =
                desiredLocalRotation;

            return;
        }

        Quaternion desiredWorldRotation;

        if (doorLeaf.parent != null)
        {
            desiredWorldRotation =
                doorLeaf.parent.rotation *
                desiredLocalRotation;
        }
        else
        {
            desiredWorldRotation =
                desiredLocalRotation;
        }

        /*
         * ÉSTA ES LA DIFERENCIA IMPORTANTE.
         *
         * No hacemos:
         *
         * doorLeaf.localRotation = ...
         *
         * El movimiento pasa por PhysX.
         */
        doorRigidbody.MoveRotation(
            desiredWorldRotation
        );
    }

    // =========================================================
    // ROTACIÓN ACTUAL
    // =========================================================

    private Quaternion ObtenerRotacionLocalActual()
    {
        if (doorLeaf == null)
            return Quaternion.identity;

        if (doorRigidbody == null)
            return doorLeaf.localRotation;

        Quaternion worldRotation =
            doorRigidbody.rotation;

        if (doorLeaf.parent == null)
            return worldRotation;

        return
            Quaternion.Inverse(
                doorLeaf.parent.rotation
            ) *
            worldRotation;
    }

    // =========================================================
    // APLICACIÓN INMEDIATA
    // =========================================================

    private void AplicarRotacionInmediata(
        Quaternion localRotation
    )
    {
        if (doorLeaf == null)
            return;

        if (doorRigidbody == null)
        {
            doorLeaf.localRotation =
                localRotation;

            return;
        }

        Quaternion worldRotation;

        if (doorLeaf.parent != null)
        {
            worldRotation =
                doorLeaf.parent.rotation *
                localRotation;
        }
        else
        {
            worldRotation =
                localRotation;
        }

        /*
         * Esto solamente se usa para sincronización inicial.
         * Durante el movimiento utilizamos MoveRotation.
         */
        doorRigidbody.rotation =
            worldRotation;
    }

    // =========================================================
    // AUDIO NETWORK
    // =========================================================

    [ClientRpc]
    private void PlayDoorSoundClientRpc(
        bool opening
    )
    {
        ReproducirSonidoLocal(
            opening
        );
    }

    // =========================================================
    // AUDIO
    // =========================================================

    private void ReproducirSonidoLocal(
        bool opening
    )
    {
        if (audioSource == null)
            return;

        AudioClip clip =
            opening
                ? doorOpenSound
                : doorCloseSound;

        if (clip == null)
            return;

        audioSource.PlayOneShot(
            clip
        );
    }
}