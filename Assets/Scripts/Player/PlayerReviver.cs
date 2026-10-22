using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

// Reanimar a un compañero abatido manteniendo la acción Interact (E).
// Va en el prefab del jugador, junto a PlayerHealth. Solo actúa en el dueño.
public class PlayerReviver : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Reanimar")]
    [Tooltip("Distancia máxima (m) al compañero abatido.")]
    [SerializeField] private float rangoReanimar = 1f;
    [Tooltip("Segundos que hay que mantener la tecla.")]
    [SerializeField] private float tiempoMantenerParaReanimar = 3f;
    [Tooltip("Margen extra (m) que tolera el servidor por latencia.")]
    [SerializeField] private float margenValidacionServidor = 0.5f;
    [SerializeField] private float intervaloBusqueda = 0.2f;

    [Header("UI (opcional)")]
    [SerializeField] private GameObject panelReanimar;
    [SerializeField] private TextMeshProUGUI textoReanimar;
    [Tooltip("{0} se reemplaza por la tecla (E).")]
    [SerializeField] private string formatoTexto = "Mantené {0} para reanimar";
    [Tooltip("Image en modo Filled.")]
    [SerializeField] private Image progresoFill;

    private PlayerControls controls;

    private PlayerHealth objetivo;
    private float tiempoMantenido;
    private float proximaBusqueda;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        // Igual que NPCShopUI: controles propios con los rebinds del jugador.
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
        Cancelar();
    }

    public override void OnNetworkSpawn()
{
    if (!IsOwner || playerHealth == null)
        return;

    playerHealth.OnHealthChanged += AlCambiarVida;
    playerHealth.OnEscudoDanado += InterrumpirReanimacion;
}

public override void OnNetworkDespawn()
{
    if (playerHealth != null)
    {
        playerHealth.OnHealthChanged -= AlCambiarVida;
        playerHealth.OnEscudoDanado -= InterrumpirReanimacion;
    }

    Cancelar();
}

   /* public override void OnNetworkDespawn()
    {
        Cancelar();
    }*/

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (!PuedeReanimar())
        {
            Cancelar();
            return;
        }

        // El objetivo actual deja de valer si se levantó, murió o se alejó.
        if (objetivo != null && !ObjetivoValido(objetivo))
        {
            objetivo = null;
            ReiniciarProgreso();
        }

        if (objetivo == null)
        {
            if (Time.time >= proximaBusqueda)
            {
                proximaBusqueda = Time.time + intervaloBusqueda;
                objetivo = BuscarObjetivo();
            }

            if (objetivo == null)
            {
                MostrarPanel(false);
                return;
            }
        }

        MostrarPanel(true);
        ActualizarMantenido();
    }

    // =========================================================
    // DETECCIÓN
    // =========================================================

    private bool PuedeReanimar()
    {
        return playerHealth != null &&
               playerHealth.IsAlive &&
               !NPCShopUI.LocalShopOpen &&
               !PauseController.LocalPlayerPaused;
    }

    private bool ObjetivoValido(PlayerHealth candidato)
    {
        if (candidato == null || candidato == playerHealth)
            return false;

        if (!candidato.IsSpawned || !candidato.IsDowned)
            return false;

        return DistanciaA(candidato) <= rangoReanimar;
    }

    private float DistanciaA(PlayerHealth otro)
    {
        return Vector3.Distance(
            playerHealth.transform.position,
            otro.transform.position
        );
    }

    private PlayerHealth BuscarObjetivo()
    {
        PlayerHealth mejor = null;
        float mejorDistancia = float.MaxValue;

        PlayerHealth[] jugadores =
            FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth candidato in jugadores)
        {
            if (!ObjetivoValido(candidato))
                continue;

            float distancia = DistanciaA(candidato);

            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejor = candidato;
            }
        }

        return mejor;
    }

    // =========================================================
    // MANTENER PRESIONADO
    // =========================================================

    private void ActualizarMantenido()
    {
        if (textoReanimar != null)
        {
            textoReanimar.text = string.Format(
                formatoTexto,
                controls.Player.Interact.GetBindingDisplayString()
            );
        }

        if (!controls.Player.Interact.IsPressed())
        {
            ReiniciarProgreso();
            return;
        }

        tiempoMantenido += Time.deltaTime;

        if (progresoFill != null)
        {
            progresoFill.fillAmount =
                Mathf.Clamp01(tiempoMantenido / tiempoMantenerParaReanimar);
        }

        if (tiempoMantenido >= tiempoMantenerParaReanimar)
        {
            ulong idObjetivo = objetivo.NetworkObjectId;

            ReiniciarProgreso();
            objetivo = null;

            // Pausa corta para no reiniciar la barra sobre el mismo
            // compañero mientras llega la actualización del servidor.
            proximaBusqueda = Time.time + 0.5f;

            RequestReviveServerRpc(idObjetivo);
        }
    }

    private void ReiniciarProgreso()
    {
        tiempoMantenido = 0f;

        if (progresoFill != null)
            progresoFill.fillAmount = 0f;
    }

    private void AlCambiarVida(int vidaAnterior, int vidaNueva)
    {
        // La regeneración también cambia la vida: solo cuenta si bajó.
        if (vidaNueva < vidaAnterior)
            InterrumpirReanimacion();
    }

    private void InterrumpirReanimacion()
    {
        ReiniciarProgreso();
    }

    private void Cancelar()
    {
        objetivo = null;
        ReiniciarProgreso();
        MostrarPanel(false);
    }

    private void MostrarPanel(bool visible)
    {
        if (panelReanimar != null && panelReanimar.activeSelf != visible)
            panelReanimar.SetActive(visible);
    }

    // =========================================================
    // SERVIDOR
    // =========================================================

    [ServerRpc]
    private void RequestReviveServerRpc(ulong targetNetworkObjectId)
    {
        if (playerHealth == null || !playerHealth.IsAlive)
            return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId,
                out NetworkObject targetObject))
        {
            return;
        }

        PlayerHealth target = targetObject.GetComponent<PlayerHealth>();

        if (target == null || target == playerHealth)
            return;

        if (!target.IsDowned)
            return;

        float distancia = Vector3.Distance(
            playerHealth.transform.position,
            target.transform.position
        );

        if (distancia > rangoReanimar + margenValidacionServidor)
            return;

        target.ReviveFromTeammate();
    }
}