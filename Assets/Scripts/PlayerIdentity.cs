using System;
using UnityEngine;

public static class PlayerIdentity
{
    private const string PREF_KEY_USER_ID = "DEVICE_LOCAL_USER_ID";

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
}