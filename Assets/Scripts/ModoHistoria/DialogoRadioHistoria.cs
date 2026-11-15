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

    private static readonly Color32 Fondo = new Color32(26, 27, 20, 245);
    private static readonly Color32 Oliva = new Color32(43, 47, 34, 255);
    private static readonly Color32 Borde = new Color32(65, 66, 51, 255);
    private static readonly Color32 Amarillo = new Color32(231, 184, 0, 255);
    private static readonly Color32 Texto = new Color32(242, 239, 222, 255);
    private static readonly Color32 TextoSecundario = new Color32(166, 164, 143, 255);
    private static readonly Vector2 TamanoPanel = new Vector2(1200f, 312f);

    private RectTransform hud;
    private TextMeshProUGUI mensaje;
    private TextMeshProUGUI accion;
    private AudioSource radio;
    private AudioClip[] audios;
    private Action alCerrar;
    private GameObject escenarioRetrato;
    private RenderTexture texturaRetrato;
    private int pagina;
    private int caracteres;
    private float tiempoPagina;
    private float demoraTexto;
    private float duracionTexto;
    private bool paginaCompleta;
    private bool pausado;

    internal static DialogoRadioHistoria Crear(
        RectTransform hud, TMP_FontAsset fuente, GameObject modelo,
        RuntimeAnimatorController animador, AudioClip[] audios, Action alCerrar)
    {
        RectTransform marco = CrearRect("Transmisión de radio", hud,
            new Rect(44f, 184f, TamanoPanel.x, TamanoPanel.y));
        AgregarFondo(marco, Borde);
        DialogoRadioHistoria panel = marco.gameObject.AddComponent<DialogoRadioHistoria>();
        panel.hud = hud;
        panel.audios = audios;
        panel.alCerrar = alCerrar;
        panel.radio = marco.gameObject.AddComponent<AudioSource>();
        panel.radio.playOnAwake = false;
        panel.radio.spatialBlend = 0f;
        panel.radio.volume = 0.8f;

        RectTransform fondo = CrearRect("Fondo", marco, new Rect(2, 2, 1196, 308));
        AgregarFondo(fondo, Fondo);
        AgregarFondo(CrearRect("Acento", marco, new Rect(0, 0, 8, 312)), Amarillo);

        RectTransform marcoRetrato = CrearRect("Marco del retrato", marco, new Rect(27, 20, 272, 272));
        AgregarFondo(marcoRetrato, Amarillo);
        RectTransform retrato = CrearRect("Retrato del sargento", marcoRetrato, new Rect(2, 2, 268, 268));
        RawImage imagen = retrato.gameObject.AddComponent<RawImage>();
        imagen.raycastTarget = false;
        panel.CrearRetrato(imagen, modelo, animador);

        CrearTexto(marco, fuente, new Rect(323, 236, 315, 57), 36f, Amarillo, "SARGENTO RIVAS");
        CrearTexto(marco, fuente, new Rect(640, 241, 494, 45), 22f, TextoSecundario,
            "BASE MILITAR · CANAL DE EMERGENCIA");
        AgregarFondo(CrearRect("Señal activa", marco, new Rect(1154, 258, 14, 14)),
            new Color32(198, 36, 29, 255));

        panel.mensaje = CrearTexto(marco, fuente, new Rect(323, 69, 841, 162), 39f, Texto, "");
        panel.mensaje.alignment = TextAlignmentOptions.TopLeft;
        panel.mensaje.textWrappingMode = TextWrappingModes.Normal;
        panel.mensaje.enableAutoSizing = true;
        panel.mensaje.fontSizeMin = 31f;
        panel.mensaje.fontSizeMax = 39f;

        RectTransform marcoAccion = CrearRect("Tecla de diálogo", marco, new Rect(868, 19, 302, 41));
        AgregarFondo(marcoAccion, Borde);
        AgregarFondo(CrearRect("Fondo de tecla", marcoAccion, new Rect(2, 2, 298, 37)), Oliva);
        panel.accion = CrearTexto(marcoAccion, fuente, new Rect(0, 0, 302, 41), 22f, Texto, "");
        panel.accion.alignment = TextAlignmentOptions.Center;
        panel.MostrarPagina();
        return panel;
    }

    private void MostrarPagina()
    {
        radio.Stop();
        radio.clip = audios != null && pagina < audios.Length ? audios[pagina] : null;
        if (radio.clip != null)
            radio.Play();

        mensaje.text = Paginas[pagina];
        mensaje.ForceMeshUpdate();
        caracteres = mensaje.textInfo.characterCount;
        mensaje.maxVisibleCharacters = 0;
        paginaCompleta = false;
        tiempoPagina = 0f;
        demoraTexto = radio.clip == null ? 0f : pagina == 0 ? 0.44f : 0.16f;
        duracionTexto = radio.clip == null
            ? caracteres / 28f
            : Mathf.Max(1f, radio.clip.length - demoraTexto - 0.4f);
        accion.text = "[ENTER] MOSTRAR TEXTO";
    }

    private void Update()
    {
        float escala = Mathf.Min(1f, hud.rect.width * 0.62f / (TamanoPanel.x + 44f));
        transform.localScale = Vector3.one * escala;

        if (pausado != PauseController.LocalPlayerPaused)
        {
            pausado = PauseController.LocalPlayerPaused;
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
            float avance = (tiempoPagina - demoraTexto) / duracionTexto;
            mensaje.maxVisibleCharacters = Mathf.FloorToInt(Mathf.Clamp01(avance) * caracteres);
            if (avance >= 1f)
                RevelarPagina();
        }

        Keyboard teclado = Keyboard.current;
        if (teclado != null && (teclado.enterKey.wasPressedThisFrame ||
                               teclado.numpadEnterKey.wasPressedThisFrame))
            Avanzar();
    }

    private void RevelarPagina()
    {
        paginaCompleta = true;
        mensaje.maxVisibleCharacters = caracteres;
        accion.text = pagina == Paginas.Length - 1 ? "[ENTER] CERRAR" : "[ENTER] CONTINUAR";
    }

    private void Avanzar()
    {
        if (!paginaCompleta)
        {
            RevelarPagina();
            return;
        }

        if (pagina == Paginas.Length - 1)
        {
            alCerrar?.Invoke();
            return;
        }

        pagina++;
        MostrarPagina();
    }

    private void CrearRetrato(RawImage imagen, GameObject prefab, RuntimeAnimatorController controlador)
    {
        int capa = LayerMask.NameToLayer("CharacterPreview");
        if (prefab == null || capa < 0)
        {
            imagen.color = Oliva;
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
        camara.backgroundColor = Oliva;
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

    private static void AgregarFondo(RectTransform rect, Color color)
    {
        Image imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
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
