using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Globalization;

public class GuardarScoreCartelUI : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [SerializeField] private TMP_Text txtTitulo;
    [SerializeField] private Button btnOk;
    [SerializeField] private TMP_Text txtBtnOk;
    [SerializeField] private Button btnCancel;
    [SerializeField] private TMP_Text txtResult;
    [SerializeField] private TMP_Text txtEstado;

    [Header("Endpoint")]
    [SerializeField] private string apiUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";

    [Header("Mensajes y Tiempos")]
    [SerializeField] private string mensajeExito = "Se subió correctamente";
    [SerializeField] private string mensajeCancelado = "Acceso denegado";
    [SerializeField] private string mensajeError = "No se pudo subir. Reintentá o cancelá.";
    [SerializeField] private float segundosAntesDeCerrar = 1.5f;

    [Header("Reglas de Guardado")]
    [SerializeField] private int puntajeMinimoParaGuardar = 50;

    [Serializable]
    private class GeoDataResponse
    {
        public string ip;
        public string country;
        public string country_code;
        public ConnectionInfo connection;

        [Serializable]
        public class ConnectionInfo
        {
            public bool is_vpn;
        }
    }

    [Serializable]
    private class ScorePayload
    {
        public string user_id;       // UUID persistente del dispositivo
        public string username;
        public int puntos;
        public string modo;
        public string ip;
        public string pais;
        public string codigo_pais;
        public int usa_vpn;
        public string creado_en;
        public string dificultad;
    }

    private string nombreJugador;
    private string dificultadJugador;
    private int scoreJugador;
    private string modoJugador;
    private Action onFinalizadoCallback;
    private Color colorOriginalBtnOk = Color.white;
    private RectTransform vistaRanking;
    private RectTransform cortinaRanking;
    private Coroutine consultaPuesto;

    [Serializable]
    private class PuestoResponse
    {
        public int puesto;
    }

    private IEnumerator ConsultarPuestoProyectado()
    {
        char separador = apiUrl.Contains("?") ? '&' : '?';
        string url = $"{apiUrl}{separador}score={scoreJugador.ToString(CultureInfo.InvariantCulture)}" +
            $"&mode={UnityWebRequest.EscapeURL(modoJugador)}" +
            $"&dificultad={UnityWebRequest.EscapeURL(dificultadJugador.ToLowerInvariant())}";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            PuestoResponse resultado = null;
            if (req.result == UnityWebRequest.Result.Success)
            {
                try { resultado = JsonUtility.FromJson<PuestoResponse>(req.downloadHandler.text); }
                catch (ArgumentException) { }
            }
            txtResult.text = resultado is { puesto: > 0 } ? $"{resultado.puesto}.º" : "NO DISPONIBLE";
        }
        consultaPuesto = null;
    }

    private void DetenerConsultaPuesto()
    {
        if (consultaPuesto == null)
            return;
        StopCoroutine(consultaPuesto);
        consultaPuesto = null;
    }

    private void AjustarCortina()
    {
        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null || cortinaRanking == null)
            return;
        Canvas.ForceUpdateCanvases();
        var raiz = (RectTransform)canvas.transform;
        Vector3 inferior = transform.InverseTransformPoint(raiz.TransformPoint(raiz.rect.min));
        Vector3 superior = transform.InverseTransformPoint(raiz.TransformPoint(raiz.rect.max));
        cortinaRanking.sizeDelta = new Vector2(Mathf.Abs(superior.x - inferior.x), Mathf.Abs(superior.y - inferior.y));
        Vector3 centro = (inferior + superior) * .5f;
        cortinaRanking.localPosition = new Vector3(centro.x, centro.y, 0f);
    }

    private void ConstruirVista()
    {
        if (vistaRanking != null)
            return;
        foreach (Transform child in transform)
            child.gameObject.SetActive(false);
        if (TryGetComponent<Image>(out var fondo))
            fondo.color = Color.clear;

        cortinaRanking = PartidaMenuVisual.Caja(transform, "FondoRanking", 0, 0, 0, 0, new Color32(7, 11, 16, 184));
        cortinaRanking.anchorMin = cortinaRanking.anchorMax = cortinaRanking.pivot = new Vector2(.5f, .5f);
        cortinaRanking.GetComponent<Image>().raycastTarget = true;

        vistaRanking = PartidaMenuVisual.Panel(transform, "GuardarRankingActualizado", 670, 1300);
        vistaRanking.localScale = new Vector3(1.35f, 1.35f, 1f);
        var textoClaro = new Color32(226, 235, 240, 255);
        PartidaMenuVisual.Texto(vistaRanking, "Modo", "SUPERVIVENCIA · DERROTA", 64, 52, 1172, 38, 28, true).color = textoClaro;
        PartidaMenuVisual.Texto(vistaRanking, "Titulo", "GUARDAR EN EL RANKING", 64, 93, 1172, 105, 90, true);
        PartidaMenuVisual.Linea(vistaRanking, 64, 226, 1172);
        txtTitulo = PartidaMenuVisual.Texto(vistaRanking, "Puntos", "", 64, 240, 350, 123, 105, true);
        PartidaMenuVisual.Texto(vistaRanking, "Unidad", "PTS", 425, 306, 80, 44, 29, true).color = textoClaro;
        var etiquetaPuesto = PartidaMenuVisual.Texto(vistaRanking, "EtiquetaPuesto", "PUESTO", 956, 254, 280, 39, 29, true);
        etiquetaPuesto.alignment = TextAlignmentOptions.MidlineRight;
        etiquetaPuesto.color = PartidaMenuVisual.Verde;
        txtResult = PartidaMenuVisual.Texto(vistaRanking, "Puesto", "—", 936, 293, 300, 61, 48, true);
        txtResult.alignment = TextAlignmentOptions.MidlineRight;
        txtResult.color = PartidaMenuVisual.Verde;
        PartidaMenuVisual.Linea(vistaRanking, 64, 374, 1172);
        PartidaMenuVisual.Texto(vistaRanking, "Pregunta", "¿Querés guardar tu puntuación en el ranking de Supervivencia?", 64, 403, 1172, 54, 37, true).color = textoClaro;
        txtEstado = PartidaMenuVisual.Texto(vistaRanking, "Estado", "", 64, 466, 1172, 43, 28, true);
        btnCancel = PartidaMenuVisual.Boton(vistaRanking, "Cancelar", "CANCELAR", 570, 533);
        btnOk = PartidaMenuVisual.Boton(vistaRanking, "Guardar", "GUARDAR PUNTUACIÓN", 910, 533, true);
        txtBtnOk = btnOk.GetComponentInChildren<TMP_Text>();
        colorOriginalBtnOk = btnOk.targetGraphic.color;
    }

    private void Awake()
    {
        gameObject.SetActive(false);

        if (btnOk != null)
        {
            if (txtBtnOk == null)
                txtBtnOk = btnOk.GetComponentInChildren<TMP_Text>();

            var img = btnOk.GetComponent<Image>();
            if (img != null)
                colorOriginalBtnOk = img.color;
        }
    }

    private string ObtenerDificultadActual()
    {
        var cfg = ConfiguracionPartidaSeleccionada.Instancia;
        if (cfg == null) return "Normal";

        var d = cfg.dificultadSeleccionada;
        return d == ConfiguracionPartidaSeleccionada.Dificultad.Ninguna ? "Normal" : d.ToString();
    }

    public void AbrirCartel(string username, int puntos, string modo, Action onFinalizado, MonoBehaviour runner = null, string dificultad = "Normal")
    {
        if (puntos < puntajeMinimoParaGuardar)
        {
            Debug.Log($"[GuardarScoreCartelUI] Score {puntos} menor al mínimo ({puntajeMinimoParaGuardar}). No se guarda.");
            onFinalizado?.Invoke();
            return;
        }

        // Prioridad 1: Obtener directamente de PlayerIdentity si existe guardado
        string nombreActualizado = PlayerIdentity.GetUsername();

        if (string.IsNullOrWhiteSpace(nombreActualizado))
        {
            nombreActualizado = string.IsNullOrWhiteSpace(username) ? "Superviviente" : username;
        }

        nombreJugador = nombreActualizado.Trim();
        scoreJugador = puntos;
        modoJugador = string.IsNullOrWhiteSpace(modo) ? "singleplayer" : modo.ToLower();
        dificultadJugador = ObtenerDificultadActual();
        onFinalizadoCallback = onFinalizado;

        gameObject.SetActive(true);
        enabled = true;
        transform.localScale = Vector3.one;
        transform.SetAsLastSibling();

        ConstruirVista();
        AjustarCortina();
        DetenerConsultaPuesto();

        if (txtTitulo != null)
            txtTitulo.text = puntos.ToString("N0", CultureInfo.GetCultureInfo("es-AR"));

        if (txtEstado != null)
        {
            txtEstado.text = "";
            txtEstado.color = new Color32(226, 235, 240, 255);
        }

        if (txtResult != null)
            txtResult.text = "CONSULTANDO…";

        RestaurarBotonOk();

        if (btnOk != null)
        {
            btnOk.onClick.RemoveAllListeners();
            btnOk.onClick.AddListener(OnOkClicked);
            btnOk.interactable = true;
        }

        if (btnCancel != null)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(OnCancelClicked);
            btnCancel.interactable = true;
        }

        consultaPuesto = StartCoroutine(ConsultarPuestoProyectado());

    }

    private void CerrarYAvisar()
    {
        DetenerConsultaPuesto();
        gameObject.SetActive(false);
        onFinalizadoCallback?.Invoke();
    }

    private void OnOkClicked()
    {
        Debug.Log("<color=green>[GuardarScoreCartelUI] Clic en OK...</color>");
        StartCoroutine(ObtenerDatosRedYSubirScore());
    }

    private IEnumerator ObtenerDatosRedYSubirScore()
    {
        BloquearBotones(true);

        if (txtEstado != null)
        {
            txtEstado.color = Color.white;
            txtEstado.text = "Detectando ubicación...";
        }

        string ipDetectada = "127.0.0.1";
        string paisDetectado = "Desconocido";
        string codigoPaisDetectado = "XX";
        int usaVpnDetectado = 0;

        using (UnityWebRequest geoReq = UnityWebRequest.Get("https://ipwho.is/"))
        {
            geoReq.timeout = 5;
            yield return geoReq.SendWebRequest();

            if (geoReq.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    GeoDataResponse geo = JsonUtility.FromJson<GeoDataResponse>(geoReq.downloadHandler.text);
                    if (geo != null)
                    {
                        if (!string.IsNullOrEmpty(geo.ip)) ipDetectada = geo.ip;
                        if (!string.IsNullOrEmpty(geo.country)) paisDetectado = geo.country;
                        if (!string.IsNullOrEmpty(geo.country_code)) codigoPaisDetectado = geo.country_code;
                        if (geo.connection != null) usaVpnDetectado = geo.connection.is_vpn ? 1 : 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GuardarScoreCartelUI] Error parseando datos de IP: {ex.Message}");
                }
            }
        }

        yield return StartCoroutine(SubirScore(ipDetectada, paisDetectado, codigoPaisDetectado, usaVpnDetectado));
    }

    private IEnumerator SubirScore(string ip, string pais, string codigoPais, int usaVpn)
    {
        if (txtEstado != null)
        {
            txtEstado.color = Color.white;
            txtEstado.text = "Guardando...";
        }

        // Obtener siempre el ID persistente y el nombre fresco
        string currentId = PlayerIdentity.GetUserId();
        string currentUsername = PlayerIdentity.GetUsername();
        if (string.IsNullOrWhiteSpace(currentUsername))
            currentUsername = nombreJugador;

        ScorePayload payload = new ScorePayload
        {
            user_id = currentId,
            username = currentUsername,
            puntos = scoreJugador,
            modo = modoJugador,
            dificultad = dificultadJugador,
            ip = ip,
            pais = pais,
            codigo_pais = codigoPais,
            usa_vpn = usaVpn,
            creado_en = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(payload);
        Debug.Log($"<color=yellow>[GuardarScoreCartelUI] Enviando Payload Completo a {apiUrl}:</color>\n{json}");

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            long httpCode = req.responseCode;
            string respuestaServidor = req.downloadHandler != null ? req.downloadHandler.text : "(sin respuesta)";

            if (req.result == UnityWebRequest.Result.Success && (httpCode >= 200 && httpCode < 300))
            {
                Debug.Log($"<color=green>[GuardarScoreCartelUI] Score subido con éxito para {currentUsername} (ID: {currentId}). HTTP {httpCode}</color>");

                if (txtEstado != null)
                {
                    txtEstado.color = Color.green;
                    txtEstado.text = mensajeExito;
                }

                CerrarYAvisar();
            }
            else
            {
                Debug.LogError($"<color=red>[GuardarScoreCartelUI] ERROR AL GUARDAR SCORE:</color>\n" +
                               $"- HTTP Code: {httpCode}\n" +
                               $"- Error: {req.error}\n" +
                               $"- Servidor: {respuestaServidor}\n" +
                               $"- Payload: {json}");

                if (txtEstado != null)
                {
                    txtEstado.color = Color.red;
                    txtEstado.text = mensajeError;
                }

                ActualizarTextoBotonOk("Reintentar");
                RestaurarColorBotonOk();
                BloquearBotones(false);
            }
        }
    }

    private void OnCancelClicked()
    {
        StartCoroutine(CancelarCoroutine());
    }

    private IEnumerator CancelarCoroutine()
    {
        BloquearBotones(true);

        if (txtEstado != null)
        {
            txtEstado.color = Color.red;
            txtEstado.text = mensajeCancelado;
        }

        yield return new WaitForSecondsRealtime(segundosAntesDeCerrar);
        CerrarYAvisar();
    }

    private void RestaurarBotonOk()
    {
        ActualizarTextoBotonOk("GUARDAR PUNTUACIÓN");
        RestaurarColorBotonOk();
    }

    private void ActualizarTextoBotonOk(string nuevoTexto)
    {
        if (txtBtnOk == null && btnOk != null)
            txtBtnOk = btnOk.GetComponentInChildren<TMP_Text>();

        if (txtBtnOk != null)
            txtBtnOk.text = nuevoTexto;
    }

    private void RestaurarColorBotonOk()
    {
        if (btnOk != null)
        {
            var img = btnOk.GetComponent<Image>();
            if (img != null)
                img.color = colorOriginalBtnOk;
        }
    }

    private void BloquearBotones(bool bloquear)
    {
        if (btnOk != null) btnOk.interactable = !bloquear;
        if (btnCancel != null) btnCancel.interactable = !bloquear;
    }
}
