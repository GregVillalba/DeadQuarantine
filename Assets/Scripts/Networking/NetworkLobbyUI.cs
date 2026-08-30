using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkLobbyUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NetworkBootstrap networkBootstrap;

    [Header("Crear Sala")]
    [SerializeField] private Button createButton;
    [SerializeField] private TextMeshProUGUI joinCodeDisplay;

    [Header("Unirse")]
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInput;

    private void Start()
    {
        createButton.onClick.AddListener(OnCreateButtonClicked);
        joinButton.onClick.AddListener(OnJoinButtonClicked);

        joinCodeDisplay.text = "";
    }

    private async void OnCreateButtonClicked()
    {
        createButton.interactable = false;
        joinButton.interactable = false;

        try
        {
            string joinCode =
                await networkBootstrap.StartHost();

            if (string.IsNullOrEmpty(joinCode))
            {
                createButton.interactable = true;
                joinButton.interactable = true;
                return;
            }

            joinCodeDisplay.text = joinCode;

            // El host pasa directamente a MainScene.

        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "[Lobby] Error creando sala: " +
                e.Message
            );

            createButton.interactable = true;
            joinButton.interactable = true;
        }
    }

    private async void OnJoinButtonClicked()
    {
        string joinCode = joinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning(
                "[Lobby] Ingresá un código."
            );
            return;
        }

        joinButton.interactable = false;
        createButton.interactable = false;

        try
        {
            bool success =
                await networkBootstrap.StartClient(joinCode);

            if (!success)
            {
                joinButton.interactable = true;
                createButton.interactable = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "[Lobby] Error uniéndose: " +
                e.Message
            );

            joinButton.interactable = true;
            createButton.interactable = true;
        }
    }
}