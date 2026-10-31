using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Daño por caída. PlayerMovement (del dueño) mide la altura de la caída y la manda al servidor,
/// que aplica el daño con TakeDamage: si deja la vida en 0 el jugador queda abatido o, sin vidas,
/// eliminado, igual que con cualquier otro daño (también en multijugador).
/// </summary>
public partial class PlayerHealth
{
    [Header("Daño por caída")]
    [Tooltip("Caídas más bajas que esto no hacen daño (un salto normal es ~1,1 m).")]
    [Min(0f)] [SerializeField] private float alturaMinimaCaida = 4f;
    [Tooltip("Desde esta altura la caída saca toda la vida máxima. Entre la mínima y esta, el daño crece parejo.")]
    [Min(0f)] [SerializeField] private float alturaLetalCaida = 12f;

    /// <summary>0 en la altura mínima, 1 en la letal (o más). Lo usa PlayerMovement para la sacudida de cámara.</summary>
    public float IntensidadCaida(float altura)
    {
        float rango = Mathf.Max(0.01f, alturaLetalCaida - alturaMinimaCaida);
        return Mathf.Clamp01((altura - alturaMinimaCaida) / rango);
    }

    /// <summary>Daño que hace una caída de esa altura (0 si es más baja que la mínima).</summary>
    public int CalcularDanoCaida(float altura)
    {
        if (altura < alturaMinimaCaida)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(maxHealth * IntensidadCaida(altura)));
    }

    /// <summary>Solo en el dueño: Time.time de la última caída que hizo daño. El HUD muestra el icono de caída unos segundos.</summary>
    public float UltimaCaidaConDano { get; private set; } = float.NegativeInfinity;

    /// <summary>Lo llama PlayerMovement del dueño al aterrizar. El servidor calcula y aplica el daño.</summary>
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

        // El chaleco y el casco no protegen de una caída.
        if (dano > 0)
            TakeDamage(dano, true);
    }
}
