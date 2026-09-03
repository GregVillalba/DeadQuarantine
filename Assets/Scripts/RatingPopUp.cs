using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class RatingPopup : MonoBehaviour
{
    public Button[] estrellas;
    public TextMeshProUGUI textoEstado;

    private string urlWebApp = "https://script.google.com/macros/s/AKfycbzKRaMRKNLNDhNxRDX2jwwEqctZKE3zL6TDVMpM6wVe3V6VGpOQNiPL6faL2d6dlQ4o/exec";
    private string urlIP = "https://api.ipify.org?format=json";

    private bool procesando = false;
    private PauseController pauseController;

    void Awake()
    {
        // busca el PauseController en el mismo Player (padre en la jerarquía)
        pauseController = GetComponentInParent<PauseController>();
    }

    void OnEnable()
    {
        // se reinicia cada vez que se activa el panel
        procesando = false;
        HabilitarBotones();

        if (textoEstado != null)
            textoEstado.text = "";
    }

    void Start()
    {
        for (int i = 0; i < estrellas.Length; i++)
        {
            int index = i;
            estrellas[i].onClick.AddListener(() => Seleccionar(index + 1));
        }
    }

    void Seleccionar(int calificacion)
    {
        if (procesando)
            return;

        procesando = true;
        BloquearBotones();

        Debug.Log("Calificación seleccionada: " + calificacion);
        StartCoroutine(ProcesoCompleto(calificacion));
    }

    IEnumerator ProcesoCompleto(int calificacion)
    {
        SetEstado("Capturando resultado...");

        string direccionIP = "";

        UnityWebRequest ipRequest = UnityWebRequest.Get(urlIP);
        yield return ipRequest.SendWebRequest();

        if (ipRequest.result == UnityWebRequest.Result.Success)
        {
            string respuesta = ipRequest.downloadHandler.text;
            direccionIP = respuesta.Replace("{\"ip\":\"", "").Replace("\"}", "");
        }
        else
        {
            Debug.LogWarning("No se pudo obtener la IP, se deja vacía.");
        }

        SetEstado("Cargando resultado...");

        yield return StartCoroutine(EnviarAGoogleSheets(calificacion, direccionIP));

        procesando = false;

        // esperar un momento para que el jugador lea el resultado final
        yield return new WaitForSeconds(2f);

        if (pauseController != null)
            pauseController.ContinuarDespuesDeRating();
        else
            Debug.LogError("RatingPopup: no se encontró PauseController en el padre.");
    }

    IEnumerator EnviarAGoogleSheets(int calificacion, string direccionIP)
    {
        string fecha = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        string json = "{\"fecha\":\"" + fecha + "\",\"direccion\":\"" + direccionIP + "\",\"calificacion\":" + calificacion + "}";

        UnityWebRequest request = new UnityWebRequest(urlWebApp, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Guardado en Google Sheets correctamente");
            SetEstado("¡Gracias! Tu calificación se guardó correctamente.");
        }
        else
        {
            Debug.LogError("Error al enviar: " + request.error);
            SetEstado("No se pudo guardar tu calificación.");
        }
    }

    void SetEstado(string mensaje)
    {
        if (textoEstado != null)
            textoEstado.text = mensaje;
    }

    void BloquearBotones()
    {
        foreach (var boton in estrellas)
            boton.interactable = false;
    }

    void HabilitarBotones()
    {
        foreach (var boton in estrellas)
            boton.interactable = true;
    }
}