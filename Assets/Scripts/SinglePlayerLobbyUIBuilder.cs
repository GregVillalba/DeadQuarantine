#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Herramienta de editor: arma el lobby 3D de UN JUGADOR (versión de LobbyUIBuilder con una sola columna).
/// Pensada para una escena nueva (SinglePlayerLobby), pero también funciona sobre un panel que ya tengas:
///  - Un escenario 3D lejos de todo (LobbyCharacterStage) con cámara y luces propias, que dibuja tu personaje.
///  - Una columna (Lobby3D/Slot1) con la tarjeta del jugador: nombre, rol, icono + nombre del personaje, conexión y listo.
///  - Botones COMENZAR, PERSONAJE y VOLVER.
///  - Resumen de la partida (MODO / MAPA / DIFICULTAD) arriba a la derecha y botón CONFIGURAR PARTIDA con su ventana
///    emergente (el mismo apartado del lobby multijugador; por ahora solo muestra lo elegido en el menú).
///  - Ventana de mensajes y el SinglePlayerLobbyController ya conectado.
/// La apariencia es la MISMA que la del lobby multijugador (escena NetworkLobby): fondo del panel, título, texto superior,
/// botón ATRAS arriba a la izquierda, botones verdes (con su contorno y su hover), tarjeta, columna, cámara, luces,
/// resumen y ventanas. Los valores salen de esa escena, así que no hace falta copiar ningún botón ni ningún título.
/// Solo cambia lo propio de un jugador: una columna y los botones COMENZAR / PERSONAJE / ATRAS (el ATRAS llama a VOLVER del controlador).
/// Si no hay panel, crea un Canvas con su panel (y un EventSystem si falta). No crea el fondo: copiá el salón 3D
/// del NetworkLobby a la escena nueva si lo querés igual.
/// Va en una carpeta "Editor". Menú: Game > Construir UI del Lobby Singleplayer (3D). Se puede ejecutar de nuevo para reconstruir.
/// </summary>
public class SinglePlayerLobbyUIBuilder : EditorWindow
{
    private CharacterCatalog catalog;
    private CharacterPreviewStage copyFrom;
    private RectTransform panelExistente;
    // True si el panel lo maneja este builder (el que él crea, o el 'salaEsperaSinglePlayerPanel'): solo ahí arma fondo, título y textos.
    private bool panelPropio;

    private const string PanelName = "salaEsperaSinglePlayerPanel";

    // Estilo ya existente en tu lobby (mismos GUID que LobbyUIBuilder).
    private const string TitleFontGuid = "0d82eaf50c142fa4ba34beb1fc9a12e4";   // serif de títulos y botones
    private const string MonoFontGuid = "102734b590693d240886df79eaaa686c";    // mono de estados
    private const string OkIconGuid = "322cbf41b591f044fa4e97dc805822b8";      // escudo verde
    private const string BadIconGuid = "5beab3f5f64e4ca4790cdced6b8a0369";     // alerta roja

    private static readonly Color PanelColor = new Color(0.039f, 0.071f, 0.110f, 0.80f);
    private static readonly Color ColumnColor = new Color(0.03f, 0.05f, 0.07f, 0.40f);
    private static readonly Color Green = new Color(0f, 1f, 0.4f, 1f);
    private static readonly Color RoleColor = new Color(0.384f, 0.464f, 0.394f, 1f);
    private static readonly Color Red = new Color(1f, 0f, 0.038f, 1f);

    // Estilo tomado de la escena NetworkLobby (salaEsperaPanel).
    private const string PanelBgGuid = "3ffec16ac0388784f849fc4f40647b28";                          // sprite de fondo del panel
    private static readonly Color PanelBgColor = new Color(0.502f, 0.502f, 0.502f, 1f);
    private static readonly Color LobbyTitleColor = new Color(0.70088285f, 0.78867924f, 0.713606f, 1f);
    private static readonly Color TopTextColor = new Color(0.4095763f, 0.735849f, 0.53563625f, 1f);
    private static readonly Color BtnBgColor = new Color(0.060925562f, 0.16226411f, 0.085070916f, 1f);   // botones verdes (LISTO / PERSONAJE...)
    private static readonly Color BtnTextColor = new Color(0.19607845f, 1f, 0.2784314f, 1f);
    private static readonly Color BtnOutlineColor = new Color(0f, 1f, 0.14210796f, 0.5f);
    private static readonly Color BackBgColor = new Color(0.05882353f, 0.09803922f, 0.15686275f, 0.627451f); // botón ATRAS
    private static readonly Color BackTextColor = new Color(0.39215687f, 0.45490196f, 0.54509807f, 1f);

    // Geometría en píxeles del Canvas (referencia 1920 x 1080). Una sola columna centrada, del mismo ancho que las del multijugador.
    private const float SlotW = 425f;
    private const float BandW = SlotW;
    private const float BandH = 600f;
    private const float PanelH = 200f;      // alto de la tarjeta del jugador
    private const float BottomY = 130f;     // la columna arranca 130 px sobre el borde de abajo (debajo van los botones)

    // Cámara del escenario 3D
    private const float Fov = 30f;
    private const float ViewH = 2.3f;       // metros visibles de alto
    private const float FeetMargin = 0.1f;  // aire bajo los pies

    private TMP_FontAsset titleFont, monoFont;
    private Sprite okIcon, badIcon;

    // Textos que el controlador completa en runtime.
    private TMP_Text slotRolText;
    private TMP_Text resModo, resMapa, resDificultad;
    private TMP_Text popModo, popMapa, popDificultad;

    private static readonly Vector2 TL = new Vector2(0f, 1f);
    private static readonly Vector2 TR = new Vector2(1f, 1f);
    private static readonly Vector2 BC = new Vector2(0.5f, 0f);
    private static readonly Vector2 BL = new Vector2(0f, 0f);

    [MenuItem("Game/Construir UI del Lobby Singleplayer (3D)")]
    private static void Open()
    {
        GetWindow<SinglePlayerLobbyUIBuilder>("Lobby 1 jugador");
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
            "Abrí la escena donde va el lobby de un jugador (la del menú) y tocá Construir. Arma el fondo y el título, una columna con el personaje 3D, " +
            "los botones COMENZAR / PERSONAJE / ATRAS, el resumen de la partida con CONFIGURAR PARTIDA y el controlador. " +
            "Si la escena no tiene panel, crea un Canvas con uno. La apariencia (fondo, título, botones, textos) es la misma del lobby multijugador.", MessageType.Info);

        catalog = (CharacterCatalog)EditorGUILayout.ObjectField("Catálogo", catalog, typeof(CharacterCatalog), false);

        copyFrom = (CharacterPreviewStage)EditorGUILayout.ObjectField(
            new GUIContent("Copiar pose y arma de (opcional)", "Prefab del CharacterStage de la escena de selección (arrastrá ese objeto a Project para crear el prefab)."),
            copyFrom, typeof(CharacterPreviewStage), false);

        panelExistente = (RectTransform)EditorGUILayout.ObjectField(
            new GUIContent("Panel existente (opcional)", "Si lo dejás vacío se usa el panel '" + PanelName + "' de la escena, o se crea uno."),
            panelExistente, typeof(RectTransform), true);

        EditorGUILayout.Space();

        if (GUILayout.Button("Construir Lobby 1 jugador", GUILayout.Height(32)))
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

        Transform panel = GetOrCreatePanel();

        // Reconstrucción: borra lo generado antes.
        DestroyByName(panel, "Lobby3D");
        DestroyByName(panel, "Partida");
        DestroyByName(panel, "Btn_Comenzar");
        DestroyByName(panel, "Btn_Personaje");
        DestroyByName(panel, "Btn_Volver");

        // Fondo, título y texto superior del panel (misma apariencia que salaEsperaPanel del multijugador).
        BuildPanelBase(panel);

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
        float camY = ViewH * 0.5f - FeetMargin;

        // ---------------------------------------------------------
        // Escenario 3D (lejos de todo)
        // ---------------------------------------------------------
        GameObject stageGO = new GameObject("LobbyCharacterStage");
        Undo.RegisterCreatedObjectUndo(stageGO, "Construir lobby singleplayer");
        stageGO.transform.position = new Vector3(0f, -300f, 0f);

        GameObject anchorGO = new GameObject("Slot1");
        anchorGO.transform.SetParent(stageGO.transform, false);
        anchorGO.transform.localPosition = Vector3.zero;

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

        // Luces puntuales con alcance corto: iluminan solo al personaje (no tocan el resto, que está a 300 m).
        // Valores pensados para URP (la intensidad cae con el cuadrado de la distancia); ajustalos a gusto.
        CreateLight("Luz_Frente", stageGO.transform, new Vector3(0f, 2.4f, dist - 0.6f), 28f, 16f, new Color(1f, 0.97f, 0.92f));
        CreateLight("Luz_Borde", stageGO.transform, new Vector3(0f, 2.6f, -3.2f), 14f, 14f, new Color(0.55f, 0.8f, 1f));

        // Que la cámara del lobby no vea al personaje del escenario.
        Camera main = Camera.main;

        if (main != null)
        {
            Undo.RecordObject(main, "Culling Mask");
            main.cullingMask &= ~(1 << layer);
        }
        else
        {
            Debug.LogWarning("[SinglePlayerLobbyUIBuilder] No encontré la Main Camera: desmarcá el layer CharacterPreview de su Culling Mask a mano.");
        }

        // ---------------------------------------------------------
        // UI: contenedor al fondo del panel (detrás de los textos y botones existentes)
        // ---------------------------------------------------------
        GameObject container = NewUIObject("Lobby3D", panel.gameObject);
        Undo.RegisterCreatedObjectUndo(container, "Construir lobby singleplayer");
        Stretch((RectTransform)container.transform, 0f);
        container.transform.SetSiblingIndex(0);

        // Columna de fondo
        GameObject columns = NewUIObject("Columnas", container);
        Stretch((RectTransform)columns.transform, 0f);

        Image col = NewImage("Columna1", columns.transform, ColumnColor, false);
        Place(col.rectTransform, BC, BC, new Vector2(0f, BottomY), new Vector2(SlotW, BandH));

        // Imagen donde se ve el render del personaje
        GameObject modelsGO = NewUIObject("Modelos", container);
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
        anchorsProp.arraySize = 1;
        anchorsProp.GetArrayElementAtIndex(0).objectReferenceValue = anchorGO.transform;

        stageSo.ApplyModifiedProperties();

        if (copyFrom != null)
            CopyStageSettings(copyFrom, stage);

        // ---------------------------------------------------------
        // Controlador (si ya hay uno en la escena se reutiliza)
        // ---------------------------------------------------------
        SinglePlayerLobbyController controller = FindObjectOfType<SinglePlayerLobbyController>(true);

        if (controller == null)
        {
            GameObject ctrlGO = new GameObject("SinglePlayerLobbyController");
            Undo.RegisterCreatedObjectUndo(ctrlGO, "Construir lobby singleplayer");
            controller = ctrlGO.AddComponent<SinglePlayerLobbyController>();
        }

        // ---------------------------------------------------------
        // Columna con la tarjeta del jugador
        // ---------------------------------------------------------
        LobbyPlayerSlot slot = BuildSlot(container.transform, stage);

        // ---------------------------------------------------------
        // Botones: COMENZAR a la derecha del centro, PERSONAJE a la izquierda, VOLVER abajo a la izquierda
        // ---------------------------------------------------------
        Vector2 center = new Vector2(0.5f, 0.5f);

        Button comenzarBtn = MakeButton(panel, "Btn_Comenzar", "COMENZAR", BC, center, new Vector2(170f, 70f), new Vector2(300f, 70f));
        Button personajeBtn = MakeButton(panel, "Btn_Personaje", "PERSONAJE", BC, center, new Vector2(-170f, 70f), new Vector2(300f, 70f));
        Button volverBtn = MakeBackButton(panel, "Btn_Volver", "ATRAS", TL, center, new Vector2(130f, -60f), new Vector2(160f, 45f));

        if (comenzarBtn != null) UnityEventTools.AddPersistentListener(comenzarBtn.onClick, controller.OnClick_Comenzar);
        if (personajeBtn != null) UnityEventTools.AddPersistentListener(personajeBtn.onClick, controller.OnClick_Personaje);
        if (volverBtn != null) UnityEventTools.AddPersistentListener(volverBtn.onClick, controller.OnClick_Volver);

        // Resumen de la partida + ventana CONFIGURAR PARTIDA
        BuildGameSettingsUI(panel);

        // Ventana de mensajes (configuración faltante, escena que no carga...)
        GameObject msgPopup;
        TMP_Text msgTitle;
        TMP_Text msgText;
        BuildMessagePopup(panel, out msgPopup, out msgTitle, out msgText);

        // Tarjetas viejas con la foto (si el panel venía de otro lobby)
        HideByName(panel, "player");
        HideByName(panel, "player2");

        // ---------------------------------------------------------
        // Referencias del controlador
        // ---------------------------------------------------------
        SerializedObject cso = new SerializedObject(controller);

        SetObj(cso, "slot", slot);
        SetObj(cso, "catalogoPersonajes", catalog);
        SetObj(cso, "escenarioPersonajes", stage);
        SetObj(cso, "rolTexto", slotRolText);
        SetObj(cso, "iconoVerde", okIcon);
        SetObj(cso, "iconoRojo", badIcon);

        SetObj(cso, "btnComenzar", comenzarBtn);
        SetObj(cso, "btnPersonaje", personajeBtn);
        SetObj(cso, "btnVolver", volverBtn);

        SetObj(cso, "resumenModo", resModo);
        SetObj(cso, "resumenMapa", resMapa);
        SetObj(cso, "resumenDificultad", resDificultad);

        SetObj(cso, "popupModo", popModo);
        SetObj(cso, "popupMapa", popMapa);
        SetObj(cso, "popupDificultad", popDificultad);

        SetObj(cso, "popupMensaje", msgPopup);
        SetObj(cso, "popupMensajeTitulo", msgTitle);
        SetObj(cso, "popupMensajeTexto", msgText);

        // Si ya hay una ventana de selección de personaje en la escena, se conecta (si no, el controlador la busca por nombre al tocar PERSONAJE).
        Transform selectCanvas = FindInScene("CharacterSelectCanvas");

        if (selectCanvas != null)
            SetObj(cso, "ventanaSeleccionPersonaje", selectCanvas.gameObject);

        cso.ApplyModifiedProperties();

        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);

        Debug.Log("[SinglePlayerLobbyUIBuilder] Lobby 1 jugador construido. Faltan: pose/arma en LobbyCharacterStage (si no copiaste), los iconos del catálogo, " +
                  "la ventana de selección (Game > Construir UI de Selección de Personaje) y agregar la escena a Build Settings.");
    }

    // =========================================================
    // PANEL / CANVAS
    // =========================================================

    private Transform GetOrCreatePanel()
    {
        if (panelExistente != null)
        {
            panelPropio = false;
            return panelExistente;
        }

        Transform found = FindInScene(PanelName);

        if (found != null)
        {
            panelPropio = true;
            return found;
        }

        // Escena nueva: se crea un Canvas con su panel a pantalla completa.
        GameObject canvasGO = new GameObject("SinglePlayerLobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Construir lobby singleplayer");
        canvasGO.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Igual que el Canvas del lobby multijugador: referencia 1920 x 1080 ajustada por el ANCHO.
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        GameObject panelGO = NewUIObject(PanelName, canvasGO);
        Stretch((RectTransform)panelGO.transform, 0f);

        EnsureEventSystem();

        panelPropio = true;
        return panelGO.transform;
    }

    // Fondo, título y texto superior: los mismos valores que salaEsperaPanel (escena NetworkLobby).
    // Solo se arma en el panel propio; si pasaste un panel tuyo, se respeta el que ya tiene.
    private void BuildPanelBase(Transform panel)
    {
        if (!panelPropio)
            return;

        DestroyByName(panel, "Titulo");
        DestroyByName(panel, "TextxSuperior");

        // Fondo (mismo sprite y color que el panel del multijugador)
        Image bg = panel.GetComponent<Image>();

        if (bg == null)
            bg = Undo.AddComponent<Image>(panel.gameObject);

        Sprite bgSprite = LoadSprite(PanelBgGuid);

        bg.sprite = bgSprite;
        bg.type = Image.Type.Simple;
        bg.raycastTarget = true;

        if (bgSprite != null)
        {
            bg.color = PanelBgColor;
        }
        else
        {
            bg.color = new Color(0.02f, 0.03f, 0.04f, 1f);
            Debug.LogWarning("[SinglePlayerLobbyUIBuilder] No encontré el sprite de fondo del lobby multijugador: el panel queda con un fondo oscuro liso.");
        }

        // Título: serif 72 en negrita, alineado arriba a la izquierda (igual que "SALA DE ESPERA CO - OP")
        TMP_Text title = NewExactText("Titulo", panel, "SALA DE ESPERA // UN JUGADOR", titleFont, 72f, FontStyles.Bold,
                                      LobbyTitleColor, TextAlignmentOptions.TopLeft, 0f);
        Place(title.rectTransform, TL, new Vector2(0.5f, 0.5f), new Vector2(1002.689f, -109f), new Vector2(1905.378f, 40f));

        // Texto superior: mono 24 (igual que "CONEXIÓN ESTABLE...")
        TMP_Text top = NewExactText("TextxSuperior", panel, "PARTIDA LOCAL: UN JUGADOR", monoFont, 24f, FontStyles.Normal,
                                    TopTextColor, TextAlignmentOptions.TopLeft, 0f);
        Place(top.rectTransform, TL, new Vector2(0.5f, 0.5f), new Vector2(550f, -209f), new Vector2(1000f, 40f));
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(es, "Construir lobby singleplayer");

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    // =========================================================
    // PARTIDA: resumen + ventana emergente (mismo apartado que el lobby multijugador)
    // =========================================================

    private void BuildGameSettingsUI(Transform panel)
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Transform canvasT = canvas != null ? canvas.rootCanvas.transform : panel.parent;

        // ---- Resumen arriba a la derecha (debajo del título) ----
        GameObject partida = NewUIObject("Partida", panel.gameObject);
        Undo.RegisterCreatedObjectUndo(partida, "Construir partida");
        Place((RectTransform)partida.transform, TR, TR, new Vector2(-80f, -190f), new Vector2(760f, 140f));

        string[] labels = { "MODO", "MAPA", "DIFICULTAD" };
        string[] ids = { "Modo", "Mapa", "Dificultad" };
        TMP_Text[] values = new TMP_Text[3];

        for (int i = 0; i < 3; i++)
        {
            Image box = NewImage("Caja" + ids[i], partida.transform, PanelColor, false);
            Place(box.rectTransform, TL, TL, new Vector2(i * 260f, 0f), new Vector2(240f, 72f));

            TMP_Text label = NewText("Etiqueta", box.transform, labels[i], 16f, RoleColor, TextAlignmentOptions.Left, monoFont, 11f);
            Place(label.rectTransform, TL, TL, new Vector2(12f, -6f), new Vector2(216f, 20f));

            values[i] = NewText(ids[i] + "Valor", box.transform, "-", 26f, Color.white, TextAlignmentOptions.Left, monoFont, 14f);
            Place(values[i].rectTransform, TL, TL, new Vector2(12f, -28f), new Vector2(216f, 38f));
        }

        resModo = values[0];
        resMapa = values[1];
        resDificultad = values[2];

        // ---- Ventana emergente CONFIGURAR PARTIDA (arriba de todo el Canvas) ----
        GameObject popup = NewUIObject("ConfigurarPartidaPopup", canvasT.gameObject);
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

        TMP_Text sub = NewText("Subtitulo", window.transform, "POR AHORA SE ELIGEN ANTES DE ENTRAR AL LOBBY - PROXIMAMENTE SE PODRAN CAMBIAR ACA", 18f, RoleColor, TextAlignmentOptions.Left, monoFont, 12f);
        Place(sub.rectTransform, TL, TL, new Vector2(40f, -86f), new Vector2(920f, 24f));

        string[] sectionNames = { "Seccion_Modo", "Seccion_Mapa", "Seccion_Dificultad" };
        string[] sectionTitles = { "MODO DE JUEGO", "MAPA", "DIFICULTAD" };
        TMP_Text[] popupValues = new TMP_Text[3];

        for (int i = 0; i < 3; i++)
        {
            GameObject section = NewUIObject(sectionNames[i], window.gameObject);
            Place((RectTransform)section.transform, TL, TL, new Vector2(40f, -130f - i * 138f), new Vector2(920f, 124f));

            TMP_Text st = NewText("Titulo", section.transform, sectionTitles[i], 24f, Green, TextAlignmentOptions.Left, monoFont, 14f);
            Place(st.rectTransform, TL, TL, Vector2.zero, new Vector2(920f, 30f));

            // Acá van después los botones de cada opción. Mientras tanto muestra lo que se eligió en el menú.
            Image content = NewImage("Contenido", section.transform, new Color(1f, 1f, 1f, 0.05f), false);
            Place(content.rectTransform, TL, TL, new Vector2(0f, -36f), new Vector2(920f, 84f));

            popupValues[i] = NewText("Valor", content.transform, "-", 30f, Color.white, TextAlignmentOptions.Center, monoFont, 14f);
            Stretch(popupValues[i].rectTransform, 0f);
        }

        popModo = popupValues[0];
        popMapa = popupValues[1];
        popDificultad = popupValues[2];

        // ---- Botones ----
        Button open = MakeButton(partida.transform, "Btn_ConfigurarPartida", "CONFIGURAR PARTIDA", TL, TL, new Vector2(0f, -84f), new Vector2(760f, 52f));

        if (open != null)
            UnityEventTools.AddBoolPersistentListener(open.onClick, popup.SetActive, true);

        Button close = MakeButton(window.transform, "Btn_Cerrar", "CERRAR", BC, BC, new Vector2(0f, 24f), new Vector2(300f, 64f));

        if (close != null)
            UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);

        popup.SetActive(false);
    }

    // Ventana emergente genérica de mensajes (la usa el controlador para "SIN CONFIGURACION", "NO SE PUDO CARGAR", etc.).
    private void BuildMessagePopup(Transform panel, out GameObject popup, out TMP_Text title, out TMP_Text text)
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Transform canvasT = canvas != null ? canvas.rootCanvas.transform : panel.parent;

        popup = NewUIObject("MensajePopup", canvasT.gameObject);
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

        title = NewText("Titulo", window.transform, "AVISO", 48f, new Color(0.70f, 0.79f, 0.71f, 1f), TextAlignmentOptions.Center, titleFont, 26f);
        Place(title.rectTransform, TL, TL, new Vector2(30f, -40f), new Vector2(700f, 64f));

        text = NewText("Mensaje", window.transform, "", 24f, Color.white, TextAlignmentOptions.Top, monoFont, 14f);
        // El mensaje puede ocupar varias líneas.
#pragma warning disable 618
        text.enableWordWrapping = true;
#pragma warning restore 618
        text.enableAutoSizing = false;
        Place(text.rectTransform, TL, TL, new Vector2(40f, -130f), new Vector2(680f, 130f));

        Button ok = MakeButton(window.transform, "Btn_Aceptar", "ACEPTAR", BC, BC, new Vector2(0f, 30f), new Vector2(300f, 64f));

        if (ok != null)
            UnityEventTools.AddBoolPersistentListener(ok.onClick, popup.SetActive, false);

        popup.SetActive(false);
    }

    // =========================================================
    // BOTONES
    // =========================================================

    // Botón verde del lobby multijugador (LISTO / PERSONAJE / CONFIGURAR PARTIDA / CERRAR / ACEPTAR):
    // mismo fondo, contorno, colores de hover, fuente, tamaño y color de texto que PLAY_MULTIPLAYER.
    private Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        Image bg = NewImage(name, parent, BtnBgColor, true);
        bg.sprite = GetButtonSprite();
        bg.type = Image.Type.Sliced;
        Place(bg.rectTransform, anchor, pivot, pos, size);

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.ColorTint;

        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        cb.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        cb.selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        cb.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        b.colors = cb;

        // Mismos componentes que el botón LISTO: el hover (sin referencias asignadas, igual que en el multijugador) y el contorno verde.
        bg.gameObject.AddComponent<ButtonHoverMenuPrincipal>();

        Outline outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = BtnOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        // Texto: serif 32 en negrita, verde, centrado. Se achica solo si el texto no entra (CONFIGURAR PARTIDA, etc.).
        TMP_Text label = NewExactText("Text (TMP)", bg.transform, text, titleFont, 32f, FontStyles.Bold,
                                      BtnTextColor, TextAlignmentOptions.Center, 14f);
        Stretch(label.rectTransform, 0f);

        return b;
    }

    // Botón ATRAS del lobby multijugador (el "Home" de salaEsperaPanel): más chico, azul oscuro, texto gris azulado a la izquierda.
    private Button MakeBackButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        Image bg = NewImage(name, parent, BackBgColor, true);
        bg.sprite = GetButtonSprite();
        bg.type = Image.Type.Sliced;
        Place(bg.rectTransform, anchor, pivot, pos, size);

        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.ColorTint;

        ColorBlock cb = b.colors;
        cb.normalColor = new Color(0.039215688f, 0.07058824f, 0.10980392f, 1f);
        cb.highlightedColor = new Color(0.050980393f, 0.21960784f, 0.1254902f, 1f);
        cb.pressedColor = new Color(0f, 1f, 0.4f, 1f);
        cb.selectedColor = new Color(0.039215688f, 0.07058824f, 0.10980392f, 1f);
        cb.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        b.colors = cb;

        bg.gameObject.AddComponent<ButtonHoverMenuPrincipal>();

        TMP_Text label = NewExactText("Text (TMP)", bg.transform, text, titleFont, 36f, FontStyles.Normal,
                                      BackTextColor, TextAlignmentOptions.Left, 0f);
        Stretch(label.rectTransform, 0f);

        return b;
    }

    // Sprite redondeado que usan los botones de Unity UI (el mismo que tienen los botones del multijugador).
    private static Sprite GetButtonSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    // =========================================================
    // TARJETA DEL JUGADOR
    // =========================================================

    private LobbyPlayerSlot BuildSlot(Transform parent, CharacterPreviewStage stage)
    {
        GameObject slotGO = NewUIObject("Slot1", parent.gameObject);
        Place((RectTransform)slotGO.transform, BC, BC, new Vector2(0f, BottomY), new Vector2(SlotW, BandH));

        // Zona para girar el modelo (transparente, encima del personaje, arriba de la tarjeta)
        Image drag = NewImage("ZonaGiro", slotGO.transform, new Color(0f, 0f, 0f, 0f), true);
        Place(drag.rectTransform, BC, BC, new Vector2(0f, PanelH), new Vector2(SlotW, BandH - PanelH));

        CharacterPreviewDrag dragComp = drag.gameObject.AddComponent<CharacterPreviewDrag>();
        SerializedObject dragSo = new SerializedObject(dragComp);
        SetObj(dragSo, "stage", stage);
        dragSo.FindProperty("slotIndex").intValue = 0;
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

        TMP_Text title = NewText("Titulo", card.transform, "JUGADOR 1", 34f, Color.white, TextAlignmentOptions.Center, monoFont, 20f);
        Place(title.rectTransform, TL, TL, new Vector2(16f, -8f), new Vector2(SlotW - 32f, 38f));

        TMP_Text role = NewText("Rol", card.transform, "[ ESPERANDO JUGADOR ]", 18f, RoleColor, TextAlignmentOptions.Center, monoFont, 12f);
        Place(role.rectTransform, TL, TL, new Vector2(16f, -46f), new Vector2(SlotW - 32f, 22f));
        slotRolText = role;

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
        so.FindProperty("slotIndex").intValue = 0;
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
    // HELPERS (los mismos que LobbyUIBuilder)
    // =========================================================

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
            Debug.LogWarning("[SinglePlayerLobbyUIBuilder] No encuentro el campo '" + field + "' en " + so.targetObject.GetType().Name);
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

    private static GameObject NewUIObject(string name, GameObject parent)
    {
        return NewUIObject(name, parent.transform);
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

    // Texto con los valores EXACTOS de la escena del multijugador (estilo, tamaño y alineación). autoMin > 0 = se achica hasta ese tamaño si no entra.
    private static TMP_Text NewExactText(string name, Transform parent, string text, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align, float autoMin)
    {
        GameObject go = NewUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        if (font != null)
            tmp.font = font;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
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
        else
        {
            tmp.enableAutoSizing = false;
        }

        tmp.overflowMode = TextOverflowModes.Overflow;

        return tmp;
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