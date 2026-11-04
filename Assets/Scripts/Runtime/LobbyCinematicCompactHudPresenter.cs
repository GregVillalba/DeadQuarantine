using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD compacto del lobby multiplayer:
/// - nombre arriba del personaje
/// - tarjeta compacta arriba derecha
/// - código de sala visible y funcional
/// - tarjeta de selección actual (modo/dificultad/mapa)
/// </summary>
public class LobbyCinematicCompactHudPresenter : MonoBehaviour
{
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private Transform[] slots;

    [Header("Nombre arriba del personaje")]
    [SerializeField] private RectTransform headPlate;
    [SerializeField] private TMP_Text headName;
    [SerializeField] private Vector3 headWorldOffset =
        new Vector3(0f, 2.25f, 0f);

    [SerializeField] private Vector2 headScreenOffset =
        new Vector2(0f, 16f);

    [Header("Tarjeta compacta derecha")]
    [SerializeField] private RectTransform rightCard;
    [SerializeField] private TMP_Text playersHeader;
    [SerializeField] private Image playerIcon;
    [SerializeField] private TMP_Text playerName;
    [SerializeField] private TMP_Text playerSubtitle;
    [SerializeField] private TMP_Text playerStatus;

    [Header("Código sala")]
    [SerializeField] private TMP_Text codeValue;
    [SerializeField] private Button copyButton;

    [Header("Seleccion actual")]
    [SerializeField] private TMP_Text selectionLine1;
    [SerializeField] private TMP_Text selectionLine2;

    private Transform localSlot;
    private Button sourceCopyButton;
    private TMP_Text sourceCodeValue;

    public void Configure(
        Camera camera,
        RectTransform panel,
        Transform anchor,
        Transform[] sourceSlots,
        RectTransform headPlateRect,
        TMP_Text headNameText,
        RectTransform compactCardRect,
        TMP_Text headerText,
        Image iconImage,
        TMP_Text nameText,
        TMP_Text subtitleText,
        TMP_Text statusText,
        TMP_Text codeValueText,
        Button copyButtonRef,
        TMP_Text line1,
        TMP_Text line2)
    {
        lobbyCamera = camera;
        panelRect = panel;
        playerAnchor = anchor;
        slots = sourceSlots;

        headPlate = headPlateRect;
        headName = headNameText;

        rightCard = compactCardRect;
        playersHeader = headerText;
        playerIcon = iconImage;
        playerName = nameText;
        playerSubtitle = subtitleText;
        playerStatus = statusText;

        codeValue = codeValueText;
        copyButton = copyButtonRef;

        selectionLine1 = line1;
        selectionLine2 = line2;
    }

    private void Awake()
    {
        FindOriginalCodeRefs();

        if (copyButton != null)
        {
            copyButton.onClick.RemoveAllListeners();
            copyButton.onClick.AddListener(
                ForwardCopyClick
            );
        }
    }

    private void LateUpdate()
    {
        FindLocalSlot();
        UpdateHeadPlate();
        UpdateRightCard();
        UpdateCode();
        UpdateSelectionCard();
    }

    private void FindOriginalCodeRefs()
    {
        Transform originalCopy =
            FindSceneObject("copiar_codigo");

        if (originalCopy != null)
        {
            sourceCopyButton =
                originalCopy.GetComponent<Button>();
        }

        Transform originalPlaceholder =
            FindSceneObject("placeholder_codigo");

        if (originalPlaceholder != null)
        {
            TMP_Text[] texts =
                originalPlaceholder
                    .GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                    continue;

                string value =
                    Clean(texts[i].text);

                if (!string.IsNullOrEmpty(value))
                {
                    sourceCodeValue = texts[i];
                    break;
                }
            }
        }
    }

    private void ForwardCopyClick()
    {
        if (sourceCopyButton != null)
        {
            sourceCopyButton.onClick.Invoke();
        }
    }

    private void FindLocalSlot()
    {
        localSlot = null;

        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];

            if (slot == null)
                continue;

            Transform marker =
                FindDeep(slot, "MarcaLocal");

            if (marker != null &&
                marker.gameObject.activeInHierarchy)
            {
                localSlot = slot;
                return;
            }
        }

        // Fallback: primer slot conectado.
        for (int i = 0; i < slots.Length; i++)
        {
            Transform slot = slots[i];

            if (slot == null)
                continue;

            TMP_Text connection =
                FindText(slot, "Conexion");

            if (connection != null &&
                IsConnected(connection.text))
            {
                localSlot = slot;
                return;
            }
        }
    }

    private void UpdateHeadPlate()
    {
        if (headPlate == null)
            return;

        bool show =
            localSlot != null &&
            playerAnchor != null &&
            lobbyCamera != null &&
            panelRect != null;

        headPlate.gameObject.SetActive(show);

        if (!show)
            return;

        TMP_Text title =
            FindText(localSlot, "Titulo");

        if (headName != null)
        {
            headName.text =
                !string.IsNullOrWhiteSpace(
                    title != null ? title.text : null
                )
                    ? Clean(title.text)
                    : "JUGADOR";
        }

        Vector3 world =
            playerAnchor.position +
            headWorldOffset;

        Vector3 screen =
            lobbyCamera.WorldToScreenPoint(world);

        if (screen.z <= 0f)
        {
            headPlate.gameObject.SetActive(false);
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
            headPlate.anchoredPosition =
                localPoint +
                headScreenOffset;
        }
    }

    private void UpdateRightCard()
    {
        if (rightCard == null)
            return;

        rightCard.gameObject.SetActive(
            localSlot != null
        );

        if (localSlot == null)
            return;

        int connectedCount =
            CountConnectedSlots();

        if (playersHeader != null)
        {
            playersHeader.text =
                "JUGADORES  " +
                connectedCount +
                "/4";
        }

        if (playerName != null)
        {
            TMP_Text title =
                FindText(localSlot, "Titulo");

            playerName.text =
                !string.IsNullOrWhiteSpace(
                    title != null ? title.text : null
                )
                    ? Clean(title.text)
                    : "JUGADOR";
        }

        if (playerSubtitle != null)
        {
            TMP_Text charName =
                FindText(
                    localSlot,
                    "PersonajeNombre"
                );

            playerSubtitle.text =
                !string.IsNullOrWhiteSpace(
                    charName != null
                        ? charName.text
                        : null
                )
                    ? Clean(charName.text)
                    : "PERSONAJE";
        }

        if (playerStatus != null)
        {
            TMP_Text ready =
                FindText(
                    localSlot,
                    "Estado"
                );

            playerStatus.text =
                !string.IsNullOrWhiteSpace(
                    ready != null ? ready.text : null
                )
                    ? Clean(ready.text)
                    : "NO LISTO";
        }

        if (playerIcon != null)
        {
            Image icon =
                FindImage(
                    localSlot,
                    "Icono"
                );

            if (icon != null &&
                icon.sprite != null)
            {
                playerIcon.sprite =
                    icon.sprite;
                playerIcon.color =
                    Color.white;
                playerIcon.preserveAspect = true;
            }
        }
    }

    private void UpdateCode()
    {
        if (codeValue == null)
            return;

        if (sourceCodeValue == null)
        {
            FindOriginalCodeRefs();
        }

        string value =
            sourceCodeValue != null
                ? Clean(sourceCodeValue.text)
                : "";

        if (string.IsNullOrWhiteSpace(value))
            value = "------";

        codeValue.text = value;
    }

    private void UpdateSelectionCard()
    {
        string modo =
            ReadSectionSelectedValue(
                "Seccion_Modo",
                "MODO"
            );

        string dificultad =
            ReadSectionSelectedValue(
                "Seccion_Dificultad",
                "DIFICULTAD"
            );

        string mapa =
            ReadSectionSelectedValue(
                "Seccion_Mapa",
                "MAPA"
            );

        if (selectionLine1 != null)
        {
            string top = "";

            if (!string.IsNullOrEmpty(modo))
                top = modo;

            if (!string.IsNullOrEmpty(dificultad))
            {
                top = string.IsNullOrEmpty(top)
                    ? dificultad
                    : top + " - " + dificultad;
            }

            selectionLine1.text =
                !string.IsNullOrEmpty(top)
                    ? top
                    : "MODO - DIFICULTAD";
        }

        if (selectionLine2 != null)
        {
            selectionLine2.text =
                !string.IsNullOrEmpty(mapa)
                    ? mapa
                    : "MAPA";
        }
    }

    private int CountConnectedSlots()
    {
        if (slots == null)
            return 0;

        int count = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            TMP_Text connection =
                FindText(
                    slots[i],
                    "Conexion"
                );

            if (connection != null &&
                IsConnected(connection.text))
            {
                count++;
            }
        }

        return count;
    }

    private string ReadSectionSelectedValue(
        string sectionName,
        string labelToSkip)
    {
        Transform section =
            FindSceneObject(
                sectionName
            );

        if (section == null)
            return "";

        TMP_Text[] texts =
            section.GetComponentsInChildren<TMP_Text>(true);

        string labelUpper =
            labelToSkip.ToUpperInvariant();

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string value =
                Clean(text.text);

            if (string.IsNullOrEmpty(value))
                continue;

            if (value.ToUpperInvariant() == labelUpper)
                continue;

            return value;
        }

        return "";
    }

    private static bool IsConnected(
        string value)
    {
        string clean =
            Clean(value)
                .ToUpperInvariant();

        return clean.Contains("CONECTADO") &&
            !clean.Contains("DESCONECTADO");
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

    private static Transform FindSceneObject(
        string name)
    {
        Transform[] all =
            UnityEngine.Object
                .FindObjectsOfType<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null &&
                all[i].name == name)
            {
                return all[i];
            }
        }

        return null;
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

    private static string Clean(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();
    }
}
