using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Cuántos recursos de cada tipo pide el mercader por una oferta (o por repararla).
/// Se manda en las RPC de compra de PlayerRecursos.
/// </summary>
public struct PrecioRecursos : INetworkSerializeByMemcpy
{
    public int pilaDeLatas;
    public int cantimplora;
    public int bidon;

    public PrecioRecursos(int pilaDeLatas, int cantimplora, int bidon)
    {
        this.pilaDeLatas = pilaDeLatas;
        this.cantimplora = cantimplora;
        this.bidon = bidon;
    }

    /// <summary>Cuánto pide de un recurso. Nunca es negativo (0 = no lo pide).</summary>
    public int Cantidad(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return Mathf.Max(0, cantimplora);
            case TipoRecurso.Bidon: return Mathf.Max(0, bidon);
            default: return Mathf.Max(0, pilaDeLatas);
        }
    }

    /// <summary>
    /// El precio con la inflación de la tienda: cada recurso por el multiplicador, redondeado al entero más cercano
    /// (la mitad sube). Un multiplicador menor a 1 no abarata: se toma como 1. Lo que no se pide sigue en 0.
    /// </summary>
    public PrecioRecursos Multiplicado(float multiplicador)
    {
        if (multiplicador <= 1f)
            return this;

        return new PrecioRecursos(
            Redondear(Cantidad(TipoRecurso.PilaDeLatas) * multiplicador),
            Redondear(Cantidad(TipoRecurso.Cantimplora) * multiplicador),
            Redondear(Cantidad(TipoRecurso.Bidon) * multiplicador));
    }

    // El margen evita que un 2.5 guardado como 2.4999998 baje a 2.
    private static int Redondear(float valor)
    {
        return Mathf.FloorToInt(valor + 0.5001f);
    }

    public bool EsGratis =>
        Cantidad(TipoRecurso.PilaDeLatas) == 0 &&
        Cantidad(TipoRecurso.Cantimplora) == 0 &&
        Cantidad(TipoRecurso.Bidon) == 0;
}
