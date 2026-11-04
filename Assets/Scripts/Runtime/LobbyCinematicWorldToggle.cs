using UnityEngine;

/// <summary>
/// Mantiene el escenario cinematográfico activo solamente mientras
/// el panel real del lobby está visible.
/// </summary>
public class LobbyCinematicWorldToggle : MonoBehaviour
{
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject contentRoot;

    private bool lastState;

    public void Configure(
        GameObject panel,
        GameObject content)
    {
        lobbyPanel = panel;
        contentRoot = content;
        Apply(true);
    }

    private void Awake()
    {
        Apply(true);
    }

    private void Update()
    {
        Apply(false);
    }

    private void Apply(bool force)
    {
        if (lobbyPanel == null ||
            contentRoot == null)
        {
            return;
        }

        bool wanted =
            lobbyPanel.activeInHierarchy;

        if (!force &&
            wanted == lastState)
        {
            return;
        }

        lastState = wanted;

        if (contentRoot.activeSelf != wanted)
            contentRoot.SetActive(wanted);
    }
}
