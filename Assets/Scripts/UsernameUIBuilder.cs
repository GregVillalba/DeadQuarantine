#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builder del popup obligatorio de nombre de usuario.
/// Configura UsernameFirstRunUI y lo conecta a CharacterRenameManager.
/// </summary>
public class UsernameUIBuilder : EditorWindow
{
    private const string RootName = "UsernameFirstRunUI";

    private const string TitleFontGuid = "0d82eaf50c142fa4ba34beb1fc9a12e4";
    private const string MonoFontGuid = "102734b590693d240886df79eaaa686c";

    private static readonly Color Green = new Color(0f, 1f, 0.4f, 1f);
    private static readonly Color RoleColor = new Color(0.384f, 0.464f, 0.394f, 1f);
    private static readonly Color LobbyTitleColor = new Color(0.70088285f, 0.78867924f, 0.713606f, 1f);
    private static readonly Color BtnBgColor = new Color(0.060925562f, 0.16226411f, 0.085070916f, 1f);
    private static readonly Color BtnTextColor = new Color(0.19607845f, 1f, 0.2784314f, 1f);
    private static readonly Color BtnOutlineColor = new Color(0f, 1f, 0.14210796f, 0.5f);
    private static readonly Color WindowColor = new Color(0.04f, 0.07f, 0.11f, 0.98f);
    private static readonly Color InputBgColor = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color InputOutlineColor = new Color(0f, 1f, 0.4f, 0.22f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.82f);

    private TMP_FontAsset titleFont;
    private TMP_FontAsset monoFont;

    [SerializeField] private RectTransform panelExistente;

    [MenuItem("Game/Construir UI/Username - Primera ejecucion")]
    private static void Open()
    {
        GetWindow<UsernameUIBuilder>("Username UI");
    }

    private void OnEnable()
    {
        titleFont = LoadFont(TitleFontGuid);
        monoFont = LoadFont(MonoFontGuid);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Construye el popup obligatorio de primera ejecución y lo enlaza directamente con CharacterRenameManager.",
            MessageType.Info
        );

        panelExistente = (RectTransform)EditorGUILayout.ObjectField(
            new GUIContent("Panel / Canvas existente (opcional)"),
            panelExistente,
            typeof(RectTransform),
            true
        );

        EditorGUILayout.Space(8f);

        if (GUILayout.Button("Construir popup de Username", GUILayout.Height(34f)))
        {
            Build();
        }
    }

    private void Build()
    {
        titleFont = LoadFont(TitleFontGuid);
        monoFont = LoadFont(MonoFontGuid);

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("No se pudo crear la UI", "No se encontró ni pudo crear un Canvas.", "OK");
            return;
        }

        EnsureEventSystem();

        Transform old = FindDirectOrDeep(canvas.transform, RootName);
        if (old != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Username UI",
                "Ya existe \"" + RootName + "\".\n\n¿Querés reconstruirla?",
                "Reconstruir",
                "Cancelar"
            );

            if (!replace) return;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        // --------------------------------------------------------
        // OVERLAY GENERAL
        // --------------------------------------------------------
        GameObject root = NewUIObject(RootName, canvas.transform);
        Undo.RegisterCreatedObjectUndo(root, "Construir Username UI");
        Stretch((RectTransform)root.transform, 0f);
        root.transform.SetAsLastSibling();

        Image dim = root.AddComponent<Image>();
        dim.color = DimColor;
        dim.raycastTarget = true;

        Canvas popupCanvas = root.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 5000;

        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<CanvasGroup>();

        // --------------------------------------------------------
        // VENTANA CENTRAL
        // --------------------------------------------------------
        Image window = NewImage("Ventana", root.transform, WindowColor, true);
        Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 540f));

        Image topLine = NewImage("LineaSuperior", window.transform, Green, false);
        topLine.rectTransform.anchorMin = new Vector2(0f, 1f);
        topLine.rectTransform.anchorMax = new Vector2(1f, 1f);
        topLine.rectTransform.pivot = new Vector2(0.5f, 1f);
        topLine.rectTransform.anchoredPosition = Vector2.zero;
        topLine.rectTransform.sizeDelta = new Vector2(0f, 4f);

        // --------------------------------------------------------
        // TÍTULO Y ESTADO
        // --------------------------------------------------------
        TMP_Text title = NewExactText("Titulo", window.transform, "IDENTIDAD DE SUPERVIVIENTE", titleFont, 48f, FontStyles.Bold, LobbyTitleColor, TextAlignmentOptions.Center, 24f);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(820f, 62f));

        TMP_Text state = NewText("Estado", window.transform, "PERFIL LOCAL // PRIMER INICIO", 18f, Green, TextAlignmentOptions.Center, monoFont, 12f);
        Place(state.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(820f, 28f));

        TMP_Text description = NewText("Descripcion", window.transform, "ELEGÍ EL NOMBRE QUE VA A IDENTIFICARTE EN MULTIJUGADOR, RANKING Y LOGROS.", 19f, RoleColor, TextAlignmentOptions.Center, monoFont, 12f);
        Place(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(790f, 32f));

        // --------------------------------------------------------
        // INPUT
        // --------------------------------------------------------
        GameObject inputSection = NewUIObject("Seccion_Username", window.transform);
        Place((RectTransform)inputSection.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(720f, 110f));

        TMP_Text inputLabel = NewText("Etiqueta", inputSection.transform, "NOMBRE DE USUARIO:", 20f, Green, TextAlignmentOptions.Left, monoFont, 13f);
        Place(inputLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(720f, 28f));

        TMP_InputField input = CreateInputField(inputSection.transform);
        Place(input.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -36f), new Vector2(720f, 65f));

        TMP_Text validation = NewText("ValidationText", window.transform, "1-10 CARACTERES // SOLO LETRAS Y NÚMEROS", 16f, RoleColor, TextAlignmentOptions.Center, monoFont, 11f);
        Place(validation.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(760f, 26f));

        TMP_Text feedbackServidor = NewText("TxtFeedbackServidor", window.transform, "", 16f, Green, TextAlignmentOptions.Center, monoFont, 11f);
        Place(feedbackServidor.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(760f, 24f));

        // --------------------------------------------------------
        // BOTONES
        // --------------------------------------------------------
        Button confirm = MakeButton(window.transform, "Btn_Confirmar", "CONFIRMAR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 85f), new Vector2(280f, 58f));
        Button cancel = MakeButton(window.transform, "Btn_Cancelar", "CANCELAR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 85f), new Vector2(280f, 58f));
        Button idClave = MakeButton(window.transform, "Btn_ID", "ID CLAVE", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(320f, 50f));

        // Botones de respuesta de guardado
        Button salir = MakeButton(window.transform, "Btn_SALIR", "SALIR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 85f), new Vector2(280f, 58f));
        salir.gameObject.SetActive(false);

        Button reintentar = MakeButton(window.transform, "Btn_REINTENTAR", "REINTENTAR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 85f), new Vector2(280f, 58f));
        reintentar.gameObject.SetActive(false);

        // --------------------------------------------------------
        // VINCULACIÓN A CHARACTERRENAMEMANAGER
        // --------------------------------------------------------
        CharacterRenameManager manager = FindObjectOfType<CharacterRenameManager>();
        if (manager != null)
        {
            SerializedObject so = new SerializedObject(manager);
            SetObj(so, "rootFirstRunUI", root);
            SetObj(so, "ventanaNombre", window.gameObject);
            SetObj(so, "inputNombre", input);
            SetObj(so, "txtValidationNombre", validation);
            SetObj(so, "txtFeedbackServidorNombre", feedbackServidor);
            SetObj(so, "txtIDLabel", inputLabel);
            SetObj(so, "btnConfirmarNombre", confirm);
            SetObj(so, "btnCancelarNombre", cancel);
            SetObj(so, "btnSalirNombre", salir);
            SetObj(so, "btnReintentarNombre", reintentar);
            SetObj(so, "btnIrAVentanaClave", idClave);

            so.ApplyModifiedProperties();
            Debug.Log("[UsernameUIBuilder] Conectado exitosamente con CharacterRenameManager en la escena.");
        }
        else
        {
            Debug.LogWarning("[UsernameUIBuilder] No se encontró CharacterRenameManager en la escena. Asegúrate de tenerlo en un GameObject.");
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
    }

    private TMP_InputField CreateInputField(Transform parent)
    {
        Image bg = NewImage("UsernameInput", parent, InputBgColor, true);
        bg.sprite = GetButtonSprite();
        bg.type = Image.Type.Sliced;

        Outline outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = InputOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        TMP_InputField input = bg.gameObject.AddComponent<TMP_InputField>();
        input.characterLimit = 10;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Alphanumeric;
        input.customCaretColor = true;
        input.caretColor = Green;
        input.selectionColor = new Color(0f, 1f, 0.4f, 0.20f);

        GameObject viewportGO = NewUIObject("Text Area", bg.transform);
        RectTransform viewport = (RectTransform)viewportGO.transform;
        Stretch(viewport, 0f);
        viewport.offsetMin = new Vector2(22f, 8f);
        viewport.offsetMax = new Vector2(-22f, -8f);
        viewportGO.AddComponent<RectMask2D>();

        TMP_Text placeholder = NewExactText("Placeholder", viewport, "INGRESÁ TU NOMBRE...", monoFont, 24f, FontStyles.Normal, new Color(0.38f, 0.45f, 0.41f, 1f), TextAlignmentOptions.MidlineLeft, 14f);
        Stretch(placeholder.rectTransform, 0f);

        TMP_Text text = NewExactText("Text", viewport, "", monoFont, 26f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft, 14f);
        Stretch(text.rectTransform, 0f);

        input.textViewport = viewport;
        input.textComponent = text as TextMeshProUGUI;
        input.placeholder = placeholder;

        return input;
    }

    private Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        Image bg = NewImage(name, parent, BtnBgColor, true);
        bg.sprite = GetButtonSprite();
        bg.type = Image.Type.Sliced;
        Place(bg.rectTransform, anchor, pivot, pos, size);

        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        cb.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        button.colors = cb;

        Outline outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = BtnOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        TMP_Text label = NewExactText("Text (TMP)", bg.transform, text, titleFont, 28f, FontStyles.Bold, BtnTextColor, TextAlignmentOptions.Center, 14f);
        Stretch(label.rectTransform, 0f);

        return button;
    }

    private Canvas ResolveCanvas()
    {
        if (panelExistente != null)
        {
            Canvas existingCanvas = panelExistente.GetComponentInParent<Canvas>();
            if (existingCanvas != null) return existingCanvas;
        }

        Canvas found = FindObjectOfType<Canvas>();
        if (found != null) return found.rootCanvas;

        GameObject canvasGO = new GameObject("UsernameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Crear Canvas Username");
        canvasGO.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(es, "Crear EventSystem");
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private static TMP_FontAsset LoadFont(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static Sprite GetButtonSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static void SetObj(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
        else
        {
            Debug.LogWarning("[UsernameUIBuilder] No se encontró el campo: " + field);
        }
    }

    private static GameObject NewUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image NewImage(string name, Transform parent, Color color, bool raycast)
    {
        GameObject go = NewUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TMP_Text NewExactText(string name, Transform parent, string text, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions alignment, float autoMin)
    {
        GameObject go = NewUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.margin = Vector4.zero;
#pragma warning disable 618
        tmp.enableWordWrapping = false;
#pragma warning restore 618
        if (autoMin > 0f)
        {
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = autoMin;
            tmp.fontSizeMax = size;
        }
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment, TMP_FontAsset font, float autoMin)
    {
        GameObject go = NewUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
#pragma warning disable 618
        tmp.enableWordWrapping = false;
#pragma warning restore 618
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = autoMin;
        tmp.fontSizeMax = size;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static Transform FindDirectOrDeep(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDirectOrDeep(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}
#endif