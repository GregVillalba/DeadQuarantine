
using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class IndicadoresObjetivosHistoria : MonoBehaviour
{
    [Header("Panel del HUD")]
    [SerializeField] private GameObject panelObjetosHistoria;

    [Header("Iconos")]
    [SerializeField] private GameObject indicadorTarjeta;
    [SerializeField] private GameObject indicadorRadio;

    [Header("Animacion de llamada")]
    [SerializeField] private RectTransform iconoRadioAnimado;
    [SerializeField] private RectTransform ondaRadio1;
    [SerializeField] private RectTransform ondaRadio2;

    [SerializeField] private CanvasGroup transparenciaOnda1;
    [SerializeField] private CanvasGroup transparenciaOnda2;

    [SerializeField, Min(0.1f)]
    private float duracionLlamada = 3f;

    [SerializeField, Min(0f)]
    private float movimientoVertical = 6f;

    [SerializeField, Min(0.1f)]
    private float duracionOnda = 0.8f;

    [Header("Animacion")]
    [SerializeField, Min(0f)]
    private float escalaMinimaOnda = 0.5f;

    [SerializeField, Min(0f)]
    private float escalaMaximaOnda = 1.5f;

    private GestorHistoriaPartida gestorVinculado;
    private Coroutine rutinaLlamada;

    private Vector2 posicionOriginal;
    private Vector3 escalaOriginalOnda1;
    private Vector3 escalaOriginalOnda2;

    private bool posicionGuardada;
    private bool llamadaFinalizada;

    private const int AbrirPuerta = 2;
    private const int BuscarSenal = 5;
    private const int ContactarSobrevivientes = 6;
    private const int HistoriaCiudadCompletada = 7;
    private const int EnZonaSegura = 8;
    private int ultimoEstadoDetectado = -1;

    private void Awake()
    {
        PrepararOnda(ondaRadio1, ref transparenciaOnda1);
        PrepararOnda(ondaRadio2, ref transparenciaOnda2);

        if (ondaRadio1 != null)
            escalaOriginalOnda1 = ondaRadio1.localScale;

        if (ondaRadio2 != null)
            escalaOriginalOnda2 = ondaRadio2.localScale;
    }

    private void Start()
    {
        OcultarIndicadores();
        ActualizarVinculo();
    }

   private void Update()
{

    ActualizarVinculo();

    if (gestorVinculado == null ||
        !gestorVinculado.IsSpawned)
    {
        return;
    }

    int estadoActual =
        gestorVinculado.EstadoMisionNetwork.Value;

    if (estadoActual != ultimoEstadoDetectado)
    {
        Debug.Log(
            $"[HUD Historia] Estado detectado por Update: " +
            $"{ultimoEstadoDetectado} -> {estadoActual}"
        );

        ultimoEstadoDetectado = estadoActual;

        if (estadoActual == ContactarSobrevivientes)
        {
            IniciarAnimacionLlamada();
        }
        else if (estadoActual == HistoriaCiudadCompletada ||
                 estadoActual == EnZonaSegura)
        {
            DetenerAnimacionLlamada();
        }
    }
}

    private void PrepararOnda(
        RectTransform onda,
        ref CanvasGroup transparencia)
    {
        if (onda == null)
            return;

        if (transparencia == null)
            transparencia = onda.GetComponent<CanvasGroup>();

        if (transparencia == null)
            transparencia = onda.gameObject.AddComponent<CanvasGroup>();

        transparencia.alpha = 0f;
        onda.localScale = Vector3.one * escalaMinimaOnda;
        onda.gameObject.SetActive(false);
    }

   private void ActualizarVinculo()
{
    GestorHistoriaPartida gestorActual =
        GestorHistoriaPartida.Instance;

    if (gestorActual == gestorVinculado)
        return;

    DesvincularGestor();

    if (gestorActual == null)
    {
        Debug.LogWarning(
            "[HUD Historia] GestorHistoriaPartida.Instance es null."
        );
        return;
    }

    if (!gestorActual.IsSpawned)
    {
        Debug.LogWarning(
            "[HUD Historia] El gestor existe, pero no esta Spawned."
        );
        return;
    }

    gestorVinculado = gestorActual;

    gestorVinculado.EstadoMisionNetwork.OnValueChanged +=
        AlCambiarEstadoMision;

    gestorVinculado.ClienteConTarjetaNetwork.OnValueChanged +=
        AlCambiarPropietarioTarjeta;

    gestorVinculado.ClienteConRadioNetwork.OnValueChanged +=
        AlCambiarPropietarioRadio;

    ultimoEstadoDetectado =
    gestorVinculado.EstadoMisionNetwork.Value;
    Debug.Log(
        "[HUD Historia] Vinculado al gestor. Estado actual: " +
        gestorVinculado.EstadoMisionNetwork.Value
    );

    ActualizarIndicadores();
}

   private void AlCambiarEstadoMision(int anterior, int nuevo)
{
    Debug.Log(
        $"[HUD Historia] Cambio de estado detectado: {anterior} -> {nuevo}"
    );

    ActualizarIndicadores();

    if (nuevo == ContactarSobrevivientes)
    {
        Debug.Log(
            "[HUD Historia] El estado es ContactarSobrevivientes."
        );

        IniciarAnimacionLlamada();
    }
    else if (nuevo != anterior)
    {
        DetenerAnimacionLlamada();
    }
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

        if (indicadorTarjeta != null)
            indicadorTarjeta.SetActive(tieneTarjeta);

        if (indicadorRadio != null)
            indicadorRadio.SetActive(tieneRadio);

        if (panelObjetosHistoria != null)
            panelObjetosHistoria.SetActive(
                tieneTarjeta || tieneRadio
            );

        // Si el HUD se vincula despues de cambiar el estado,
        // iniciar la animacion si corresponde.
        if (estado == ContactarSobrevivientes &&
            !llamadaFinalizada)
        {
             Debug.Log(
        "[HUD Historia] Estado 6 detectado desde ActualizarIndicadores()."
        );
            IniciarAnimacionLlamada();
        }

        if (estado == HistoriaCiudadCompletada ||
            estado == EnZonaSegura)
        {
            DetenerAnimacionLlamada();
        }
    }

    private void IniciarAnimacionLlamada()
    {

        Debug.Log("[HUD Historia] Se intento iniciar la animacion.");

        if (llamadaFinalizada || rutinaLlamada != null)
            return;

        if (gestorVinculado == null ||
            !gestorVinculado.IsSpawned ||
            NetworkManager.Singleton == null)
        {
            return;
        }

        ulong clienteLocal =
            NetworkManager.Singleton.LocalClientId;

        // Solo anima el HUD del jugador que tiene la radio.
        if (gestorVinculado.ClienteConRadioNetwork.Value !=
            clienteLocal)
        {
            Debug.Log(
                "[HUD Historia] La radio pertenece a otro jugador."
            );
            return;
        }

        if (iconoRadioAnimado == null)
        {
            Debug.LogError(
                "[HUD Historia] Falta asignar Icono Radio Animado."
            );
            return;
        }

        if (!posicionGuardada)
        {
            posicionOriginal =
                iconoRadioAnimado.anchoredPosition;

            posicionGuardada = true;
        }

        Debug.Log(
            "[HUD Historia] Iniciando animacion de llamada."
        );

        rutinaLlamada = StartCoroutine(AnimarLlamada());
    }

    private IEnumerator AnimarLlamada()
    {
        Debug.Log(
            "[HUD Historia] Animando ondas de radio."
        );

        float tiempo = 0f;

        if (ondaRadio1 != null)
            ondaRadio1.gameObject.SetActive(true);

        if (ondaRadio2 != null)
            ondaRadio2.gameObject.SetActive(true);

        while (tiempo < duracionLlamada)
        {
            if (gestorVinculado == null ||
                !gestorVinculado.IsSpawned ||
                gestorVinculado.EstadoMisionNetwork.Value
                    != ContactarSobrevivientes)
            {
                RestaurarAnimacion();
                rutinaLlamada = null;
                yield break;
            }

            tiempo += Time.deltaTime;

            // Vibracion horizontal del icono de radio.
            float vibracion =
                Mathf.Sin(tiempo * 24f) * movimientoVertical;

            iconoRadioAnimado.anchoredPosition =
                posicionOriginal + new Vector2(vibracion, 0f);

            // Dos ondas desfasadas para simular una llamada.
            float ciclo1 =
                (tiempo % duracionOnda) / duracionOnda;

            float ciclo2 =
                ((tiempo + duracionOnda * 0.5f) % duracionOnda)
                / duracionOnda;

            AnimarOnda(
                ondaRadio1,
                transparenciaOnda1,
                ciclo1
            );

            AnimarOnda(
                ondaRadio2,
                transparenciaOnda2,
                ciclo2
            );

            yield return null;
        }

        RestaurarAnimacion();
        rutinaLlamada = null;
        llamadaFinalizada = true;

        Debug.Log(
            "[HUD Historia] Animacion terminada. " +
            "Confirmando contacto con sobrevivientes."
        );

        if (gestorVinculado != null &&
            gestorVinculado.IsSpawned)
        {
            gestorVinculado.ConfirmarContactoSobrevivientes();
        }
    }

    private void AnimarOnda(
        RectTransform onda,
        CanvasGroup transparencia,
        float progreso)
    {
        if (onda == null)
            return;

        float escala = Mathf.Lerp(
            escalaMinimaOnda,
            escalaMaximaOnda,
            progreso
        );

        onda.localScale = Vector3.one * escala;

        if (transparencia != null)
            transparencia.alpha = 1f - progreso;
    }

    private void DetenerAnimacionLlamada()
    {
        if (rutinaLlamada != null)
        {
            StopCoroutine(rutinaLlamada);
            rutinaLlamada = null;
        }

        RestaurarAnimacion();
    }

    private void RestaurarAnimacion()
    {
        if (iconoRadioAnimado != null && posicionGuardada)
        {
            iconoRadioAnimado.anchoredPosition =
                posicionOriginal;
        }

        ReiniciarOnda(
            ondaRadio1,
            transparenciaOnda1,
            escalaOriginalOnda1
        );

        ReiniciarOnda(
            ondaRadio2,
            transparenciaOnda2,
            escalaOriginalOnda2
        );
    }

    private void ReiniciarOnda(
        RectTransform onda,
        CanvasGroup transparencia,
        Vector3 escalaOriginal)
    {
        if (onda == null)
            return;

        onda.localScale = escalaOriginal;

        if (transparencia != null)
            transparencia.alpha = 0f;

        onda.gameObject.SetActive(false);
    }

    private void OcultarIndicadores()
    {
        if (indicadorTarjeta != null)
            indicadorTarjeta.SetActive(false);

        if (indicadorRadio != null)
            indicadorRadio.SetActive(false);

        if (panelObjetosHistoria != null)
            panelObjetosHistoria.SetActive(false);

        DetenerAnimacionLlamada();
    }

    private void DesvincularGestor()
    {
        DetenerAnimacionLlamada();
        llamadaFinalizada = false;

        if (gestorVinculado == null)
            return;

        if (gestorVinculado.IsSpawned)
        {
            gestorVinculado.EstadoMisionNetwork.OnValueChanged -=
                AlCambiarEstadoMision;

            gestorVinculado.ClienteConTarjetaNetwork.OnValueChanged -=
                AlCambiarPropietarioTarjeta;

            gestorVinculado.ClienteConRadioNetwork.OnValueChanged -=
                AlCambiarPropietarioRadio;
        }

        gestorVinculado = null;
    }

    private void OnDestroy()
    {
        DesvincularGestor();
    }
}
