using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentación cinematográfica del roster del lobby:
/// - La info del jugador LOCAL aparece arriba de su cabeza.
/// - Los otros jugadores aparecen en una lista compacta a la derecha.
/// - Lee los textos del LobbyPlayerSlot original, así no toca la lógica de red.
/// </summary>
public class LobbyCinematicRosterPresenter : MonoBehaviour
{
    [Serializable]
    public class RowView
    {
        public GameObject root;
        public Image background;
        public Image icon;
        public TMP_Text name;
        public TMP_Text character;
        public TMP_Text status;
    }

    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private RectTransform panelRect;

    [Header("Slots originales")]
    [SerializeField] private Transform[] slots;

    [Header("Nameplate local")]
    [SerializeField] private RectTransform localPlate;
    [SerializeField] private TMP_Text localName;
    [SerializeField] private TMP_Text localCharacter;
    [SerializeField] private TMP_Text localStatus;
    [SerializeField] private Vector3 nameplateWorldOffset =
        new Vector3(0f, 2.35f, 0f);
    [SerializeField] private Vector2 nameplateScreenOffset =
        new Vector2(0f, 18f);

    [Header("Roster remoto")]
    [SerializeField] private RowView[] rows;

    private Transform localSlot;

    private static readonly Color ConnectedColor =
        new Color(0.12f, 0.94f, 0.50f, 1f);

    private static readonly Color DisconnectedColor =
        new Color(0.95f, 0.28f, 0.25f, 1f);

    private static readonly Color ReadyColor =
        new Color(0.12f, 0.94f, 0.50f, 1f);

    private static readonly Color WaitingColor =
        new Color(0.82f, 0.68f, 0.24f, 1f);

    public void Configure(
        Camera camera,
        Transform anchor,
        RectTransform panel,
        Transform[] sourceSlots,
        RectTransform plate,
        TMP_Text plateName,
        TMP_Text plateCharacter,
        TMP_Text plateStatus,
        RowView[] remoteRows)
    {
        lobbyCamera = camera;
        playerAnchor = anchor;
        panelRect = panel;
        slots = sourceSlots;

        localPlate = plate;
        localName = plateName;
        localCharacter = plateCharacter;
        localStatus = plateStatus;

        rows = remoteRows;
    }

    private void LateUpdate()
    {
        FindLocalSlot();
        UpdateLocalNameplate();
        UpdateRemoteRows();
    }

    private void FindLocalSlot()
    {
        if (slots == null)
        {
            localSlot = null;
            return;
        }

        Transform found = null;

        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];

            if (slot == null)
                continue;

            Transform marker =
                FindDeep(
                    slot,
                    "MarcaLocal"
                );

            if (marker != null &&
                marker.gameObject.activeInHierarchy)
            {
                found = slot;
                break;
            }
        }

        // Mientras todavía no se publicó la presencia, el host suele ser Slot1.
        if (found == null &&
            slots.Length > 0)
        {
            Transform first = slots[0];

            if (first != null)
            {
                TMP_Text connection =
                    FindText(
                        first,
                        "Conexion"
                    );

                if (connection != null &&
                    IsConnected(connection.text))
                {
                    found = first;
                }
            }
        }

        localSlot = found;
    }

    private void UpdateLocalNameplate()
    {
        if (localPlate == null)
            return;

        bool show =
            localSlot != null &&
            lobbyCamera != null &&
            playerAnchor != null &&
            panelRect != null;

        localPlate.gameObject.SetActive(show);

        if (!show)
            return;

        TMP_Text title =
            FindText(
                localSlot,
                "Titulo"
            );

        TMP_Text characterName =
            FindText(
                localSlot,
                "PersonajeNombre"
            );

        TMP_Text characterType =
            FindText(
                localSlot,
                "PersonajeTipo"
            );

        TMP_Text role =
            FindText(
                localSlot,
                "Rol"
            );

        TMP_Text ready =
            FindText(
                localSlot,
                "Estado"
            );

        if (localName != null)
        {
            localName.text =
                title != null &&
                !string.IsNullOrWhiteSpace(title.text)
                    ? title.text
                    : "JUGADOR";
        }

        if (localCharacter != null)
        {
            string a =
                Clean(
                    characterName != null
                        ? characterName.text
                        : ""
                );

            string b =
                Clean(
                    characterType != null
                        ? characterType.text
                        : ""
                );

            if (!string.IsNullOrEmpty(a) &&
                !string.IsNullOrEmpty(b))
            {
                localCharacter.text =
                    a + "  //  " + b;
            }
            else
            {
                localCharacter.text =
                    !string.IsNullOrEmpty(a)
                        ? a
                        : b;
            }
        }

        if (localStatus != null)
        {
            string roleText =
                CleanBrackets(
                    role != null
                        ? role.text
                        : ""
                );

            string readyText =
                Clean(
                    ready != null
                        ? ready.text
                        : ""
                );

            localStatus.text =
                JoinStatus(
                    roleText,
                    readyText
                );

            localStatus.color =
                IsReady(readyText)
                    ? ReadyColor
                    : WaitingColor;
        }

        Vector3 world =
            playerAnchor.position +
            nameplateWorldOffset;

        Vector3 screen =
            lobbyCamera.WorldToScreenPoint(
                world
            );

        if (screen.z <= 0f)
        {
            localPlate.gameObject.SetActive(false);
            return;
        }

        Vector2 localPoint;

        if (RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                panelRect,
                screen,
                null,
                out localPoint))
        {
            localPlate.anchoredPosition =
                localPoint +
                nameplateScreenOffset;
        }
    }

    private void UpdateRemoteRows()
    {
        if (rows == null ||
            rows.Length == 0)
        {
            return;
        }

        int rowIndex = 0;

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot =
                    slots[i];

                if (slot == null ||
                    slot == localSlot)
                {
                    continue;
                }

                if (rowIndex >= rows.Length)
                    break;

                FillRow(
                    rows[rowIndex],
                    slot
                );

                rowIndex++;
            }
        }

        while (rowIndex < rows.Length)
        {
            if (rows[rowIndex] != null &&
                rows[rowIndex].root != null)
            {
                rows[rowIndex].root.SetActive(false);
            }

            rowIndex++;
        }
    }

    private void FillRow(
        RowView row,
        Transform slot)
    {
        if (row == null ||
            row.root == null)
        {
            return;
        }

        row.root.SetActive(true);

        TMP_Text title =
            FindText(
                slot,
                "Titulo"
            );

        TMP_Text characterName =
            FindText(
                slot,
                "PersonajeNombre"
            );

        TMP_Text characterType =
            FindText(
                slot,
                "PersonajeTipo"
            );

        TMP_Text connection =
            FindText(
                slot,
                "Conexion"
            );

        TMP_Text ready =
            FindText(
                slot,
                "Estado"
            );

        TMP_Text role =
            FindText(
                slot,
                "Rol"
            );

        Image sourceIcon =
            FindImage(
                slot,
                "Icono"
            );

        bool connected =
            connection != null &&
            IsConnected(connection.text);

        if (row.name != null)
        {
            row.name.text =
                title != null
                    ? title.text
                    : "JUGADOR";
        }

        if (row.character != null)
        {
            if (!connected)
            {
                row.character.text =
                    "ESPERANDO JUGADOR";
            }
            else
            {
                string a =
                    Clean(
                        characterName != null
                            ? characterName.text
                            : ""
                    );

                string b =
                    Clean(
                        characterType != null
                            ? characterType.text
                            : ""
                    );

                row.character.text =
                    !string.IsNullOrEmpty(a) &&
                    !string.IsNullOrEmpty(b)
                        ? a + "  //  " + b
                        : a + b;
            }
        }

        if (row.status != null)
        {
            if (!connected)
            {
                row.status.text =
                    "DESCONECTADO";
                row.status.color =
                    DisconnectedColor;
            }
            else
            {
                string readyText =
                    Clean(
                        ready != null
                            ? ready.text
                            : ""
                    );

                string roleText =
                    CleanBrackets(
                        role != null
                            ? role.text
                            : ""
                    );

                row.status.text =
                    JoinStatus(
                        roleText,
                        readyText
                    );

                row.status.color =
                    IsReady(readyText)
                        ? ReadyColor
                        : WaitingColor;
            }
        }

        if (row.icon != null)
        {
            if (connected &&
                sourceIcon != null &&
                sourceIcon.sprite != null)
            {
                row.icon.sprite =
                    sourceIcon.sprite;

                row.icon.color =
                    Color.white;

                row.icon.preserveAspect =
                    true;
            }
            else
            {
                row.icon.sprite = null;
                row.icon.color =
                    new Color(
                        0.23f,
                        0.25f,
                        0.26f,
                        1f
                    );
            }
        }
    }

    private static bool IsConnected(
        string value)
    {
        string clean =
            Clean(value)
                .ToUpperInvariant();

        return clean.Contains(
            "CONECTADO"
        ) &&
        !clean.Contains(
            "DESCONECTADO"
        );
    }

    private static bool IsReady(
        string value)
    {
        string clean =
            Clean(value)
                .ToUpperInvariant();

        if (clean.Contains(
            "NO LISTO"))
        {
            return false;
        }

        return clean.Contains(
            "LISTO"
        );
    }

    private static string JoinStatus(
        string a,
        string b)
    {
        if (string.IsNullOrEmpty(a))
            return b;

        if (string.IsNullOrEmpty(b))
            return a;

        return a + "  //  " + b;
    }

    private static string Clean(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
            value))
        {
            return "";
        }

        return value
            .Replace("\n", " ")
            .Trim();
    }

    private static string CleanBrackets(
        string value)
    {
        return Clean(value)
            .Trim('[', ']', ' ');
    }

    private static TMP_Text FindText(
        Transform root,
        string name)
    {
        Transform t =
            FindDeep(
                root,
                name
            );

        return t != null
            ? t.GetComponent<TMP_Text>()
            : null;
    }

    private static Image FindImage(
        Transform root,
        string name)
    {
        Transform t =
            FindDeep(
                root,
                name
            );

        return t != null
            ? t.GetComponent<Image>()
            : null;
    }

    private static Transform FindDeep(
        Transform root,
        string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found =
                FindDeep(
                    child,
                    name
                );

            if (found != null)
                return found;
        }

        return null;
    }
}
