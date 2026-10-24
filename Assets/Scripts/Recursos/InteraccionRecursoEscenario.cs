using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Recolección de recursos del mapa (US 19.2). Va en el HUD del jugador (InteractionsUI) y corre solo para el dueño:
/// si mira un RecursoEscenario a distanciaMaxima o menos muestra "[tecla] para recoger [recurso]" y, al apretar la
/// tecla de interacción, le pide al servidor que lo sume a su inventario. Abatido, espectando o con un menú abierto
/// no aparece el mensaje.
/// </summary>
public class InteraccionRecursoEscenario : MonoBehaviour
{
    [Header("Interfaz")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;

    [Header("Configuración")]
    [Tooltip("Distancia (m) desde la cámara a la que se puede recoger un recurso.")]
    [SerializeField] private float distanciaMaxima = 3f;

    private Camera playerCamera;
    private NetworkObject playerNetworkObject;
    private PlayerRecursos playerRecursos;
    private PlayerControls controls;

    private void Awake()
    {
        playerNetworkObject = GetComponentInParent<NetworkObject>();
        playerRecursos = transform.root.GetComponentInChildren<PlayerRecursos>(true);

        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        Ocultar();
    }

    private void OnEnable()
    {
        controls?.Player.Enable();
    }

    private void OnDisable()
    {
        controls?.Player.Disable();
        Ocultar();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void Update()
    {
        if (playerNetworkObject == null || !playerNetworkObject.IsSpawned || !playerNetworkObject.IsOwner ||
            playerRecursos == null || !playerRecursos.PuedeRecolectar || PlayerInputGate.ActionsBlocked)
        {
            Ocultar();
            return;
        }

        if (playerCamera == null || !playerCamera.isActiveAndEnabled)
            BuscarCamara();

        RecursoEscenario recurso = playerCamera != null ? RecursoMirado() : null;

        if (recurso == null)
        {
            Ocultar();
            return;
        }

        Mostrar(TextoCartel(ObtenerTecla(), recurso.Tipo));

        if (controls.Player.Interact.WasPressedThisFrame())
            playerRecursos.RecolectarRecursoEscenarioServerRpc(recurso.NetworkObject);
    }

    /// <summary>Ej: "E para recoger Pila de latas".</summary>
    public static string TextoCartel(string tecla, TipoRecurso tipo)
    {
        return tecla + " para recoger " + tipo.Nombre();
    }

    // El primer recurso que toca la mira. Las paredes y objetos sólidos lo tapan; los triggers (zonas) no.
    private RecursoEscenario RecursoMirado()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] golpes = Physics.RaycastAll(ray, distanciaMaxima, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit golpe in golpes)
        {
            if (golpe.collider.transform.root == transform.root)
                continue;

            RecursoEscenario recurso = golpe.collider.GetComponentInParent<RecursoEscenario>();

            if (recurso != null)
                return recurso.IsSpawned && !recurso.Recolectado ? recurso : null;

            if (!golpe.collider.isTrigger)
                return null;
        }

        return null;
    }

    private void BuscarCamara()
    {
        playerCamera = null;

        foreach (Camera cam in transform.root.GetComponentsInChildren<Camera>(true))
        {
            if (cam.isActiveAndEnabled && (cam.GetComponentInParent<NetworkObject>() != null || cam == Camera.main))
            {
                playerCamera = cam;
                return;
            }
        }
    }

    private string ObtenerTecla()
    {
        string tecla = controls.Player.Interact.GetBindingDisplayString();

        return string.IsNullOrEmpty(tecla) ? "E" : tecla;
    }

    private void Mostrar(string mensaje)
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
            interactText.text = mensaje;

        if (!interactPrompt.activeSelf)
            interactPrompt.SetActive(true);
    }

    private void Ocultar()
    {
        if (interactPrompt != null && interactPrompt.activeSelf)
            interactPrompt.SetActive(false);
    }
}
