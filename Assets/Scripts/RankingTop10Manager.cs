using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class RankingTop10Manager : MonoBehaviour
{
    [Serializable]
    public class JugadorRanking
    {
        public string puesto;      // "1".."5", "..." o el puesto real del usuario
        public string user_id;
        public string username;
        public int puntos;
        public string modo;
        public string dificultad;
    }

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return new T[0];

            string raw = json.Trim();
            if (!raw.StartsWith("[")) raw = "[" + raw + "]";

            string newJson = "{\"array\":" + raw + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper != null && wrapper.array != null ? wrapper.array : new T[0];
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

    [Header("Conexión URL")]
    [SerializeField] private string rankingUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";

    [Header("Referencias de UI - Tabla")]
    [SerializeField] private Transform contenedorFilas;
    [SerializeField] private GameObject prefabFila;
    [SerializeField] private TMP_Text txtEstado;

    [Header("Dropdowns de Filtro (índice 0 = Todos)")]
    [SerializeField] private TMP_Dropdown dropdownOrden;
    [SerializeField] private TMP_Dropdown dropdownModo;
    [SerializeField] private TMP_Dropdown dropdownDificultad;

    [Header("Colores Podio")]
    [SerializeField] private Color colorOro = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color colorPlata = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color colorBronce = new Color(0.8f, 0.5f, 0.2f);
    [SerializeField] private Color colorResto = Color.white;
    [SerializeField] private Color colorMiFila = new Color(0.2f, 1f, 0.5f);

    [Header("Mi registro")]
    [SerializeField] private bool mostrarMensajeSinRegistro = true;
    [SerializeField] private string mensajeSinRegistro = "Aún no tenés puntaje";

    private Coroutine cargaActual;
    private bool listenersConfigurados;

    private void Awake()
    {
        ConfigurarListeners();
    }

    private void ConfigurarListeners()
    {
        if (listenersConfigurados) return;
        listenersConfigurados = true;

        if (dropdownModo != null) dropdownModo.onValueChanged.AddListener(_ => ActualizarRanking());
        if (dropdownDificultad != null) dropdownDificultad.onValueChanged.AddListener(_ => ActualizarRanking());
        if (dropdownOrden != null) dropdownOrden.onValueChanged.AddListener(_ => ActualizarRanking());
    }

    private void OnEnable()
    {
        ConfigurarListeners();
        ActualizarRanking();
    }

    private void OnDisable()
    {
        cargaActual = null;
    }

    public void ActualizarRanking()
    {
        if (!isActiveAndEnabled) return;

        if (cargaActual != null) StopCoroutine(cargaActual);
        cargaActual = StartCoroutine(CargarRankingDesdeURL());
    }

    // ============================================================
    // CONSTRUCCIÓN DE FILTROS (valores exactos que guarda la base)
    // ============================================================

    private static string Limpiar(string texto)
    {
        return string.IsNullOrEmpty(texto) ? "" : texto.Trim().ToLowerInvariant();
    }

    // La base guarda "singleplayer" y "multiplayer".
    // Acepta textos del dropdown como "Un jugador", "Solitario", "Multijugador", "Online", etc.
    private static string ModoParaApi(string textoDropdown)
    {
        string t = Limpiar(textoDropdown);
        if (string.IsNullOrEmpty(t)) return "";

        if (t.Contains("multi") || t.Contains("online") || t.Contains("linea") || t.Contains("línea"))
            return "multiplayer";

        if (t.Contains("single") || t.Contains("solo") || t.Contains("un jugador") ||
            t.Contains("individual") || t.Contains("solit"))
            return "singleplayer";

        return t;
    }

    // La base guarda "normal", "pesadilla", etc. en minúscula.
    private static string DificultadParaApi(string textoDropdown)
    {
        return Limpiar(textoDropdown);
    }

    private string ConstruirUrl(string idLimpio)
    {
        // Limpia la URL base si tuviera caracteres sobrantes al final
        string url = rankingUrl.TrimEnd('&', '?');

        // Determina el separador correcto (? si no hay query params previos, & si ya existen)
        char separador = url.Contains("?") ? '&' : '?';

        if (!string.IsNullOrEmpty(idLimpio))
        {
            url += $"{separador}user_id={UnityWebRequest.EscapeURL(idLimpio)}";
            separador = '&';
        }

        if (dropdownModo != null && dropdownModo.value > 0 &&
            dropdownModo.value < dropdownModo.options.Count)
        {
            string modo = ModoParaApi(dropdownModo.options[dropdownModo.value].text);
            if (!string.IsNullOrEmpty(modo))
            {
                url += $"{separador}mode={UnityWebRequest.EscapeURL(modo)}";
                separador = '&';
            }
        }

        if (dropdownDificultad != null && dropdownDificultad.value > 0 &&
            dropdownDificultad.value < dropdownDificultad.options.Count)
        {
            string dif = DificultadParaApi(dropdownDificultad.options[dropdownDificultad.value].text);
            if (!string.IsNullOrEmpty(dif))
            {
                url += $"{separador}dificultad={UnityWebRequest.EscapeURL(dif)}";
            }
        }

        return url;
    }

    // ============================================================
    // CARGA
    // ============================================================

    private IEnumerator CargarRankingDesdeURL()
    {
        MostrarEstado("Cargando Ranking...");

        string miId = PlayerIdentity.GetUserId();
        string miUsername = PlayerIdentity.GetUsername();
        string idLimpio = !string.IsNullOrEmpty(miId) ? miId.Trim() : "";

        string url = ConstruirUrl(idLimpio);
        Debug.Log($"<color=yellow>[RANKING GET]</color> {url}");

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            req.SetRequestHeader("Accept", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                MostrarEstado("Error al conectar con el servidor.");
                Debug.LogWarning($"[RANKING ERROR] {req.error} (HTTP {req.responseCode})\n{req.downloadHandler?.text}");
                cargaActual = null;
                yield break;
            }

            string rawJson = req.downloadHandler.text;
            Debug.Log($"<color=cyan>[RANKING JSON RECIBIDO]</color>\n{rawJson}");

            JugadorRanking[] datos = null;
            try
            {
                datos = JsonHelper.FromJson<JugadorRanking>(rawJson);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RANKING] Error parseando JSON: " + e.Message);
            }

            Renderizar(datos, idLimpio, miUsername);
        }

        cargaActual = null;
    }

    // ============================================================
    // RENDER
    // ============================================================

    private void LimpiarFilas()
    {
        if (contenedorFilas == null) return;

        for (int i = contenedorFilas.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = contenedorFilas.GetChild(i).gameObject;
            hijo.SetActive(false);
            Destroy(hijo);
        }
    }

    private void Renderizar(JugadorRanking[] datos, string miId, string miUsername)
    {
        LimpiarFilas();

        var top = new List<JugadorRanking>();
        JugadorRanking filaPropiaFueraDeTop = null;
        bool haySeparador = false;

        if (datos != null)
        {
            foreach (var j in datos)
            {
                if (j == null) continue;

                if (j.puesto == "...")
                {
                    haySeparador = true;
                    continue;
                }

                if (haySeparador) filaPropiaFueraDeTop = j;
                else top.Add(j);
            }
        }

        Debug.Log($"[RANKING] miId='{miId}' miUser='{miUsername}' top={top.Count} separador={haySeparador} propia={(filaPropiaFueraDeTop != null)}");

        if (top.Count == 0 && filaPropiaFueraDeTop == null)
        {
            MostrarEstado("No hay registros disponibles.");
            return;
        }

        if (txtEstado != null) txtEstado.gameObject.SetActive(false);

        if (dropdownOrden != null && dropdownOrden.options.Count > dropdownOrden.value &&
            dropdownOrden.options[dropdownOrden.value].text.ToLowerInvariant().Contains("descendente"))
        {
            top.Reverse();
        }

        bool miFilaEncontrada = false;

        foreach (var j in top)
        {
            bool esMio = EsMiRegistro(j, miId, miUsername);
            if (esMio) miFilaEncontrada = true;
            CrearFilaUI(j, esMio);
        }

        if (haySeparador && filaPropiaFueraDeTop != null)
        {
            CrearFilaPuntosSuspensivos();
            CrearFilaUI(filaPropiaFueraDeTop, true);
            miFilaEncontrada = true;
        }

        if (mostrarMensajeSinRegistro && !miFilaEncontrada)
            CrearFilaMensaje(mensajeSinRegistro);
    }

    private bool EsMiRegistro(JugadorRanking j, string miId, string miUsername)
    {
        if (j == null) return false;

        // Prioridad: comparar por ID
        if (!string.IsNullOrEmpty(miId) && !string.IsNullOrEmpty(j.user_id))
        {
            return string.Equals(j.user_id.Trim(), miId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // Solo si la fila no trae ID, se compara por nombre
        if (!string.IsNullOrEmpty(miUsername) && !string.IsNullOrEmpty(j.username))
        {
            return string.Equals(j.username.Trim(), miUsername.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void CrearFilaMensaje(string msg)
    {
        if (prefabFila == null || contenedorFilas == null) return;

        GameObject filaObj = Instantiate(prefabFila, contenedorFilas);
        TMP_Text[] t = filaObj.GetComponentsInChildren<TMP_Text>();
        for (int i = 0; i < t.Length; i++)
        {
            t[i].text = i == 1 ? msg : "";
            t[i].color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        }
    }

    private void CrearFilaUI(JugadorRanking j, bool esMiFila)
    {
        if (prefabFila == null || contenedorFilas == null) return;

        GameObject filaObj = Instantiate(prefabFila, contenedorFilas);
        TMP_Text[] t = filaObj.GetComponentsInChildren<TMP_Text>();

        int.TryParse(j.puesto, out int puesto);
        Color color = esMiFila ? colorMiFila : ObtenerColorPuesto(puesto);

        string[] valores =
        {
            $"{j.puesto}°",
            esMiFila ? $"{j.username} (Tú)" : j.username,
            $"{j.puntos:N0} PTS",
            j.modo,
            string.IsNullOrEmpty(j.dificultad) ? "Normal" : j.dificultad
        };

        for (int i = 0; i < t.Length && i < valores.Length; i++)
        {
            t[i].text = valores[i];
            t[i].color = color;
        }
    }

    private void CrearFilaPuntosSuspensivos()
    {
        if (prefabFila == null || contenedorFilas == null) return;

        GameObject filaObj = Instantiate(prefabFila, contenedorFilas);
        foreach (var txt in filaObj.GetComponentsInChildren<TMP_Text>())
        {
            txt.text = "...";
            txt.color = new Color(0.8f, 0.8f, 0.8f, 0.6f);
        }
    }

    private void MostrarEstado(string msg)
    {
        if (txtEstado == null) return;
        txtEstado.gameObject.SetActive(true);
        txtEstado.text = msg;
    }

    private Color ObtenerColorPuesto(int puesto)
    {
        if (puesto == 1) return colorOro;
        if (puesto == 2) return colorPlata;
        if (puesto == 3) return colorBronce;
        return colorResto;
    }
}