using UnityEngine;
using Unity.Netcode;

public class GameStartCurtain : MonoBehaviour
{
    [Header("Cortina de carga")]
    [SerializeField] private GameObject curtainCanvas;

    [Header("Configuración")]
    [SerializeField] private int expectedPlayerCount = 2;

    private bool curtainHidden;

    private void Awake()
    {
        // Se activa apenas arranca esta escena (= arranca la partida).
        if (curtainCanvas != null)
            curtainCanvas.SetActive(true);
    }

    private void Update()
    {
        if (curtainHidden)
            return;

        if (!TodosListos())
            return;

        OcultarCortina();
    }

    private bool TodosListos()
    {
        // FindObjectsByType SÍ funciona igual en Host y en Cliente,
        // porque cada Player es un NetworkObject spawneado y visible
        // localmente en todas las máquinas — a diferencia de
        // ConnectedClientsList, que solo está completa en el servidor.
        MultiplayerPlayerSpawnAssigner[] assigners =
            FindObjectsByType<MultiplayerPlayerSpawnAssigner>(FindObjectsSortMode.None);

        if (assigners.Length < expectedPlayerCount)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (!assigner.IsAtSpawn.Value)
                return false;
        }

        return true;
    }

    private void OcultarCortina()
    {
        curtainHidden = true;

        if (curtainCanvas != null)
            curtainCanvas.SetActive(false);

        RevelarHUDParaJugadorLocal();
    }

    private void RevelarHUDParaJugadorLocal()
    {
        PauseController[] controllers =
            FindObjectsByType<PauseController>(FindObjectsSortMode.None);

        foreach (PauseController controller in controllers)
        {
            if (controller.IsOwner)
            {
                controller.MostrarHUD();
                break;
            }
        }
    }
}