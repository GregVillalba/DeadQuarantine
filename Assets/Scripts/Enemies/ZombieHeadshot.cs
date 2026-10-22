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

    private ZombieHeadHitbox[] hitboxes;

    private void Awake()
    {
        hitboxes = GetComponentsInChildren<ZombieHeadHitbox>(true);
    }

    // =========================================================
    // DAÑO (se llama desde el servidor)
    // =========================================================

    public int CalcularDano(int danoBase, int vidaActual, int vidaMaxima)
    {
        // 1) Muerte instantánea (zombie común).
        if (muerteInstantanea)
            return Mathf.Max(danoBase, vidaActual);

        // 2) Porcentaje de la vida máxima (jefes). Nunca hace menos que un impacto normal.
        if (porcentajeVidaMaxima > 0f)
        {
            int danoPorcentaje = Mathf.RoundToInt(vidaMaxima * porcentajeVidaMaxima);
            return Mathf.Max(danoBase, danoPorcentaje);
        }

        // 3) Multiplicador de daño (C.A 2: x2).
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