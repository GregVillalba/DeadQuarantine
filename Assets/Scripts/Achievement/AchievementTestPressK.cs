using UnityEngine;
using UnityEngine.InputSystem;

public class AchievementTestK : MonoBehaviour
{
    [SerializeField]
    private AchievementId logro = AchievementId.PresionasteK;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (AchievementsManager.Instance != null)
            {
                AchievementsManager.Instance.Desbloquear(logro);
            }
        }
    }
}