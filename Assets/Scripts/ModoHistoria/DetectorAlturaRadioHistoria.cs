
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class DetectorAlturaRadioHistoria : MonoBehaviour
{
    [Header("Altura requerida")]
    [SerializeField] private float alturaMinima = 3f;
    [SerializeField] private float tiempoEnAltura = 1.5f;

    [Header("Detección del suelo")]
    [SerializeField] private string tagSuelo = "SueloCiudad";
    [SerializeField] private float distanciaMaximaSuelo = 50f;
    [SerializeField] private float origenRaycastExtra = 0.2f;

    [Header("Evento")]
    [SerializeField] private UnityEvent alEncontrarSenal;

    private CharacterController characterController;
    private NetworkObject jugadorNetworkObject;

    private bool busquedaActiva;
    private bool senalEncontrada;
    private float tiempoEnAlturaActual;

    public bool SenalEncontrada => senalEncontrada;
    public float AlturaMinima => alturaMinima;

    public float AlturaActual { get; private set; }

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        if (characterController == null)
        {
            characterController =
                GetComponentInParent<CharacterController>();
        }

        if (characterController == null)
        {
            characterController =
                GetComponentInChildren<CharacterController>(true);
        }

        jugadorNetworkObject =
            GetComponentInParent<NetworkObject>();

        if (jugadorNetworkObject == null)
            jugadorNetworkObject = GetComponent<NetworkObject>();

        if (characterController == null)
        {
            Debug.LogError(
                "[Radio] No se encontró CharacterController.",
                this
            );
        }
    }

    private void Update()
    {
        if (!EsJugadorLocal() ||
            !busquedaActiva ||
            senalEncontrada)
            return;

        if (!CalcularAltura(out float altura))
        {
            AlturaActual = 0f;
            tiempoEnAlturaActual = 0f;
            return;
        }

        AlturaActual = altura;

        if (altura >= alturaMinima)
        {
            tiempoEnAlturaActual += Time.deltaTime;

            if (tiempoEnAlturaActual >= tiempoEnAltura)
            {
                senalEncontrada = true;
                busquedaActiva = false;

                Debug.Log(
                    $"[Radio] Señal encontrada. Altura: " +
                    $"{AlturaActual:F2} m."
                );

                alEncontrarSenal?.Invoke();

                NotificarSenalEncontrada();
            }
        }
        else
        {
            tiempoEnAlturaActual = 0f;
        }
    }

    private bool EsJugadorLocal()
    {
        if (jugadorNetworkObject == null)
            return true;

        return jugadorNetworkObject.IsSpawned &&
               jugadorNetworkObject.IsOwner;
    }

    private bool CalcularAltura(out float altura)
    {
        altura = 0f;

        if (characterController == null)
            return false;

        Bounds bounds = characterController.bounds;

        Vector3 origen = new Vector3(
            bounds.center.x,
            bounds.min.y + origenRaycastExtra,
            bounds.center.z
        );

        RaycastHit[] impactos = Physics.RaycastAll(
            origen,
            Vector3.down,
            distanciaMaximaSuelo,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(
            impactos,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit impacto in impactos)
        {
            if (!impacto.collider.CompareTag(tagSuelo))
                continue;

            altura = Mathf.Max(
                0f,
                impacto.distance - origenRaycastExtra
            );

            return true;
        }

        return false;
    }

    // Este método también puede ejecutarse en la instancia del servidor.
    public bool TryGetAlturaActual(out float altura)
    {
        return CalcularAltura(out altura);
    }

    public void ActivarBusquedaDeSenal()
    {
        if (senalEncontrada)
            return;

        busquedaActiva = true;
        tiempoEnAlturaActual = 0f;

        Debug.Log(
            "[Radio] Búsqueda de señal activada."
        );
    }

    public void DesactivarBusquedaDeSenal()
    {
        busquedaActiva = false;
        tiempoEnAlturaActual = 0f;
    }

    public void ReintentarBusquedaDeSenal()
    {
        senalEncontrada = false;
        busquedaActiva = true;
        tiempoEnAlturaActual = 0f;

        Debug.Log(
            "[Radio] El servidor pidió reintentar la validación de señal."
        );
    }

    public void ReiniciarBusquedaDeSenal()
    {
        busquedaActiva = false;
        senalEncontrada = false;
        tiempoEnAlturaActual = 0f;
        AlturaActual = 0f;
    }

    private void LateUpdate()
    {
        if (!EsJugadorLocal() || Keyboard.current == null)
            return;

        if (Keyboard.current.f9Key.wasPressedThisFrame)
            ActivarBusquedaDeSenal();
    }

    private void NotificarSenalEncontrada()
    {
        GestorHistoriaPartida gestor =
            GestorHistoriaPartida.Instance;

        if (gestor == null || !gestor.IsSpawned)
        {
            Debug.LogError(
                "[Radio] No se encontró GestorHistoriaPartida activo."
            );
            return;
        }

        Debug.Log(
            "[Radio] Notificando al servidor que se encontró la señal."
        );

        gestor.IntentarRegistrarSenalEncontrada();
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController cc =
            GetComponent<CharacterController>();

        if (cc == null)
            cc = GetComponentInParent<CharacterController>();

        if (cc == null)
            cc = GetComponentInChildren<CharacterController>(true);

        if (cc == null)
            return;

        Bounds bounds = cc.bounds;

        Vector3 origen = new Vector3(
            bounds.center.x,
            bounds.min.y + origenRaycastExtra,
            bounds.center.z
        );

        Gizmos.color =
            senalEncontrada ? Color.green : Color.yellow;

        Gizmos.DrawLine(
            origen,
            origen + Vector3.down * distanciaMaximaSuelo
        );
    }
}
