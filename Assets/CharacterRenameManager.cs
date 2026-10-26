using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class CharacterRenameManager : MonoBehaviour
{
    public static CharacterRenameManager Instance { get; private set; }

    private const string PREF_OSCAR = "NOMBRE_PERSONAJE_OSCAR";
    private const string PREF_JANET = "NOMBRE_PERSONAJE_JANET";
    private const string PREF_ULTIMO_ACTIVO = "PLAYER_CUSTOM_USERNAME";

    // Endpoint directo a Wasmer
    private const string API_URL_USUARIOS = "https://dead-quarentine-web.wasmer.app/api/usuarios";

    public enum PersonajeSeleccionado { Oscar, Janet }
    private PersonajeSeleccionado personajeEnEdicion;

    [Header("Identificador de Jugador")]
    [Tooltip("Label donde se muestra el código único de jugador")]
    [SerializeField] private TMP_Text txtLabelJugador;

    [Header("Referencias Jugador 1 (Oscar)")]
    [SerializeField] private Button btnCambiarOscar;
    [SerializeField] private TMP_Text txtNombreOscar;

    [Header("Referencias Jugador 2 (Janet)")]
    [SerializeField] private Button btnCambiarJanet;
    [SerializeField] private TMP_Text txtNombreJanet;

    [Header("Ventana Edición Nombre (emergente_Cambiar_nombre)")]
    [SerializeField] private GameObject emergenteCambiarNombre;
    [SerializeField] private TMP_InputField inputFieldNombre;
    [SerializeField] private Button btnOk;
    [SerializeField] private Button btnCancel;

    [Header("Validación en Tiempo Real")]
    [SerializeField] private TMP_Text txtEstadoValidacion;
    [SerializeField] private TMP_Text txtIDOriginal;
    [SerializeField] private Color colorValido = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color colorInvalido = new Color(1f, 0.3f, 0.3f);

    [Header("Cartel de Notificación / Output")]
    [SerializeField] private GameObject cartelOutput;
    [SerializeField] private TMP_Text txtMensajeOutput;
    [SerializeField] private Button btnOkOutput;

    private static readonly Regex patronAlfanumerico = new Regex("^[a-zA-Z0-9]{1,10}$");

    public string NombreOscarActual { get; private set; }
    public string NombreJanetActual { get; private set; }

    [Serializable]
    private class PerfilPayload
    {
        public string user_id;
        public string username;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        string idUnico = PlayerIdentity.GetUserId();
        string sufijoCorto = idUnico.Length >= 4 ? idUnico.Substring(0, 4).ToUpper() : "0001";

        // Si nunca se personalizó, se inicializa como Oscar_XXXX o Janet_XXXX
        NombreOscarActual = PlayerPrefs.GetString(PREF_OSCAR, $"Oscar_{sufijoCorto}");
        NombreJanetActual = PlayerPrefs.GetString(PREF_JANET, $"Janet_{sufijoCorto}");

        // Mostrar el código en label_jugador
        ActualizarLabelJugador(idUnico, sufijoCorto);

        ActualizarTextosEnPantalla();
        ConfigurarBotones();

        if (inputFieldNombre != null)
        {
            inputFieldNombre.characterLimit = 10;
            inputFieldNombre.onValueChanged.AddListener(OnNombreInputCambiado);
        }

        if (emergenteCambiarNombre != null)
            emergenteCambiarNombre.SetActive(false);

        if (cartelOutput != null)
            cartelOutput.SetActive(false);
    }

    private void ActualizarLabelJugador(string idCompleto, string sufijoCorto)
    {
        if (txtLabelJugador != null)
        {
            txtLabelJugador.text = $"ID: #{sufijoCorto}";
        }
    }

    private void ConfigurarBotones()
    {
        if (btnCambiarOscar != null)
            btnCambiarOscar.onClick.AddListener(() => AbrirEmergente(PersonajeSeleccionado.Oscar));

        if (btnCambiarJanet != null)
            btnCambiarJanet.onClick.AddListener(() => AbrirEmergente(PersonajeSeleccionado.Janet));

        if (btnOk != null)
            btnOk.onClick.AddListener(ConfirmarNuevoNombre);

        if (btnCancel != null)
            btnCancel.onClick.AddListener(CerrarEmergente);

        if (btnOkOutput != null)
            btnOkOutput.onClick.AddListener(CerrarCartelOutput);
    }

    public void CerrarEmergente()
    {
        if (emergenteCambiarNombre != null)
            emergenteCambiarNombre.SetActive(false);
    }

    public void MostrarCartelOutput(string mensaje)
    {
        if (cartelOutput != null)
        {
            // Traer al frente del Canvas para evitar que quede oculto
            cartelOutput.transform.SetAsLastSibling();
            cartelOutput.SetActive(true);
        }

        if (txtMensajeOutput != null)
        {
            txtMensajeOutput.text = mensaje;
        }
    }

    public void CerrarCartelOutput()
    {
        if (cartelOutput != null)
            cartelOutput.SetActive(false);
    }

    private void OnNombreInputCambiado(string texto) => ValidarNombre(texto);

    private bool ValidarNombre(string texto)
    {
        texto = texto.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            ActualizarFeedback(false, "El nombre no puede estar vacío");
            return false;
        }

        if (texto.Length > 10)
        {
            ActualizarFeedback(false, "Máximo 10 caracteres");
            return false;
        }

        if (!patronAlfanumerico.IsMatch(texto))
        {
            ActualizarFeedback(false, "Solo letras y números (sin espacios)");
            return false;
        }

        ActualizarFeedback(true, "Nombre válido ✓");
        return true;
    }

    private void ActualizarFeedback(bool esValido, string mensaje)
    {
        if (btnOk != null)
            btnOk.interactable = esValido;

        if (txtEstadoValidacion != null)
        {
            txtEstadoValidacion.text = mensaje;
            txtEstadoValidacion.color = esValido ? colorValido : colorInvalido;
        }
    }

    private void ConfirmarNuevoNombre()
    {
        string textoIngresado = inputFieldNombre != null ? inputFieldNombre.text.Trim() : "";
        if (!ValidarNombre(textoIngresado)) return;

        string nuevoNombre = textoIngresado;

        if (personajeEnEdicion == PersonajeSeleccionado.Oscar)
        {
            NombreOscarActual = nuevoNombre;
            PlayerPrefs.SetString(PREF_OSCAR, nuevoNombre);
        }
        else
        {
            NombreJanetActual = nuevoNombre;
            PlayerPrefs.SetString(PREF_JANET, nuevoNombre);
        }

        PlayerPrefs.Save();

        ActualizarTextosEnPantalla();

        // IMPORTANTE:
        // Este manager queda limitado al nombre local del personaje legacy.
        // Ya NO modifica el username, logros, ranking ni perfil del jugador.
        CerrarEmergente();
        MostrarCartelOutput($"Nombre del personaje actualizado a <b>{nuevoNombre}</b>.");
    }

    private void ActualizarTextosEnPantalla()
    {
        if (txtNombreOscar != null) txtNombreOscar.text = $"✍️ {NombreOscarActual}";
        if (txtNombreJanet != null) txtNombreJanet.text = $"✍️ {NombreJanetActual}";
    }

    private void SincronizarConLogros(string nombreFinal)
    {
        if (AchievementsManager.Instance != null)
            AchievementsManager.Instance.UsernameJugador = nombreFinal;
    }

    public void SeleccionarPersonajeParaJugar(PersonajeSeleccionado personaje)
    {
        // Se conserva el método para no romper botones/eventos existentes.
        // Elegir un personaje ya NO cambia la identidad del usuario.
    }

    private IEnumerator SincronizarConMySQL(string nuevoNombre)
    {
        string idDispositivo = PlayerIdentity.GetUserId();

        PerfilPayload payload = new PerfilPayload
        {
            user_id = idDispositivo,
            username = nuevoNombre
        };

        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(API_URL_USUARIOS, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.certificateHandler = new BypassCertificateHandler();
            req.timeout = 10;

            yield return req.SendWebRequest();

            long httpCode = req.responseCode;
            string respuestaTexto = req.downloadHandler != null ? req.downloadHandler.text : "";

            if (req.result == UnityWebRequest.Result.Success && (httpCode >= 200 && httpCode < 300))
            {
                Debug.Log($"<color=green>[PERFIL DB] Sincronizado: {respuestaTexto}</color>");
                MostrarCartelOutput($"¡Nombre actualizado correctamente!\n\nNuevo nombre: <b>{nuevoNombre}</b>");
            }
            else
            {
                Debug.LogWarning($"<color=orange>[PERFIL DB] Servidor no disponible ({req.error}): {respuestaTexto}</color>");
                MostrarCartelOutput($"Tu nombre se guardó localmente como <b>{nuevoNombre}</b>.\n\n(Se actualizará en la nube al jugar)");
            }
        }
    }

    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    private void AbrirEmergente(PersonajeSeleccionado personaje)
    {
        personajeEnEdicion = personaje;

        if (emergenteCambiarNombre != null)
            emergenteCambiarNombre.SetActive(true);

        // Mostrar el ID único local del jugador en el label de la ventana emergente
        if (txtIDOriginal != null)
        {
            string idDispositivo = PlayerIdentity.GetUserId();
            txtIDOriginal.text = $"ID: {idDispositivo}";
        }

        if (inputFieldNombre != null)
        {
            string nombreActual = (personaje == PersonajeSeleccionado.Oscar) ? NombreOscarActual : NombreJanetActual;
            inputFieldNombre.text = nombreActual;
            inputFieldNombre.Select();
            inputFieldNombre.ActivateInputField();
            ValidarNombre(nombreActual);
        }
    }



}