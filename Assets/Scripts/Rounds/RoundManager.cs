using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; //esto se elimina, solo es para probar

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Zombie")]
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Rondas")]
    [SerializeField] private int startingZombies = 6;
    [SerializeField] private int zombiesPerRound = 6;
    [SerializeField] private int maxRounds = 5;
    [SerializeField] private float nextRoundDelay = 5f;

    [Header("HUD de inicio de ronda")]
    [SerializeField] private RoundStartHUD roundStartHUD;

    public int CurrentRound => currentRound;
    public int MaxRounds => maxRounds;
    public int AliveZombies => aliveZombies;
    public int ZombiesThisRound => zombiesThisRound;

    // Estas dos propiedades son las que necesita HUDController.
    public bool IsCountingDown => countdownInProgress;
    public int CountdownRemaining => countdownRemaining;

    private int currentRound;
    private int aliveZombies;
    private int zombiesThisRound;
    private int countdownRemaining;

    private bool roundInProgress;
    private bool countdownInProgress;

    private int pendingNextRound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (roundStartHUD != null)
            roundStartHUD.Hide();

        StartRound(1);
    }

    public void StartRound(int round)
    {
        currentRound = round;

        zombiesThisRound =
            startingZombies +
            (currentRound - 1) * zombiesPerRound;

        aliveZombies = zombiesThisRound;

        roundInProgress = true;
        countdownInProgress = false;
        countdownRemaining = 0;

        if (roundStartHUD != null)
            roundStartHUD.Hide();

        SpawnZombies(zombiesThisRound);

        Debug.Log(
            "Ronda " + currentRound +
            " iniciada. Zombies: " +
            zombiesThisRound
        );
    }

    private void SpawnZombies(int amount)
    {
        if (zombiePrefab == null)
        {
            Debug.LogError(
                "RoundManager: falta asignar Zombie Prefab."
            );

            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "RoundManager: no hay Spawn Points."
            );

            return;
        }

        for (int i = 0; i < amount; i++)
        {
            Transform spawnPoint =
                spawnPoints[i % spawnPoints.Length];

            Instantiate(
                zombiePrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
    }

    public void ZombieDied()
    {
        if (!roundInProgress)
            return;

        aliveZombies--;

        if (aliveZombies < 0)
            aliveZombies = 0;

        Debug.Log(
            "Zombie muerto. Restantes: " +
            aliveZombies
        );

        if (aliveZombies <= 0)
        {
            EndRound();
        }
    }

    public void PlayerDied()
    {
        if (!roundInProgress)
            return;

        roundInProgress = false;

        if (GameplayPopupsController.Instance != null)
            GameplayPopupsController.Instance.MostrarPanelPerdedor();
      
        else
            Debug.LogError("RoundManager: GameplayPopupsController.Instance es null, no se pudo mostrar el panel perdedor.");
    }

    private void EndRound()
    {
        roundInProgress = false;

        if (GameplayPopupsController.Instance != null)
            GameplayPopupsController.Instance.MostrarPanelGanador();
      
        else
            Debug.LogError("RoundManager: GameplayPopupsController.Instance es null, no se pudo mostrar el panel ganador.");

        if (currentRound >= maxRounds)
        {
            Debug.Log("Ronda 5 completada.");

        if (roundStartHUD != null)
            roundStartHUD.Hide();

        // No hay ronda siguiente, el panel se queda mostrado sin acción de "siguiente".
        return;
    }

    // Guarda la próxima ronda pero NO la arranca todavía.
    pendingNextRound = currentRound + 1;
    
    /*roundInProgress = false;

        // Si terminó la última ronda.
        if (currentRound >= maxRounds)
        {
            Debug.Log("Ronda 5 completada.");

            if (roundStartHUD != null)
                roundStartHUD.Hide();

            return;
        }

        int nextRound = currentRound + 1;

        StartCoroutine(
            CountdownToNextRound(nextRound)
        );*/
    }

    public void ConfirmarSiguienteRonda()
{
    if (pendingNextRound <= 0)
        return;

    int nextRound = pendingNextRound;
    pendingNextRound = 0;

    StartCoroutine(
        CountdownToNextRound(nextRound)
    );
}

    private IEnumerator CountdownToNextRound(int nextRound)
    {
        if (countdownInProgress)
            yield break;

        countdownInProgress = true;

        if (roundStartHUD != null)
            roundStartHUD.Show(nextRound, 5);

        countdownRemaining = 5;

        yield return new WaitForSeconds(1f);

        countdownRemaining = 4;

        if (roundStartHUD != null)
            roundStartHUD.Show(nextRound, 4);

        yield return new WaitForSeconds(1f);

        countdownRemaining = 3;

        if (roundStartHUD != null)
            roundStartHUD.Show(nextRound, 3);

        yield return new WaitForSeconds(1f);

        countdownRemaining = 2;

        if (roundStartHUD != null)
            roundStartHUD.Show(nextRound, 2);

        yield return new WaitForSeconds(1f);

        countdownRemaining = 1;

        if (roundStartHUD != null)
            roundStartHUD.Show(nextRound, 1);

        yield return new WaitForSeconds(1f);

        countdownRemaining = 0;

        if (roundStartHUD != null)
            roundStartHUD.Show(nextRound, 0);

        yield return new WaitForSeconds(0.2f);

        countdownInProgress = false;

        StartRound(nextRound);
    }

    // --- prueba ---
 /*   private void Update()
{
    // SOLO PARA TESTING - sacar antes de entregar
    if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
    {
        ZombieDied();
    }
}*/
}