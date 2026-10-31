using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Quemadura del jugador: el fuego de su propia granada incendiaria lo quema si lo pisa
/// (a los compañeros no, ver GranadaIncendiaria). Los compañeros ven las llamas sobre él;
/// el dueño no, para que el fuego no le tape la cámara.
/// </summary>
public partial class PlayerHealth
{
    [Header("Quemadura (granada incendiaria)")]
    [Tooltip("Fuego que ven los compañeros encima del jugador mientras se quema. Vacío = sin efecto visual.")]
    [SerializeField] private GameObject efectoQuemadura;
    [SerializeField] private float alturaEfectoQuemadura = 1f;

    // true mientras se está quemando: los clientes prenden / apagan el fuego encima del jugador.
    public NetworkVariable<bool> Quemandose =
        new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Quemadura (solo servidor).
    private float finQuemadura;
    private int danoQuemadura;
    private float intervaloQuemadura;
    private Coroutine rutinaQuemadura;

    // Fuego instanciado en este cliente.
    private GameObject fuegoActivo;

    /// <summary>
    /// Solo servidor. Prende fuego al jugador o, si ya se está quemando, le renueva la duración.
    /// Hace danoPorTick cada intervalo; se apaga al pasar la duración o si queda abatido.
    /// </summary>
    public void Quemar(float duracion, int danoPorTick, float intervalo)
    {
        if (!IsServer || !IsAlive)
            return;

        finQuemadura = Mathf.Max(finQuemadura, Time.time + duracion);
        danoQuemadura = danoPorTick;
        intervaloQuemadura = Mathf.Max(0.1f, intervalo);

        if (rutinaQuemadura == null)
            rutinaQuemadura = StartCoroutine(RutinaQuemadura());
    }

    private IEnumerator RutinaQuemadura()
    {
        Quemandose.Value = true;
        float proximoTick = Time.time + intervaloQuemadura;

        // Se revisa cada frame para apagar el fuego apenas queda abatido.
        while (IsAlive && Time.time < finQuemadura)
        {
            if (Time.time >= proximoTick)
            {
                TakeDamage(danoQuemadura);
                proximoTick += intervaloQuemadura;
            }

            yield return null;
        }

        rutinaQuemadura = null;
        finQuemadura = 0f;
        Quemandose.Value = false;
    }

    private void SuscribirQuemadura()
    {
        Quemandose.OnValueChanged += QuemandoseChanged;
        ActualizarEfectoQuemadura(Quemandose.Value);
    }

    private void DesuscribirQuemadura()
    {
        Quemandose.OnValueChanged -= QuemandoseChanged;
        ActualizarEfectoQuemadura(false);
    }

    private void QuemandoseChanged(bool anterior, bool actual)
    {
        ActualizarEfectoQuemadura(actual);
    }

    private void ActualizarEfectoQuemadura(bool encendido)
    {
        if (encendido)
        {
            if (IsOwner || fuegoActivo != null || efectoQuemadura == null)
                return;

            fuegoActivo = Instantiate(
                efectoQuemadura,
                transform.position + Vector3.up * alturaEfectoQuemadura,
                Quaternion.identity,
                transform);

            return;
        }

        if (fuegoActivo == null)
            return;

        // Se suelta del jugador y deja de emitir: las llamas que ya salieron se apagan solas.
        fuegoActivo.transform.SetParent(null, true);

        foreach (ParticleSystem particulas in fuegoActivo.GetComponentsInChildren<ParticleSystem>())
            particulas.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(fuegoActivo, 2f);
        fuegoActivo = null;
    }
}
