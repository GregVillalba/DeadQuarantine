using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

public class GuardarScoreCartelUI : MonoBehaviour
{
    [System.Serializable]
    public class RankingEntry
    {
        public string username;
        public int puntos;
    }

    [System.Serializable]
    public class GuardarScoreRequest
    {
        public string username;
        public int puntos;
        public string modo;
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

    [Header("Endpoints API")]
    [SerializeField] private string getRankingUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";
    [SerializeField] private string postGuardarUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";

    [Header("UI - Elementos del Cartel")]
    [SerializeField] private TMP_Text txtPuesto;           // Label que dice en qué puesto queda
    [SerializeField] private TMP_Text txtMensajePregunta;  // "¿Deseas guardar tu récord?"
    [SerializeField] private Button btnSiGuardar;          // Botón SÍ
    [SerializeField] private Button btnNoOmitir;           // Botón NO
    [SerializeField] private TMP_Text txtEstado;          // Label para "Guardando...", "¡Guardado con éxito!"

    private string nombreJugador;
    private int puntosJugador;
    private string modoJuego;
    private Action accionIrAlMenu;

    /// <summary>
    /// Abre el cartel pasando los datos de la partida y la acción final para volver al menú.
    /// </summary>
    public void AbrirCartel(string username, int puntos, string modo, Action onFinalizarIrAlMenu)
    {
        nombreJugador = string.IsNullOrEmpty(username) ? "Superviviente" : username;
        puntosJugador = puntos;
        modoJuego = modo;
        accionIrAlMenu = onFinalizarIrAlMenu;

        gameObject.SetActive(true);

        if (txtMensajePregunta != null)
            txtMensajePregunta.text = $"¿Deseas registrar tu récord de {puntosJugador:N0} PTS en el Ranking?";

        if (txtEstado != null)
            txtEstado.text = "";

        if (btnSiGuardar != null)
        {
            btnSiGuardar.interactable = true;
            btnSiGuardar.onClick.RemoveAllListeners();
            btnSiGuardar.onClick.AddListener(OnConfirmarGuardado);
        }

        if (btnNoOmitir != null)
        {
            btnNoOmitir.interactable = true;
            btnNoOmitir.onClick.RemoveAllListeners();
            btnNoOmitir.onClick.AddListener(OnRechazarGuardado);
        }

        // Consultamos la API para indicarle al usuario en qué puesto entra su score
        StartCoroutine(CalcularPuestoRanking(puntosJugador));
    }

    private IEnumerator CalcularPuestoRanking(int score)
    {
        if (txtPuesto != null)
            txtPuesto.text = "Calculando puesto...";

        using (UnityWebRequest req = UnityWebRequest.Get(getRankingUrl))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                RankingEntry[] lista = JsonHelper.FromJson<RankingEntry>(req.downloadHandler.text);

                if (lista != null)
                {
                    // Contamos cuántos jugadores tienen más puntos
                    int superados = lista.Count(j => j.puntos > score);
                    int puesto = superados + 1;

                    if (txtPuesto != null)
                    {
                        if (puesto <= 10)
                            txtPuesto.text = $"🏆 ¡Tu puntuación entra en el puesto #{puesto} del Top 10!";
                        else
                            txtPuesto.text = $"Posición en el ranking: Puesto #{puesto}";
                    }
                }
            }
            else
            {
                if (txtPuesto != null)
                    txtPuesto.text = "Puesto no disponible";
            }
        }
    }

    private void OnConfirmarGuardado()
    {
        StartCoroutine(EnviarScoreAPI());
    }

    private IEnumerator EnviarScoreAPI()
    {
        if (btnSiGuardar != null) btnSiGuardar.interactable = false;
        if (btnNoOmitir != null) btnNoOmitir.interactable = false;
        if (txtEstado != null) txtEstado.text = "Guardando puntuación...";

        GuardarScoreRequest payload = new GuardarScoreRequest
        {
            username = nombreJugador,
            puntos = puntosJugador,
            modo = modoJuego
        };

        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(postGuardarUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (txtEstado != null)
                    txtEstado.text = "¡Puntaje guardado con éxito! Redirigiendo...";

                // Breve pausa para que el jugador lea el mensaje antes de ir al menú
                yield return new WaitForSeconds(1.2f);
                CerrarEIrAlMenu();
            }
            else
            {
                if (btnSiGuardar != null) btnSiGuardar.interactable = true;
                if (btnNoOmitir != null) btnNoOmitir.interactable = true;
                if (txtEstado != null)
                    txtEstado.text = "Error al conectar con la base de datos.";
            }
        }
    }

    private void OnRechazarGuardado()
    {
        CerrarEIrAlMenu();
    }

    private void CerrarEIrAlMenu()
    {
        gameObject.SetActive(false);
        accionIrAlMenu?.Invoke();
    }
}