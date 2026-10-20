using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Qué recursos suelta un zombie al morir y con qué probabilidad. Se ajusta desde el Inspector del asset
/// (Assets/Data/Recursos) sin tocar código ni prefabs; varios tipos de zombie pueden usar la misma tabla.
/// </summary>
[CreateAssetMenu(fileName = "TablaDropRecursos", menuName = "Recursos/Tabla de drop de recursos")]
public class TablaDropRecursos : ScriptableObject
{
    [Serializable]
    public class Drop
    {
        public TipoRecurso tipo;

        [Tooltip("Probabilidad de que el zombie suelte este recurso (0.1 = 10%).")]
        [Range(0f, 1f)] public float probabilidad;

        [Tooltip("Unidades que da al recolectarlo.")]
        [Min(1)] public int cantidad = 1;
    }

    [Tooltip("Cada zombie suelta como máximo UNO de estos recursos. Lo que le falta a la suma para llegar a 1 " +
             "es la probabilidad de no soltar nada. Si la suma pasa de 1, los últimos de la lista salen menos.")]
    [SerializeField] private List<Drop> drops = new List<Drop>();

    [Tooltip("Máximo de recursos que sueltan por ronda, entre todos los zombies que usan esta tabla. 0 = sin tope.")]
    [Min(0)]
    [SerializeField] private int maximoPorRonda;

    public IReadOnlyList<Drop> Drops => drops;
    public int MaximoPorRonda => maximoPorRonda;

    /// <summary>Probabilidad de que un zombie suelte algo (suma de la tabla, como mucho 1).</summary>
    public float ProbabilidadDeSoltarAlgo
    {
        get
        {
            float total = 0f;

            foreach (Drop drop in drops)
            {
                if (drop != null)
                    total += Mathf.Max(0f, drop.probabilidad);
            }

            return Mathf.Min(1f, total);
        }
    }

    /// <summary>
    /// Elige el drop según una tirada entre 0 y 1 (Random.value). null = no suelta nada.
    /// Ej: latas 0.2 y bidón 0.05 → tirada menor a 0.2 da latas, entre 0.2 y 0.25 da bidón, el resto nada.
    /// </summary>
    public Drop Elegir(float tirada)
    {
        float acumulado = 0f;

        foreach (Drop drop in drops)
        {
            if (drop == null || drop.probabilidad <= 0f)
                continue;

            acumulado += drop.probabilidad;

            if (tirada < acumulado)
                return drop;
        }

        return null;
    }

    private void OnValidate()
    {
        float total = 0f;

        foreach (Drop drop in drops)
        {
            if (drop != null)
                total += Mathf.Max(0f, drop.probabilidad);
        }

        if (total > 1f)
            Debug.LogWarning("[TablaDropRecursos] " + name + ": las probabilidades suman " + total.ToString("0.##") +
                             " (más de 1). Los últimos recursos de la lista van a salir menos de lo configurado.", this);
    }
}
