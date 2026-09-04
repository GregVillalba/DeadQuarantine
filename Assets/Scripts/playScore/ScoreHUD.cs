using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreHUD : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI textoPuntaje;

    private PlayerScore playerScore;

    private void Awake()
    {
        playerScore = GetComponentInParent<PlayerScore>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (playerScore != null)
        {
            playerScore.ScoreNetwork.OnValueChanged += ActualizarTexto;
            ActualizarTexto(0, playerScore.ScoreNetwork.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerScore != null)
            playerScore.ScoreNetwork.OnValueChanged -= ActualizarTexto;
    }

    private void ActualizarTexto(int anterior, int nuevo)
    {
        if (textoPuntaje != null)
            textoPuntaje.text = "Puntaje: " + nuevo;
    }
}