using System;
using UnityEngine;

/// <summary>
/// El personaje que eligió el jugador en el lobby. Se guarda en PlayerPrefs, así sobrevive al cambio de escena y
/// al cerrar el juego. El lobby solo tiene que hacer: CharacterSelection.SelectedIndex = numero;
/// (PlayerCharacter lo lee cuando el jugador aparece en la partida).
/// </summary>
public static class CharacterSelection
{
    private const string Key = "SelectedCharacter";

    public static event Action<int> Changed;

    public static int SelectedIndex
    {
        get { return PlayerPrefs.GetInt(Key, 0); }
        set
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(0, value));
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }
}
