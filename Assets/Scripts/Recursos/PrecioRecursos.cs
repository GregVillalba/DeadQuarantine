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

    public bool EsGratis =>
        Cantidad(TipoRecurso.PilaDeLatas) == 0 &&
        Cantidad(TipoRecurso.Cantimplora) == 0 &&
        Cantidad(TipoRecurso.Bidon) == 0;
}
