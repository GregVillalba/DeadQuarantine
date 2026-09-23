using UnityEngine;
using Unity.Netcode;

public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager Instance { get; private set; }

    private PlayerScore playerScore;

    private bool logro50Desbloqueado;
    private bool logro100Desbloqueado;
    private bool logro300Desbloqueado;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Todavía no encontramos al jugador local
        if (playerScore == null)
        {
            BuscarPlayerScore();
        }
    }

    private void BuscarPlayerScore()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClient == null)
            return;

        if (NetworkManager.Singleton.LocalClient.PlayerObject == null)
            return;

        playerScore =
            NetworkManager.Singleton.LocalClient.PlayerObject
                .GetComponentInChildren<PlayerScore>();

        if (playerScore == null)
            return;

        // Nos suscribimos a los cambios
        playerScore.ZombiesEliminadosNetwork.OnValueChanged +=
            OnZombiesEliminadosChanged;

        // Comprobamos el valor actual por si ya había progreso
        ComprobarProgreso(
            playerScore.ZombiesEliminadosNetwork.Value
        );

        Debug.Log(
            "[ACHIEVEMENTS] PlayerScore encontrado. " +
            $"Zombies actuales: {playerScore.ZombiesEliminadosNetwork.Value}"
        );
    }

    private void OnZombiesEliminadosChanged(
        int valorAnterior,
        int valorNuevo)
    {
        ComprobarProgreso(valorNuevo);
    }

    private void ComprobarProgreso(int zombiesEliminados)
    {
        Debug.Log(
            $"[ACHIEVEMENTS] Zombies eliminados: " +
            $"{zombiesEliminados}"
        );

        // LOGRO 50
        if (!logro50Desbloqueado)
        {
            Debug.Log(
                $"[ACHIEVEMENTS] Derrota 50 zombies: " +
                $"{Mathf.Min(zombiesEliminados, 50)}/50"
            );

            if (zombiesEliminados >= 50)
            {
                logro50Desbloqueado = true;

                Debug.Log(
                    "[ACHIEVEMENTS] ¡LOGRO DESBLOQUEADO! " +
                    "Derrota 50 zombies"
                );
            }
        }

        // LOGRO 100
        if (!logro100Desbloqueado)
        {
            Debug.Log(
                $"[ACHIEVEMENTS] Derrota 100 zombies: " +
                $"{Mathf.Min(zombiesEliminados, 100)}/100"
            );

            if (zombiesEliminados >= 100)
            {
                logro100Desbloqueado = true;

                Debug.Log(
                    "[ACHIEVEMENTS] ¡LOGRO DESBLOQUEADO! " +
                    "Derrota 100 zombies"
                );
            }
        }

        // LOGRO 300
        if (!logro300Desbloqueado)
        {
            Debug.Log(
                $"[ACHIEVEMENTS] Derrota 300 zombies: " +
                $"{Mathf.Min(zombiesEliminados, 300)}/300"
            );

            if (zombiesEliminados >= 300)
            {
                logro300Desbloqueado = true;

                Debug.Log(
                    "[ACHIEVEMENTS] ¡LOGRO DESBLOQUEADO! " +
                    "Derrota 300 zombies"
                );
            }
        }
    }

    private void OnDestroy()
    {
        if (playerScore != null)
        {
            playerScore.ZombiesEliminadosNetwork.OnValueChanged -=
                OnZombiesEliminadosChanged;
        }
    }
}