using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CharacterRenameManager : MonoBehaviour
{
    public static CharacterRenameManager Instance { get; private set; }

    private const string API_URL_USUARIOS = "https://dead-quarentine-web.wasmer.app/api/usuarios";

    [Header("Contenedor Raíz (UsernameFirstRunUI)")]
    [SerializeField] private GameObject rootFirstRunUI;

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
    [SerializeField] private Button btnIrAVentanaClave;

    [Header("Formulario Clave (Ventana_CLAVE)")]
    [SerializeField] private TMP_InputField inputClaveAnterior;
    [SerializeField] private TMP_Text txtValidationClave;
    [SerializeField] private TMP_Text txtFeedbackServidorClave;

    [Header("Botones Ventana Clave")]
    [SerializeField] private Button btnConfirmarClave;
    [SerializeField] private Button btnAtrasANombre;
    [SerializeField] private Button btnCancelarClave;
    [SerializeField] private Button btnSalirClave;
    [SerializeField] private Button btnReintentarClave;

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
    private class PerfilPayload
    {
        public string user_id;
        public string username;
        public string nuevo_username;
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (rootFirstRunUI != null)
        {
            rootCanvasGroup = rootFirstRunUI.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
                rootCanvasGroup = rootFirstRunUI.AddComponent<CanvasGroup>();
            rootRaycaster = rootFirstRunUI.GetComponent<GraphicRaycaster>();
        }

        ConfigurarListeners();

        if (inputNombre != null)
        {
            inputNombre.characterLimit = PlayerIdentity.MaxUsernameLength;
            inputNombre.onValueChanged.AddListener(OnInputNombreCambiado);
        }

        if (inputClaveAnterior != null)
        {
            inputClaveAnterior.onValueChanged.AddListener(OnInputClaveCambiado);
        }

        ForzarCierreUI();
    }

    private void Start()
    {
        string idUnico = PlayerIdentity.GetUserId();
        string sufijoCorto = idUnico.Length >= 4 ? idUnico.Substring(0, 4).ToUpper() : "0001";
        ActualizarTextos(sufijoCorto);

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
        if (btnConfirmarNombre != null) btnConfirmarNombre.onClick.AddListener(EjecutarConfirmarNombre);
        if (btnCancelarNombre != null) btnCancelarNombre.onClick.AddListener(BotonCancelar);
        if (btnSalirNombre != null) btnSalirNombre.onClick.AddListener(BotonCancelar);
        if (btnReintentarNombre != null) btnReintentarNombre.onClick.AddListener(EjecutarConfirmarNombre);
        if (btnIrAVentanaClave != null) btnIrAVentanaClave.onClick.AddListener(BotonIrAClave);

        if (btnConfirmarClave != null) btnConfirmarClave.onClick.AddListener(EjecutarTransferenciaClave);
        if (btnAtrasANombre != null) btnAtrasANombre.onClick.AddListener(() => AbrirPantallaNombre(esPrimerInicioActual));
        if (btnCancelarClave != null) btnCancelarClave.onClick.AddListener(BotonCancelar);
        if (btnSalirClave != null) btnSalirClave.onClick.AddListener(BotonCancelar);
        if (btnReintentarClave != null) btnReintentarClave.onClick.AddListener(EjecutarTransferenciaClave);

        if (btnAbrirPopupCambio != null)
            btnAbrirPopupCambio.onClick.AddListener(() => AbrirPantallaNombre(esPrimerInicio: false));

        if (btnMenuTransferirDatos != null)
        {
            btnMenuTransferirDatos.onClick.AddListener(() =>
            {
                PlayerIdentity.CopiarIdConFeedback(this, txtFeedbackTransferirMenu, 2.0f);
            });
        }
    }

    public void BotonCancelar()
    {
        Debug.Log("<color=yellow>[UI]</color> Botón Cancelar presionado exitosamente.");
        ForzarCierreUI();
    }

    public void BotonIrAClave()
    {
        Debug.Log("<color=cyan>[UI]</color> Cambiando a Ventana Clave.");
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

        string idUnico = PlayerIdentity.GetUserId();
        string sufijoCorto = idUnico.Length >= 4 ? idUnico.Substring(0, 4).ToUpper() : "0001";
        ActualizarTextos(sufijoCorto);

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
                if (btnCancelarNombre != null)
                {
                    btnCancelarNombre.gameObject.SetActive(true);
                    btnCancelarNombre.interactable = true;
                }
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
                    btnCancelarNombre.gameObject.SetActive(true);
                    btnCancelarNombre.interactable = true;
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
                if (btnCancelarClave != null)
                {
                    btnCancelarClave.gameObject.SetActive(true);
                    btnCancelarClave.interactable = true;
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
                    btnCancelarClave.gameObject.SetActive(true);
                    btnCancelarClave.interactable = true;
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
        texto = texto.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            SetFeedbackNombre(false, "El nombre no puede estar vacío");
            return false;
        }

        if (texto.Length > PlayerIdentity.MaxUsernameLength)
        {
            SetFeedbackNombre(false, $"Máximo {PlayerIdentity.MaxUsernameLength} caracteres");
            return false;
        }

        if (!patronAlfanumerico.IsMatch(texto))
        {
            SetFeedbackNombre(false, "Solo letras y números (sin espacios)");
            return false;
        }

        SetFeedbackNombre(true, "Nombre válido ✓");
        return true;
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
        PerfilPayload payload = new PerfilPayload
        {
            user_id = idDispositivo,
            username = nuevoNombre,
            nuevo_username = nuevoNombre
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
            req.timeout = 8;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success && (req.responseCode >= 200 && req.responseCode < 300))
            {
                PlayerIdentity.TryUpdateUsername(nuevoNombre, out _);

                string sufijoCorto = idDispositivo.Length >= 4 ? idDispositivo.Substring(0, 4).ToUpper() : "0001";
                ActualizarTextos(sufijoCorto);

                if (AchievementsManager.Instance != null)
                    AchievementsManager.Instance.UsernameJugador = nuevoNombre;

                if (txtFeedbackServidorNombre != null)
                {
                    txtFeedbackServidorNombre.text = "¡Guardado exitosamente!";
                    txtFeedbackServidorNombre.color = colorValido;
                }

                SetEstadoBotonesNombre(EstadoFormulario.GuardadoExito);
            }
            else
            {
                if (txtFeedbackServidorNombre != null)
                {
                    txtFeedbackServidorNombre.text = "Error al conectar con el servidor.";
                    txtFeedbackServidorNombre.color = colorInvalido;
                }

                SetEstadoBotonesNombre(EstadoFormulario.GuardadoFallo);
            }
        }
    }

    private void ActualizarTextos(string sufijoCorto)
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

        if (btnConfirmarClave != null)
            btnConfirmarClave.interactable = !string.IsNullOrWhiteSpace(texto) && texto.Trim().Length >= 8;

        if (txtValidationClave != null) txtValidationClave.text = "";
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

                string sufijoCorto = idClave.Length >= 4 ? idClave.Substring(0, 4).ToUpper() : "0001";
                ActualizarTextos(sufijoCorto);

                if (AchievementsManager.Instance != null)
                {
                    AchievementsManager.Instance.UsernameJugador = nombreRecuperado;
                }

                if (txtFeedbackServidorClave != null)
                {
                    txtFeedbackServidorClave.text = $"¡Cuenta #{sufijoCorto} vinculada con éxito!";
                    txtFeedbackServidorClave.color = colorValido;
                }

                Debug.Log($"<color=green>[TRANSFERENCIA EXITOSA]</color> Nuevo User ID activo en el juego: {idClave}");

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
