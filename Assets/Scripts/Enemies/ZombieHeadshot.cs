using Unity.Netcode;
using UnityEngine;

// Va en el objeto raíz del zombie, junto a ZombieHealth.
// Concentra toda la lógica de headshot: cuánto daño hace,
// si da puntos extra y el control del hitbox de la cabeza.
public class ZombieHeadshot : MonoBehaviour
{
    [Header("--- HEADSHOT ---")]
    [Tooltip("Si está activo, un headshot mata al zombie de un solo impacto.")]
    [SerializeField] private bool muerteInstantanea = true;

    [Tooltip("Multiplicador de daño cuando la muerte instantánea está apagada (C.A 2: x2).")]
    [SerializeField] private float multiplicadorDano = 2f;

    [Tooltip("Si es mayor que 0 (y la muerte instantánea está apagada), el headshot quita este porcentaje de la vida MÁXIMA del zombie. 0.5 = 50%.")]
    [Range(0f, 1f)]
    [SerializeField] private float porcentajeVidaMaxima = 0f;

    [Tooltip("Puntos extra por headshot, además de los 50 por impacto y los 500 por eliminación. En 0 no suma nada.")]
    [SerializeField] private int puntosExtraHeadshot = 0;

    [Header("--- HEADSHOT SEGÚN DIFICULTAD Y RONDA ---")]
    [Tooltip("Si está activo, el daño es un porcentaje de la vida máxima que depende de la dificultad y la ronda. Para que funcione, desactivá Muerte Instantánea.")]
    [SerializeField] private bool usarEscaladoPorDificultad = true;

    [Tooltip("Porcentaje de la vida máxima que quita el headshot en la ronda 1. Orden: Normal, Difícil, Pesadilla.")]
    [SerializeField] private float[] porcentajeBase = { 0.80f, 0.65f, 0.50f };

    [Tooltip("Cuánto baja ese porcentaje por cada ronda que avanza (0.01 = 1%).")]
    [SerializeField] private float reduccionPorRonda = 0.01f;

    [Tooltip("El porcentaje nunca baja de este valor.")]
    [SerializeField] private float porcentajeMinimo = 0.40f;

    private ZombieHeadHitbox[] hitboxes;

    private void Awake()
    {
        hitboxes = GetComponentsInChildren<ZombieHeadHitbox>(true);
    }

    // =========================================================
    // DAÑO (se llama desde el servidor)
    // =========================================================

    private float ObtenerPorcentajeActual()
    {
    if (porcentajeBase == null || porcentajeBase.Length == 0)
        return 0.5f;

    int dificultad = 0;
    int ronda = 1;

    if (RoundManager.Instance != null)
        {
            dificultad = Mathf.Clamp(
                RoundManager.Instance.DifficultyNetwork.Value,
                0,
                porcentajeBase.Length - 1
            );

            ronda = Mathf.Max(1, RoundManager.Instance.CurrentRound);
        }

        float porcentaje = porcentajeBase[dificultad] - reduccionPorRonda * (ronda - 1);

        return Mathf.Max(porcentaje, porcentajeMinimo);
    }


    public int CalcularDano(int danoBase, int vidaActual, int vidaMaxima)
    {
        // 1) Muerte instantánea (zombie común).
        if (muerteInstantanea)
            return Mathf.Max(danoBase, vidaActual);

        // 2) Según dificultad y ronda (porcentaje de la vida máxima).
        if (usarEscaladoPorDificultad)
        {
            int danoEscalado = Mathf.RoundToInt(vidaMaxima * ObtenerPorcentajeActual());
            return Mathf.Max(danoBase, danoEscalado);
        }

        // 3) Porcentaje de la vida máxima (jefes). Nunca hace menos que un impacto normal.
        if (porcentajeVidaMaxima > 0f)
        {
            int danoPorcentaje = Mathf.RoundToInt(vidaMaxima * porcentajeVidaMaxima);
            return Mathf.Max(danoBase, danoPorcentaje);
        }

        // 4) Multiplicador de daño (C.A 2: x2).
        return Mathf.Max(1, Mathf.RoundToInt(danoBase * multiplicadorDano));
    }

    // =========================================================
    // PUNTOS EXTRA (se llama desde el servidor)
    // =========================================================

    public void OtorgarPuntosExtra(ulong shooterClientId)
    {
        if (puntosExtraHeadshot <= 0)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            shooterClientId,
            out NetworkClient shooterClient))
        {
            return;
        }

        if (shooterClient.PlayerObject == null)
            return;

        PlayerScore playerScore =
            shooterClient.PlayerObject.GetComponentInChildren<PlayerScore>();

        if (playerScore != null)
            playerScore.SumarPuntos(puntosExtraHeadshot);
    }

    // =========================================================
    // HITBOX DE LA CABEZA
    // =========================================================

    // Al morir: el hitbox ya no debe interferir con los disparos ni con el ragdoll.
    public void DesactivarHitbox()
    {
        foreach (ZombieHeadHitbox hitbox in hitboxes)
        {
            if (hitbox != null)
                hitbox.SetActive(false);
        }
    }

    // Al revivir (respawn del tutorial).
    public void ActivarHitbox()
    {
        foreach (ZombieHeadHitbox hitbox in hitboxes)
        {
            if (hitbox != null)
                hitbox.SetActive(true);
        }
    }
}