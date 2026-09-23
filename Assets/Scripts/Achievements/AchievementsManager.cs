using System.IO;
using UnityEngine;
using Unity.Netcode;

public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager Instance { get; private set; }

    private PlayerScore playerScore;

    // Progreso persistente
    private int zombiesEliminadosTotales;

    private bool logro50Desbloqueado;
    private bool logro100Desbloqueado;
    private bool logro300Desbloqueado;

    private string rutaArchivo;

    [System.Serializable]
    private class AchievementsData
    {
        public int zombiesEliminados;

        public bool logro50Desbloqueado;
        public bool logro100Desbloqueado;
        public bool logro300Desbloqueado;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        rutaArchivo = Path.Combine(
            Application.persistentDataPath,
            "achievements.json"
        );

        CargarProgreso();
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

        // Comprobamos el valor actual
        OnZombiesEliminadosChanged(
            0,
            playerScore.ZombiesEliminadosNetwork.Value
        );

        Debug.Log(
            "[ACHIEVEMENTS] PlayerScore encontrado. " +
            $"Zombies de la partida: " +
            $"{playerScore.ZombiesEliminadosNetwork.Value}"
        );

        Debug.Log(
            "[ACHIEVEMENTS] Zombies acumulados: " +
            $"{zombiesEliminadosTotales}"
        );
    }

    private void OnZombiesEliminadosChanged(
        int valorAnterior,
        int valorNuevo)
    {
        // Calculamos cuántos zombies nuevos se eliminaron
        int zombiesNuevos = valorNuevo - valorAnterior;

        if (zombiesNuevos <= 0)
            return;

        zombiesEliminadosTotales += zombiesNuevos;

        Debug.Log(
            "[ACHIEVEMENTS] Zombies acumulados: " +
            $"{zombiesEliminadosTotales}"
        );

        ComprobarProgreso();

        GuardarProgreso();
    }

    private void ComprobarProgreso()
    {
        // LOGRO 50
        if (!logro50Desbloqueado)
        {
            Debug.Log(
                $"[ACHIEVEMENTS] Derrota 50 zombies: " +
                $"{Mathf.Min(zombiesEliminadosTotales, 50)}/50"
            );

            if (zombiesEliminadosTotales >= 50)
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
                $"{Mathf.Min(zombiesEliminadosTotales, 100)}/100"
            );

            if (zombiesEliminadosTotales >= 100)
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
                $"{Mathf.Min(zombiesEliminadosTotales, 300)}/300"
            );

            if (zombiesEliminadosTotales >= 300)
            {
                logro300Desbloqueado = true;

                Debug.Log(
                    "[ACHIEVEMENTS] ¡LOGRO DESBLOQUEADO! " +
                    "Derrota 300 zombies"
                );
            }
        }
    }

    private void GuardarProgreso()
    {
        AchievementsData datos = new AchievementsData();

        datos.zombiesEliminados = zombiesEliminadosTotales;

        datos.logro50Desbloqueado = logro50Desbloqueado;
        datos.logro100Desbloqueado = logro100Desbloqueado;
        datos.logro300Desbloqueado = logro300Desbloqueado;

        string json = JsonUtility.ToJson(datos, true);

        File.WriteAllText(rutaArchivo, json);

        Debug.Log(
            "[ACHIEVEMENTS] Progreso guardado en: " +
            rutaArchivo
        );
    }

    private void CargarProgreso()
    {
        if (!File.Exists(rutaArchivo))
        {
            Debug.Log(
                "[ACHIEVEMENTS] No existe un archivo de progreso. " +
                "Se comenzará desde cero."
            );

            return;
        }

        string json = File.ReadAllText(rutaArchivo);

        AchievementsData datos =
            JsonUtility.FromJson<AchievementsData>(json);

        if (datos == null)
        {
            Debug.LogWarning(
                "[ACHIEVEMENTS] No se pudo cargar el progreso."
            );

            return;
        }

        zombiesEliminadosTotales = datos.zombiesEliminados;

        logro50Desbloqueado = datos.logro50Desbloqueado;
        logro100Desbloqueado = datos.logro100Desbloqueado;
        logro300Desbloqueado = datos.logro300Desbloqueado;

        Debug.Log(
            "[ACHIEVEMENTS] Progreso cargado. " +
            $"Zombies: {zombiesEliminadosTotales}"
        );

        // Por seguridad, comprobamos nuevamente los logros
        ComprobarProgreso();
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