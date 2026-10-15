using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class GuardarScoreCartelUI : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [SerializeField] private TMP_Text txtTitulo;   // "¿Deseas guardar tu score?"
    [SerializeField] private Button btnOk;         // Objeto 'ok'
    [SerializeField] private TMP_Text txtBtnOk;    // Texto dentro de btnOk (si está vacío, se busca automáticamente)
    [SerializeField] private Button btnCancel;     // Objeto 'CANCEL'
    [SerializeField] private TMP_Text txtResult;   // Label 'result': puesto en el ranking
    [SerializeField] private TMP_Text txtEstado;   // Label de estado ("Se subió correctamente" / "Acceso denegado")

    [Header("Endpoint")]
    [SerializeField] private string apiUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";

    [Header("Mensajes y Tiempos")]
    [SerializeField] private string mensajeExito = "Se subió correctamente";
    [SerializeField] private string mensajeCancelado = "Acceso denegado";
    [SerializeField] private string mensajeError = "No se pudo subir. Reintentá o cancelá.";
    [SerializeField] private float segundosAntesDeCerrar = 1.5f;

    [Serializable]
    private class RankingEntry
    {
        public string username;
        public int puntos;
    }

    [Serializable]
    private class GeoDataResponse
    {
        public string ip;             // IP pública
        public string country;        // Nombre del país (ej: Argentina)
        public string country_code;   // Código ISO (ej: AR)
        public ConnectionInfo connection;

        [Serializable]
        public class ConnectionInfo
        {
            public bool is_vpn;       // Detecta VPN/Proxy
        }
    }

    [Serializable]
    private class ScorePayload
    {
        public string username;
        public int puntos;
        public string modo;
        public string ip;
        public string pais;
        public string codigo_pais;
        public int usa_vpn;
        public string creado_en;
    }

    private static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

    private string nombreJugador;
    private int scoreJugador;
    private string modoJugador;
    private Action onFinalizadoCallback;
    private Color colorOriginalBtnOk = Color.white;

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

    /// <summary>
    /// Abre el panel de score sobre la pantalla de derrota.
    /// </summary>
    public void AbrirCartel(string username, int puntos, string modo, Action onFinalizado, MonoBehaviour runner = null)
    {
        nombreJugador = string.IsNullOrWhiteSpace(username) ? "Superviviente" : username;
        scoreJugador = puntos;
        modoJugador = modo;
        onFinalizadoCallback = onFinalizado;

        gameObject.SetActive(true);
        enabled = true;
        transform.localScale = Vector3.one;
        transform.SetAsLastSibling();

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(true);
        }

        if (txtTitulo != null)
            txtTitulo.text = $"¿Deseas guardar tu score de {puntos:N0} pts en el ranking?";

        if (txtEstado != null)
        {
            txtEstado.text = "";
            txtEstado.color = Color.white;
        }

        if (txtResult != null)
            txtResult.text = "Calculando puesto...";

        // Restaurar estado original del botón OK
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

        MonoBehaviour coroutineHost = (runner != null && runner.gameObject.activeInHierarchy) ? runner : this;

        if (coroutineHost.gameObject.activeInHierarchy)
        {
            coroutineHost.StartCoroutine(CalcularPuesto());
        }
        else
        {
            Debug.LogError("[GuardarScoreCartelUI] Host inactivo para corrutina.");
        }
    }

    private void OnCancelClicked()
    {
        Debug.Log("<color=red>[GuardarScoreCartelUI] Clic en CANCEL: Acción cancelada.</color>");
        PintarBotonNaranjaOscuro(btnCancel);
        StartCoroutine(CancelarCoroutine());
    }
    private void CerrarYAvisar()
    {
        gameObject.SetActive(false);
        onFinalizadoCallback?.Invoke();
    }

    private IEnumerator CalcularPuesto()
    {
        Debug.Log("[GuardarScoreCartelUI] Consultando API para calcular puesto...");

        using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GuardarScoreCartelUI] Error al obtener ranking: {req.error}");
                if (txtResult != null) txtResult.text = "Puesto: no disponible (sin conexión)";
                yield break;
            }

            RankingEntry[] lista = null;
            try
            {
                lista = JsonHelper.FromJson<RankingEntry>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GuardarScoreCartelUI] Error parseando JSON: " + e.Message);
            }

            if (lista == null || txtResult == null)
            {
                if (txtResult != null) txtResult.text = "Puesto: no disponible";
                yield break;
            }

            int puesto = lista.Count(j => j.puntos > scoreJugador) + 1;
            Debug.Log($"<color=cyan>[GuardarScoreCartelUI] Puesto calculado: #{puesto}</color>");

            txtResult.text = puesto <= 10
                ? $"Tu score entra en el puesto #{puesto} del Top 10"
                : $"Tu puesto estimado: #{puesto}";
        }
    }

    private void OnOkClicked()
    {
        Debug.Log("<color=green>[GuardarScoreCartelUI] Clic en OK / Reintentar...</color>");
        PintarBotonNaranjaOscuro(btnOk);
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

        // Endpoint seguro con HTTPS y gratuito
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
                        ipDetectada = !string.IsNullOrEmpty(geo.ip) ? geo.ip : ipDetectada;
                        paisDetectado = !string.IsNullOrEmpty(geo.country) ? geo.country : paisDetectado;
                        codigoPaisDetectado = !string.IsNullOrEmpty(geo.country_code) ? geo.country_code : codigoPaisDetectado;
                        usaVpnDetectado = (geo.connection != null && geo.connection.is_vpn) ? 1 : 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GuardarScoreCartelUI] Error parseando datos de IP: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[GuardarScoreCartelUI] No se pudo consultar GeoIP ({geoReq.error}). Usando valores por defecto.");
            }
        }

        // Ejecuta la subida a tu backend con los datos obtenidos
        yield return StartCoroutine(SubirScore(ipDetectada, paisDetectado, codigoPaisDetectado, usaVpnDetectado));
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
        ActualizarTextoBotonOk("OK");
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

    private void PintarBotonNaranjaOscuro(Button btn)
    {
        if (btn != null)
        {
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = new Color(0.8f, 0.35f, 0.0f, 1f);
        }
    }

    private void BloquearBotones(bool bloquear)
    {
        if (btnOk != null) btnOk.interactable = !bloquear;
        if (btnCancel != null) btnCancel.interactable = !bloquear;
    }

    private IEnumerator SubirScore(string ip, string pais, string codigoPais, int usaVpn)
    {
        if (txtEstado != null)
        {
            txtEstado.color = Color.white;
            txtEstado.text = "Guardando...";
        }

        ScorePayload payload = new ScorePayload
        {
            username = nombreJugador,
            puntos = scoreJugador,
            modo = modoJugador,
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
                Debug.Log($"<color=green>[GuardarScoreCartelUI] Score subido con éxito. HTTP {httpCode}</color>");

                if (txtEstado != null)
                {
                    txtEstado.color = Color.green;
                    txtEstado.text = mensajeExito;
                }

                yield return new WaitForSecondsRealtime(segundosAntesDeCerrar);
                CerrarYAvisar();
            }
            else
            {
                Debug.LogError($"<color=red>[GuardarScoreCartelUI] ERROR AL GUARDAR SCORE:</color>\n" +
                               $"- HTTP Code: {httpCode}\n" +
                               $"- WebRequest Error: {req.error}\n" +
                               $"- Respuesta del Servidor: {respuestaServidor}\n" +
                               $"- Payload enviado: {json}");

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


}