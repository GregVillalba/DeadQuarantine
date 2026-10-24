using System;
using UnityEngine;

/// <summary>
/// El personaje que eligió el jugador en el lobby. Se guarda en PlayerPrefs para recordarlo entre sesiones, pero durante
/// la partida el valor vive EN MEMORIA de cada instancia del juego: así dos instancias abiertas en la misma PC
/// (Multiplayer Play Mode, ParrelSync, dos builds) comparten el archivo de PlayerPrefs pero NO se pisan la elección.
/// El lobby solo tiene que hacer: CharacterSelection.SelectedIndex = numero;
/// (PlayerCharacter lo lee cuando el jugador aparece en la partida).
/// </summary>
public static class CharacterSelection
{
    private const string Key = "SelectedCharacter";

    public static event Action<int> Changed;

    // -1 = todavía no se leyó de PlayerPrefs. Se lee UNA vez por instancia y después manda la memoria.
    private static int cached = -1;

    // Por si se desactiva el "Domain Reload" al entrar a Play: arranca cada Play leyendo de nuevo.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cached = -1;
    }

    public static int SelectedIndex
    {
        get
        {
            if (cached < 0)
                cached = Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));

            return cached;
        }
        set
        {
            cached = Mathf.Max(0, value);
            PlayerPrefs.SetInt(Key, cached);
            PlayerPrefs.Save();
            Changed?.Invoke(cached);
        }
    }
}