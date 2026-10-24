using System;
using UnityEngine;

/// <summary>
/// Una de las 3 distribuciones de recursos de un escenario: los PuntoRecursoEscenario que tiene como hijos
/// (8 de Pila de latas, 4 de Cantimplora y 2 de Bidón). Al empezar la partida GestorRecursosEscenario
/// elige una al azar y solo usa esa (US 19.1).
/// </summary>
public class OpcionRecursosEscenario : MonoBehaviour
{
    public const int OpcionesPorEscenario = 3;

    public PuntoRecursoEscenario[] Puntos => GetComponentsInChildren<PuntoRecursoEscenario>(true);

    /// <summary>Cuántos puntos de cada tipo tiene que tener cada opción.</summary>
    public static int CantidadEsperada(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return 4;
            case TipoRecurso.Bidon: return 2;
            default: return 8;
        }
    }

    public int Cantidad(TipoRecurso tipo)
    {
        int cantidad = 0;

        foreach (PuntoRecursoEscenario punto in Puntos)
        {
            if (punto.Tipo == tipo)
                cantidad++;
        }

        return cantidad;
    }

    /// <summary>true si tiene exactamente los puntos esperados de cada tipo.</summary>
    public bool EstaCompleta
    {
        get
        {
            foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
            {
                if (Cantidad(tipo) != CantidadEsperada(tipo))
                    return false;
            }

            return true;
        }
    }

    /// <summary>Ej: "Pila de latas 8/8, Cantimplora 3/4, Bidón 2/2".</summary>
    public string Resumen()
    {
        string resumen = "";

        foreach (TipoRecurso tipo in Enum.GetValues(typeof(TipoRecurso)))
        {
            if (resumen.Length > 0)
                resumen += ", ";

            resumen += tipo.Nombre() + " " + Cantidad(tipo) + "/" + CantidadEsperada(tipo);
        }

        return resumen;
    }

    // Click derecho en el componente → "Contar puntos", para revisar la opción mientras se arma.
    [ContextMenu("Contar puntos")]
    private void ContarPuntos()
    {
        if (EstaCompleta)
            Debug.Log("[Recursos] " + name + " completa: " + Resumen() + ".", this);
        else
            Debug.LogWarning("[Recursos] " + name + " incompleta: " + Resumen() + ".", this);
    }
}
