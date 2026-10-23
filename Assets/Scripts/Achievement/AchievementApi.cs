using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LogrosApiClient : MonoBehaviour
{
    private static LogrosApiClient _instancia;
    public static LogrosApiClient Instancia
    {
        get
        {
            if (_instancia == null)
            {
                GameObject go = new GameObject("[LogrosApiClient]");
                _instancia = go.AddComponent<LogrosApiClient>();
                DontDestroyOnLoad(go);
            }
            return _instancia;
        }
    }

    [Header("Configuración Backend")]
    [SerializeField]
    private string urlDesbloquear = "https://dead-quarentine-web.wasmer.app/api/logros/desbloquear";

    [Serializable]
    private class DesbloquearRequestDTO
    {
        public string id_local;
        public string username;
        public string sublogro_clave;
    }

    [Serializable]
    private class DesbloquearResponseDTO
    {
        public bool success;
        public string mensaje;
        public string logro_padre_desbloqueado;
    }

    private void Awake()
    {
        if (_instancia != null && _instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        _instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Obtiene las credenciales locales del jugador.
    /// Ajusta estos métodos según dónde las guarde tu juego (PlayerPrefs, UserManager, etc.)
    /// </summary>
    public static string ObtenerIdLocal()
    {
        // Ejemplo si está en PlayerPrefs:
        if (PlayerPrefs.HasKey("UsuarioIdLocal"))
            return PlayerPrefs.GetString("UsuarioIdLocal");

        // Si no existe, creamos uno único persistente:
        string nuevoId = System.Guid.NewGuid().ToString();
        PlayerPrefs.SetString("UsuarioIdLocal", nuevoId);
        PlayerPrefs.Save();
        return nuevoId;
    }

    public static string ObtenerUsernameLocal()
    {
        // Ejemplo si está en PlayerPrefs:
        return PlayerPrefs.GetString("UsuarioNombreLocal", "Jugador");
    }

    /// <summary>
    /// Desbloqueo remoto resolviendo el usuario automáticamente.
    /// </summary>
    public static void DesbloquearSublogroRemoto(string sublogroClave)
    {
        string idLocal = ObtenerIdLocal();
        string username = ObtenerUsernameLocal();

        DesbloquearSublogroRemoto(sublogroClave, idLocal, username);
    }

    /// <summary>
    /// Desbloqueo remoto pasando usuario y id explícitos.
    /// </summary>
    public static void DesbloquearSublogroRemoto(string sublogroClave, string idLocal, string username)
    {
        if (string.IsNullOrEmpty(sublogroClave) || string.IsNullOrEmpty(idLocal) || string.IsNullOrEmpty(username))
        {
            Debug.LogWarning("[LogrosApi] Datos incompletos para reportar el sublogro.");
            return;
        }

        Instancia.StartCoroutine(Instancia.EnviarDesbloqueoCoroutine(sublogroClave, idLocal, username));
    }

    private IEnumerator EnviarDesbloqueoCoroutine(string sublogroClave, string idLocal, string username)
    {
        DesbloquearRequestDTO requestData = new DesbloquearRequestDTO
        {
            id_local = idLocal,
            username = username,
            sublogro_clave = sublogroClave
        };

        string jsonPayload = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest www = new UnityWebRequest(urlDesbloquear, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Accept", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[LogrosApi] Error al registrar sublogro '{sublogroClave}': {www.error} - Resp: {www.downloadHandler.text}");
            }
            else
            {
                DesbloquearResponseDTO respuesta = JsonUtility.FromJson<DesbloquearResponseDTO>(www.downloadHandler.text);
                Debug.Log($"[LogrosApi] Sublogro '{sublogroClave}' registrado con éxito en la base de datos.");

                if (!string.IsNullOrEmpty(respuesta?.logro_padre_desbloqueado))
                {
                    Debug.Log($"[LogrosApi] ¡LOGRO PADRE DESBLOQUEADO!: {respuesta.logro_padre_desbloqueado}");
                }
            }
        }
    }
}