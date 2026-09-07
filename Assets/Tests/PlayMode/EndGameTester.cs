using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class EndGameTester : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int rondaBossParaSaltar = 5;
    [SerializeField] private float segundosEsperaSpawnBoss = 6f;

    [Header("Salto solo al Boss")]
    [SerializeField] private float duracionLimpiezaSegundos = 40f;
    [SerializeField] private float intervaloLimpieza = 0.5f;

    private Coroutine limpiezaEnCurso;

    void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (Keyboard.current == null)
            return;
        ////
        //// F2: mata todos los zombies vivos de la ronda actual
        //if (Keyboard.current.f2Key.wasPressedThisFrame)
        //{
        //    MatarTodosLosZombiesVivos();
        //}

        //// F3: fuerza el salto directo a la ronda del Boss (por defecto 5)
        //if (Keyboard.current.f3Key.wasPressedThisFrame)
        //{
        //    SaltarARondaBoss();
        //}

        //// F4: mata a todos los jugadores (fuerza Derrota)
        //if (Keyboard.current.f4Key.wasPressedThisFrame)
        //{
        //    MatarATodosLosJugadores();
        //}

        //// F5: salta a la ronda del Boss, matando automáticamente
        //// a cada zombie normal apenas aparece.
        //if (Keyboard.current.f5Key.wasPressedThisFrame)
        //{
        //    SaltarSoloAlBoss();
        //}
    }

    // =========================================================
    // NUEVO: ronda del Boss, sin la horda normal
    // =========================================================
    private void SaltarSoloAlBoss()
    {
        if (RoundManager.Instance == null)
        {
            Debug.LogError("[EndGameTester] No se encontró RoundManager.Instance.");
            return;
        }

        Debug.Log(
            "[EndGameTester] F5 → Forzando Ronda " + rondaBossParaSaltar +
            " y limpiando zombies normales automáticamente durante " +
            duracionLimpiezaSegundos + "s, para dejar solo al Boss."
        );

        RoundManager.Instance.StartRound(rondaBossParaSaltar);

        if (limpiezaEnCurso != null)
        {
            StopCoroutine(limpiezaEnCurso);
        }

        limpiezaEnCurso = StartCoroutine(LimpiarNormalesDurante(duracionLimpiezaSegundos));
    }

    private IEnumerator LimpiarNormalesDurante(float duracionTotal)
    {
        float tiempoTranscurrido = 0f;

        while (tiempoTranscurrido < duracionTotal)
        {
            MatarTodosLosZombiesVivos();

            yield return new WaitForSeconds(intervaloLimpieza);
            tiempoTranscurrido += intervaloLimpieza;
        }

        Debug.Log("[EndGameTester] F5 → Limpieza automática finalizada. Solo debería quedar el Boss.");
        limpiezaEnCurso = null;
    }

    // =========================================================
    // (sin cambios respecto a la versión anterior)
    // =========================================================
    private void MatarTodosLosZombiesVivos()
    {
        ZombieHealth[] zombies = FindObjectsOfType<ZombieHealth>();
        int eliminados = 0;
        int saltadosPorSerBoss = 0;

        foreach (ZombieHealth zombie in zombies)
        {
            if (zombie == null || zombie.IsDead)
                continue;

            bool esBoss = zombie.gameObject.name.ToLower().Contains("boss");

            if (esBoss)
            {
                saltadosPorSerBoss++;
                continue;
            }

            zombie.TakeDamage(999999, zombie.transform.position, Vector3.up);
            eliminados++;
        }

        Debug.Log(
            "[EndGameTester] Limpieza → Se eliminaron " + eliminados +
            " zombies normales (se dejó con vida a " + saltadosPorSerBoss + " Boss)."
        );
    }

    private void SaltarARondaBoss()
    {
        if (RoundManager.Instance == null)
        {
            Debug.LogError("[EndGameTester] No se encontró RoundManager.Instance.");
            return;
        }

        RoundManager.Instance.StartRound(rondaBossParaSaltar);
        Invoke(nameof(ListarZombiesActivos), segundosEsperaSpawnBoss);
    }

    private void ListarZombiesActivos()
    {
        ZombieHealth[] zombies = FindObjectsOfType<ZombieHealth>();

        if (zombies.Length == 0)
        {
            Debug.LogWarning("[EndGameTester] No hay zombies vivos para listar.");
            return;
        }

        foreach (ZombieHealth zombie in zombies)
        {
            bool pareceBoss = zombie.gameObject.name.ToLower().Contains("boss");

            Debug.Log(
                "[EndGameTester] Zombie activo: " + zombie.gameObject.name +
                (pareceBoss ? "  <-- candidato a BOSS" : "")
            );
        }
    }

    private void MatarATodosLosJugadores()
    {
        if (NetworkManager.Singleton == null)
            return;

        int cantidad = 0;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth = client.PlayerObject.GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            playerHealth.Lives.Value = 0;
        }

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth = client.PlayerObject.GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            playerHealth.TakeDamage(playerHealth.MaxHealth);
            cantidad++;
        }

        Debug.Log("[EndGameTester] F4 → Se eliminó la vida de " + cantidad + " jugador(es).");
    }
}