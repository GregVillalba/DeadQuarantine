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
    private class ScorePayload
    {
        public string username;
        public int puntos;
        public string modo;
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

    private void Awake()
    {
        // Aseguramos que arranque completamente deshabilitado
        gameObject.SetActive(false);
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

        // 1. Si está adentro de 'scoreUI' u otro panel padre desactivado, encendemos al padre primero
        if (transform.parent != null)
        {
            transform.parent.gameObject.SetActive(true);
            transform.parent.SetAsLastSibling();
        }

        // 2. Encendemos el cartel propio
        gameObject.SetActive(true);
        enabled = true;
        transform.SetAsLastSibling();

        if (txtTitulo != null)
            txtTitulo.text = $"¿Deseas guardar tu score de {puntos:N0} pts en el ranking?";

        if (txtEstado != null)
        {
            txtEstado.text = "";
            txtEstado.color = Color.white;
        }

        if (txtResult != null)
            txtResult.text = "Calculando puesto...";

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

        // Correr la corrutina asegurando que use un runner activo si hiciera falta
        MonoBehaviour coroutineHost = (runner != null && runner.gameObject.activeInHierarchy) ? runner : this;

        if (coroutineHost.gameObject.activeInHierarchy)
        {
            coroutineHost.StartCoroutine(CalcularPuesto());
        }
        else
        {
            Debug.LogError("[GuardarScoreCartelUI] No se pudo iniciar corrutina: host inactivo.");
        }
    }

    // ---------------------------------------------------------
    // Cálculo del puesto en el ranking (GET)
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // Botón OK -> Subir score (POST)
    // ---------------------------------------------------------
    private void OnOkClicked()
    {
        Debug.Log("<color=green>[GuardarScoreCartelUI] Clic en OK: enviando score a la API...</color>");
        PintarBotonNaranjaOscuro(btnOk);
        StartCoroutine(SubirScore());
    }

    private IEnumerator SubirScore()
    {
        BloquearBotones(true);

        if (txtEstado != null)
        {
            txtEstado.color = Color.white;
            txtEstado.text = "Guardando...";
        }

        ScorePayload payload = new ScorePayload
        {
            username = nombreJugador,
            puntos = scoreJugador,
            modo = modoJugador
        };

        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("<color=green>[GuardarScoreCartelUI] Score subido exitosamente.</color>");
                if (txtEstado != null)
                {
                    txtEstado.color = Color.green;
                    txtEstado.text = mensajeExito; // "Se subió correctamente"
                }

                yield return new WaitForSecondsRealtime(segundosAntesDeCerrar);
                CerrarYAvisar();
            }
            else
            {
                Debug.LogWarning($"[GuardarScoreCartelUI] Error al subir: {req.error} | {req.downloadHandler.text}");

                if (txtEstado != null)
                {
                    txtEstado.color = Color.red;
                    txtEstado.text = mensajeError;
                }

                BloquearBotones(false);
            }
        }
    }

    // ---------------------------------------------------------
    // Botón CANCEL -> Acceso denegado
    // ---------------------------------------------------------
    private void OnCancelClicked()
    {
        Debug.Log("<color=red>[GuardarScoreCartelUI] Clic en CANCEL: Acción cancelada.</color>");
        PintarBotonNaranjaOscuro(btnCancel);
        StartCoroutine(CancelarCoroutine());
    }

    private IEnumerator CancelarCoroutine()
    {
        BloquearBotones(true);

        if (txtEstado != null)
        {
            txtEstado.color = Color.red;
            txtEstado.text = mensajeCancelado; // "Acceso denegado"
        }

        yield return new WaitForSecondsRealtime(segundosAntesDeCerrar);
        CerrarYAvisar();
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

    private void CerrarYAvisar()
    {
        gameObject.SetActive(false);

        // Apagamos también al padre scoreUI para que no quede un panel vacío en pantalla
        if (transform.parent != null)
        {
            transform.parent.gameObject.SetActive(false);
        }

        onFinalizadoCallback?.Invoke();
    }
}