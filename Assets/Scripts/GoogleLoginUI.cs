using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

[Serializable]
public class GoogleAuthPayload
{
    public string id_token;
}

[Serializable]
public class PerfilUsuarioData
{
    public int id;
    public string id_local;
    public string google_id;
    public string email;
    public string nombre;
    public int nivel;
    public int record;
}

[Serializable]
public class ApiResponse
{
    public bool success;
    public PerfilUsuarioData usuario;
    public string error;
    public string detalle;
}

public class GoogleLoginUI : MonoBehaviour
{
    [Header("Configuración de Google OAuth")]
    [SerializeField] private string clientId = "630096344135-jd032bjdtp8el1l1kp1fu54je4vqsraa.apps.googleusercontent.com";
    private const string RedirectUri = "http://localhost:5000/callback/";

    [Header("Endpoint Backend")]
    [SerializeField] private string apiAuthUrl = "https://dead-quarentine-web.wasmer.app/api/auth";

    [Header("Referencias de UI")]
    [SerializeField] private Button botonLogin;
    [SerializeField] private TextMeshProUGUI textoEstado;
    [SerializeField] private TextMeshProUGUI textoBienvenida;

    public static PerfilUsuarioData JugadorActivo { get; private set; }

    private void Awake()
    {
        ActualizarEstadoBoton();
    }

    private void OnEnable()
    {
        // Se ejecuta cada vez que se abre la ventana en pantalla
        ActualizarEstadoBoton();
    }

    private void Start()
    {
        ActualizarEstadoBoton();
    }

    public void ActualizarEstadoBoton()
    {
        if (PlayerIdentity.IsGoogleLinked())
        {
            DeshabilitarBotonLogin();
        }
        else
        {
            if (botonLogin != null)
            {
                botonLogin.interactable = true;

                // Asegura que pueda recibir clics si aún no está vinculada
                var graphic = botonLogin.GetComponent<Graphic>();
                if (graphic != null) graphic.raycastTarget = true;

                botonLogin.onClick.RemoveAllListeners();
                botonLogin.onClick.AddListener(IniciarLogin);

                // Texto sin tilde para evitar errores de fuente
                var txtBtn = botonLogin.GetComponentInChildren<TextMeshProUGUI>();
                if (txtBtn != null)
                {
                    txtBtn.text = "INICIAR SESION CON GOOGLE";
                    txtBtn.color = Color.white;
                }
            }
        }
    }

    private void DeshabilitarBotonLogin()
    {
        if (botonLogin != null)
        {
            // 1. Quita la acción de clic
            botonLogin.onClick.RemoveAllListeners();

            // 2. Apaga la interactividad del botón
            botonLogin.interactable = false;

            // 3. Bloquea que el cursor detecte el botón (imposible cliquear)
            var graphic = botonLogin.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = false;

            // 4. Muestra en el texto del propio botón que ya está conectada
            var txtBtn = botonLogin.GetComponentInChildren<TextMeshProUGUI>();
            if (txtBtn != null)
            {
                txtBtn.text = "SESION INICIADA";
                txtBtn.color = new Color(0.2f, 0.9f, 0.3f); // Verde de conectado o gris
            }
        }

        string email = PlayerIdentity.GetGoogleEmail();
        if (textoEstado != null)
        {
            textoEstado.text = !string.IsNullOrEmpty(email) ? $"Conectado: {email}" : "Cuenta vinculada con exito";
            textoEstado.color = new Color(0.2f, 0.9f, 0.3f);
        }
    }

    public async void IniciarLogin()
    {
        if (PlayerIdentity.IsGoogleLinked()) return;

        if (textoEstado != null) textoEstado.text = "Abriendo navegador para iniciar sesión...";
        if (botonLogin != null) botonLogin.interactable = false;

        try
        {
            await EjecutarFlujoOAuthLocal();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error en autenticación: " + ex.Message);
            if (textoEstado != null) textoEstado.text = "Error al iniciar sesión.";
            if (botonLogin != null) botonLogin.interactable = true;
        }
    }

    private async Task EjecutarFlujoOAuthLocal()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        string nonce = Guid.NewGuid().ToString("N");
        string urlAuth = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                         $"client_id={clientId}&" +
                         $"response_type=id_token&" +
                         $"scope=openid%20email%20profile&" +
                         $"redirect_uri={UnityWebRequest.EscapeURL(RedirectUri)}&" +
                         $"nonce={nonce}";

        Application.OpenURL(urlAuth);

        HttpListenerContext context = await listener.GetContextAsync();

        string htmlPuente = @"
            <html><head><meta charset='utf-8'></head>
            <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#121212; color:#fff;'>
                <h2>Procesando inicio de sesión...</h2>
                <script>
                    if (window.location.hash) {
                        window.location.href = '/callback/?' + window.location.hash.substring(1);
                    }
                </script>
            </body></html>";

        byte[] buffer = Encoding.UTF8.GetBytes(htmlPuente);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();

        context = await listener.GetContextAsync();
        string query = context.Request.Url.Query;

        string idToken = ExtraerParametro(query, "id_token");

        string htmlFinal = @"
            <html><head><meta charset='utf-8'></head>
            <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#121212; color:#fff;'>
                <h1 style='color:#4CAF50;'>¡Autenticación Exitosa!</h1>
                <p>Ya podés cerrar esta pestaña y volver al juego.</p>
            </body></html>";

        byte[] bufferFinal = Encoding.UTF8.GetBytes(htmlFinal);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = bufferFinal.Length;
        await context.Response.OutputStream.WriteAsync(bufferFinal, 0, bufferFinal.Length);
        context.Response.OutputStream.Close();
        listener.Stop();

        if (!string.IsNullOrEmpty(idToken))
        {
            if (textoEstado != null) textoEstado.text = "Sincronizando con base de datos...";
            StartCoroutine(ConsultarApiBackend(idToken));
        }
        else
        {
            throw new Exception("No se pudo obtener el id_token de Google.");
        }
    }

    private string ExtraerParametro(string query, string clave)
    {
        if (string.IsNullOrEmpty(query)) return null;
        if (query.StartsWith("?")) query = query.Substring(1);

        string[] pares = query.Split('&');
        foreach (string par in pares)
        {
            string[] kv = par.Split('=');
            if (kv.Length == 2 && kv[0] == clave)
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }
        return null;
    }

    private System.Collections.IEnumerator ConsultarApiBackend(string idToken)
    {
        GoogleAuthPayload payload = new GoogleAuthPayload { id_token = idToken };
        string jsonBody = JsonUtility.ToJson(payload);

        var request = new UnityWebRequest(apiAuthUrl, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json");
        request.certificateHandler = new BypassCertificateHandler();
        request.timeout = 15;

        yield return request.SendWebRequest();

        long httpCode = request.responseCode;

        if (request.result == UnityWebRequest.Result.Success && (httpCode >= 200 && httpCode < 300))
        {
            ApiResponse respuesta = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);

            if (respuesta != null && respuesta.success)
            {
                JugadorActivo = respuesta.usuario;

                // Guardado persistente a través de PlayerIdentity
                PlayerIdentity.SetGoogleSession(JugadorActivo.google_id, JugadorActivo.email);

                // Bloquea el botón de inmediato
                DeshabilitarBotonLogin();

                Debug.Log($"<color=green>[AUTH API] Login persistido con éxito:</color> {JugadorActivo.email}");
            }
            else
            {
                string err = respuesta != null && !string.IsNullOrEmpty(respuesta.error) ? respuesta.error : "Error en el servidor";
                if (textoEstado != null) textoEstado.text = err;
                if (botonLogin != null) botonLogin.interactable = true;
            }
        }
        else
        {
            Debug.LogError($"[AUTH API] Error: {request.error} (HTTP {httpCode}) | Detalle: {request.downloadHandler?.text}");
            if (textoEstado != null) textoEstado.text = "Error al conectar con la API.";
            if (botonLogin != null) botonLogin.interactable = true;
        }

        request.Dispose();
    }

    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}