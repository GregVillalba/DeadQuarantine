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

    private const string API_URL_USUARIOS = "https://dead-quarentine-web.wasmer.app/api/usuarios";

    [Header("Contenedor Raíz (UsernameFirstRunUI)")]
    [SerializeField] private GameObject rootFirstRunUI;

    [Header("Opcional: Bloqueo de UI externa")]
    [Tooltip("Objeto o menú de fondo que se desactiva mientras el popup obligatorio esté abierto.")]
    [SerializeField] private GameObject uiToDisableWhileOpen;

    [Header("Ventanas")]
    [SerializeField] private GameObject ventanaNombre;
    [SerializeField] private GameObject ventanaClave;

    [Header("Formulario Nombre (Ventana)")]
    [SerializeField] private TMP_InputField inputNombre;
    [SerializeField] private TMP_Text txtValidationNombre;
    [SerializeField] private TMP_Text txtFeedbackServidorNombre;
    [SerializeField] private TMP_Text txtIDLabel;

    [Header("Botones Ventana Nombre")]
    [SerializeField] private Button btnConfirmarNombre;
    [SerializeField] private Button btnCancelarNombre;
    [SerializeField] private Button btnSalirNombre;
    [SerializeField] private Button btnReintentarNombre;
    [SerializeField] private Button btnIrAVentanaClave; // Botón ID CLAVE

    [Header("Formulario Clave (Ventana_CLAVE)")]
    [SerializeField] private TMP_InputField inputClaveAnterior;
    [SerializeField] private TMP_Text txtValidationClave;
    [SerializeField] private TMP_Text txtFeedbackServidorClave;

    [Header("Botones Ventana Clave")]
    [SerializeField] private Button btnConfirmarClave;
    [SerializeField] private Button btnAtrasANombre;   // Botón ATRÁS
    [SerializeField] private Button btnCancelarClave;
    [SerializeField] private Button btnSalirClave;     // Botón SALIR (tras éxito)
    [SerializeField] private Button btnReintentarClave; // Botón REINTENTAR (tras fallo)

    [Header("Botones Menú / Lobby")]
    [SerializeField] private TMP_Text txtNombreLobby;
    [SerializeField] private Button btnAbrirPopupCambio;
    [SerializeField] private Button btnMenuTransferirDatos;
    [SerializeField] private TMP_Text txtFeedbackTransferirMenu;

    [Header("Colores")]
    [SerializeField] private Color colorValido = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color colorInvalido = new Color(1f, 0.3f, 0.3f);

    private CanvasGroup rootCanvasGroup;
    private GraphicRaycaster rootRaycaster;
    private static readonly Regex patronAlfanumerico = new Regex("^[a-zA-Z0-9]{1,10}$");
    private bool esPrimerInicioActual = false;

    [Serializable]
    public class PerfilPayload
    {
        public string user_id;
        public string username;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (rootFirstRunUI != null)
        {
            rootCanvasGroup = rootFirstRunUI.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null) rootCanvasGroup = rootFirstRunUI.AddComponent<CanvasGroup>();
            rootRaycaster = rootFirstRunUI.GetComponent<GraphicRaycaster>();
        }

        ConfigurarListeners();

        if (inputNombre != null)
        {
            inputNombre.characterLimit = PlayerIdentity.MaxUsernameLength;
            inputNombre.onValueChanged.RemoveAllListeners();
            inputNombre.onValueChanged.AddListener(OnInputNombreCambiado);
        }

        if (inputClaveAnterior != null)
        {
            inputClaveAnterior.onValueChanged.RemoveAllListeners();
            inputClaveAnterior.onValueChanged.AddListener(OnInputClaveCambiado);
        }

        ForzarCierreUI();
    }

    private void Start()
    {
        // Asegura la existencia del User ID persistente
        PlayerIdentity.GetUserId();
        ActualizarTextos();

        // LÓGICA DE PRIMERA EJECUCIÓN (Heredada de UsernameFirstRunPopup):
        // Si no existe un nombre registrado, abre obligatoriamente la pantalla
        if (!PlayerIdentity.HasUsername())
        {
            AbrirPantallaNombre(esPrimerInicio: true);
        }
        else
        {
            ForzarCierreUI();
        }
    }

    private void ConfigurarListeners()
    {
        // --- Ventana Nombre ---
        if (btnConfirmarNombre != null)
        {
            btnConfirmarNombre.onClick.RemoveAllListeners();
            btnConfirmarNombre.onClick.AddListener(EjecutarConfirmarNombre);
        }

        if (btnCancelarNombre != null)
        {
            btnCancelarNombre.onClick.RemoveAllListeners();
            btnCancelarNombre.onClick.AddListener(BotonCancelar);
        }

        if (btnSalirNombre != null)
        {
            btnSalirNombre.onClick.RemoveAllListeners();
            btnSalirNombre.onClick.AddListener(BotonCancelar);
        }

        if (btnReintentarNombre != null)
        {
            btnReintentarNombre.onClick.RemoveAllListeners();
            btnReintentarNombre.onClick.AddListener(EjecutarConfirmarNombre);
        }

        if (btnIrAVentanaClave != null)
        {
            btnIrAVentanaClave.onClick.RemoveAllListeners();
            btnIrAVentanaClave.onClick.AddListener(BotonIrAClave);
        }

        // --- Ventana Clave ---
        if (btnConfirmarClave != null)
        {
            btnConfirmarClave.onClick.RemoveAllListeners();
            btnConfirmarClave.onClick.AddListener(EjecutarTransferenciaClave);
        }

        if (btnAtrasANombre != null)
        {
            btnAtrasANombre.onClick.RemoveAllListeners();
            btnAtrasANombre.onClick.AddListener(() => AbrirPantallaNombre(esPrimerInicioActual));
        }

        if (btnCancelarClave != null)
        {
            btnCancelarClave.onClick.RemoveAllListeners();
            btnCancelarClave.onClick.AddListener(BotonCancelar);
        }

        if (btnSalirClave != null)
        {
            btnSalirClave.onClick.RemoveAllListeners();
            btnSalirClave.onClick.AddListener(BotonCancelar);
        }

        if (btnReintentarClave != null)
        {
            btnReintentarClave.onClick.RemoveAllListeners();
            btnReintentarClave.onClick.AddListener(EjecutarTransferenciaClave);
        }

        // --- Menú / Lobby ---
        if (btnAbrirPopupCambio != null)
        {
            btnAbrirPopupCambio.onClick.RemoveAllListeners();
            btnAbrirPopupCambio.onClick.AddListener(() => AbrirPantallaNombre(esPrimerInicio: false));
        }

        if (btnMenuTransferirDatos != null)
        {
            btnMenuTransferirDatos.onClick.RemoveAllListeners();
            btnMenuTransferirDatos.onClick.AddListener(() =>
            {
                PlayerIdentity.CopiarIdConFeedback(this, txtFeedbackTransferirMenu, 2.0f);
            });
        }
    }

    // ==========================================================
    // MÉTODOS PÚBLICOS
    // ==========================================================

    public void BotonCancelar()
    {
        // Solo bloquea si es el primer inicio Y no tiene NI nombre NI ID transferido
        if (esPrimerInicioActual && !PlayerIdentity.HasUsername() && string.IsNullOrEmpty(PlayerIdentity.GetUserId()))
        {
            Debug.LogWarning("<color=orange>[UI]</color> No se puede cancelar durante el primer inicio obligatorio.");
            return;
        }

        // Si ya transfirió o ya guardó, se resetea la bandera y se cierra
        esPrimerInicioActual = false;
        Debug.Log("<color=yellow>[UI]</color> Cerrando interfaz.");
        ForzarCierreUI();
    }

    public void BotonIrAClave()
    {
        Debug.Log("<color=cyan>[UI]</color> Abriendo Ventana Clave.");
        AbrirPantallaClave();
    }

    #region Manejo de Visibilidad
    public void ForzarCierreUI()
    {
        if (ventanaNombre != null) ventanaNombre.SetActive(false);
        if (ventanaClave != null) ventanaClave.SetActive(false);

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        if (rootRaycaster != null) rootRaycaster.enabled = false;
        if (rootFirstRunUI != null) rootFirstRunUI.SetActive(false);

        // Reactiva la UI de fondo que se había desactivado al abrir el popup
        if (uiToDisableWhileOpen != null)
            uiToDisableWhileOpen.SetActive(true);
    }

    private void MostrarRootUI()
    {
        if (rootFirstRunUI != null)
        {
            rootFirstRunUI.SetActive(true);
            rootFirstRunUI.transform.SetAsLastSibling();
        }

        if (rootRaycaster != null) rootRaycaster.enabled = true;

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }

        // Si hay una interfaz que ocultar/bloquear de fondo durante el popup, se apaga
        if (uiToDisableWhileOpen != null)
            uiToDisableWhileOpen.SetActive(false);
    }

    public void AbrirPantallaNombre(bool esPrimerInicio = false)
    {
        esPrimerInicioActual = esPrimerInicio;
        MostrarRootUI();

        if (ventanaClave != null) ventanaClave.SetActive(false);
        if (ventanaNombre != null)
        {
            ventanaNombre.transform.SetAsLastSibling();
            ventanaNombre.SetActive(true);
        }

        SetEstadoBotonesNombre(EstadoFormulario.Escribiendo);
        ActualizarTextos();

        if (inputNombre != null)
        {
            string textoBase = !esPrimerInicio ? PlayerIdentity.GetUsername() : "";
            inputNombre.text = textoBase;
            inputNombre.Select();
            inputNombre.ActivateInputField();
            ValidarNombre(textoBase);
        }

        if (txtFeedbackServidorNombre != null) txtFeedbackServidorNombre.text = "";
    }

    public void AbrirPantallaClave()
    {
        MostrarRootUI();

        if (ventanaNombre != null) ventanaNombre.SetActive(false);
        if (ventanaClave != null)
        {
            ventanaClave.transform.SetAsLastSibling();
            ventanaClave.SetActive(true);
        }

        SetEstadoBotonesClave(EstadoFormulario.Escribiendo);

        if (inputClaveAnterior != null)
        {
            inputClaveAnterior.text = "";
            inputClaveAnterior.Select();
            inputClaveAnterior.ActivateInputField();
        }

        if (txtValidationClave != null) txtValidationClave.text = "";
        if (txtFeedbackServidorClave != null) txtFeedbackServidorClave.text = "";
    }
    #endregion

    #region Control de Estados de Botones
    private enum EstadoFormulario { Escribiendo, Guardando, GuardadoExito, GuardadoFallo }

    private void SetEstadoBotonesNombre(EstadoFormulario estado)
    {
        switch (estado)
        {
            case EstadoFormulario.Escribiendo:
                if (btnConfirmarNombre != null) btnConfirmarNombre.gameObject.SetActive(true);

                // Si es primer inicio, el botón cancelar no está disponible para forzar el registro
                if (btnCancelarNombre != null)
                {
                    btnCancelarNombre.gameObject.SetActive(!esPrimerInicioActual);
                    btnCancelarNombre.interactable = !esPrimerInicioActual;
                }

                // El botón para recuperar por ID clave siempre se permite para que pueda restaurar su cuenta
                if (btnIrAVentanaClave != null)
                {
                    btnIrAVentanaClave.gameObject.SetActive(true);
                    btnIrAVentanaClave.interactable = true;
                }
                if (btnSalirNombre != null) btnSalirNombre.gameObject.SetActive(false);
                if (btnReintentarNombre != null) btnReintentarNombre.gameObject.SetActive(false);
                break;

            case EstadoFormulario.Guardando:
                if (btnConfirmarNombre != null) btnConfirmarNombre.interactable = false;
                if (btnCancelarNombre != null) btnCancelarNombre.interactable = false;
                if (btnIrAVentanaClave != null) btnIrAVentanaClave.interactable = false;
                break;

            case EstadoFormulario.GuardadoExito:
                if (btnConfirmarNombre != null) btnConfirmarNombre.gameObject.SetActive(false);
                if (btnCancelarNombre != null) btnCancelarNombre.gameObject.SetActive(false);
                if (btnReintentarNombre != null) btnReintentarNombre.gameObject.SetActive(false);
                if (btnIrAVentanaClave != null) btnIrAVentanaClave.gameObject.SetActive(false);
                if (btnSalirNombre != null)
                {
                    btnSalirNombre.gameObject.SetActive(true);
                    btnSalirNombre.interactable = true;
                }
                break;

            case EstadoFormulario.GuardadoFallo:
                if (btnConfirmarNombre != null) btnConfirmarNombre.gameObject.SetActive(false);
                if (btnSalirNombre != null) btnSalirNombre.gameObject.SetActive(false);
                if (btnCancelarNombre != null)
                {
                    btnCancelarNombre.gameObject.SetActive(!esPrimerInicioActual);
                    btnCancelarNombre.interactable = !esPrimerInicioActual;
                }
                if (btnReintentarNombre != null)
                {
                    btnReintentarNombre.gameObject.SetActive(true);
                    btnReintentarNombre.interactable = true;
                }
                break;
        }
    }

    private void SetEstadoBotonesClave(EstadoFormulario estado)
    {
        switch (estado)
        {
            case EstadoFormulario.Escribiendo:
                if (btnConfirmarClave != null) btnConfirmarClave.gameObject.SetActive(true);

                // Si es primer inicio tampoco dejamos cancelar en clave (debe volver a nombre o transferir)
                if (btnCancelarClave != null)
                {
                    btnCancelarClave.gameObject.SetActive(!esPrimerInicioActual);
                    btnCancelarClave.interactable = !esPrimerInicioActual;
                }
                if (btnAtrasANombre != null)
                {
                    btnAtrasANombre.gameObject.SetActive(true);
                    btnAtrasANombre.interactable = true;
                }
                if (btnSalirClave != null) btnSalirClave.gameObject.SetActive(false);
                if (btnReintentarClave != null) btnReintentarClave.gameObject.SetActive(false);
                break;

            case EstadoFormulario.Guardando:
                if (btnConfirmarClave != null) btnConfirmarClave.interactable = false;
                if (btnCancelarClave != null) btnCancelarClave.interactable = false;
                if (btnAtrasANombre != null) btnAtrasANombre.interactable = false;
                break;

            case EstadoFormulario.GuardadoExito:
                if (btnConfirmarClave != null) btnConfirmarClave.gameObject.SetActive(false);
                if (btnCancelarClave != null) btnCancelarClave.gameObject.SetActive(false);
                if (btnAtrasANombre != null) btnAtrasANombre.gameObject.SetActive(false);
                if (btnReintentarClave != null) btnReintentarClave.gameObject.SetActive(false);
                if (btnSalirClave != null)
                {
                    btnSalirClave.gameObject.SetActive(true);
                    btnSalirClave.interactable = true;
                }
                break;

            case EstadoFormulario.GuardadoFallo:
                if (btnConfirmarClave != null) btnConfirmarClave.gameObject.SetActive(false);
                if (btnSalirClave != null) btnSalirClave.gameObject.SetActive(false);
                if (btnAtrasANombre != null)
                {
                    btnAtrasANombre.gameObject.SetActive(true);
                    btnAtrasANombre.interactable = true;
                }
                if (btnCancelarClave != null)
                {
                    btnCancelarClave.gameObject.SetActive(!esPrimerInicioActual);
                    btnCancelarClave.interactable = !esPrimerInicioActual;
                }
                if (btnReintentarClave != null)
                {
                    btnReintentarClave.gameObject.SetActive(true);
                    btnReintentarClave.interactable = true;
                }
                break;
        }
    }
    #endregion

    #region Guardado y Validación (Nombre)
    private void OnInputNombreCambiado(string texto)
    {
        SetEstadoBotonesNombre(EstadoFormulario.Escribiendo);
        ValidarNombre(texto);
    }

    private bool ValidarNombre(string texto)
    {
        // Usa la validación centralizada de PlayerIdentity
        bool esValido = PlayerIdentity.IsValidUsername(texto, out string error);

        SetFeedbackNombre(esValido, esValido ? "Nombre válido ✓" : error);
        return esValido;
    }

    private void SetFeedbackNombre(bool esValido, string mensaje)
    {
        if (btnConfirmarNombre != null) btnConfirmarNombre.interactable = esValido;

        if (txtValidationNombre != null)
        {
            txtValidationNombre.text = mensaje;
            txtValidationNombre.color = esValido ? colorValido : colorInvalido;
        }
    }

    private void EjecutarConfirmarNombre()
    {
        string textoIngresado = inputNombre != null ? inputNombre.text.Trim() : "";
        if (!ValidarNombre(textoIngresado)) return;

        SetEstadoBotonesNombre(EstadoFormulario.Guardando);

        if (txtFeedbackServidorNombre != null)
            txtFeedbackServidorNombre.text = "Guardando...";

        StartCoroutine(RutinaGuardarNombre(textoIngresado));
    }

    private IEnumerator RutinaGuardarNombre(string nuevoNombre)
    {
        string idDispositivo = PlayerIdentity.GetUserId();
        PerfilPayload payload = new PerfilPayload { user_id = idDispositivo, username = nuevoNombre };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(API_URL_USUARIOS, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.certificateHandler = new BypassCertificateHandler();
            req.timeout = 8;

            yield return req.SendWebRequest();

            // Guarda tanto si el backend respondió OK como si hubo fallo (guardado local)
            if (esPrimerInicioActual)
                PlayerIdentity.TryCreateUsername(nuevoNombre, out _);
            else
                PlayerIdentity.TryUpdateUsername(nuevoNombre, out _);

            ActualizarTextos();

            if (AchievementsManager.Instance != null)
                AchievementsManager.Instance.UsernameJugador = nuevoNombre;

            if (req.result == UnityWebRequest.Result.Success && (req.responseCode >= 200 && req.responseCode < 300))
            {
                if (txtFeedbackServidorNombre != null)
                {
                    txtFeedbackServidorNombre.text = "¡Guardado exitosamente!";
                    txtFeedbackServidorNombre.color = colorValido;
                }
            }
            else
            {
                if (txtFeedbackServidorNombre != null)
                {
                    txtFeedbackServidorNombre.text = "Guardado localmente.";
                    txtFeedbackServidorNombre.color = colorValido;
                }
            }

            // Una vez guardado el nombre con éxito, se habilita el botón SALIR para ingresar al juego
            SetEstadoBotonesNombre(EstadoFormulario.GuardadoExito);
        }
    }

    private void ActualizarTextos()
    {
        string idCompleto = PlayerIdentity.GetUserId();

        if (txtIDLabel != null)
            txtIDLabel.text = $"ID: {idCompleto}";

        if (txtNombreLobby != null)
        {
            string nombre = PlayerIdentity.GetUsername();
            if (!string.IsNullOrEmpty(nombre))
                txtNombreLobby.text = nombre;
        }
    }
    #endregion

    #region Transferencia por Clave
    private void OnInputClaveCambiado(string texto)
    {
        SetEstadoBotonesClave(EstadoFormulario.Escribiendo);

        string claveLimpia = texto != null ? texto.Trim() : "";

        // Acepta letras, números y símbolos especiales (mínimo 8 caracteres)
        bool esValida = !string.IsNullOrEmpty(claveLimpia) && claveLimpia.Length >= 8;

        if (btnConfirmarClave != null)
            btnConfirmarClave.interactable = esValida;

        if (txtValidationClave != null)
            txtValidationClave.text = "";
    }

    private void EjecutarTransferenciaClave()
    {
        string claveIngresada = inputClaveAnterior != null ? inputClaveAnterior.text.Trim() : "";
        string idLocalActual = PlayerIdentity.GetUserId();

        if (claveIngresada.Equals(idLocalActual, StringComparison.OrdinalIgnoreCase))
        {
            if (txtFeedbackServidorClave != null)
            {
                txtFeedbackServidorClave.text = "Esta cuenta ya está activa.";
                txtFeedbackServidorClave.color = colorInvalido;
            }
            return;
        }

        if (string.IsNullOrEmpty(claveIngresada) || claveIngresada.Length < 8)
        {
            if (txtFeedbackServidorClave != null)
            {
                txtFeedbackServidorClave.text = "Formato de clave inválido (mínimo 8 caracteres).";
                txtFeedbackServidorClave.color = colorInvalido;
            }
            return;
        }

        SetEstadoBotonesClave(EstadoFormulario.Guardando);

        if (txtFeedbackServidorClave != null)
        {
            txtFeedbackServidorClave.text = "Buscando cuenta...";
            txtFeedbackServidorClave.color = Color.white;
        }

        StartCoroutine(RutinaTransferirCuentaPorClave(claveIngresada));
    }

    private IEnumerator RutinaTransferirCuentaPorClave(string idClave)
    {
        string url = $"{API_URL_USUARIOS}?user_id={UnityWebRequest.EscapeURL(idClave)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.certificateHandler = new BypassCertificateHandler();
            req.timeout = 5;

            yield return req.SendWebRequest();

            string nombreRecuperado = null;

            if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
            {
                string rawJson = req.downloadHandler.text != null ? req.downloadHandler.text.Trim() : "";

                if (rawJson.StartsWith("[") && rawJson.EndsWith("]"))
                {
                    rawJson = rawJson.Substring(1, rawJson.Length - 2).Trim();
                }

                if (!string.IsNullOrEmpty(rawJson) && rawJson.StartsWith("{"))
                {
                    try
                    {
                        PerfilPayload datos = JsonUtility.FromJson<PerfilPayload>(rawJson);
                        if (datos != null && !string.IsNullOrEmpty(datos.username))
                        {
                            nombreRecuperado = datos.username;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TRANSFERIR] Parseo opcional omitido: {ex.Message}");
                    }
                }
            }

            if (string.IsNullOrEmpty(nombreRecuperado))
            {
                nombreRecuperado = PlayerIdentity.GetUsername();
                if (string.IsNullOrEmpty(nombreRecuperado))
                {
                    nombreRecuperado = "Superviviente";
                }
            }

            bool importado = PlayerIdentity.TryImportarUserId(idClave, out _);

            if (importado)
            {
                PlayerIdentity.TryUpdateUsername(nombreRecuperado, out _);
                ActualizarTextos();

                if (AchievementsManager.Instance != null)
                {
                    AchievementsManager.Instance.UsernameJugador = nombreRecuperado;
                }

                if (txtFeedbackServidorClave != null)
                {
                    txtFeedbackServidorClave.text = "¡Cuenta vinculada con éxito!";
                    txtFeedbackServidorClave.color = colorValido;
                }

                Debug.Log($"<color=green>[TRANSFERENCIA EXITOSA]</color> Nuevo User ID activo: {idClave}");
                SetEstadoBotonesClave(EstadoFormulario.GuardadoExito);
            }
            else
            {
                if (txtFeedbackServidorClave != null)
                {
                    txtFeedbackServidorClave.text = "Clave o ID inválido.";
                    txtFeedbackServidorClave.color = colorInvalido;
                }

                SetEstadoBotonesClave(EstadoFormulario.GuardadoFallo);
            }
        }
    }
    #endregion

    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}