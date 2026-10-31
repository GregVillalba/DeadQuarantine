/*using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup obligatorio de primera ejecución.
/// Colocarlo en la primera escena/pantalla del juego (por ejemplo PantallasUI).
///
/// Si ya existe PlayerIdentity.PlayerUsername, el popup nunca se muestra.
/// Si todavía no existe, bloquea la interacción mediante su panel hasta que
/// el jugador ingrese un username válido.
/// </summary>
public class UsernameFirstRunPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text validationText;

    [Header("Opcional")]
    [Tooltip("Objeto que se puede desactivar mientras el popup está abierto. Dejar vacío si el propio panel ya bloquea toda la pantalla.")]
    [SerializeField] private GameObject uiToDisableWhileOpen;

    private void Awake()
    {
        if (usernameInput != null)
        {
            usernameInput.characterLimit = PlayerIdentity.MaxUsernameLength;
            usernameInput.onValueChanged.AddListener(OnUsernameChanged);
        }

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmUsername);
    }

    private void Start()
    {
        // Fuerza la creación del ID local persistente independientemente del username.
        PlayerIdentity.GetUserId();

        if (PlayerIdentity.HasUsername())
        {
            SetOpen(false);
            return;
        }

        SetOpen(true);

        if (usernameInput != null)
        {
            usernameInput.text = string.Empty;
            usernameInput.Select();
            usernameInput.ActivateInputField();
        }

        UpdateValidation(string.Empty);
    }

    private void OnDestroy()
    {
        if (usernameInput != null)
            usernameInput.onValueChanged.RemoveListener(OnUsernameChanged);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmUsername);
    }

    private void OnUsernameChanged(string value)
    {
        UpdateValidation(value);
    }

    private void UpdateValidation(string value)
    {
        bool valid = PlayerIdentity.IsValidUsername(value, out string error);

        if (confirmButton != null)
            confirmButton.interactable = valid;

        if (validationText != null)
        {
            validationText.text = valid
                ? "Nombre válido"
                : error;
        }
    }

    private void ConfirmUsername()
    {
        if (usernameInput == null)
            return;

        string value = usernameInput.text.Trim();

        if (!PlayerIdentity.TryCreateUsername(value, out string error))
        {
            if (validationText != null)
                validationText.text = error;

            return;
        }

        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        if (panel != null)
            panel.SetActive(open);

        if (uiToDisableWhileOpen != null)
            uiToDisableWhileOpen.SetActive(!open);
    }
}*/
