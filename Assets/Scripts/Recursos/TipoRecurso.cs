/// <summary>Recursos de supervivencia que se juntan en el escenario para intercambiar con el mercader.</summary>
public enum TipoRecurso
{
    PilaDeLatas,    // común
    Cantimplora,    // rara
    Bidon           // escaso
}

public static class TipoRecursoNombres
{
    /// <summary>Nombre que ve el jugador (ej: "[E] para recoger Pila de latas").</summary>
    public static string Nombre(this TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return "Cantimplora";
            case TipoRecurso.Bidon: return "Bidón";
            default: return "Pila de latas";
        }
    }
}
