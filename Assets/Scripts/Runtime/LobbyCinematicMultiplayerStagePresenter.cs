using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presenta 4 jugadores del lobby directamente en el escenario,
/// cada uno con su nombre + icono encima de la cabeza.
/// </summary>
public class LobbyCinematicMultiplayerStagePresenter : MonoBehaviour
{
    [Serializable]
    public struct NameplateView
    {
        public RectTransform root;
        public Image background;
        public Image icon;
        public TMP_Text name;
        public TMP_Text line1;
        public TMP_Text line2;
    }

    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private LobbyCinematicPlayerSlotVisual[] actors;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Transform[] slots;
    [SerializeField] private NameplateView[] plates;

    [Header("Altura del cartel")]
    [SerializeField] private Vector3 nameplateWorldOffset =
        new Vector3(0f, 2.15f, 0f);

    [SerializeField] private Vector2 nameplateScreenOffset =
        new Vector2(0f, 22f);

    [Header("Indices de variantes")]
    [SerializeField] private int exploradorHombreIndex = 0;
    [SerializeField] private int exploradorMujerIndex = 1;
    [SerializeField] private int soldadoHombreIndex = 2;
    [SerializeField] private int soldadoMujerIndex = 3;
    [SerializeField] private int tanqueIndex = 4;

    public void ConfigureActors(
        Camera camera,
        LobbyCinematicPlayerSlotVisual[] sourceActors)
    {
        lobbyCamera = camera;
        actors = sourceActors;
    }

    public void ConfigureUI(
        RectTransform panel,
        Transform[] sourceSlots,
        NameplateView[] sourcePlates)
    {
        panelRect = panel;
        slots = sourceSlots;
        plates = sourcePlates;
    }

    private void LateUpdate()
    {
        if (actors == null ||
            plates == null ||
            slots == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                Mathf.Min(actors.Length, plates.Length),
                slots.Length
            );

        for (int i = 0; i < count; i++)
        {
            UpdateSlot(i);
        }
    }

    private void UpdateSlot(
        int index)
    {
        LobbyCinematicPlayerSlotVisual actor =
            actors[index];

        Transform slot =
            slots[index];

        NameplateView plate =
            plates[index];

        if (actor == null ||
            slot == null ||
            plate.root == null)
        {
            return;
        }

        TMP_Text connection =
            FindText(slot, "Conexion");

        bool connected =
            connection != null &&
            IsConnected(connection.text);

        actor.SetVisible(connected);
        plate.root.gameObject.SetActive(connected);

        if (!connected)
            return;

        int characterIndex =
            GuessCharacterIndex(slot);

        actor.SetCharacterIndex(characterIndex);

        TMP_Text title =
            FindText(slot, "Titulo");

        TMP_Text charName =
            FindText(slot, "PersonajeNombre");

        TMP_Text charType =
            FindText(slot, "PersonajeTipo");

        TMP_Text role =
            FindText(slot, "Rol");

        TMP_Text ready =
            FindText(slot, "Estado");

        Image sourceIcon =
            FindImage(slot, "Icono");

        if (plate.name != null)
        {
            plate.name.text =
                title != null &&
                !string.IsNullOrWhiteSpace(title.text)
                    ? title.text.Trim()
                    : "JUGADOR";
        }

        if (plate.line1 != null)
        {
            string a =
                Clean(
                    charName != null
                        ? charName.text
                        : ""
                );

            string b =
                Clean(
                    charType != null
                        ? charType.text
                        : ""
                );

            if (!string.IsNullOrEmpty(a) &&
                !string.IsNullOrEmpty(b))
            {
                plate.line1.text =
                    a + "  //  " + b;
            }
            else
            {
                plate.line1.text =
                    !string.IsNullOrEmpty(a)
                        ? a
                        : b;
            }
        }

        if (plate.line2 != null)
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

            plate.line2.text =
                JoinNonEmpty(
                    roleText,
                    readyText
                );
        }

        if (plate.icon != null)
        {
            if (sourceIcon != null &&
                sourceIcon.sprite != null)
            {
                plate.icon.sprite =
                    sourceIcon.sprite;
                plate.icon.color =
                    Color.white;
                plate.icon.preserveAspect = true;
            }
            else
            {
                plate.icon.sprite = null;
                plate.icon.color =
                    new Color(
                        0.25f,
                        0.25f,
                        0.25f,
                        1f
                    );
            }
        }

        UpdatePlatePosition(
            actor,
            plate
        );
    }

    private void UpdatePlatePosition(
        LobbyCinematicPlayerSlotVisual actor,
        NameplateView plate)
    {
        if (lobbyCamera == null ||
            panelRect == null ||
            actor.Anchor == null)
        {
            return;
        }

        Vector3 world =
            actor.Anchor.position +
            nameplateWorldOffset;

        Vector3 screen =
            lobbyCamera.WorldToScreenPoint(world);

        if (screen.z <= 0f)
        {
            plate.root.gameObject.SetActive(false);
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
            plate.root.anchoredPosition =
                localPoint +
                nameplateScreenOffset;
        }
    }

    private int GuessCharacterIndex(
        Transform slot)
    {
        TMP_Text charType =
            FindText(slot, "PersonajeTipo");

        TMP_Text charName =
            FindText(slot, "PersonajeNombre");

        string combined =
            (
                Clean(
                    charType != null
                        ? charType.text
                        : ""
                ) + " " +
                Clean(
                    charName != null
                        ? charName.text
                        : ""
                )
            ).ToUpperInvariant();

        if (combined.Contains("TANQUE"))
            return tanqueIndex;

        bool mujer =
            combined.Contains("MUJER");

        bool soldado =
            combined.Contains("SOLDADO");

        bool explorador =
            combined.Contains("EXPLORADOR");

        if (soldado)
            return mujer
                ? soldadoMujerIndex
                : soldadoHombreIndex;

        if (explorador)
            return mujer
                ? exploradorMujerIndex
                : exploradorHombreIndex;

        return soldadoHombreIndex;
    }

    private static bool IsConnected(
        string value)
    {
        string clean =
            Clean(value).ToUpperInvariant();

        return clean.Contains("CONECTADO") &&
            !clean.Contains("DESCONECTADO");
    }

    private static string JoinNonEmpty(
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
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Replace("\n", " ").Trim();
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
            FindDeep(root, name);

        return t != null
            ? t.GetComponent<TMP_Text>()
            : null;
    }

    private static Image FindImage(
        Transform root,
        string name)
    {
        Transform t =
            FindDeep(root, name);

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
                FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }
}
