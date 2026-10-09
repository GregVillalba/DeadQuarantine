using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class CharacterRenameManager : MonoBehaviour
{
    public static CharacterRenameManager Instance { get; private set; }

    // Claves de guardado local permanente
    private const string PREF_OSCAR = "NOMBRE_PERSONAJE_OSCAR";
    private const string PREF_JANET = "NOMBRE_PERSONAJE_JANET";
    private const string PREF_ULTIMO_ACTIVO = "PLAYER_CUSTOM_USERNAME";

    public enum PersonajeSeleccionado { Oscar, Janet }
    private PersonajeSeleccionado personajeEnEdicion;

    [Header("Backend")]
    [SerializeField] private string apiUrl = "http://localhost:3000/api/usuarios/perfil";

    [Header("Referencias Jugador 1 (Oscar)")]
    [SerializeField] private Button btnCambiarOscar;
    [SerializeField] private TMP_Text txtNombreOscar; // Objeto 'tit' de Jugador1

    [Header("Referencias Jugador 2 (Janet)")]
    [SerializeField] private Button btnCambiarJanet;
    [SerializeField] private TMP_Text txtNombreJanet; // Objeto 'tit' de Jugador2

    [Header("Ventana Emergente (emergente_Cambiar_nombre)")]
    [SerializeField] private GameObject emergenteCambiarNombre;
    [SerializeField] private TMP_InputField inputFieldNombre;
    [SerializeField] private Button btnOk;
    [SerializeField] private Button btnCancel;

    [Header("Validación de Nombre")]
    [SerializeField] private TMP_Text txtEstadoValidacion; // Label para mostrar si está OK
    [SerializeField] private Color colorValido = new Color(0.2f, 0.9f, 0.3f);    // Verde
    [SerializeField] private Color colorInvalido = new Color(1f, 0.3f, 0.3f);    // Rojo

    // Patrón Regex: Solo letras y números (sin espacios ni símbolos), entre 1 y 10 caracteres
    private static readonly Regex patronAlfanumerico = new Regex("^[a-zA-Z0-9]{1,10}$");

    public string NombreOscarActual { get; private set; }
    public string NombreJanetActual { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        NombreOscarActual = PlayerPrefs.GetString(PREF_OSCAR, "Oscar");
        NombreJanetActual = PlayerPrefs.GetString(PREF_JANET, "Janet");

        ActualizarTextosEnPantalla();
        ConfigurarBotones();

        if (inputFieldNombre != null)
        {
            // Limitar longitud máxima en el componente a 10 caracteres
            inputFieldNombre.characterLimit = 10;
            // Escuchar cambios en tiempo real mientras el usuario escribe
            inputFieldNombre.onValueChanged.AddListener(OnNombreInputCambiado);
        }

        if (emergenteCambiarNombre != null)
            emergenteCambiarNombre.SetActive(false);
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
    }

    private void AbrirEmergente(PersonajeSeleccionado personaje)
    {
        personajeEnEdicion = personaje;

        if (emergenteCambiarNombre != null)
            emergenteCambiarNombre.SetActive(true);

        if (inputFieldNombre != null)
        {
            string nombreActual = (personaje == PersonajeSeleccionado.Oscar) ? NombreOscarActual : NombreJanetActual;
            inputFieldNombre.text = nombreActual;
            inputFieldNombre.Select();
            inputFieldNombre.ActivateInputField();

            // Validar de inmediato al abrir
            ValidarNombre(nombreActual);
        }
    }

    public void CerrarEmergente()
    {
        if (emergenteCambiarNombre != null)
            emergenteCambiarNombre.SetActive(false);
    }

    private void OnNombreInputCambiado(string texto)
    {
        ValidarNombre(texto);
    }

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
            ActualizarFeedback(false, "Solo letras y números (sin signos ni espacios)");
            return false;
        }

        ActualizarFeedback(true, "Nombre válido ✓");
        return true;
    }

    private void ActualizarFeedback(bool esValido, string mensaje)
    {
        // Activar o desactivar el botón OK según el estado
        if (btnOk != null)
            btnOk.interactable = esValido;

        // Actualizar el texto del label y su color
        if (txtEstadoValidacion != null)
        {
            txtEstadoValidacion.text = mensaje;
            txtEstadoValidacion.color = esValido ? colorValido : colorInvalido;
        }
    }

    private void ConfirmarNuevoNombre()
    {
        string textoIngresado = inputFieldNombre != null ? inputFieldNombre.text.Trim() : "";

        if (!ValidarNombre(textoIngresado))
            return;

        string nombreAnterior = "";
        string nuevoNombre = textoIngresado;

        if (personajeEnEdicion == PersonajeSeleccionado.Oscar)
        {
            nombreAnterior = NombreOscarActual;
            NombreOscarActual = nuevoNombre;
            PlayerPrefs.SetString(PREF_OSCAR, nuevoNombre);
        }
        else
        {
            nombreAnterior = NombreJanetActual;
            NombreJanetActual = nuevoNombre;
            PlayerPrefs.SetString(PREF_JANET, nuevoNombre);
        }

        PlayerPrefs.SetString(PREF_ULTIMO_ACTIVO, nuevoNombre);
        PlayerPrefs.Save();

        ActualizarTextosEnPantalla();
        SincronizarConLogros(nuevoNombre);
        CerrarEmergente();

        StartCoroutine(SincronizarConMySQL(nombreAnterior, nuevoNombre));
    }

    private void ActualizarTextosEnPantalla()
    {
        if (txtNombreOscar != null) txtNombreOscar.text = $"✍️ {NombreOscarActual}";
        if (txtNombreJanet != null) txtNombreJanet.text = $"✍️ {NombreJanetActual}";
    }

    private void SincronizarConLogros(string nombreFinal)
    {
        if (AchievementsManager.Instance != null)
        {
            AchievementsManager.Instance.UsernameJugador = nombreFinal;
        }
    }

    public void SeleccionarPersonajeParaJugar(PersonajeSeleccionado personaje)
    {
        string nombreElegido = (personaje == PersonajeSeleccionado.Oscar) ? NombreOscarActual : NombreJanetActual;
        PlayerPrefs.SetString(PREF_ULTIMO_ACTIVO, nombreElegido);
        PlayerPrefs.Save();
        SincronizarConLogros(nombreElegido);
    }

    // ============================================================
    // SINCRONIZACIÓN CON BASE DE DATOS
    // ============================================================

    [System.Serializable]
    private class PerfilPayload
    {
        public string username;
        public string nuevo_username;
    }

    private IEnumerator SincronizarConMySQL(string nombreAnterior, string nuevoNombre)
    {
        PerfilPayload payload = new PerfilPayload
        {
            username = nombreAnterior,
            nuevo_username = nuevoNombre
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
                Debug.Log($"[PERFIL DB] Sincronizado en MySQL: {nuevoNombre}");
            }
            else
            {
                Debug.LogWarning($"[PERFIL DB] Sin conexión al backend ({req.error}). El nombre se mantiene seguro en local.");
            }
        }
    }
}