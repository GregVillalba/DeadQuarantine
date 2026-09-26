using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class TutorialBootstrap : NetworkBehaviour
{
    [SerializeField] private GameObject hud;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;

    [Header("Escena")]
    [SerializeField] private string tutorialSceneName = "TutorialScene"; // <- poné el nombre EXACTO de tu escena

    public override void OnNetworkSpawn()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        Debug.Log(
            "[TutorialBootstrap] OnNetworkSpawn | IsOwner=" + IsOwner +
            " | Escena actual='" + sceneName + "'" +
            " | Esperada='" + tutorialSceneName + "'" +
            " | Coinciden=" + (sceneName == tutorialSceneName)
        );

        if (!IsOwner)
        {
            Debug.Log("[TutorialBootstrap] Cortado: no es el dueño (IsOwner=false).");
            return;
        }

        if (sceneName != tutorialSceneName)
        {
            Debug.Log("[TutorialBootstrap] Cortado: nombre de escena no coincide.");
            enabled = false;
            return;
        }

        Debug.Log("[TutorialBootstrap] Arrancando HabilitarConDelay().");
        StartCoroutine(HabilitarConDelay());
    }
    private IEnumerator HabilitarConDelay()
    {
        // Espera un frame para que WeaponSwitcher.Start() ya haya
        // terminado de bloquear el arma por defecto, y recién ahí
        // la desbloqueamos nosotros — si no, es una carrera y a veces gana él.
        yield return null;

        if (hud != null)
            hud.SetActive(true);

        if (playerMovement != null)
            playerMovement.MovementLocked = false;

        if (playerLook != null)
            playerLook.enabled = true;

        foreach (Weapon arma in transform.root.GetComponentsInChildren<Weapon>(true))
            arma.InputLocked = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}