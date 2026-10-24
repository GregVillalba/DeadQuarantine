using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class LogrosGlobalesOrdenados : MonoBehaviour
{
    [Header("Configuración de API")]
    public string urlApi = "https://dead-quarentine-web.wasmer.app/api/logros/globales";

    [System.Serializable]
    public class TarjetaLogroItem
    {
        [Tooltip("El ID exacto del logro en la base de datos (1, 2, 3...)")]
        public int idLogro;
        [Tooltip("El componente TextMeshPro que muestra el porcentaje (ej: GLOBAL 87.5%)")]
        public TextMeshProUGUI textoPorcentaje;
    }

    [Header("Lista de Logros en Orden")]
    [Tooltip("Define aquí los logros en el orden que quieras")]
    public List<TarjetaLogroItem> listaTarjetas = new List<TarjetaLogroItem>();

    [Serializable]
    private class RespuestaApi
    {
        public int total_usuarios;
        public List<LogroDTO> logros;
    }

    [Serializable]
    private class LogroDTO
    {
        public int id;
        public string clave;
        public string titulo;
        public float porcentaje_global;
    }

    private void Start()
    {
        CargarPorcentajes();
    }

    public void CargarPorcentajes()
    {
        StartCoroutine(ObtenerDatosApi());
    }

    private IEnumerator ObtenerDatosApi()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(urlApi))
        {
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Logros] Error al contactar la API: {request.error}");
                PonerErrorEnTodas(" --%");
                yield break;
            }

            string json = request.downloadHandler.text;
            RespuestaApi data = null;

            try
            {
                data = JsonUtility.FromJson<RespuestaApi>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Logros] Error parseando respuesta: {ex.Message}");
                PonerErrorEnTodas(" --%");
                yield break;
            }

            if (data == null || data.logros == null || data.logros.Count == 0)
            {
                PonerErrorEnTodas(" --%");
                yield break;
            }

            Dictionary<int, float> porcentajePorId = new Dictionary<int, float>();
            foreach (var l in data.logros)
            {
                porcentajePorId[l.id] = l.porcentaje_global;
            }

            foreach (var item in listaTarjetas)
            {
                if (item.textoPorcentaje == null) continue;

                if (porcentajePorId.TryGetValue(item.idLogro, out float pct))
                {
                    item.textoPorcentaje.text = $"{pct:0.#}% OBTENIDO";
                }
                else
                {
                    item.textoPorcentaje.text = " --%";
                }
            }
        }
    }

    private void PonerErrorEnTodas(string textoFallo)
    {
        foreach (var item in listaTarjetas)
        {
            if (item.textoPorcentaje != null)
            {
                item.textoPorcentaje.text = textoFallo;
            }
        }
    }
}