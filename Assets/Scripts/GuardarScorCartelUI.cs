using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class GuardarScoreCartelUI : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [SerializeField] private TMP_Text txtTitulo;          // "¿Deseas guardar tu score?"
    [SerializeField] private Button btnOk;                // Objeto 'ok'
    [SerializeField] private Button btnCancel;            // Objeto 'CANCEL'
    [SerializeField] private TMP_Text txtResult;          // Objeto 'result' (muestra el puesto)
    [SerializeField] private TMP_Text txtEstado;          // Objeto 'estado' (muestra si se guardó)

    [Header("Endpoint")]
    [SerializeField] private string apiUrl = "https://dead-quarentine-web.wasmer.app/api/ranking";

    private string nombreJugador;
    private int scoreJugador;
    private string modoJugador;
    private Action onFinalizadoCallback;

    private void Awake()
    {
        if (btnOk != null)
            btnOk.onClick.AddListener(OnOkClicked);

        if (btnCancel != null)
            btnCancel.onClick.AddListener(OnCancelClicked);
    }

    public void AbrirCartel(string username, int puntos, string modo, Action onFinalizado)
    {
        nombreJugador = username;
        scoreJugador = puntos;
        modoJugador = modo;
        onFinalizadoCallback = onFinalizado;

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // Se pone por delante de todo

        if (btnOk != null) btnOk.interactable = true;
        if (btnCancel != null) btnCancel.interactable = true;

        if (txtResult != null) txtResult.text = "";
        if (txtEstado != null) txtEstado.text = "";
    }

    private void OnOkClicked()
    {
        StartCoroutine(EnviarScoreAPI());
    }

    private void OnCancelClicked()
    {
        CerrarCartel();
        onFinalizadoCallback?.Invoke();
    }

    private void CerrarCartel()
    {
        gameObject.SetActive(false);
    }

    [Serializable]
    private class ScorePayload
    {
        public string username;
        public int score;
        public string modo;
    }

    [Serializable]
    private class ScoreResponse
    {
        public string mensaje;
        public int puesto;
        public bool success;
    }

    private IEnumerator EnviarScoreAPI()
    {
        if (btnOk != null) btnOk.interactable = false;
        if (txtEstado != null) txtEstado.text = "Guardando...";

        ScorePayload payload = new ScorePayload
        {
            username = nombreJugador,
            score = scoreJugador,
            modo = modoJugador
        };

        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (txtEstado != null)
                {
                    txtEstado.text = "¡Score guardado con éxito!";
                    txtEstado.color = Color.green;
                }

                // Intentar parsear el puesto si la API devuelve JSON con 'puesto'
                string respuesta = req.downloadHandler.text;
                try
                {
                    ScoreResponse res = JsonUtility.FromJson<ScoreResponse>(respuesta);
                    if (res != null && res.puesto > 0 && txtResult != null)
                        txtResult.text = $"Puesto: #{res.puesto}";
                }
                catch
                {
                    if (txtResult != null)
                        txtResult.text = "Top Ranking Actualizado";
                }

                // Esperamos 1.5s para que el usuario lea su puesto y avisamos a PauseController
                yield return new WaitForSeconds(1.5f);
                CerrarCartel();
                onFinalizadoCallback?.Invoke();
            }
            else
            {
                if (txtEstado != null)
                {
                    txtEstado.text = "Error al guardar score";
                    txtEstado.color = Color.red;
                }

                if (btnOk != null) btnOk.interactable = true;

                // Aun en error, tras 2s cerramos y devolvemos el control
                yield return new WaitForSeconds(2f);
                CerrarCartel();
                onFinalizadoCallback?.Invoke();
            }
        }
    }
}