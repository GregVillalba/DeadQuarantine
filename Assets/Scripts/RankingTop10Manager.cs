using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class RankingTop10Manager : MonoBehaviour
{
    [Serializable]
    public class JugadorRanking
    {
        public string puesto;   // "1".."5", "..." o el puesto real del usuario
        public string user_id;
        public string username;
        public int puntos;
        public string modo;
        public string dificultad; // Recibido desde MySQL
    }

    public static class JsonHelper
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

    [Header("Conexión URL")]
    [SerializeField] private string rankingUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";

    [Header("Referencias de UI - Tabla")]
    [SerializeField] private Transform contenedorFilas;
    [SerializeField] private GameObject prefabFila;
    [SerializeField] private TMP_Text txtEstado;

    [Header("Dropdowns de Filtro")]
    [SerializeField] private TMP_Dropdown dropdownOrden;
    [SerializeField] private TMP_Dropdown dropdownModo;
    [SerializeField] private TMP_Dropdown dropdownDificultad; // <-- Dropdown para filtrar Dificultad

    [Header("Colores Podio")]
    [SerializeField] private Color colorOro = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color colorPlata = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color colorBronce = new Color(0.8f, 0.5f, 0.2f);
    [SerializeField] private Color colorResto = Color.white;
    [SerializeField] private Color colorMiFila = new Color(0.2f, 1f, 0.5f);

    private Coroutine cargaActual;

    [Header("Mi registro")]
    [SerializeField] private bool mostrarMensajeSinRegistro = true;
    [SerializeField] private string mensajeSinRegistro = "Aún no tenés puntaje";

    private void Awake()
    {
        if (dropdownModo != null)
            dropdownModo.onValueChanged.AddListener(_ => ActualizarRanking());

        if (dropdownDificultad != null)
            dropdownDificultad.onValueChanged.AddListener(_ => ActualizarRanking());

        if (dropdownOrden != null)
            dropdownOrden.onValueChanged.AddListener(_ => ActualizarRanking());
    }

    private void OnEnable()
    {
        ActualizarRanking();
    }

    public void ActualizarRanking()
    {
        if (cargaActual != null) StopCoroutine(cargaActual);
        cargaActual = StartCoroutine(CargarRankingDesdeURL());
    }

    private IEnumerator CargarRankingDesdeURL()
    {
        MostrarEstado("Cargando Ranking...");

        string url = $"{rankingUrl}?user_id={UnityWebRequest.EscapeURL(PlayerIdentity.GetUserId())}";

        if (dropdownModo != null && dropdownModo.value > 0)
        {
            string modo = dropdownModo.options[dropdownModo.value].text.Trim();
            url += "&mode=" + UnityWebRequest.EscapeURL(modo);
        }

        if (dropdownDificultad != null && dropdownDificultad.value > 0)
        {
            string dif = dropdownDificultad.options[dropdownDificultad.value].text.Trim();
            url += "&dificultad=" + UnityWebRequest.EscapeURL(dif);
        }

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                MostrarEstado("Error al conectar con el servidor.");
                Debug.LogWarning($"[RANKING ERROR]: {req.error}\n{req.downloadHandler?.text}");
                yield break;
            }

            JugadorRanking[] datos = null;
            try
            {
                datos = JsonHelper.FromJson<JugadorRanking>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RANKING] Error parseando JSON: " + e.Message);
            }

            Renderizar(datos);
        }
    }

    private void Renderizar(JugadorRanking[] datos)
    {
        foreach (Transform child in contenedorFilas)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        var top = new List<JugadorRanking>();
        JugadorRanking mia = null;
        bool haySeparador = false;

        if (datos != null)
        {
            foreach (var j in datos)
            {
                if (j.puesto == "...") { haySeparador = true; continue; }
                if (haySeparador) mia = j;
                else top.Add(j);
            }
        }

        if (top.Count == 0)
        {
            MostrarEstado("No hay registros disponibles.");
            return;
        }

        if (txtEstado != null) txtEstado.gameObject.SetActive(false);

        if (dropdownOrden != null &&
            dropdownOrden.options[dropdownOrden.value].text.ToLower().Contains("descendente"))
        {
            top.Reverse();
        }

        string miId = PlayerIdentity.GetUserId();

        foreach (var j in top)
            CrearFilaUI(j, esMiFila: j.user_id == miId);

        if (haySeparador && mia != null)
        {
            CrearFilaPuntosSuspensivos();
            CrearFilaUI(mia, esMiFila: true);
        }
        else if (mostrarMensajeSinRegistro && !top.Any(j => j.user_id == miId))
        {
            CrearFilaMensaje(mensajeSinRegistro);
        }
    }

    private void CrearFilaMensaje(string msg)
    {
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
            string.IsNullOrEmpty(j.dificultad) ? "Normal" : j.dificultad // <-- Columna 5: Dificultad
        };

        for (int i = 0; i < t.Length && i < valores.Length; i++)
        {
            t[i].text = valores[i];
            t[i].color = color;
        }
    }

    private void CrearFilaPuntosSuspensivos()
    {
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