using System.Collections;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Granada incendiaria: al explotar deja una zona de fuego en el piso. Los zombies que pisan la zona
/// se prenden fuego y siguen quemándose unos segundos después de salir (ver ZombieHealth.Quemar).
/// El fuego también quema al jugador que la tiró si lo pisa, pero nunca a sus compañeros
/// (para que no se use contra ellos). El daño del estallido es el de "Explosion Damage" (bajo) y solo daña zombies.
/// </summary>
public class GranadaIncendiaria : GrenadeProjectile
{
    [Header("Zona de fuego")]
    [SerializeField] private float radioFuego = 4f;
    [SerializeField] private float duracionFuego = 7f;
    [Tooltip("Cada cuánto se revisa qué zombies están dentro del fuego.")]
    [SerializeField] private float intervaloRevision = 0.25f;

    [Header("Quemadura")]
    [SerializeField] private int danoPorTick = 8;
    [SerializeField] private float intervaloQuemadura = 0.5f;
    [Tooltip("Segundos que el zombie se sigue quemando después de salir del fuego.")]
    [SerializeField] private float duracionQuemadura = 3f;

    [Header("Quemadura del que la tiró (los compañeros nunca se queman)")]
    [SerializeField] private bool quemaAlLanzador = true;
    [SerializeField] private int danoPorTickJugador = 5;
    [Tooltip("Segundos que el jugador se sigue quemando después de salir del fuego.")]
    [SerializeField] private float duracionQuemaduraJugador = 1.5f;
    [Tooltip("Diferencia de altura máxima entre el jugador y el fuego para que lo queme (evita quemarlo desde un piso de arriba).")]
    [SerializeField] private float alturaMaximaJugador = 1.5f;

    [Header("Efecto visual de la zona")]
    [SerializeField] private GameObject efectoFuegoPrefab;
    [Tooltip("Cuántos fuegos se reparten dentro del radio (uno siempre en el centro).")]
    [SerializeField] private int cantidadFuegos = 7;

    protected override void Detonar()
    {
        DanarEnArea(explosionRadius, explosionDamage, true);
        ReproducirEfectosExplosion();

        // La granada queda quieta en el piso, invisible, mientras dura el fuego.
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        IniciarZonaClientRpc(transform.position, Random.Range(0, int.MaxValue));
        StartCoroutine(RutinaZona());
    }

    private IEnumerator RutinaZona()
    {
        float fin = Time.time + duracionFuego;
        Vector3 centro = transform.position;
        PlayerHealth lanzador = quemaAlLanzador ? BuscarLanzador() : null;

        while (Time.time < fin)
        {
            foreach (ZombieAlcanzado alcanzado in ZombiesEnRadio(radioFuego))
                alcanzado.zombie.Quemar(duracionQuemadura, danoPorTick, intervaloQuemadura, LanzadorClientId);

            if (lanzador != null && EstaEnElFuego(lanzador.transform.position, centro))
                lanzador.Quemar(duracionQuemaduraJugador, danoPorTickJugador, intervaloQuemadura);

            yield return new WaitForSeconds(intervaloRevision);
        }

        DespawnGranada();
    }

    // Solo el jugador que la tiró: a los compañeros el fuego no los quema.
    private PlayerHealth BuscarLanzador()
    {
        if (NetworkManager == null ||
            !NetworkManager.ConnectedClients.TryGetValue(LanzadorClientId, out NetworkClient cliente) ||
            cliente.PlayerObject == null)
            return null;

        return cliente.PlayerObject.GetComponentInChildren<PlayerHealth>();
    }

    private bool EstaEnElFuego(Vector3 posicionJugador, Vector3 centro)
    {
        Vector3 distancia = posicionJugador - centro;
        float horizontal = new Vector2(distancia.x, distancia.z).magnitude;

        return horizontal <= radioFuego && Mathf.Abs(distancia.y) <= alturaMaximaJugador;
    }

    [ClientRpc]
    private void IniciarZonaClientRpc(Vector3 centro, int semilla)
    {
        foreach (Renderer render in GetComponentsInChildren<Renderer>())
            render.enabled = false;

        if (efectoFuegoPrefab == null)
            return;

        // Misma semilla en todos los clientes: los fuegos aparecen en los mismos lugares.
        System.Random azar = new System.Random(semilla);

        for (int i = 0; i < Mathf.Max(1, cantidadFuegos); i++)
        {
            Vector3 posicion = centro;

            if (i > 0)
            {
                float angulo = (float)(azar.NextDouble() * Mathf.PI * 2f);
                float distancia = radioFuego * Mathf.Sqrt((float)azar.NextDouble()) * 0.85f;
                posicion += new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * distancia;
            }

            GameObject fuego = Instantiate(efectoFuegoPrefab, AlPiso(posicion), Quaternion.identity);
            Destroy(fuego, duracionFuego);
        }
    }

    // Apoya el fuego en el piso aunque la zona tenga desniveles.
    private static Vector3 AlPiso(Vector3 posicion)
    {
        if (Physics.Raycast(posicion + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point;

        return posicion;
    }
}
