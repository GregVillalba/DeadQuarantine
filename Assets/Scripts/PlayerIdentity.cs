using System;
using UnityEngine;

public static class PlayerIdentity
{
    private const string PREF_KEY_USER_ID = "DEVICE_LOCAL_USER_ID";
    private const string PREF_KEY_USERNAME = "PlayerUsername";

    public static string GetUserId()
    {
        string currentId = PlayerPrefs.GetString(PREF_KEY_USER_ID, string.Empty);

        if (string.IsNullOrEmpty(currentId))
        {
            // Genera un UUID único de 36 caracteres para este dispositivo
            currentId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PREF_KEY_USER_ID, currentId);
            PlayerPrefs.Save();
            Debug.Log($"[IDENTITY] Nuevo ID único generado: {currentId}");
        }

        return currentId;
    }

    public static string GetUsername()
    {
        // Si no hay un nombre guardado aún, usa "Luch10" como valor inicial por defecto
        return PlayerPrefs.GetString(PREF_KEY_USERNAME, "Luch10");
    }

    public static void SetUsername(string newUsername)
    {
        if (string.IsNullOrWhiteSpace(newUsername)) return;

        PlayerPrefs.SetString(PREF_KEY_USERNAME, newUsername.Trim());
        PlayerPrefs.Save();
        Debug.Log($"[IDENTITY] Nombre de usuario actualizado: {newUsername}");
    }
}