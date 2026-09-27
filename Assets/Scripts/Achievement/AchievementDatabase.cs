using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AchievementDatabase",
    menuName = "Achievements/Database"
)]
public class AchievementDatabase : ScriptableObject
{
    [SerializeField]
    private List<AchievementDefinition> logros =
        new List<AchievementDefinition>();

    private Dictionary<AchievementId, AchievementDefinition>
        diccionario;

    public void Inicializar()
    {
        diccionario =
            new Dictionary<AchievementId, AchievementDefinition>();

        foreach (AchievementDefinition logro in logros)
        {
            if (logro == null)
                continue;

            if (diccionario.ContainsKey(logro.id))
            {
                Debug.LogWarning(
                    $"Achievement duplicado: {logro.id}"
                );

                continue;
            }

            diccionario.Add(logro.id, logro);
        }
    }

    public AchievementDefinition Obtener(AchievementId id)
    {
        if (diccionario == null)
        {
            Inicializar();
        }

        if (diccionario.TryGetValue(
            id,
            out AchievementDefinition logro
        ))
        {
            return logro;
        }

        Debug.LogWarning(
            $"No existe un AchievementDefinition para {id}"
        );

        return null;
    }

    public IReadOnlyList<AchievementDefinition> ObtenerTodos()
    {
        return logros;
    }
}