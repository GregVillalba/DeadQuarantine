using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class RoundSpeedTest : MonoBehaviour
{
    [SerializeField] private float segundosEsperaPorRonda = 3f;
    [SerializeField] private int rondaInicial = 1;
    [SerializeField] private int rondaFinal = 4;

    private float velocidadPromedioRondaAnterior = -1f;
    private bool testEnCurso = false;

    //si se presiona f1 se pasa de ronda pero los zombies quedan vivos :O
    //void Update()
    //{
    //   if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
    //    {
     //       EjecutarTest();
    //    }
   // }

    public void EjecutarTest()
    {
        if (testEnCurso)
        {
            Debug.LogWarning("[RoundSpeedTestAutomatico] El test ya está en curso.");
            return;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[RoundSpeedTestAutomatico] Este test debe correrse como Host/Servidor.");
            return;
        }

        StartCoroutine(CorrerRondas());
    }

    private IEnumerator CorrerRondas()
    {
        testEnCurso = true;
        velocidadPromedioRondaAnterior = -1f; // reinicia por si lo corrés más de una vez

        for (int ronda = rondaInicial; ronda <= rondaFinal; ronda++)
        {
            Debug.Log("[RoundSpeedTestAutomatico] Forzando inicio de Ronda " + ronda);

            RoundManager.Instance.StartRound(ronda);

            yield return new WaitForSeconds(segundosEsperaPorRonda);

            float promedioRonda = MedirVelocidadPromedio();
            EvaluarRonda(ronda, promedioRonda);

            velocidadPromedioRondaAnterior = promedioRonda;
        }

        Debug.Log("[RoundSpeedTestAutomatico] Test finalizado (rondas " + rondaInicial + " a " + rondaFinal + ").");
        testEnCurso = false;
    }

    private float MedirVelocidadPromedio()
    {
        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();

        if (zombies.Length == 0)
        {
            Debug.LogWarning("[RoundSpeedTestAutomatico] No hay zombies activos para medir.");
            return 0f;
        }

        float suma = 0f;
        int contados = 0;

        foreach (ZombieAI zombie in zombies)
        {
            NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                suma += agent.speed;
                contados++;
            }
        }

        return contados > 0 ? suma / contados : 0f;
    }

    private void EvaluarRonda(int ronda, float promedioRonda)
    {
        if (velocidadPromedioRondaAnterior < 0f)
        {
            Debug.Log(
                "[RoundSpeedTestAutomatico] Ronda " + ronda +
                ": velocidad promedio " + promedioRonda.ToString("F2") +
                " (primera ronda medida, sin ronda previa para comparar)"
            );
            return;
        }

        bool cumpleCriterio = promedioRonda > velocidadPromedioRondaAnterior;
        string estado = cumpleCriterio ? "OK" : "FALLA";

        Debug.Log(
            "[RoundSpeedTestAutomatico] Ronda " + ronda +
            ": velocidad promedio " + promedioRonda.ToString("F2") +
            " | Ronda anterior: " + velocidadPromedioRondaAnterior.ToString("F2") +
            " | US 3.3 (1-4): " + estado
        );
    }
}