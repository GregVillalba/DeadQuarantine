using Unity.Netcode;
using UnityEngine;

/// <summary>
/// DaÃ±o por caÃ­da. PlayerMovement (del dueÃ±o) mide la altura de la caÃ­da y la manda al servidor,
/// que aplica el daÃ±o con TakeDamage: si deja la vida en 0 el jugador queda abatido o, sin vidas,
/// eliminado, igual que con cualquier otro daÃ±o (tambiÃ©n en multijugador).
/// </summary>
public partial class PlayerHealth
{
    [Header("DaÃ±o por caÃ­da")]
    [Tooltip("CaÃ­das mÃ¡s bajas que esto no hacen daÃ±o (un salto normal es ~1,1 m).")]
    [Min(0f)] [SerializeField] private float alturaMinimaCaida = 4f;
    [Tooltip("Desde esta altura la caÃ­da saca toda la vida mÃ¡xima. Entre la mÃ­nima y esta, el daÃ±o crece parejo.")]
    [Min(0f)] [SerializeField] private float alturaLetalCaida = 12f;

    /// <summary>0 en la altura mÃ­nima, 1 en la letal (o mÃ¡s). Lo usa PlayerMovement para la sacudida de cÃ¡mara.</summary>
    public float IntensidadCaida(float altura)
    {
        float rango = Mathf.Max(0.01f, alturaLetalCaida - alturaMinimaCaida);
        return Mathf.Clamp01((altura - alturaMinimaCaida) / rango);
    }

    /// <summary>DaÃ±o que hace una caÃ­da de esa altura (0 si es mÃ¡s baja que la mÃ­nima).</summary>
    public int CalcularDanoCaida(float altura)
    {
        if (altura < alturaMinimaCaida)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(maxHealth * IntensidadCaida(altura)));
    }

    /// <summary>Solo en el dueÃ±o: Time.time de la Ãºltima caÃ­da que hizo daÃ±o. El HUD muestra el icono de caÃ­da unos segundos.</summary>
    public float UltimaCaidaConDano { get; private set; } = float.NegativeInfinity;

    /// <summary>Lo llama PlayerMovement del dueÃ±o al aterrizar. El servidor calcula y aplica el daÃ±o.</summary>
    public void ReportarCaida(float altura)
    {
        if (!IsOwner || CalcularDanoCaida(altura) <= 0)
            return;

        UltimaCaidaConDano = Time.time;
        RecibirDanoCaidaServerRpc(altura);
    }

    [ServerRpc]
    private void RecibirDanoCaidaServerRpc(float altura)
    {
        int dano = CalcularDanoCaida(altura);

        // El chaleco y el casco no protegen de una caÃ­da.
        if (dano > 0)
            TakeDamage(dano, true);
    }
    /// <summary>El vacío es una caída sin posibilidad de recuperación en el mismo lugar.</summary>
    private bool eliminatedOutsideMap;
    private float outsideMapGraceUntil;
    private void PrepareOutsideMapRespawn()
    {
        if (eliminatedOutsideMap) outsideMapGraceUntil = Time.unscaledTime + 2;
        eliminatedOutsideMap = false;
    }
    public void KillOutsideMapServer()
    {
        if (!IsSpawned || !IsServer || IsDead || IsSpectating || EnMinijuego || PlayerInputGate.MatchLost || Time.unscaledTime < outsideMapGraceUntil) return;
        eliminatedOutsideMap = true;
        if (IsAlive) TakeDamage(Mathf.Max(1, CurrentHealth.Value), true);
        // No dejar al jugador abatido fuera del escenario: allí nadie puede rescatarlo.
        if (IsAlive || IsDowned) EliminatePlayer();
    }

}
