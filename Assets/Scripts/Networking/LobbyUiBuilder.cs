#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Herramienta de editor: arma lo VISUAL del lobby 3D en la escena NetworkLobby, SIN tocar el fondo (el salón 3D):
///  - Un escenario 3D lejos del salón (LobbyCharacterStage) con una cámara y luces propias, que dibuja el personaje de
///    cada jugador sobre su columna.
///  - Columnas (Lobby3D/Slot1..N) con la tarjeta de cada jugador: nombre, rol, icono + nombre del personaje, conexión y listo.
///  - Un botón PERSONAJE al lado de LISTO (clon de LISTO, con su mismo estilo).
///  - Un resumen de la partida (MODO / MAPA / DIFICULTAD) arriba a la derecha y un botón CONFIGURAR PARTIDA que abre una
///    ventana emergente con el lugar reservado para cada opción (se completa después).
///  - Oculta las tarjetas viejas con la foto (player / player2); no las borra.
/// Usa el estilo que ya tiene tu lobby (fuentes, colores e iconos). Va en una carpeta "Editor".
/// Menú: Game > Construir UI del Lobby (3D). Se puede ejecutar de nuevo para reconstruir.
/// </summary>
public class LobbyUIBuilder : EditorWindow
{
    private CharacterCatalog catalog;
    private CharacterPreviewStage copyFrom;
    private int slotCount = 4;

    // Estilo ya existente en tu lobby (sacado de la escena NetworkLobby).
    private const string TitleFontGuid = "0d82eaf50c142fa4ba34beb1fc9a12e4";   // serif de títulos y botones
    private const string MonoFontGuid = "102734b590693d240886df79eaaa686c";    // mono de estados
    private const string OkIconGuid = "322cbf41b591f044fa4e97dc805822b8";      // escudo verde
    private const string BadIconGuid = "5beab3f5f64e4ca4790cdced6b8a0369";     // alerta roja

    private static readonly Color PanelColor = new Color(0.039f, 0.071f, 0.110f, 0.80f);
    private static readonly Color ColumnColor = new Color(0.03f, 0.05f, 0.07f, 0.40f);
    private static readonly Color Green = new Color(0f, 1f, 0.4f, 1f);
    private static readonly Color RoleColor = new Color(0.384f, 0.464f, 0.394f, 1f);
    private static readonly Color Red = new Color(1f, 0f, 0.038f, 1f);

    // Geometría en píxeles del Canvas (referencia 1920 x 1080)
    private const float BandW = 1760f;      // ancho de la franja de columnas
    private const float BandH = 600f;       // alto de la franja (arranca debajo del encabezado)
    private const float SlotW = 425f;
    private const float Gap = 20f;
    private const float PanelH = 200f;      // alto de la tarjeta de cada jugador
    private const float BottomY = 130f;     // la franja arranca 130 px sobre el borde de abajo (debajo van los botones)

    // Cámara del escenario 3D
    private const float Fov = 30f;
    private const float ViewH = 2.3f;       // metros visibles de alto
    private const float FeetMargin = 0.1f;  // aire bajo los pies

    private TMP_FontAsset titleFont, monoFont;
    private Sprite okIcon, badIcon;

    private static readonly Vector2 TL = new Vector2(0f, 1f);
    private static readonly Vector2 TR = new Vector2(1f, 1f);
    private static readonly Vector2 BC = new Vector2(0.5f, 0f);

    [MenuItem("Game/Construir UI del Lobby (3D)")]
    private static void Open()
    {
        GetWindow<LobbyUIBuilder>("Lobby 3D");
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
            "Abrí la escena NetworkLobby y tocá Construir. No cambia el fondo: agrega columnas con el personaje 3D de cada " +
            "jugador, un botón PERSONAJE y un resumen de la partida con CONFIGURAR PARTIDA. Las tarjetas viejas " +
            "(player / player2) se ocultan, no se borran.", MessageType.Info);

        catalog = (CharacterCatalog)EditorGUILayout.ObjectField("Catálogo", catalog, typeof(CharacterCatalog), false);
        slotCount = EditorGUILayout.IntSlider("Jugadores (columnas)", slotCount, 2, 4);
        copyFrom = (CharacterPreviewStage)EditorGUILayout.ObjectField(
            new GUIContent("Copiar pose y arma de (opcional)", "Prefab del CharacterStage de la escena de selección (arrastrá ese objeto a Project para crear el prefab)."),
            copyFrom, typeof(CharacterPreviewStage), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("Construir Lobby 3D", GUILayout.Height(32)))
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

        Transform panel = FindInScene("salaEsperaPanel");

        if (panel == null)
        {
            EditorUtility.DisplayDialog("No encuentro salaEsperaPanel", "Abrí la escena NetworkLobby (la que tiene el Canvas con salaEsperaPanel).", "OK");
            return;
        }

        int layer = EnsureLayer("CharacterPreview");

        if (layer < 0)
        {
            EditorUtility.DisplayDialog("Sin layers libres", "No hay un User Layer libre para 'CharacterPreview'. Liberá uno en Tags & Layers.", "OK");
            return;
        }

        titleFont = LoadFont(TitleFontGuid);
        monoFont = LoadFont(MonoFontGuid);
        okIcon = LoadSprite(OkIconGuid);
        badIcon = LoadSprite(BadIconGuid);

        // Reconstrucción: borra lo generado antes.
        DestroyByName(panel, "Lobby3D");
        DestroyByName(panel, "Partida");

        Canvas rootCanvas = panel.GetComponentInParent<Canvas>();

        if (rootCanvas != null)
        {
            DestroyByName(rootCanvas.transform, "ConfigurarPartidaPopup");
            DestroyByName(rootCanvas.transform, "MensajePopup");
        }

        GameObject oldStage = GameObject.Find("LobbyCharacterStage");

        if (oldStage != null)
            Undo.DestroyObjectImmediate(oldStage);

        // Cálculos de la cámara
        float tanHalf = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        float dist = ViewH / (2f * tanHalf);
        float viewW = ViewH * (BandW / BandH);
        float metersPerPx = viewW / BandW;
        float camY = ViewH * 0.5f - FeetMargin;

        // ---------------------------------------------------------
        // Escenario 3D (lejos del salón)
        // ---------------------------------------------------------
        GameObject stageGO = new GameObject("LobbyCharacterStage");
        Undo.RegisterCreatedObjectUndo(stageGO, "Construir lobby 3D");
        stageGO.transform.position = new Vector3(0f, -300f, 0f);

        Transform[] anchors = new Transform[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            GameObject a = new GameObject("Slot" + (i + 1));
            a.transform.SetParent(stageGO.transform, false);
            // La cámara está rotada 180°: el +X del mundo se ve a la IZQUIERDA, por eso el signo menos (JUGADOR 1 = izquierda).
            a.transform.localPosition = new Vector3(-SlotCenterX(i) * metersPerPx, 0f, 0f);
            anchors[i] = a.transform;
        }

        GameObject camGO = new GameObject("PreviewCamera", typeof(Camera));
        camGO.transform.SetParent(stageGO.transform, false);
        camGO.transform.localPosition = new Vector3(0f, camY, dist);
        camGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Camera cam = camGO.GetComponent<Camera>();
        cam.fieldOfView = Fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.depth = -20f;

        // Luces puntuales con alcance corto: iluminan solo a los personajes (no tocan el salón, que está a 300 m).
        // Valores pensados para URP (la intensidad cae con el cuadrado de la distancia); ajustalos a gusto.
        CreateLight("Luz_Frente", stageGO.transform, new Vector3(0f, 2.4f, dist - 0.6f), 28f, 16f, new Color(1f, 0.97f, 0.92f));
        CreateLight("Luz_Borde", stageGO.transform, new Vector3(0f, 2.6f, -3.2f), 14f, 14f, new Color(0.55f, 0.8f, 1f));

        // Que la cámara del lobby no vea a los personajes del escenario.
        Camera main = Camera.main;

        if (main != null)
        {
            Undo.RecordObject(main, "Culling Mask");
            main.cullingMask &= ~(1 << layer);
        }
        else
        {
            Debug.LogWarning("[LobbyUIBuilder] No encontré la Main Camera: desmarcá el layer CharacterPreview de su Culling Mask a mano.");
        }

        // ---------------------------------------------------------
        // UI: contenedor al fondo del panel (detrás de los textos y botones existentes)
        // ---------------------------------------------------------
        GameObject container = NewUIObject("Lobby3D", panel);
        Undo.RegisterCreatedObjectUndo(container, "Construir lobby 3D");
        Stretch((RectTransform)container.transform, 0f);
        container.transform.SetSiblingIndex(0);

        // Columnas de fondo
        GameObject columns = NewUIObject("Columnas", container.transform);
        Stretch((RectTransform)columns.transform, 0f);

        for (int i = 0; i < slotCount; i++)
        {
            Image col = NewImage("Columna" + (i + 1), columns.transform, ColumnColor, false);
            Place(col.rectTransform, BC, BC, new Vector2(SlotCenterX(i), BottomY), new Vector2(SlotW, BandH));
        }

        // Imagen donde se ve el render de los personajes
        GameObject modelsGO = NewUIObject("Modelos", container.transform);
        RawImage models = modelsGO.AddComponent<RawImage>();
        models.raycastTarget = false;
        Place(models.rectTransform, BC, BC, new Vector2(0f, BottomY), new Vector2(BandW, BandH));

        // Componente del escenario
        CharacterPreviewStage stage = stageGO.AddComponent<CharacterPreviewStage>();
        SerializedObject stageSo = new SerializedObject(stage);
        SetObj(stageSo, "catalog", catalog);
        SetObj(stageSo, "previewCamera", cam);
        SetObj(stageSo, "target", models);
        stageSo.FindProperty("previewLayerName").stringValue = "CharacterPreview";
        stageSo.FindProperty("textureSize").vector2IntValue = new Vector2Int(Mathf.RoundToInt(BandW * 0.8f), Mathf.RoundToInt(BandH * 0.8f));

        SerializedProperty anchorsProp = stageSo.FindProperty("slotAnchors");
        anchorsProp.arraySize = slotCount;

        for (int i = 0; i < slotCount; i++)
            anchorsProp.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];

        stageSo.ApplyModifiedProperties();

        if (copyFrom != null)
            CopyStageSettings(copyFrom, stage);

        // ---------------------------------------------------------
        // Columnas con la tarjeta de cada jugador
        // ---------------------------------------------------------
        LobbyPlayerSlot[] slots = new LobbyPlayerSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
            slots[i] = BuildSlot(container.transform, i, stage);

        // ---------------------------------------------------------
        // Botones: LISTO a la derecha del centro, PERSONAJE a la izquierda
        // ---------------------------------------------------------
        Transform listo = panel.Find("PLAY_MULTIPLAYER");
        Button personajeBtn = null;

        if (listo != null)
        {
            RectTransform lr = (RectTransform)listo;
            Place(lr, BC, new Vector2(0.5f, 0.5f), new Vector2(170f, 70f), new Vector2(300f, 70f));

            Transform old = panel.Find("Btn_Personaje");

            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            GameObject clone = Instantiate(listo.gameObject, panel);
            Undo.RegisterCreatedObjectUndo(clone, "Boton personaje");
            clone.name = "Btn_Personaje";
            Place((RectTransform)clone.transform, BC, new Vector2(0.5f, 0.5f), new Vector2(-170f, 70f), new Vector2(300f, 70f));

            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
                label.text = "PERSONAJE";

            // El clon hereda los eventos del botón LISTO: se borran.
            Button btn = clone.GetComponent<Button>();
            personajeBtn = btn;

            if (btn != null)
            {
                for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(btn.onClick, i);

                // Si ya hay una ventana de selección en la escena, el botón la abre.
                Transform selectCanvas = FindInScene("CharacterSelectCanvas");

                if (selectCanvas != null)
                    UnityEventTools.AddBoolPersistentListener(btn.onClick, selectCanvas.gameObject.SetActive, true);
            }
        }
        else
        {
            Debug.LogWarning("[LobbyUIBuilder] No encontré PLAY_MULTIPLAYER (botón LISTO) dentro de salaEsperaPanel.");
        }

        // Resumen de la partida + ventana CONFIGURAR PARTIDA
        BuildGameSettingsUI(panel, listo);

        // Ventana de mensajes (sala llena, sala cerrada...)
        GameObject msgPopup;
        TMP_Text msgTitle;
        TMP_Text msgText;
        BuildMessagePopup(panel, listo, out msgPopup, out msgTitle, out msgText);

        // Tarjetas viejas con la foto
        HideByName(panel, "player");
        HideByName(panel, "player2");

        // ---------------------------------------------------------
        // Controlador del lobby (si ya tiene los campos nuevos)
        // ---------------------------------------------------------
        MultiplayerLobbyController controller = FindObjectOfType<MultiplayerLobbyController>(true);

        if (controller != null)
        {
            SerializedObject cso = new SerializedObject(controller);
            SerializedProperty slotsProp = cso.FindProperty("slots");

            if (slotsProp != null)
            {
                slotsProp.arraySize = slotCount;

                for (int i = 0; i < slotCount; i++)
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

                SetObj(cso, "catalogoPersonajes", catalog);
                SetObj(cso, "escenarioPersonajes", stage);

                if (cso.FindProperty("btnPersonaje") != null)
                    SetObj(cso, "btnPersonaje", personajeBtn);

                if (cso.FindProperty("popupMensaje") != null)
                {
                    SetObj(cso, "popupMensaje", msgPopup);
                    SetObj(cso, "popupMensajeTitulo", msgTitle);
                    SetObj(cso, "popupMensajeTexto", msgText);
                }

                cso.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[LobbyUIBuilder] El MultiplayerLobbyController no tiene el campo 'slots': usá la versión nueva del script y volvé a construir.");
            }
        }
        else
        {
            Debug.LogWarning("[LobbyUIBuilder] No hay un MultiplayerLobbyController en esta escena: asigná Slots, Catálogo y Escenario a mano cuando lo agregues.");
        }

        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);

        Debug.Log("[LobbyUIBuilder] Lobby 3D construido. Faltan: pose/arma en LobbyCharacterStage (si no copiaste), los iconos del catálogo y la ventana de selección (Game > Construir UI de Selección de Personaje).");
    }

    // =========================================================
    // PARTIDA: resumen + ventana emergente (el contenido se completa después)
    // =========================================================

    private void BuildGameSettingsUI(Transform panel, Transform listo)
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Transform canvasT = canvas != null ? canvas.rootCanvas.transform : panel.parent;

        // ---- Resumen arriba a la derecha (libre en tu lobby, debajo del título) ----
        GameObject partida = NewUIObject("Partida", panel);
        Undo.RegisterCreatedObjectUndo(partida, "Construir partida");
        Place((RectTransform)partida.transform, TR, TR, new Vector2(-80f, -190f), new Vector2(760f, 140f));

        string[] labels = { "MODO", "MAPA", "DIFICULTAD" };
        string[] ids = { "Modo", "Mapa", "Dificultad" };

        for (int i = 0; i < 3; i++)
        {
            Image box = NewImage("Caja" + ids[i], partida.transform, PanelColor, false);
            Place(box.rectTransform, TL, TL, new Vector2(i * 260f, 0f), new Vector2(240f, 72f));

            TMP_Text label = NewText("Etiqueta", box.transform, labels[i], 16f, RoleColor, TextAlignmentOptions.Left, monoFont, 11f);
            Place(label.rectTransform, TL, TL, new Vector2(12f, -6f), new Vector2(216f, 20f));

            TMP_Text value = NewText(ids[i] + "Valor", box.transform, "-", 26f, Color.white, TextAlignmentOptions.Left, monoFont, 14f);
            Place(value.rectTransform, TL, TL, new Vector2(12f, -28f), new Vector2(216f, 38f));
        }

        // ---- Ventana emergente CONFIGURAR PARTIDA (arriba de todo el Canvas) ----
        GameObject popup = NewUIObject("ConfigurarPartidaPopup", canvasT);
        Undo.RegisterCreatedObjectUndo(popup, "Construir partida");
        Stretch((RectTransform)popup.transform, 0f);

        Image dim = popup.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        dim.raycastTarget = true;

        Image window = NewImage("Ventana", popup.transform, new Color(0.04f, 0.07f, 0.11f, 0.97f), true);
        Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 640f));

        Image line = NewImage("LineaSuperior", window.transform, Green, false);
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(1f, 1f);
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.rectTransform.sizeDelta = new Vector2(0f, 4f);

        TMP_Text title = NewText("Titulo", window.transform, "CONFIGURAR PARTIDA", 44f, new Color(0.70f, 0.79f, 0.71f, 1f), TextAlignmentOptions.Left, titleFont, 24f);
        Place(title.rectTransform, TL, TL, new Vector2(40f, -26f), new Vector2(920f, 56f));

        TMP_Text sub = NewText("Subtitulo", window.transform, "SOLO EL ANFITRION PUEDE CAMBIAR ESTOS VALORES", 18f, RoleColor, TextAlignmentOptions.Left, monoFont, 12f);
        Place(sub.rectTransform, TL, TL, new Vector2(40f, -86f), new Vector2(920f, 24f));

        string[] sectionNames = { "Seccion_Modo", "Seccion_Mapa", "Seccion_Dificultad" };
        string[] sectionTitles = { "MODO DE JUEGO", "MAPA", "DIFICULTAD" };

        for (int i = 0; i < 3; i++)
        {
            GameObject section = NewUIObject(sectionNames[i], window.transform);
            Place((RectTransform)section.transform, TL, TL, new Vector2(40f, -130f - i * 138f), new Vector2(920f, 124f));

            TMP_Text st = NewText("Titulo", section.transform, sectionTitles[i], 24f, Green, TextAlignmentOptions.Left, monoFont, 14f);
            Place(st.rectTransform, TL, TL, Vector2.zero, new Vector2(920f, 30f));

            // Acá van después los botones de cada opción.
            Image content = NewImage("Contenido", section.transform, new Color(1f, 1f, 1f, 0.05f), false);
            Place(content.rectTransform, TL, TL, new Vector2(0f, -36f), new Vector2(920f, 84f));

            TMP_Text soon = NewText("Pendiente", content.transform, "PROXIMAMENTE", 20f, RoleColor, TextAlignmentOptions.Center, monoFont, 12f);
            Stretch(soon.rectTransform, 0f);
        }

        // ---- Botones (clones de LISTO, mismo estilo) ----
        if (listo != null)
        {
            Button open = CloneButton(listo, partida.transform, "Btn_ConfigurarPartida", "CONFIGURAR PARTIDA", TL, TL, new Vector2(0f, -84f), new Vector2(760f, 52f));
            UnityEventTools.AddBoolPersistentListener(open.onClick, popup.SetActive, true);

            Button close = CloneButton(listo, window.transform, "Btn_Cerrar", "CERRAR", BC, BC, new Vector2(0f, 24f), new Vector2(300f, 64f));
            UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);
        }

        popup.SetActive(false);
    }

    // Ventana emergente genérica de mensajes (la usa el controlador para "SALA LLENA", "SALA CERRADA", etc.).
    private void BuildMessagePopup(Transform panel, Transform listo, out GameObject popup, out TMP_Text title, out TMP_Text text)
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Transform canvasT = canvas != null ? canvas.rootCanvas.transform : panel.parent;

        popup = NewUIObject("MensajePopup", canvasT);
        Undo.RegisterCreatedObjectUndo(popup, "Construir mensaje");
        Stretch((RectTransform)popup.transform, 0f);

        Image dim = popup.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        dim.raycastTarget = true;

        Image window = NewImage("Ventana", popup.transform, new Color(0.04f, 0.07f, 0.11f, 0.98f), true);
        Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 380f));

        Image line = NewImage("LineaSuperior", window.transform, Red, false);
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(1f, 1f);
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.rectTransform.sizeDelta = new Vector2(0f, 4f);

        title = NewText("Titulo", window.transform, "SALA LLENA", 48f, new Color(0.70f, 0.79f, 0.71f, 1f), TextAlignmentOptions.Center, titleFont, 26f);
        Place(title.rectTransform, TL, TL, new Vector2(30f, -40f), new Vector2(700f, 64f));

        text = NewText("Mensaje", window.transform, "", 24f, Color.white, TextAlignmentOptions.Top, monoFont, 14f);
        // El mensaje puede ocupar varias líneas.
#pragma warning disable 618
        text.enableWordWrapping = true;
#pragma warning restore 618
        text.enableAutoSizing = false;
        Place(text.rectTransform, TL, TL, new Vector2(40f, -130f), new Vector2(680f, 130f));

        if (listo != null)
        {
            Button ok = CloneButton(listo, window.transform, "Btn_Aceptar", "ACEPTAR", BC, BC, new Vector2(0f, 30f), new Vector2(300f, 64f));
            UnityEventTools.AddBoolPersistentListener(ok.onClick, popup.SetActive, false);
        }

        popup.SetActive(false);
    }

    private static Button CloneButton(Transform source, Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject clone = Instantiate(source.gameObject, parent);
        Undo.RegisterCreatedObjectUndo(clone, "Clonar boton");
        clone.name = name;
        clone.SetActive(true);
        Place((RectTransform)clone.transform, anchor, pivot, pos, size);

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            float max = label.fontSize;
            label.text = text;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = max;
        }

        Button b = clone.GetComponent<Button>();

        if (b != null)
        {
            // Hereda los eventos del botón original: se borran.
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(b.onClick, i);
        }

        return b;
    }

    private LobbyPlayerSlot BuildSlot(Transform parent, int i, CharacterPreviewStage stage)
    {
        GameObject slotGO = NewUIObject("Slot" + (i + 1), parent);
        Place((RectTransform)slotGO.transform, BC, BC, new Vector2(SlotCenterX(i), BottomY), new Vector2(SlotW, BandH));

        // Zona para girar el modelo (transparente, encima del personaje, arriba de la tarjeta)
        Image drag = NewImage("ZonaGiro", slotGO.transform, new Color(0f, 0f, 0f, 0f), true);
        Place(drag.rectTransform, BC, BC, new Vector2(0f, PanelH), new Vector2(SlotW, BandH - PanelH));

        CharacterPreviewDrag dragComp = drag.gameObject.AddComponent<CharacterPreviewDrag>();
        SerializedObject dragSo = new SerializedObject(dragComp);
        SetObj(dragSo, "stage", stage);
        dragSo.FindProperty("slotIndex").intValue = i;
        dragSo.ApplyModifiedProperties();

        // Tarjeta
        Image card = NewImage("Tarjeta", slotGO.transform, PanelColor, false);
        Place(card.rectTransform, BC, BC, Vector2.zero, new Vector2(SlotW, PanelH));

        // Marca de "jugador local"
        Image highlight = NewImage("MarcaLocal", card.transform, Green, false);
        highlight.rectTransform.anchorMin = new Vector2(0f, 1f);
        highlight.rectTransform.anchorMax = new Vector2(1f, 1f);
        highlight.rectTransform.pivot = new Vector2(0.5f, 1f);
        highlight.rectTransform.anchoredPosition = Vector2.zero;
        highlight.rectTransform.sizeDelta = new Vector2(0f, 4f);
        highlight.gameObject.SetActive(false);

        TMP_Text title = NewText("Titulo", card.transform, "JUGADOR " + (i + 1), 34f, Color.white, TextAlignmentOptions.Center, monoFont, 20f);
        Place(title.rectTransform, TL, TL, new Vector2(16f, -8f), new Vector2(SlotW - 32f, 38f));

        TMP_Text role = NewText("Rol", card.transform, "[ ESPERANDO JUGADOR ]", 18f, RoleColor, TextAlignmentOptions.Center, monoFont, 12f);
        Place(role.rectTransform, TL, TL, new Vector2(16f, -46f), new Vector2(SlotW - 32f, 22f));

        // Fila del personaje: icono + nombre + tipo
        Image iconBg = NewImage("IconoFondo", card.transform, new Color(1f, 1f, 1f, 0.07f), false);
        Place(iconBg.rectTransform, TL, TL, new Vector2(16f, -74f), new Vector2(56f, 56f));

        Image icon = NewImage("Icono", iconBg.transform, Color.white, false);
        Stretch(icon.rectTransform, 2f);
        icon.preserveAspect = true;
        icon.enabled = false;

        TMP_Text charName = NewText("PersonajeNombre", card.transform, "", 28f, Color.white, TextAlignmentOptions.Left, monoFont, 16f);
        Place(charName.rectTransform, TL, TL, new Vector2(82f, -72f), new Vector2(SlotW - 98f, 32f));

        TMP_Text charTier = NewText("PersonajeTipo", card.transform, "", 18f, Green, TextAlignmentOptions.Left, monoFont, 12f);
        Place(charTier.rectTransform, TL, TL, new Vector2(82f, -104f), new Vector2(SlotW - 98f, 24f));

        // Conexión y estado
        Image connIcon = NewImage("IconoConexion", card.transform, Color.white, false);
        connIcon.sprite = badIcon;
        connIcon.preserveAspect = true;
        Place(connIcon.rectTransform, TL, TL, new Vector2(16f, -138f), new Vector2(24f, 24f));

        TMP_Text conn = NewText("Conexion", card.transform, "DESCONECTADO", 22f, Red, TextAlignmentOptions.Left, monoFont, 14f);
        Place(conn.rectTransform, TL, TL, new Vector2(48f, -137f), new Vector2(SlotW - 64f, 26f));

        Image readyIcon = NewImage("IconoEstado", card.transform, Color.white, false);
        readyIcon.sprite = badIcon;
        readyIcon.preserveAspect = true;
        Place(readyIcon.rectTransform, TL, TL, new Vector2(16f, -166f), new Vector2(24f, 24f));

        TMP_Text ready = NewText("Estado", card.transform, "ESTADO: NO LISTO", 22f, Red, TextAlignmentOptions.Left, monoFont, 14f);
        Place(ready.rectTransform, TL, TL, new Vector2(48f, -165f), new Vector2(SlotW - 64f, 26f));

        // Componente + referencias
        LobbyPlayerSlot slot = slotGO.AddComponent<LobbyPlayerSlot>();
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("slotIndex").intValue = i;
        SetObj(so, "titleText", title);
        SetObj(so, "roleText", role);
        SetObj(so, "connectionText", conn);
        SetObj(so, "readyText", ready);
        SetObj(so, "connectionIcon", connIcon);
        SetObj(so, "readyIcon", readyIcon);
        SetObj(so, "characterIcon", icon);
        SetObj(so, "characterNameText", charName);
        SetObj(so, "characterText", charTier);
        SetObj(so, "localHighlight", highlight.gameObject);
        SetObj(so, "dragZone", drag.gameObject);
        so.ApplyModifiedProperties();

        return slot;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    // Centro horizontal (px, respecto al centro de la pantalla) de la columna i.
    private float SlotCenterX(int i)
    {
        return (i - (slotCount - 1) * 0.5f) * (SlotW + Gap);
    }

    private static void CopyStageSettings(CharacterPreviewStage from, CharacterPreviewStage to)
    {
        string[] fields =
        {
            "poseController", "poseClip", "extraLayersWeight",
            "weaponPrefab", "weaponBoneName", "weaponLocalPosition", "weaponLocalEuler", "weaponLocalScale",
            "startYaw"
        };

        SerializedObject src = new SerializedObject(from);
        SerializedObject dst = new SerializedObject(to);

        for (int i = 0; i < fields.Length; i++)
        {
            SerializedProperty s = src.FindProperty(fields[i]);
            SerializedProperty d = dst.FindProperty(fields[i]);

            if (s != null && d != null)
                CopyValue(s, d);
        }

        dst.ApplyModifiedProperties();
    }

    // Copia el valor de una propiedad serializada a otra del mismo tipo (sin depender de APIs que cambian entre versiones).
    private static void CopyValue(SerializedProperty s, SerializedProperty d)
    {
        if (s.propertyType != d.propertyType)
            return;

        switch (s.propertyType)
        {
            case SerializedPropertyType.ObjectReference: d.objectReferenceValue = s.objectReferenceValue; break;
            case SerializedPropertyType.Float: d.floatValue = s.floatValue; break;
            case SerializedPropertyType.Integer: d.intValue = s.intValue; break;
            case SerializedPropertyType.Boolean: d.boolValue = s.boolValue; break;
            case SerializedPropertyType.String: d.stringValue = s.stringValue; break;
            case SerializedPropertyType.Vector3: d.vector3Value = s.vector3Value; break;
        }
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);

        if (existing >= 0)
            return existing;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

        if (assets == null || assets.Length == 0)
            return -1;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            SerializedProperty sp = layers.GetArrayElementAtIndex(i);

            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }

        return -1;
    }

    private static void CreateLight(string name, Transform parent, Vector3 localPos, float intensity, float range, Color color)
    {
        GameObject go = new GameObject(name, typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        Light l = go.GetComponent<Light>();
        l.type = LightType.Point;
        l.intensity = intensity;
        l.range = range;
        l.color = color;
        l.shadows = LightShadows.None;
    }

    private static Transform FindInScene(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeep(roots[i].transform, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name)
            return t;

        foreach (Transform child in t)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void DestroyByName(Transform parent, string name)
    {
        Transform t = parent.Find(name);

        if (t != null)
            Undo.DestroyObjectImmediate(t.gameObject);
    }

    private static void HideByName(Transform parent, string name)
    {
        Transform t = parent.Find(name);

        if (t != null && t.gameObject.activeSelf)
        {
            Undo.RecordObject(t.gameObject, "Ocultar tarjeta vieja");
            t.gameObject.SetActive(false);
        }
    }

    private static TMP_FontAsset LoadFont(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static Sprite LoadSprite(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetObj(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);

        if (p == null)
        {
            Debug.LogWarning("[LobbyUIBuilder] No encuentro el campo '" + field + "' en " + so.targetObject.GetType().Name);
            return;
        }

        p.objectReferenceValue = value;
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

    private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, TMP_FontAsset font, float autoMin)
    {
        GameObject go = NewUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        if (font != null)
            tmp.font = font;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;

        // Una sola línea que se achica para entrar en su caja: nada se superpone.
#pragma warning disable 618
        tmp.enableWordWrapping = false;
#pragma warning restore 618
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = autoMin;
        tmp.fontSizeMax = size;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return tmp;
    }
}
#endif