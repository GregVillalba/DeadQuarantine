
using UnityEngine;
using Unity.Netcode;

public class IndicadoresObjetivosHistoria : MonoBehaviour
{
    [Header("Panel del HUD")]
    [SerializeField] private GameObject panelObjetosHistoria;

    [Header("Imágenes PNG")]
    [SerializeField] private GameObject indicadorTarjeta;
    [SerializeField] private GameObject indicadorRadio;

    private GestorHistoriaPartida gestorVinculado;

    private const int AbrirPuerta = 2;
    private const int BuscarSenal = 5;
    private const int HistoriaCiudadCompletada = 7;
    private const int EnZonaSegura = 8;

    private void Start()
    {
        OcultarIndicadores();
        ActualizarVinculo();
    }

    private void Update()
    {
        // Permite encontrar el gestor cuando se inicializa la red.
        ActualizarVinculo();
    }

    private void ActualizarVinculo()
    {
        GestorHistoriaPartida gestorActual =
            GestorHistoriaPartida.Instance;

        if (gestorActual == gestorVinculado)
            return;

        DesvincularGestor();

        if (gestorActual == null || !gestorActual.IsSpawned)
            return;

        gestorVinculado = gestorActual;

        gestorVinculado.EstadoMisionNetwork.OnValueChanged +=
            AlCambiarEstadoMision;

        gestorVinculado.ClienteConTarjetaNetwork.OnValueChanged +=
            AlCambiarPropietarioTarjeta;

        gestorVinculado.ClienteConRadioNetwork.OnValueChanged +=
            AlCambiarPropietarioRadio;

        ActualizarIndicadores();
    }

    private void AlCambiarEstadoMision(int anterior, int nuevo)
    {
        ActualizarIndicadores();
    }

    private void AlCambiarPropietarioTarjeta(
        ulong anterior, ulong nuevo)
    {
        ActualizarIndicadores();
    }

    private void AlCambiarPropietarioRadio(
        ulong anterior, ulong nuevo)
    {
        ActualizarIndicadores();
    }

    private void ActualizarIndicadores()
    {
        if (gestorVinculado == null ||
            !gestorVinculado.IsSpawned ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsClient)
        {
            OcultarIndicadores();
            return;
        }

        ulong clienteLocal =
            NetworkManager.Singleton.LocalClientId;

        int estado =
            gestorVinculado.EstadoMisionNetwork.Value;

        bool tieneTarjeta =
            estado == AbrirPuerta &&
            gestorVinculado.ClienteConTarjetaNetwork.Value
                == clienteLocal;

        bool tieneRadio =
            estado >= BuscarSenal &&
            estado <= EnZonaSegura &&
            gestorVinculado.ClienteConRadioNetwork.Value
                == clienteLocal;

        // Mostrar únicamente el objetivo recogido que corresponda.
        if (indicadorTarjeta != null)
            indicadorTarjeta.SetActive(tieneTarjeta);

        if (indicadorRadio != null)
            indicadorRadio.SetActive(tieneRadio);

        // El panel verde aparece solo si hay un indicador visible.
        if (panelObjetosHistoria != null)
            panelObjetosHistoria.SetActive(
                tieneTarjeta || tieneRadio
            );
    }

    private void OcultarIndicadores()
    {
        if (indicadorTarjeta != null)
            indicadorTarjeta.SetActive(false);

        if (indicadorRadio != null)
            indicadorRadio.SetActive(false);

        if (panelObjetosHistoria != null)
            panelObjetosHistoria.SetActive(false);
    }

    private void DesvincularGestor()
    {
        if (gestorVinculado == null)
            return;

        gestorVinculado.EstadoMisionNetwork.OnValueChanged -=
            AlCambiarEstadoMision;

        gestorVinculado.ClienteConTarjetaNetwork.OnValueChanged -=
            AlCambiarPropietarioTarjeta;

        gestorVinculado.ClienteConRadioNetwork.OnValueChanged -=
            AlCambiarPropietarioRadio;

        gestorVinculado = null;
    }

    private void OnDestroy()
    {
        DesvincularGestor();
    }
}
