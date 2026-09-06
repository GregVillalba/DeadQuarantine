using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class EndGameTester : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int rondaBossParaSaltar = 5;
    [SerializeField] private float segundosEsperaSpawnBoss = 6f;

    void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (Keyboard.current == null)
            return;

        // F2: mata todos los zombies vivos de la ronda actual
       // if (Keyboard.current.f2Key.wasPressedThisFrame)
       /// {
        //    MatarTodosLosZombiesVivos();
        //}

        // F3: fuerza el salto directo a la ronda del Boss (por defecto 5)
        //if (Keyboard.current.f3Key.wasPressedThisFrame)
      //  {
       //     SaltarARondaBoss();
      //  }

        // F4: mata a todos los jugadores (fuerza Derrota)
      //  if (Keyboard.current.f4Key.wasPressedThisFrame)
      //  {
       //     MatarATodosLosJugadores();
      //  }
    }

    // =========================================================
    // CP: "se eliminan todos los zombis... habilita siguiente ronda,
    // notificando el avance en la interfaz."
    // (y también cubre Victoria si la ronda actual ya es la final,
    // porque el mismo AliveZombiesNetwork <= 0 dispara ese flujo)
    // =========================================================
    private void MatarTodosLosZombiesVivos()
    {
        ZombieHealth[] zombies = FindObjectsOfType<ZombieHealth>();
        int eliminados = 0;

        foreach (ZombieHealth zombie in zombies)
        {
            if (zombie == null || zombie.IsDead)
                continue;

            zombie.TakeDamage(999999, zombie.transform.position, Vector3.up);
            eliminados++;
        }

        Debug.Log(
            "[EndGameTester] F2 → Se eliminaron " + eliminados +
            " zombies vivos. Si era la ronda final, debería dispararse Victoria; " +
            "si no, debería avisar el avance a la siguiente ronda."
        );
    }

    // =========================================================
    // CP: "al arrancar la ronda 5 aparece el Zombie Boss con stats mayores"
    // =========================================================
    private void SaltarARondaBoss()
    {
        if (RoundManager.Instance == null)
        {
            Debug.LogError("[EndGameTester] No se encontró RoundManager.Instance.");
            return;
        }

        Debug.Log(
            "[EndGameTester] F3 → Forzando inicio de Ronda " + rondaBossParaSaltar +
            ". Mirá el log '[RoundManager] Boss creado. Vida: X' que aparece solo " +
            "para confirmar la vida del Boss, y confirmá visualmente en el Game View " +
            "su escala/modelo y el daño al recibir un ataque."
        );

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

    // =========================================================
    // CP: "Derrota del equipo... se muestra pantalla Game Over"
    // =========================================================
    private void MatarATodosLosJugadores()
    {
        if (NetworkManager.Singleton == null)
            return;

        int cantidad = 0;

        // Primero ponemos las vidas de TODOS en 0, así el próximo
        // golpe elimina directo (sin pasar por el estado "abatido").
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerHealth playerHealth = client.PlayerObject.GetComponent<PlayerHealth>();

            if (playerHealth == null)
                continue;

            playerHealth.Lives.Value = 0;
        }

        // Ahora sí les bajamos la vida a 0 a todos.
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

        Debug.Log(
            "[EndGameTester] F4 → Se eliminó la vida de " + cantidad +
            " jugador(es). Debería dispararse Derrota / Game Over."
        );
    }
}