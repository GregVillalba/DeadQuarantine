using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

internal sealed class DialogoRadioHistoria : MonoBehaviour
{
    private static readonly string[] Paginas =
    {
        "¿Me copian? Soy el sargento Rivas. Estamos en una base militar a unos kilómetros de su posición.",
        "La ciudad es demasiado peligrosa para aterrizar. Podemos mandar un helicóptero a la mansión abandonada, en las afueras, para sacarlos de ahí.",
        "Tengan cuidado con el zombi Alfa. Es mucho más peligroso que los demás. Salgan de la ciudad y lleguen a la mansión. Cambio y fuera."
    };

    internal static int UltimaPagina => Paginas.Length - 1;

    private static readonly Color32 Fondo = new Color32(8, 34, 19, 230);
    private static readonly Color32 FondoRetrato = new Color32(12, 42, 25, 255);
    private static readonly Color32 Linea = new Color32(167, 201, 174, 116);
    private static readonly Color32 Verde = new Color32(0, 255, 102, 255);
    private static readonly Color32 Texto = new Color32(248, 251, 249, 255);
    private static readonly Color32 TextoSecundario = new Color32(179, 201, 181, 255);
    private static readonly Vector2 TamanoPanel = new Vector2(1200f, 312f);

    private RectTransform hud;
    private TextMeshProUGUI mensaje;
    private TextMeshProUGUI accion;
    private AudioSource radio;
    private AudioClip[] audios;
    private Action<int, bool> solicitarAvance;
    private GameObject escenarioRetrato;
    private RenderTexture texturaRetrato;
    private int pagina = -1;
    private int caracteres;
    private float tiempoPagina;
    private float demoraTexto;
    private float duracionTexto;
    private bool paginaCompleta;
    private bool pausado;
    private bool unJugador;
    private bool controlaRadio;

    internal static DialogoRadioHistoria Crear(
        RectTransform hud, TMP_FontAsset fuenteDialogo, TMP_FontAsset fuenteTitulo,
        Sprite fondoBoton, GameObject modelo,
        RuntimeAnimatorController animador, AudioClip[] audios, bool unJugador, Action<int, bool> solicitarAvance)
    {
        RectTransform marco = CrearRect("Transmisión de radio", hud,
            new Rect(44f, 184f, TamanoPanel.x, TamanoPanel.y));
        AgregarFondo(marco, Fondo);
        DialogoRadioHistoria panel = marco.gameObject.AddComponent<DialogoRadioHistoria>();
        panel.hud = hud;
        panel.audios = audios;
        panel.unJugador = unJugador;
        panel.solicitarAvance = solicitarAvance;
        panel.radio = marco.gameObject.AddComponent<AudioSource>();
        panel.radio.playOnAwake = false;
        panel.radio.spatialBlend = 0f;
        panel.radio.volume = 0.8f;

        AgregarFondo(CrearRect("Línea superior", marco, new Rect(0, 308, 1200, 4)), Verde);

        RectTransform marcoRetrato = CrearRect("Marco del retrato", marco, new Rect(27, 20, 272, 272));
        AgregarFondo(marcoRetrato, Linea);
        RectTransform retrato = CrearRect("Retrato del sargento", marcoRetrato, new Rect(2, 2, 268, 268));
        RawImage imagen = retrato.gameObject.AddComponent<RawImage>();
        imagen.raycastTarget = false;
        panel.CrearRetrato(imagen, modelo, animador);

        CrearTexto(marco, fuenteTitulo, new Rect(323, 236, 315, 57), 34f, TextoSecundario, "SARGENTO RIVAS");
        CrearTexto(marco, fuenteDialogo, new Rect(640, 241, 494, 45), 22f, TextoSecundario,
            "BASE MILITAR · CANAL DE EMERGENCIA");
        AgregarFondo(CrearRect("Señal activa", marco, new Rect(1154, 258, 14, 14)), Verde);
        AgregarFondo(CrearRect("Separador", marco, new Rect(323, 236, 845, 1)), Linea);

        panel.mensaje = CrearTexto(marco, fuenteDialogo, new Rect(323, 74, 845, 147), 34f, Texto, "");
        panel.mensaje.alignment = TextAlignmentOptions.TopLeft;
        panel.mensaje.textWrappingMode = TextWrappingModes.Normal;
        panel.mensaje.enableAutoSizing = true;
        panel.mensaje.fontSizeMin = 26f;
        panel.mensaje.fontSizeMax = 34f;

        RectTransform marcoAccion = CrearRect("Tecla de diálogo", marco, new Rect(826, 19, 342, 48));
        Image boton = AgregarFondo(marcoAccion, new Color32(16, 41, 22, 255));
        boton.sprite = fondoBoton;
        boton.type = Image.Type.Sliced;
        Outline bordeBoton = marcoAccion.gameObject.AddComponent<Outline>();
        bordeBoton.effectColor = new Color32(0, 255, 36, 128);
        panel.accion = CrearTexto(marcoAccion, fuenteTitulo, new Rect(0, 0, 342, 48),
            24f, new Color32(120, 145, 165, 255), "");
        panel.accion.alignment = TextAlignmentOptions.Center;
        panel.accion.fontStyle = FontStyles.Bold;
        return panel;
    }

    internal void Sincronizar(GestorHistoriaPartida.EstadoDialogoRadio estado, float segundos, bool controlaRadio)
    {
        this.controlaRadio = controlaRadio;
        accion.transform.parent.gameObject.SetActive(controlaRadio);

        if (pagina != estado.Pagina)
        {
            pagina = estado.Pagina;
            MostrarPagina(Mathf.Max(0f, segundos));
        }

        if (estado.TextoRevelado)
            RevelarPagina();
    }

    private void MostrarPagina(float segundos)
    {
        radio.Stop();
        radio.clip = audios != null && pagina < audios.Length ? audios[pagina] : null;
        if (radio.clip != null && segundos < radio.clip.length)
        {
            if (segundos > 0f)
                radio.timeSamples = Mathf.Min((int)(segundos * radio.clip.frequency), radio.clip.samples - 1);
            radio.Play();
            if (pausado)
                radio.Pause();
        }

        mensaje.text = Paginas[pagina];
        mensaje.ForceMeshUpdate();
        caracteres = mensaje.textInfo.characterCount;
        mensaje.maxVisibleCharacters = 0;
        paginaCompleta = false;
        tiempoPagina = segundos;
        demoraTexto = radio.clip == null ? 0f : pagina == 0 ? 0.44f : 0.16f;
        duracionTexto = radio.clip == null
            ? caracteres / 28f
            : Mathf.Max(1f, radio.clip.length - demoraTexto - 0.4f);
        accion.text = "[ENTER] MOSTRAR TEXTO";
        ActualizarTexto();
    }

    private void Update()
    {
        float escala = Mathf.Min(1f, hud.rect.width * 0.62f / (TamanoPanel.x + 44f));
        transform.localScale = Vector3.one * escala;

        bool pausaLocal = unJugador && PauseController.LocalPlayerPaused;
        if (pausado != pausaLocal)
        {
            pausado = pausaLocal;
            if (pausado)
                radio.Pause();
            else
                radio.UnPause();
        }
        if (pausado)
            return;

        if (!paginaCompleta)
        {
            tiempoPagina += Time.unscaledDeltaTime;
            ActualizarTexto();
        }

        if (!controlaRadio || PauseController.LocalPlayerPaused)
            return;

        Keyboard teclado = Keyboard.current;
        if (teclado != null && (teclado.enterKey.wasPressedThisFrame ||
                               teclado.numpadEnterKey.wasPressedThisFrame))
            solicitarAvance?.Invoke(pagina, !paginaCompleta);
    }

    private void ActualizarTexto()
    {
        float avance = (tiempoPagina - demoraTexto) / duracionTexto;
        mensaje.maxVisibleCharacters = Mathf.FloorToInt(Mathf.Clamp01(avance) * caracteres);
        if (avance >= 1f)
            RevelarPagina();
    }

    private void RevelarPagina()
    {
        paginaCompleta = true;
        mensaje.maxVisibleCharacters = caracteres;
        accion.text = pagina == UltimaPagina ? "[ENTER] CERRAR" : "[ENTER] CONTINUAR";
    }

    private void CrearRetrato(RawImage imagen, GameObject prefab, RuntimeAnimatorController controlador)
    {
        int capa = LayerMask.NameToLayer("CharacterPreview");
        if (prefab == null || capa < 0)
        {
            imagen.color = FondoRetrato;
            return;
        }

        escenarioRetrato = new GameObject("Cabina de radio (retrato)");
        escenarioRetrato.transform.position = new Vector3(4000f, -4000f, 0f);
        Transform escena = escenarioRetrato.transform;
        GameObject soldado = Instantiate(prefab, escena);
        soldado.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
        foreach (Transform parte in soldado.GetComponentsInChildren<Transform>(true))
            parte.gameObject.layer = capa;

        Animator animador = soldado.GetComponentInChildren<Animator>();
        if (animador != null)
        {
            animador.runtimeAnimatorController = controlador;
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        texturaRetrato = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32);
        texturaRetrato.name = "Retrato del sargento Rivas";
        imagen.texture = texturaRetrato;

        Camera camara = new GameObject("Cámara del retrato").AddComponent<Camera>();
        camara.transform.SetParent(escena, false);
        camara.transform.localPosition = new Vector3(0f, 1.57f, 2.4f);
        camara.transform.localRotation = Quaternion.Euler(3f, 180f, 0f);
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = FondoRetrato;
        camara.orthographic = true;
        camara.orthographicSize = 0.43f;
        camara.nearClipPlane = 0.05f;
        camara.farClipPlane = 6f;
        camara.cullingMask = 1 << capa;
        camara.allowHDR = false;
        camara.allowMSAA = false;
        camara.useOcclusionCulling = false;
        camara.targetTexture = texturaRetrato;
        UniversalAdditionalCameraData ajustes = camara.GetUniversalAdditionalCameraData();
        ajustes.renderPostProcessing = false;
        ajustes.renderShadows = false;
        ajustes.volumeLayerMask = 0;

        Light luz = new GameObject("Luz del retrato").AddComponent<Light>();
        luz.transform.SetParent(escena, false);
        luz.transform.localPosition = new Vector3(-0.9f, 2.2f, 1.3f);
        luz.type = LightType.Point;
        luz.range = 4f;
        luz.intensity = 2.5f;
        luz.cullingMask = 1 << capa;
    }

    private static RectTransform CrearRect(string nombre, RectTransform padre, Rect caja)
    {
        RectTransform rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = caja.position;
        rect.sizeDelta = caja.size;
        return rect;
    }

    private static Image AgregarFondo(RectTransform rect, Color color)
    {
        Image imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
        return imagen;
    }

    private static TextMeshProUGUI CrearTexto(RectTransform padre, TMP_FontAsset fuente,
        Rect caja, float tamano, Color color, string texto)
    {
        RectTransform rect = CrearRect("Texto", padre, caja);
        TextMeshProUGUI etiqueta = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null)
            etiqueta.font = fuente;
        etiqueta.text = texto;
        etiqueta.fontSize = tamano;
        etiqueta.color = color;
        etiqueta.alignment = TextAlignmentOptions.MidlineLeft;
        etiqueta.textWrappingMode = TextWrappingModes.NoWrap;
        etiqueta.raycastTarget = false;
        return etiqueta;
    }

    private void OnDisable()
    {
        if (radio != null)
            radio.Stop();
        if (escenarioRetrato != null)
            escenarioRetrato.SetActive(false);
    }

    private void OnDestroy()
    {
        if (escenarioRetrato != null)
            Destroy(escenarioRetrato);
        if (texturaRetrato != null)
            Destroy(texturaRetrato);
    }
}
