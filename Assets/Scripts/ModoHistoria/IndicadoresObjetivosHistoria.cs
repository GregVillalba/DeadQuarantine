using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class IndicadoresObjetivosHistoria : MonoBehaviour
{
    [Header("Panel del HUD")]
    [SerializeField] private GameObject panelObjetosHistoria;
    [SerializeField] private GameObject indicadorTarjeta;
    [SerializeField] private GameObject indicadorRadio;

    [Header("Animación de llamada")]
    [SerializeField] private RectTransform iconoRadioAnimado;
    [SerializeField] private RectTransform ondaRadio1;
    [SerializeField] private RectTransform ondaRadio2;
    [SerializeField] private CanvasGroup transparenciaOnda1;
    [SerializeField] private CanvasGroup transparenciaOnda2;
    [SerializeField, Min(0.1f)] private float duracionLlamada = 3f;
    [SerializeField, Min(0f)] private float movimientoVertical = 6f;
    [SerializeField, Min(0.1f)] private float duracionOnda = 0.8f;
    [SerializeField, Min(0f)] private float escalaMinimaOnda = 0.5f;
    [SerializeField, Min(0f)] private float escalaMaximaOnda = 1.5f;

    [Header("Transmisión de radio")]
    [SerializeField] private TMP_FontAsset fuenteDialogo;
    [SerializeField] private GameObject modeloSobreviviente;
    [SerializeField] private RuntimeAnimatorController animadorSobreviviente;
    [SerializeField] private AudioClip[] paginasAudio;

    private const int AbrirPuerta = 2;
    private const int BuscarSenal = 5;
    private const int EnZonaSegura = 8;

    private NetworkObject jugador;
    private RectTransform hud;
    private GestorHistoriaPartida gestorVinculado;
    private DialogoRadioHistoria dialogo;
    private Coroutine rutinaLlamada;
    private Vector2 posicionOriginal;
    private bool confirmacionEnviada;

    private void Awake()
    {
        jugador = GetComponentInParent<NetworkObject>();
        hud = (RectTransform)GetComponentInParent<Canvas>().transform;

        if (iconoRadioAnimado != null)
            posicionOriginal = iconoRadioAnimado.anchoredPosition;
        if (ondaRadio2 == ondaRadio1)
            ondaRadio2 = null;

        PrepararOnda(ondaRadio1, ref transparenciaOnda1);
        PrepararOnda(ondaRadio2, ref transparenciaOnda2);
        panelObjetosHistoria.SetActive(false);
    }

    private void Update()
    {
        GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;
        if (jugador == null || !jugador.IsSpawned || !jugador.IsOwner ||
            gestor == null || !gestor.IsSpawned)
            gestor = null;

        if (ReferenceEquals(gestor, gestorVinculado))
            return;

        DesvincularGestor();
        gestorVinculado = gestor;
        if (gestor == null)
            return;

        gestor.EstadoMisionNetwork.OnValueChanged += AlCambiarEstado;
        gestor.ClienteConTarjetaNetwork.OnValueChanged += AlCambiarPropietario;
        gestor.ClienteConRadioNetwork.OnValueChanged += AlCambiarPropietario;
        ActualizarIndicadores();
    }

    private void AlCambiarEstado(int anterior, int nuevo) => ActualizarIndicadores();

    private void AlCambiarPropietario(ulong anterior, ulong nuevo) => ActualizarIndicadores();

    private void ActualizarIndicadores()
    {
        int estado = gestorVinculado.EstadoMisionNetwork.Value;
        ulong clienteLocal = jugador.OwnerClientId;
        bool tieneTarjeta = estado == AbrirPuerta &&
            gestorVinculado.ClienteConTarjetaNetwork.Value == clienteLocal;
        bool tieneRadio = estado >= BuscarSenal && estado <= EnZonaSegura &&
            gestorVinculado.ClienteConRadioNetwork.Value == clienteLocal;

        indicadorTarjeta.SetActive(tieneTarjeta);
        indicadorRadio.SetActive(tieneRadio);
        panelObjetosHistoria.SetActive(tieneTarjeta || tieneRadio);

        if (!gestorVinculado.ContactoRadioPendienteLocal)
        {
            confirmacionEnviada = false;
            CerrarTransmision();
            return;
        }

        if (dialogo != null || confirmacionEnviada)
            return;

        dialogo = DialogoRadioHistoria.Crear(
            hud, fuenteDialogo, modeloSobreviviente, animadorSobreviviente,
            paginasAudio, ConfirmarContacto);
        if (iconoRadioAnimado != null)
            rutinaLlamada = StartCoroutine(AnimarLlamada());
    }

    private void ConfirmarContacto()
    {
        if (gestorVinculado == null || !gestorVinculado.ContactoRadioPendienteLocal)
            return;

        confirmacionEnviada = true;
        CerrarTransmision();
        gestorVinculado.ConfirmarContactoSobrevivientes();
    }

    private void PrepararOnda(RectTransform onda, ref CanvasGroup transparencia)
    {
        if (onda == null)
            return;

        if (transparencia == null && !onda.TryGetComponent(out transparencia))
            transparencia = onda.gameObject.AddComponent<CanvasGroup>();
        onda.gameObject.SetActive(false);
    }

    private IEnumerator AnimarLlamada()
    {
        if (ondaRadio1 != null)
            ondaRadio1.gameObject.SetActive(true);
        if (ondaRadio2 != null)
            ondaRadio2.gameObject.SetActive(true);

        float tiempo = 0f;
        while (tiempo < duracionLlamada)
        {
            float vibracion = Mathf.Sin(tiempo * 24f) * movimientoVertical;
            iconoRadioAnimado.anchoredPosition = posicionOriginal + Vector2.right * vibracion;
            AnimarOnda(ondaRadio1, transparenciaOnda1, tiempo);
            AnimarOnda(ondaRadio2, transparenciaOnda2, tiempo + duracionOnda * 0.5f);
            yield return null;
            tiempo += Time.deltaTime;
        }

        RestaurarAnimacion();
        rutinaLlamada = null;
    }

    private void AnimarOnda(RectTransform onda, CanvasGroup transparencia, float tiempo)
    {
        if (onda == null)
            return;

        float progreso = Mathf.Repeat(tiempo, duracionOnda) / duracionOnda;
        onda.localScale = Vector3.one * Mathf.Lerp(escalaMinimaOnda, escalaMaximaOnda, progreso);
        transparencia.alpha = 1f - progreso;
    }

    private void RestaurarAnimacion()
    {
        if (iconoRadioAnimado != null)
            iconoRadioAnimado.anchoredPosition = posicionOriginal;
        if (ondaRadio1 != null)
            ondaRadio1.gameObject.SetActive(false);
        if (ondaRadio2 != null)
            ondaRadio2.gameObject.SetActive(false);
    }

    private void CerrarTransmision()
    {
        if (dialogo != null)
        {
            dialogo.gameObject.SetActive(false);
            Destroy(dialogo.gameObject);
            dialogo = null;
        }

        if (rutinaLlamada != null)
        {
            StopCoroutine(rutinaLlamada);
            rutinaLlamada = null;
            RestaurarAnimacion();
        }
    }

    private void DesvincularGestor()
    {
        CerrarTransmision();
        confirmacionEnviada = false;

        if (!ReferenceEquals(gestorVinculado, null))
        {
            gestorVinculado.EstadoMisionNetwork.OnValueChanged -= AlCambiarEstado;
            gestorVinculado.ClienteConTarjetaNetwork.OnValueChanged -= AlCambiarPropietario;
            gestorVinculado.ClienteConRadioNetwork.OnValueChanged -= AlCambiarPropietario;
        }

        gestorVinculado = null;
        panelObjetosHistoria.SetActive(false);
    }

    private void OnDisable()
    {
        DesvincularGestor();
    }
}
