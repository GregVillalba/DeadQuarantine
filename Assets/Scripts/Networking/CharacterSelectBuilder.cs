#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta de editor: construye TODA la pantalla de selección de personaje (estilo shooter) en la escena abierta
/// y conecta las referencias de CharacterSelectMenu, CharacterSelectScreen, CharacterPreviewDrag y el Target del stage.
/// Va en una carpeta llamada "Editor" (ej: Assets/Editor/). Se usa desde el menú: Game > Construir UI de Selección de Personaje.
/// Se puede ejecutar de nuevo para reconstruirla (borra y rehace "CharacterSelectCanvas").
/// </summary>
public class CharacterSelectUIBuilder : EditorWindow
{
    private CharacterCatalog catalog;
    private TMP_FontAsset font;
    private Color accent = new Color(0.25f, 0.9f, 0.45f, 1f);

    // Paleta
    private static readonly Color Dark = new Color(0.03f, 0.04f, 0.05f, 0.94f);
    private static readonly Color Panel = new Color(0.05f, 0.07f, 0.08f, 0.85f);
    private static readonly Color Grey = new Color(0.78f, 0.82f, 0.82f, 1f);
    private static readonly Color BarBg = new Color(1f, 1f, 1f, 0.12f);

    private TMP_FontAsset F;
    private Color A;

    [MenuItem("Game/Construir UI de Selección de Personaje")]
    private static void Open()
    {
        GetWindow<CharacterSelectUIBuilder>("Selección de Personaje");
    }

    private void OnEnable()
    {
        if (catalog == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterCatalog");

            if (guids.Length > 0)
                catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Construye la pantalla de selección en la escena abierta. Necesita que la escena ya tenga el CharacterStage " +
            "(CharacterPreviewStage). Si ya habías armado un Canvas a mano, borralo después.", MessageType.Info);

        catalog = (CharacterCatalog)EditorGUILayout.ObjectField("Catálogo", catalog, typeof(CharacterCatalog), false);
        font = (TMP_FontAsset)EditorGUILayout.ObjectField("Fuente (TMP, opcional)", font, typeof(TMP_FontAsset), false);
        accent = EditorGUILayout.ColorField("Color de acento", accent);

        EditorGUILayout.Space();

        if (GUILayout.Button("Construir UI", GUILayout.Height(32)))
            Build();
    }

    // =========================================================
    // CONSTRUCCIÓN
    // =========================================================

    private void Build()
    {
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Falta el catálogo", "Asigná el Character Catalog.", "OK");
            return;
        }

        CharacterPreviewStage stage = FindObjectOfType<CharacterPreviewStage>();

        if (stage == null)
        {
            EditorUtility.DisplayDialog("Falta el escenario 3D",
                "No hay un CharacterPreviewStage en la escena. Armá primero el CharacterStage (paso 4 de la guía).", "OK");
            return;
        }

        F = font;
        A = accent;

        GameObject old = GameObject.Find("CharacterSelectCanvas");

        if (old != null)
            Undo.DestroyObjectImmediate(old);

        // ---- Canvas ----
        GameObject root = new GameObject("CharacterSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Construir UI de selección");
        root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform rt = root.transform;

        // ---- Fondo ----
        Image fondo = NewImage("Fondo", rt, Dark, true);
        Stretch(fondo.rectTransform, 0f);

        // ---- Personaje 3D (RawImage con el render del stage) ----
        RawImage preview = NewRawImage("Preview", rt);
        Place(preview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 1000f));
        CharacterPreviewDrag drag = preview.gameObject.AddComponent<CharacterPreviewDrag>();

        // ---- Encabezado ----
        Image barraTitulo = NewImage("BarraTitulo", rt, A, false);
        Place(barraTitulo.rectTransform, TL, TL, new Vector2(80f, -112f), new Vector2(8f, 78f));

        TMP_Text titulo = NewText("Titulo", rt, "SELECCIÓN DE PERSONAJE", 56f, Color.white, TextAlignmentOptions.Left, true, 30f);
        Place(titulo.rectTransform, TL, TL, new Vector2(104f, -104f), new Vector2(1300f, 70f));

        TMP_Text subtitulo = NewText("Subtitulo", rt, "ELEGÍ TU CLASE Y TU VARIANTE", 22f, A, TextAlignmentOptions.Left, false, 14f);
        Place(subtitulo.rectTransform, TL, TL, new Vector2(106f, -168f), new Vector2(900f, 30f));

        Image atrasBg;
        Button btnAtras = NewButton("Btn_Atras", rt, Panel, out atrasBg);
        Place(atrasBg.rectTransform, TL, TL, new Vector2(80f, -36f), new Vector2(170f, 50f));
        TMP_Text atrasTxt = NewText("Texto", btnAtras.transform, "< ATRAS", 24f, Grey, TextAlignmentOptions.Center, true);
        Stretch(atrasTxt.rectTransform, 0f);

        // ---- Panel de información (derecha) ----
        Image panelInfo = NewImage("PanelInfo", rt, Panel, false);
        Place(panelInfo.rectTransform, TR, TR, new Vector2(-80f, -130f), new Vector2(580f, 700f));

        Image lineaPanel = NewImage("LineaSuperior", panelInfo.transform, A, false);
        Place(lineaPanel.rectTransform, TL, TL, Vector2.zero, new Vector2(580f, 4f));

        // Pestañas Hombre / Mujer
        Image maleBg, femaleBg;
        Button btnMale = NewButton("Btn_Hombre", panelInfo.transform, Panel, out maleBg);
        Place(maleBg.rectTransform, TL, TL, new Vector2(20f, -28f), new Vector2(260f, 56f));
        TMP_Text maleTxt = NewText("Texto", btnMale.transform, "HOMBRE", 26f, Grey, TextAlignmentOptions.Center, true);
        Stretch(maleTxt.rectTransform, 0f);

        Button btnFemale = NewButton("Btn_Mujer", panelInfo.transform, Panel, out femaleBg);
        Place(femaleBg.rectTransform, TL, TL, new Vector2(300f, -28f), new Vector2(260f, 56f));
        TMP_Text femaleTxt = NewText("Texto", btnFemale.transform, "MUJER", 26f, Grey, TextAlignmentOptions.Center, true);
        Stretch(femaleTxt.rectTransform, 0f);

        TMP_Text tierText = NewText("TierText", panelInfo.transform, "NORMAL", 26f, A, TextAlignmentOptions.Left, true, 16f);
        Place(tierText.rectTransform, TL, TL, new Vector2(20f, -112f), new Vector2(540f, 36f));

        TMP_Text nameText = NewText("NombreText", panelInfo.transform, "SOLDADO", 66f, Color.white, TextAlignmentOptions.Left, true, 36f);
        Place(nameText.rectTransform, TL, TL, new Vector2(20f, -146f), new Vector2(540f, 84f));

        TMP_Text descText = NewText("DescripcionText", panelInfo.transform, "", 24f, Grey, TextAlignmentOptions.TopLeft, false);
        Place(descText.rectTransform, TL, TL, new Vector2(20f, -240f), new Vector2(540f, 100f));

        string[] statNames = { "VIDA", "VELOCIDAD", "ESTAMINA", "REGEN. ESTAMINA", "RECARGA" };
        Image[] fills = new Image[5];
        TMP_Text[] values = new TMP_Text[5];

        for (int i = 0; i < 5; i++)
        {
            float y = -372f - i * 56f;

            TMP_Text label = NewText("Label_" + statNames[i], panelInfo.transform, statNames[i], 24f, Grey, TextAlignmentOptions.Left, true, 16f);
            Place(label.rectTransform, TL, TL, new Vector2(20f, y), new Vector2(200f, 36f));

            Image bg = NewImage("Barra_" + statNames[i], panelInfo.transform, BarBg, false);
            Place(bg.rectTransform, TL, TL, new Vector2(230f, y - 11f), new Vector2(250f, 14f));

            Image fill = NewImage("Relleno", bg.transform, A, false);
            Stretch(fill.rectTransform, 0f);
            fills[i] = fill;

            TMP_Text val = NewText("Valor_" + statNames[i], panelInfo.transform, "0", 26f, Color.white, TextAlignmentOptions.Right, true, 16f);
            Place(val.rectTransform, TL, TL, new Vector2(490f, y), new Vector2(70f, 36f));
            values[i] = val;
        }

        // ---- Tarjetas de clase (abajo) ----
        CharacterClass[] order = { CharacterClass.Scout, CharacterClass.Soldier, CharacterClass.Tank };
        Button[] cardButtons = new Button[3];
        Image[] cardIcons = new Image[3];
        TMP_Text[] cardTiers = new TMP_Text[3];
        TMP_Text[] cardNames = new TMP_Text[3];
        GameObject[] cardFrames = new GameObject[3];

        for (int i = 0; i < 3; i++)
        {
            Image cardBg;
            Button card = NewButton("Carta_" + order[i], rt, Panel, out cardBg);
            Place(cardBg.rectTransform, BL, BL, new Vector2(80f + i * 390f, 70f), new Vector2(370f, 120f));

            Image retratoFondo = NewImage("RetratoFondo", card.transform, new Color(1f, 1f, 1f, 0.07f), false);
            Place(retratoFondo.rectTransform, ML, ML, new Vector2(8f, 0f), new Vector2(96f, 96f));

            Image icon = NewImage("Icono", retratoFondo.transform, Color.white, false);
            Stretch(icon.rectTransform, 4f);
            icon.preserveAspect = true;
            icon.enabled = false;

            TMP_Text tier = NewText("Tier", card.transform, order[i].ToString(), 20f, Grey, TextAlignmentOptions.Left, true, 12f);
            Place(tier.rectTransform, TL, TL, new Vector2(116f, -18f), new Vector2(244f, 28f));

            TMP_Text cname = NewText("Nombre", card.transform, "", 36f, Color.white, TextAlignmentOptions.Left, true, 16f);
            Place(cname.rectTransform, TL, TL, new Vector2(116f, -46f), new Vector2(244f, 56f));

            GameObject frame = new GameObject("Seleccion", typeof(RectTransform));
            frame.layer = LayerMask.NameToLayer("UI");
            frame.transform.SetParent(card.transform, false);
            Stretch((RectTransform)frame.transform, 0f);

            Image barraIzq = NewImage("BarraIzq", frame.transform, A, false);
            Place(barraIzq.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f), new Vector2(0f, 0.5f));

            Image lineaInf = NewImage("LineaInf", frame.transform, A, false);
            Place(lineaInf.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 4f), new Vector2(0.5f, 0f));

            cardButtons[i] = card;
            cardIcons[i] = icon;
            cardTiers[i] = tier;
            cardNames[i] = cname;
            cardFrames[i] = frame;
        }

        // ---- Botón CONFIRMAR (abajo a la derecha) ----
        Image confirmBg;
        Button btnConfirm = NewButton("Btn_Confirmar", rt, A, out confirmBg);
        Place(confirmBg.rectTransform, BR, BR, new Vector2(-80f, 70f), new Vector2(580f, 120f));
        TMP_Text confirmTxt = NewText("Texto", btnConfirm.transform, "CONFIRMAR", 48f, new Color(0.03f, 0.05f, 0.05f, 1f), TextAlignmentOptions.Center, true);
        Stretch(confirmTxt.rectTransform, 0f);

        // =====================================================
        // CONEXIONES
        // =====================================================

        CharacterSelectMenu menu = root.AddComponent<CharacterSelectMenu>();
        SerializedObject so = new SerializedObject(menu);

        SetObj(so, "catalog", catalog);

        SerializedProperty slots = so.FindProperty("classSlots");
        slots.arraySize = 3;

        for (int i = 0; i < 3; i++)
        {
            SerializedProperty el = slots.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("type").enumValueIndex = (int)order[i];
            el.FindPropertyRelative("button").objectReferenceValue = cardButtons[i];
            el.FindPropertyRelative("icon").objectReferenceValue = cardIcons[i];
            el.FindPropertyRelative("tierLabel").objectReferenceValue = cardTiers[i];
            el.FindPropertyRelative("nameLabel").objectReferenceValue = cardNames[i];
            el.FindPropertyRelative("selectedFrame").objectReferenceValue = cardFrames[i];
        }

        SetObj(so, "maleButton", btnMale);
        SetObj(so, "maleLabel", maleTxt);
        SetObj(so, "femaleButton", btnFemale);
        SetObj(so, "femaleLabel", femaleTxt);
        SetObj(so, "tierText", tierText);
        SetObj(so, "nameText", nameText);
        SetObj(so, "descriptionText", descText);

        string[] statFields = { "health", "moveSpeed", "stamina", "staminaRegen", "reloadTime" };

        for (int i = 0; i < 5; i++)
        {
            SerializedProperty row = so.FindProperty(statFields[i]);

            if (row == null)
                continue;

            row.FindPropertyRelative("fill").objectReferenceValue = fills[i];
            row.FindPropertyRelative("value").objectReferenceValue = values[i];
        }

        so.FindProperty("accentColor").colorValue = A;
        so.ApplyModifiedProperties();

        CharacterSelectScreen screen = root.AddComponent<CharacterSelectScreen>();
        SerializedObject screenSo = new SerializedObject(screen);
        SetObj(screenSo, "stage", stage);
        screenSo.ApplyModifiedProperties();

        UnityEventTools.AddPersistentListener(btnConfirm.onClick, screen.Confirm);
        UnityEventTools.AddPersistentListener(btnAtras.onClick, screen.Back);

        SerializedObject dragSo = new SerializedObject(drag);
        SetObj(dragSo, "stage", stage);
        dragSo.FindProperty("slotIndex").intValue = 0;
        dragSo.ApplyModifiedProperties();

        SerializedObject stageSo = new SerializedObject(stage);
        SetObj(stageSo, "target", preview);
        stageSo.FindProperty("textureSize").vector2IntValue = new Vector2Int(900, 1000);
        stageSo.ApplyModifiedProperties();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log("[CharacterSelectUIBuilder] UI construida. Si tenías un Canvas armado a mano, borralo. Asigná los iconos en el catálogo (Classes > Icon).");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static readonly Vector2 TL = new Vector2(0f, 1f);
    private static readonly Vector2 TR = new Vector2(1f, 1f);
    private static readonly Vector2 BL = new Vector2(0f, 0f);
    private static readonly Vector2 BR = new Vector2(1f, 0f);
    private static readonly Vector2 ML = new Vector2(0f, 0.5f);

    private static void SetObj(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);

        if (p == null)
        {
            Debug.LogWarning("[CharacterSelectUIBuilder] No encuentro el campo '" + field + "' en " + so.targetObject.GetType().Name);
            return;
        }

        p.objectReferenceValue = value;
    }

    // Ancla en un punto; el pivote es el mismo punto (esquina o centro).
    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // Para barras que se estiran en un eje: ancla con rango + pivote explícito.
    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
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
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static RawImage NewRawImage(string name, Transform parent)
    {
        GameObject go = NewUIObject(name, parent);
        RawImage img = go.AddComponent<RawImage>();
        img.color = Color.white;
        img.raycastTarget = true;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool bold, float autoMin = 0f)
    {
        GameObject go = NewUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        if (F != null)
            tmp.font = F;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.raycastTarget = false;

        // autoMin > 0: texto de UNA línea que se achica para entrar en su caja (evita que se superponga con otros).
        if (autoMin > 0f)
        {
#pragma warning disable 618
            tmp.enableWordWrapping = false;
#pragma warning restore 618
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = autoMin;
            tmp.fontSizeMax = size;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        return tmp;
    }

    private static Button NewButton(string name, Transform parent, Color bg, out Image bgImage)
    {
        bgImage = NewImage(name, parent, bg, true);

        Button button = bgImage.gameObject.AddComponent<Button>();
        button.targetGraphic = bgImage;
        button.transition = Selectable.Transition.None;

        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        return button;
    }
}
#endif