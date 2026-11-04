#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UNICO builder del lobby SINGLEPLAYER. Reemplaza el builder 3D antiguo.
/// Copia solo el MUNDO cinematografico de la escena NetworkLobby (no el lobby multiplayer,
/// ni las sesiones ni sus componentes de red). Crea UI local y conecta el controlador existente.
/// No toca la escena NetworkLobby ni el Player.prefab.
/// </summary>
public sealed class SinglePlayerLobbyUIBuilder : EditorWindow
{
    [SerializeField] private SceneAsset escenaMultiplayer;
    [SerializeField] private CharacterCatalog catalog;
    [SerializeField] private Vector3 posicionJugador = new Vector3(-1.2f, -1.26f, -65.6f);
    [SerializeField] private float rotacionJugador = 180f;
    [SerializeField] private string escenaJuego = "MainSceneSinglePlayer";
    [SerializeField] private bool usarSelectorExistente = true;

    private const string PanelName = "salaEsperaSinglePlayerPanel";
    private const string WorldSourceName = "LobbyCinematicWorld_Multiplayer";
    private const string WorldName = "DQSingleLobbyWorld";
    private const string ControllerName = "DQSingleLobbyWorldToggle";
    private const string GeneratedName = "DQSingleLobbyUI";

    private struct LightingProfile
    {
        public bool valid;
        public Material skybox;
        public AmbientMode ambientMode;
        public Color ambientLight, ambientSky, ambientEquator, ambientGround;
        public float ambientIntensity, reflectionIntensity;
        public bool fog;
        public FogMode fogMode;
        public Color fogColor;
        public float fogDensity, fogStart, fogEnd;
    }

    // RenderSettings son especificos de la escena activa en el editor.
    // Solo LEE la iluminacion de NetworkLobby; no cambia el asset de origen.
    private static LightingProfile ReadLightingProfile(Scene source, Scene destination)
    {
        LightingProfile p = new LightingProfile();
        try
        {
            if (!SceneManager.SetActiveScene(source)) return p;
            p.skybox = RenderSettings.skybox;
            p.ambientMode = RenderSettings.ambientMode;
            p.ambientLight = RenderSettings.ambientLight;
            p.ambientSky = RenderSettings.ambientSkyColor;
            p.ambientEquator = RenderSettings.ambientEquatorColor;
            p.ambientGround = RenderSettings.ambientGroundColor;
            p.ambientIntensity = RenderSettings.ambientIntensity;
            p.reflectionIntensity = RenderSettings.reflectionIntensity;
            p.fog = RenderSettings.fog;
            p.fogMode = RenderSettings.fogMode;
            p.fogColor = RenderSettings.fogColor;
            p.fogDensity = RenderSettings.fogDensity;
            p.fogStart = RenderSettings.fogStartDistance;
            p.fogEnd = RenderSettings.fogEndDistance;
            p.valid = true;
            return p;
        }
        finally
        {
            SceneManager.SetActiveScene(destination);
        }
    }

    private static readonly Color Accent = new Color(0.10f, 0.95f, 0.50f, 1f);
    private static readonly Color Main = new Color(0.93f, 0.96f, 0.98f, 1f);
    private static readonly Color Dim = new Color(0.62f, 0.72f, 0.78f, 1f);
    private static readonly Color Panel = new Color(0.015f, 0.025f, 0.035f, 0.77f);

    [MenuItem("Game/Dead Quarantine/Lobby/Construir Singleplayer Cinematografico")]
    private static void Open()
    {
        GetWindow<SinglePlayerLobbyUIBuilder>("Lobby Singleplayer");
    }

    private void OnEnable()
    {
        if (escenaMultiplayer == null)
        {
            string[] guids = AssetDatabase.FindAssets("NetworkLobby t:Scene");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == "NetworkLobby")
                {
                    escenaMultiplayer = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    break;
                }
            }
        }
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
            "Abrí PantallasUI. COPIA LITERAL de los GameObjects visuales guardados en NetworkLobby " +
            "(UI, degradados, botones, márgenes, fuentes y retratos). Solo cambia título, sesión local, " +
            "JUGADORES 1/1 y COMENZAR. No reconstruye ni modifica NetworkLobby.", MessageType.Info);
        escenaMultiplayer = (SceneAsset)EditorGUILayout.ObjectField("Escena multiplayer de referencia", escenaMultiplayer, typeof(SceneAsset), false);
        catalog = (CharacterCatalog)EditorGUILayout.ObjectField("Catalogo de personajes", catalog, typeof(CharacterCatalog), false);
        escenaJuego = EditorGUILayout.TextField("Escena al comenzar", escenaJuego);
        posicionJugador = EditorGUILayout.Vector3Field("Posicion del personaje", posicionJugador);
        rotacionJugador = EditorGUILayout.FloatField("Rotacion del personaje", rotacionJugador);
        usarSelectorExistente = EditorGUILayout.ToggleLeft("Usar CharacterSelectCanvas existente (si lo hay)", usarSelectorExistente);
        EditorGUILayout.Space();
        if (GUILayout.Button("CONSTRUIR / ACTUALIZAR LOBBY SINGLEPLAYER", GUILayout.Height(38)))
            Build();
    }

    private void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Error("Salí del Play Mode antes de construir."); return; }
        Scene dest = SceneManager.GetActiveScene();
        string sourcePath = escenaMultiplayer != null ? AssetDatabase.GetAssetPath(escenaMultiplayer) : "";
        if (!dest.IsValid() || string.IsNullOrEmpty(dest.path)) { Error("Guardá primero la escena del menú."); return; }
        if (string.IsNullOrEmpty(sourcePath) || sourcePath == dest.path) { Error("Elegí la escena NetworkLobby, distinta de la escena actual."); return; }
        if (catalog == null) { Error("Asigná CharacterCatalog."); return; }
        if (string.IsNullOrWhiteSpace(escenaJuego)) { Error("Indicá el nombre de la escena que carga la partida."); return; }

        // Prevalidar la escena fuente ANTES de editar cualquier objeto de la escena destino.
        Scene source = SceneManager.GetSceneByPath(sourcePath);
        bool openedHere = !source.isLoaded;
        if (openedHere) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
        GameObject sourceWorld = FindRoot(source, WorldSourceName);
        if (sourceWorld == null)
        {
            if (openedHere) EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(dest);
            Error("No encontré '" + WorldSourceName + "' en NetworkLobby. Guardá antes esa escena después de construir su lobby cinematográfico.");
            return;
        }
        LobbyCinematicPlayerDisplay[] sourceActors = sourceWorld.GetComponents<LobbyCinematicPlayerDisplay>();
        Camera sourceCamera = sourceWorld.GetComponentInChildren<Camera>(true);
        // Reutilizar los elementos REALES de la UI multiplayer, no dibujar imitaciones.
        Transform sourcePanel = FindDeepInScene(source, "salaEsperaPanel");
        Transform sourceCinematicUi = sourcePanel != null ? FindDeep(sourcePanel, "LobbyCinematicUI") : null;
        if (sourcePanel == null || sourceCinematicUi == null ||
            sourceCinematicUi.Find("PartyCard") == null || sourceCinematicUi.Find("SelectionCard") == null)
        {
            if (openedHere) EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(dest);
            Error("La escena NetworkLobby guardada no contiene LobbyCinematicUI (PartyCard / SelectionCard). Guardá esa escena con el lobby multiplayer que ya funciona y volvé a construir el singleplayer.");
            return;
        }
        // Sin fallback gráfico: para garantizar fidelidad, exigimos los objetos de la escena real.
        foreach (string required in new[] { "LobbyCinematicBackdrop", "Titulo", "Home", "Btn_Personaje", "PLAY_MULTIPLAYER", "Btn_ConfigurarPartida", "TextxSuperior (1)", "Cuadrado", "placeholder_codigo", "copiar_codigo" })
        {
            if (FindDeep(sourcePanel, required) == null)
            {
                if (openedHere) EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(dest);
                Error("No encontré '" + required + "' en NetworkLobby guardada. No voy a inventar un estilo distinto. Guardá el multiplayer que funciona y reintentá.");
                return;
            }
        }
        // El catálogo NO se serializa en LobbyPlayerSlot (se asigna en runtime).
        // Tomarlo del MultiplayerLobbyController real, donde sí está serializado.
        MultiplayerLobbyController sourceController = FindComponentInScene<MultiplayerLobbyController>(source);
        if (sourceController != null)
        {
            SerializedProperty catalogProperty = new SerializedObject(sourceController).FindProperty("catalogoPersonajes");
            CharacterCatalog multiplayerCatalog = catalogProperty != null
                ? catalogProperty.objectReferenceValue as CharacterCatalog : null;
            if (multiplayerCatalog != null) catalog = multiplayerCatalog;
        }
        if (catalog == null)
        {
            CharacterSelectMenu sourceSelector = FindComponentInScene<CharacterSelectMenu>(source);
            if (sourceSelector != null)
            {
                SerializedProperty catalogProperty = new SerializedObject(sourceSelector).FindProperty("catalog");
                CharacterCatalog selectorCatalog = catalogProperty != null
                    ? catalogProperty.objectReferenceValue as CharacterCatalog : null;
                if (selectorCatalog != null) catalog = selectorCatalog;
            }
        }
        if (sourceActors.Length == 0 || sourceCamera == null)
        {
            if (openedHere) EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(dest);
            Error("La referencia multiplayer no contiene cámara y LobbyCinematicPlayerDisplay. Necesito el lobby cinematográfico actual guardado.");
            return;
        }

        LightingProfile lighting = ReadLightingProfile(source, dest);
        SceneManager.SetActiveScene(dest);
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Crear lobby singleplayer cinematográfico");
        int group = Undo.GetCurrentGroup();
        try
        {
            // El panel antiguo puede existir directamente en la escena (SIN Canvas).
            // Conservamos el MISMO GameObject: SingleplayerMenuManager puede referenciarlo.
            // Lo movemos a un Canvas independiente, fuera de los paneles del menú que se ocultan.
            RectTransform lobbyPanel = FindDeepInScene(dest, PanelName) as RectTransform;
            Canvas canvas = EnsureSingleplayerCanvas(dest);
            CopyCanvasScaleFromSource(sourcePanel, canvas);
            if (lobbyPanel == null)
            {
                GameObject panelGO = NewUI(PanelName, canvas.transform);
                lobbyPanel = (RectTransform)panelGO.transform;
            }
            else if (lobbyPanel.parent != canvas.transform)
            {
                Undo.SetTransformParent(lobbyPanel, canvas.transform, "Mover panel singleplayer al Canvas");
            }
            // En un Canvas overlay los offsets heredados de un objeto raíz pueden dejarlo fuera
            // de pantalla. El lobby ocupa la pantalla completa, como en el multiplayer.
            Undo.RecordObject(lobbyPanel, "Ajustar panel singleplayer a pantalla");
            Stretch(lobbyPanel);
            lobbyPanel.gameObject.layer = LayerMask.NameToLayer("UI");

            // Limpieza acotada: los objetos del multiplayer NUNCA se borran.
            RemoveNamedInScene(dest, WorldName);
            RemoveNamedInScene(dest, ControllerName);
            RemoveNamedChild(lobbyPanel, GeneratedName);
            RemoveLegacySingleplayer(lobbyPanel, canvas);

            GameObject world = Instantiate(sourceWorld);
            world.name = WorldName;
            SceneManager.MoveGameObjectToScene(world, dest);
            Undo.RegisterCreatedObjectUndo(world, "Copiar mundo visual del multiplayer");

            LobbyCinematicPartyPresenter party = world.GetComponent<LobbyCinematicPartyPresenter>();
            if (party != null) Undo.DestroyObjectImmediate(party);

            LobbyCinematicPlayerDisplay[] actors = world.GetComponents<LobbyCinematicPlayerDisplay>();
            LobbyCinematicPlayerDisplay actor = actors[0];
            if (actor == null || actor.Anchor == null || !actor.Anchor.IsChildOf(world.transform))
                throw new InvalidOperationException("El jugador visual de referencia no tiene su anchor dentro del mundo clonado.");
            for (int i = 1; i < actors.Length; i++)
            {
                Transform anchor = actors[i].Anchor;
                Undo.DestroyObjectImmediate(actors[i]);
                if (anchor != null && anchor.IsChildOf(world.transform))
                    Undo.DestroyObjectImmediate(anchor.gameObject);
            }
            Camera cam = world.GetComponentInChildren<Camera>(true);
            AudioListener[] listeners = world.GetComponentsInChildren<AudioListener>(true);
            foreach (AudioListener listener in listeners) Undo.DestroyObjectImmediate(listener);
            if (cam == null || actor == null) throw new InvalidOperationException("No se pudieron conservar la cámara y el primer personaje.");
            actor.SetPose(posicionJugador, rotacionJugador);

            GameObject toggleGO = new GameObject(ControllerName);
            Undo.RegisterCreatedObjectUndo(toggleGO, "Activación mundo lobby");
            LobbyCinematicWorldToggle toggle = toggleGO.AddComponent<LobbyCinematicWorldToggle>();
            toggle.Configure(lobbyPanel.gameObject, world);

            // La UI nueva se crea sobre el panel real. No hay código de sala ni botones remotos.
            GameObject ui = NewUI(GeneratedName, lobbyPanel);
            Stretch((RectTransform)ui.transform);
            BuildUi(ui.transform, lobbyPanel, canvas, world, cam, actor, sourcePanel, sourceCinematicUi, lighting);

            Image oldBackground = lobbyPanel.GetComponent<Image>();
            if (oldBackground != null)
            {
                Undo.RecordObject(oldBackground, "Mostrar escenario detrás del lobby");
                oldBackground.enabled = false;
            }
            // El NetworkLobby está abierto temporalmente como escena aditiva.
            // NO usar FindObjectOfType global: podría tomar el EventSystem de esa escena.
            EnsureEventSystem(dest);

            // Vincular explícitamente el panel que acabamos de reutilizar/crear.
            // No se reemplaza el manager ni se alteran los flujos del menú existente.
            SingleplayerMenuManager menu = FindComponentInScene<SingleplayerMenuManager>(dest);
            if (menu != null)
            {
                SerializedObject menuSO = new SerializedObject(menu);
                SerializedProperty panelProperty = menuSO.FindProperty("lobbyPanel");
                SerializedProperty controllerProperty = menuSO.FindProperty("lobbyController");
                if (panelProperty != null) panelProperty.objectReferenceValue = lobbyPanel.gameObject;
                if (controllerProperty != null)
                    controllerProperty.objectReferenceValue = FindComponentInScene<SinglePlayerLobbyController>(dest);
                menuSO.ApplyModifiedProperties();
            }
            Undo.RecordObject(lobbyPanel.gameObject, "Iniciar lobby cerrado");
            lobbyPanel.gameObject.SetActive(false);
            world.SetActive(false);
            EditorSceneManager.MarkSceneDirty(dest);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = lobbyPanel.gameObject;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Undo.RevertAllDownToGroup(group);
            Error("No pude crear el lobby. Se revirtieron los cambios del builder.\n" + ex.Message);
            return;
        }
        finally
        {
            if (openedHere && source.isLoaded) EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(dest);
        }
        EditorUtility.DisplayDialog("Lobby singleplayer", "Lobby cinematográfico creado. Guardá PantallasUI y probá UN JUGADOR. El multiplayer no se modificó.", "OK");
    }

    private void BuildUi(Transform ui, RectTransform lobbyPanel, Canvas canvas,
        GameObject world, Camera cam, LobbyCinematicPlayerDisplay actor,
        Transform multiplayerPanel, Transform multiplayerUi, LightingProfile lighting)
    {
        // No dibujamos aproximaciones: TODOS los elementos del lobby en pantalla
        // provienen de GameObjects reales de la escena NetworkLobby ya guardada.
        Transform sourceBack = RequireDirect(multiplayerPanel, "LobbyCinematicBackdrop");
        CloneVisual(sourceBack, ui, "LobbyCinematicBackdrop").transform.SetAsFirstSibling();

        TMP_FontAsset font = multiplayerUi.GetComponentInChildren<TMP_Text>(true)?.font;
        if (font == null)
            throw new InvalidOperationException("La UI multiplayer no tiene ninguna fuente TMP. No se creó un reemplazo visual.");

        // Elementos DIRECTOS del panel: el multiplayer reubica estos objetos raiz.
        // FindDeep seleccionaba a veces un "Titulo" viejo de un submenu y alteraba fuente/posicion.
        TMP_Text title = RequireCopiedText(multiplayerPanel, ui, "Titulo", "UN JUGADOR // SALA");
        Button back = CloneButtonExact(multiplayerPanel, ui, "Home", "Atras", "ATRAS");
        RequireCopiedText(multiplayerPanel, ui, "TextxSuperior (1)", "SESION:");
        GameObject codeBox = CloneVisual(RequireDirect(multiplayerPanel, "Cuadrado"), ui, "Cuadrado_Local");
        Image codeBoxImage = codeBox.GetComponent<Image>();
        if (codeBoxImage != null) codeBoxImage.enabled = true;
        CanvasGroup codeBoxGroup = codeBox.GetComponent<CanvasGroup>();
        if (codeBoxGroup != null) codeBoxGroup.alpha = 1f;
        RequireCopiedText(multiplayerPanel, ui, "placeholder_codigo", "LOCAL");
        Button noCode = CloneButtonExact(multiplayerPanel, ui, "copiar_codigo", "EstadoLocal", "SIN RED");
        // El multiplayer usa la misma barra en esta posicion. No agregar ni quitar elementos.
        FixOriginalLobbyHeader(ui);
        // Mantener el mismo gráfico/rectángulo que COPIAR, pero no es un botón funcional.
        noCode.onClick = new Button.ButtonClickedEvent();
        noCode.enabled = false;

        // Bottom: mismos GameObjects, sprites, estilos, fuentes y posiciones del multiplayer.
        Button character = CloneButtonExact(multiplayerPanel, ui, "Btn_Personaje", "Personaje", "PERSONAJE");
        Button start = CloneButtonExact(multiplayerPanel, ui, "PLAY_MULTIPLAYER", "Comenzar", "COMENZAR");
        Button config = CloneButtonExact(multiplayerPanel, ui, "Btn_ConfigurarPartida", "Configurar", "CONFIGURAR PARTIDA");

        // Clonar el root visual completo: TODAS las imágenes, tarjetas, bordes y tipografías.
        GameObject exact = CloneVisual(multiplayerUi, ui, "LobbyCinematicUI_Original");
        RectTransform exactRT = exact.GetComponent<RectTransform>();
        if (exactRT == null)
            throw new InvalidOperationException("LobbyCinematicUI no tiene RectTransform.");
        // Clon real (no redibujado) y mismo orden de render que la UI fuente.
        Transform partyCard = exact.transform.Find("PartyCard");
        Transform selectionCard = exact.transform.Find("SelectionCard");
        if (partyCard == null || selectionCard == null)
            throw new InvalidOperationException("No encontré PartyCard / SelectionCard en el clon exacto.");

        // ÚNICA diferencia estructural: 1 jugador, no 4. Mantener la fila 1 tal cual.
        RectTransform partyRect = partyCard as RectTransform;
        float rowHeight = 52f;
        Transform row1 = partyCard.Find("Row_1");
        Transform headerLine = partyCard.Find("HeaderLine");
        if (row1 == null || row1.Find("Icon") == null || row1.Find("Text") == null)
            throw new InvalidOperationException("PartyCard no contiene Row_1 con Icon y Text.");
        if (partyRect != null)
        {
            // Conservar tamaño horizontal, anclajes, encabezado y estética originales.
            float headerHeight = headerLine != null
                ? Mathf.Abs(((RectTransform)headerLine).anchoredPosition.y) + 2f : 46f;
            partyRect.sizeDelta = new Vector2(partyRect.sizeDelta.x, headerHeight + rowHeight + 8f);
        }
        for (int i = 2; i <= 4; i++)
        {
            Transform row = partyCard.Find("Row_" + i);
            if (row != null) row.gameObject.SetActive(false);
            Transform otherPlate = exact.transform.Find("Nameplate_" + i);
            if (otherPlate != null) Undo.DestroyObjectImmediate(otherPlate.gameObject);
        }
        row1.gameObject.SetActive(true);
        Image icon = row1.Find("Icon").GetComponent<Image>();
        TMP_Text partyRowText = row1.Find("Text").GetComponent<TMP_Text>();
        TMP_Text partyHeader = partyCard.Find("Header")?.GetComponent<TMP_Text>();
        if (partyHeader != null) partyHeader.text = "JUGADORES 1/1";
        if (partyRowText != null) partyRowText.text = "JUGADOR";
        if (icon != null)
        {
            icon.preserveAspect = true;
            icon.color = Color.white;
        }
        TMP_Text selectedMode = selectionCard.Find("ModeDifficulty")?.GetComponent<TMP_Text>();
        TMP_Text selectedMap = selectionCard.Find("Map")?.GetComponent<TMP_Text>();
        if (selectedMode == null || selectedMap == null || icon == null || partyRowText == null)
            throw new InvalidOperationException("Faltan elementos del clon original para conectar datos locales.");
        RectTransform plate = exact.transform.Find("Nameplate_1") as RectTransform;
        TMP_Text plateText = plate != null ? plate.Find("Name")?.GetComponent<TMP_Text>() : null;
        if (plate == null || plateText == null)
            throw new InvalidOperationException("La UI de NetworkLobby no contiene Nameplate_1/Name.");
        Transform badge = plate.Find("ReadyBadge");
        if (badge != null) badge.gameObject.SetActive(false);
        plate.gameObject.SetActive(false);

        // Copiar también las ventanas modales reales, cuando están guardadas en NetworkLobby.
        GameObject configPopup;
        TMP_Text cfgMode, cfgMap, cfgDifficulty;
        // En NetworkLobby puede haber varios popups con el mismo nombre (incluidos restos
        // de versiones antiguas). Solo clonar uno que tenga las TRES filas funcionales.
        // El LobbyUIBuilder original también llama "Pendiente" a los valores anteriores.
        Transform sourceSettings = FindCompatibleSettingsPopup(multiplayerPanel.gameObject.scene, multiplayerPanel);
        if (sourceSettings != null)
        {
            configPopup = CloneVisual(sourceSettings, canvas.transform, "ConfigurarPartidaPopup");
            cfgMode = SectionValue(configPopup.transform, "Seccion_Modo", true);
            cfgMap = SectionValue(configPopup.transform, "Seccion_Mapa", true);
            cfgDifficulty = SectionValue(configPopup.transform, "Seccion_Dificultad", true);
            RebindPopupClose(configPopup, "Cerrar");
            configPopup.SetActive(false);
        }
        else
        {
            // Este es el diseño del LobbyUIBuilder multiplayer original (1000x640,
            // tipografias, filas y colores originales). Evita abortar TODO el lobby
            // al encontrar un popup antiguo o de una jerarquia incompatible.
            Debug.LogWarning("[SinglePlayerLobby] NetworkLobby no tiene un ConfigurarPartidaPopup " +
                "compatible con las tres filas editables. Se usa la plantilla visual " +
                "del LobbyUIBuilder multiplayer original. No se modifica NetworkLobby.");
            CreateSettingsPopup(canvas.transform, multiplayerPanel, font,
                out configPopup, out cfgMode, out cfgMap, out cfgDifficulty);
        }

        GameObject messagePopup;
        TMP_Text messageTitle, messageText;
        Transform sourceMessage = FindDeepInScene(multiplayerPanel.gameObject.scene, "MensajePopup");
        if (sourceMessage != null && FindDeep(sourceMessage, "Titulo") != null &&
            (FindDeep(sourceMessage, "Texto") != null || FindDeep(sourceMessage, "Mensaje") != null))
        {
            messagePopup = CloneVisual(sourceMessage, canvas.transform, "MensajePopup");
            messageTitle = FindDeep(messagePopup.transform, "Titulo")?.GetComponent<TMP_Text>();
            Transform messageBody = FindDeep(messagePopup.transform, "Texto");
            if (messageBody == null) messageBody = FindDeep(messagePopup.transform, "Mensaje");
            messageText = messageBody != null ? messageBody.GetComponent<TMP_Text>() : null;
            RebindPopupClose(messagePopup, "Cerrar");
            messagePopup.SetActive(false);
        }
        else
            CreateMessagePopup(canvas.transform, multiplayerPanel, font,
                out messagePopup, out messageTitle, out messageText);

        // La selección original queda por ENCIMA pero no oculta el mundo ni la UI del fondo.
        GameObject selector = usarSelectorExistente
            ? FindDeepInScene(SceneManager.GetActiveScene(), "CharacterSelectCanvas")?.gameObject : null;
        SinglePlayerCinematicPresenter presenter = world.AddComponent<SinglePlayerCinematicPresenter>();
        presenter.Configure(actor, cam, lobbyPanel, plate, plateText,
            null, null, selectedMode, selectedMap, icon, catalog,
            posicionJugador, rotacionJugador);
        presenter.ConfigurePartyRow(partyRowText);
        if (selector == null)
            selector = CreateFallbackSelector(canvas.transform, font, presenter);
        else if (selector.transform.IsChildOf(lobbyPanel))
            Undo.SetTransformParent(selector.transform, canvas.transform, "Separar selector del lobby");
        presenter.ConfigureSelector(selector);
        // Mismo oscurecimiento que el selector real del NetworkLobby, NO alpha inventado.
        Transform sourceSelector = FindDeepInScene(multiplayerPanel.gameObject.scene, "CharacterSelectCanvas");
        Transform sourceShade = sourceSelector != null ? FindDeep(sourceSelector, "Fondo") : null;
        Image sourceShadeImage = sourceShade != null ? sourceShade.GetComponent<Image>() : null;
        presenter.ConfigureSelectorBackgroundColor(sourceShadeImage != null
            ? sourceShadeImage.color : new Color(0.03f, 0.04f, 0.05f, 0.94f));
        if (lighting.valid)
            presenter.ConfigureLighting(lighting.skybox, lighting.ambientMode,
                lighting.ambientLight, lighting.ambientSky, lighting.ambientEquator,
                lighting.ambientGround, lighting.ambientIntensity, lighting.reflectionIntensity,
                lighting.fog, lighting.fogMode, lighting.fogColor,
                lighting.fogDensity, lighting.fogStart, lighting.fogEnd);
        Canvas selectorCanvas = selector.GetComponent<Canvas>();
        if (selectorCanvas == null) selectorCanvas = Undo.AddComponent<Canvas>(selector);
        if (selector.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(selector);
        selectorCanvas.overrideSorting = true;
        selectorCanvas.sortingOrder = 100;

        CanvasGroup group = lobbyPanel.GetComponent<CanvasGroup>();
        if (group == null) group = Undo.AddComponent<CanvasGroup>(lobbyPanel.gameObject);
        presenter.ConfigureHudGroup(group);
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        // Zona invisible de arrastre: detrás de botones y tarjetas, sin alterar su diseño.
        Image dragZone = Image(exact.transform, "GirarPersonaje", new Color(0, 0, 0, 0), true);
        Place(dragZone.rectTransform, new Vector2(.5f, .5f), new Vector2(-100, -80), new Vector2(480, 600));
        EventTrigger drag = dragZone.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        UnityEventTools.AddPersistentListener(entry.callback, presenter.RotateFromDrag);
        drag.triggers.Add(entry);
        dragZone.transform.SetAsFirstSibling();

        SinglePlayerLobbyController ctrl = FindComponentInScene<SinglePlayerLobbyController>(SceneManager.GetActiveScene());
        if (ctrl == null)
        {
            GameObject go = new GameObject("SinglePlayerLobbyController");
            Undo.RegisterCreatedObjectUndo(go, "Controlador singleplayer");
            ctrl = go.AddComponent<SinglePlayerLobbyController>();
        }
        GameObject slotGO = NewUI("DQSingleLobbyDataSlot", ui);
        LobbyPlayerSlot slot = slotGO.AddComponent<LobbyPlayerSlot>();
        SerializedObject slotSO = new SerializedObject(slot);
        slotSO.FindProperty("slotIndex").intValue = 0;
        slotSO.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject so = new SerializedObject(ctrl);
        Bind(so, "slot", slot);
        Bind(so, "catalogoPersonajes", catalog);
        Bind(so, "escenarioPersonajes", null);
        Bind(so, "rolTexto", null);
        Bind(so, "btnComenzar", start);
        Bind(so, "btnPersonaje", character);
        Bind(so, "btnVolver", back);
        Bind(so, "resumenModo", null);
        Bind(so, "resumenMapa", null);
        Bind(so, "resumenDificultad", null);
        Bind(so, "popupModo", cfgMode);
        Bind(so, "popupMapa", cfgMap);
        Bind(so, "popupDificultad", cfgDifficulty);
        Bind(so, "popupMensaje", messagePopup);
        Bind(so, "popupMensajeTitulo", messageTitle);
        Bind(so, "popupMensajeTexto", messageText);
        Bind(so, "ventanaSeleccionPersonaje", selector);
        so.FindProperty("escenaJuegoSingleplayer").stringValue = escenaJuego;
        so.ApplyModifiedProperties();
        UnityEventTools.AddPersistentListener(back.onClick, ctrl.OnClick_Volver);
        UnityEventTools.AddPersistentListener(character.onClick, ctrl.OnClick_Personaje);
        UnityEventTools.AddPersistentListener(start.onClick, ctrl.OnClick_Comenzar);
        UnityEventTools.AddBoolPersistentListener(config.onClick, configPopup.SetActive, true);
        configPopup.SetActive(false);
        messagePopup.SetActive(false);
        selector.SetActive(false);
    }

    // Se valida ANTES de clonar para no elegir el primer popup homonimo incorrecto.
    private static Transform FindCompatibleSettingsPopup(Scene source, Transform referencePanel)
    {
        if (!source.IsValid() || !source.isLoaded) return null;
        // Priorizar el popup del MISMO Canvas del lobby multiplayer visible.
        // Así no se copia por accidente otro popup antiguo de la misma escena.
        Canvas referenceCanvas = referencePanel != null
            ? referencePanel.GetComponentInParent<Canvas>(true) : null;
        Transform best = null;
        int bestScore = int.MinValue;
        foreach (GameObject root in source.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "ConfigurarPartidaPopup") continue;
                if (SectionValue(candidate, "Seccion_Modo") == null ||
                    SectionValue(candidate, "Seccion_Mapa") == null ||
                    SectionValue(candidate, "Seccion_Dificultad") == null)
                    continue;

                Transform close = FindDeep(candidate, "Cerrar");
                if (close == null) close = FindDeep(candidate, "Btn_Cerrar");
                if (close == null) close = FindDeep(candidate, "Btn_Aceptar");
                if (close == null ||
                    (close.GetComponent<Button>() == null &&
                     close.GetComponentInChildren<Button>(true) == null))
                    continue;

                Canvas candidateCanvas = candidate.GetComponentInParent<Canvas>(true);
                int score = referenceCanvas != null && candidateCanvas == referenceCanvas ? 100 : 0;
                if (candidate.gameObject.activeInHierarchy) score += 10;
                if (FindDeep(candidate, "Ventana") != null) score += 2;
                if (score > bestScore) { best = candidate; bestScore = score; }
            }
        }
        return best;
    }

    private static TMP_Text SectionValue(Transform popup, string sectionName, bool normalizeName = false)
    {
        Transform section = FindDeep(popup, sectionName);
        Transform content = section != null ? FindDeep(section, "Contenido") : null;
        if (content == null) return null;
        Transform value = FindDeep(content, "Valor");
        if (value == null) value = FindDeep(content, "Pendiente");
        if (value == null) return null;
        TMP_Text text = value.GetComponent<TMP_Text>();
        if (text != null && normalizeName && value.name == "Pendiente")
            value.name = "Valor"; // Solo modifica el CLON; nunca la escena multiplayer.
        return text;
    }

    private static void RebindPopupClose(GameObject popup, string closeName)
    {
        // El popup multiplayer original llama Btn_Cerrar / Btn_Aceptar,
        // mientras algunos menus viejos llaman Cerrar. No heredar eventos de red.
        Transform closeTransform = FindDeep(popup.transform, closeName);
        if (closeTransform == null) closeTransform = FindDeep(popup.transform, "Btn_Cerrar");
        if (closeTransform == null) closeTransform = FindDeep(popup.transform, "Btn_Aceptar");
        Button close = closeTransform != null ? closeTransform.GetComponent<Button>() : null;
        if (close == null && closeTransform != null)
            close = closeTransform.GetComponentInChildren<Button>(true);
        if (close == null)
            throw new InvalidOperationException("El popup clonado no tiene boton Cerrar/Aceptar; no se copio una accion multiplayer insegura.");
        close.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);
    }

    private static TMP_Text RequireCopiedText(Transform source, Transform parent, string originalName, string value)
    {
        Transform original = RequireDirect(source, originalName);
        GameObject copy = CloneVisual(original, parent, "SP_" + originalName);
        TMP_Text tmp = copy.GetComponent<TMP_Text>();
        if (tmp == null) tmp = copy.GetComponentInChildren<TMP_Text>(true);
        if (tmp == null)
            throw new InvalidOperationException("El objeto original no contiene TMP_Text: " + originalName);
        tmp.text = value;
        return tmp;
    }

    private static Button CloneButtonExact(Transform source, Transform parent, string originalName,
        string targetName, string value)
    {
        Transform original = RequireDirect(source, originalName);
        GameObject clone = CloneVisual(original, parent, targetName);
        Button button = clone.GetComponent<Button>();
        if (button == null) button = clone.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            // Texto con un Button transparente: respeta por completo la apariencia original.
            Image image = clone.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(clone);
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;
            button = Undo.AddComponent<Button>(clone);
            button.targetGraphic = image;
        }
        button.onClick = new Button.ButtonClickedEvent(); // Jamás heredar listeners de red.
        TMP_Text label = clone.GetComponent<TMP_Text>();
        if (label == null) label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
        return button;
    }

    private static void CopyCanvasScaleFromSource(Transform sourcePanel, Canvas targetCanvas)
    {
        Canvas parentCanvas = sourcePanel.GetComponentInParent<Canvas>(true);
        CanvasScaler sourceScaler = parentCanvas != null ? parentCanvas.GetComponent<CanvasScaler>() : null;
        CanvasScaler target = targetCanvas.GetComponent<CanvasScaler>();
        if (sourceScaler == null || target == null) return;
        Undo.RecordObject(target, "Reutilizar escala exacta del Canvas multiplayer");
        target.uiScaleMode = sourceScaler.uiScaleMode;
        target.referenceResolution = sourceScaler.referenceResolution;
        target.screenMatchMode = sourceScaler.screenMatchMode;
        target.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
        target.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        target.scaleFactor = sourceScaler.scaleFactor;
    }

    // Copia objetos reales guardados en NetworkLobby. En los botones se limpian los
    // listeners multiplayer con CloneButtonExact para no conservar acciones de red.
    private static GameObject CloneVisual(Transform source, Transform parent, string ownName)
    {
        if (source == null)
            throw new InvalidOperationException("No se encontró el elemento visual original: " + ownName);
        GameObject result = Instantiate(source.gameObject, parent, false);
        result.name = ownName;
        result.SetActive(true);
        Undo.RegisterCreatedObjectUndo(result, "Copiar UI original multiplayer");
        return result;
    }

    private static Transform RequireDirect(Transform sourcePanel, string name)
    {
        Transform direct = sourcePanel != null ? sourcePanel.Find(name) : null;
        if (direct == null)
            throw new InvalidOperationException("El NetworkLobby no tiene el objeto directo '" + name +
                "'. Guardalo con su lobby cinematografico antes de construir el singleplayer.");
        return direct;
    }

    private static void FixOriginalLobbyHeader(Transform ui)
    {
        // Replica la jerarquia y medidas finales usadas por LobbyCinematicRebuildBuilder.
        // Estas coordenadas son las del builder MULTIPLAYER, no valores inventados para SP.
        PlaceOriginalText(ui.Find("SP_Titulo"), new Vector2(0f, 1f), new Vector2(250f, -30f),
            new Vector2(1000f, 56f), 44f, TextAlignmentOptions.MidlineLeft);
        PlaceOriginalText(ui.Find("Atras"), new Vector2(0f, 1f), new Vector2(40f, -30f),
            new Vector2(190f, 56f), 30f, TextAlignmentOptions.Center);
        PlaceOriginalText(ui.Find("SP_TextxSuperior (1)"), new Vector2(0f, 1f), new Vector2(40f, -106f),
            new Vector2(260f, 52f), 30f, TextAlignmentOptions.MidlineLeft);
        PlaceOriginalRect(ui.Find("Cuadrado_Local") as RectTransform, new Vector2(0f, 1f),
            new Vector2(306f, -106f), new Vector2(250f, 52f));
        PlaceOriginalText(ui.Find("SP_placeholder_codigo"), new Vector2(0f, 1f),
            new Vector2(306f, -106f), new Vector2(250f, 52f), 34f, TextAlignmentOptions.Center);
        PlaceOriginalText(ui.Find("EstadoLocal"), new Vector2(0f, 1f), new Vector2(572f, -106f),
            new Vector2(190f, 52f), 28f, TextAlignmentOptions.Center);
    }

    private static void PlaceOriginalRect(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static void PlaceOriginalText(Transform root, Vector2 anchor, Vector2 pos, Vector2 size,
        float fontSize, TextAlignmentOptions alignment)
    {
        if (root == null) return;
        PlaceOriginalRect(root as RectTransform, anchor, pos, size);
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.margin = Vector4.zero;
            text.overflowMode = TextOverflowModes.Overflow;
        }
        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = true;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private GameObject CreateFallbackSelector(Transform parent, TMP_FontAsset font, SinglePlayerCinematicPresenter presenter)
    {
        GameObject overlay = NewUI("DQSingleLobbySelector", parent);
        Stretch((RectTransform)overlay.transform);
        Image dim = overlay.AddComponent<Image>(); dim.color = new Color(0, 0, 0, .86f);
        Image window = Image(overlay.transform, "Ventana", new Color(.035f, .06f, .065f, .98f), true);
        window.rectTransform.anchorMin = window.rectTransform.anchorMax = new Vector2(.5f, .5f);
        window.rectTransform.pivot = new Vector2(.5f, .5f);
        window.rectTransform.sizeDelta = new Vector2(880, 700);
        window.rectTransform.anchoredPosition = Vector2.zero;
        Text(window.transform, "Titulo", "SELECCIONAR PERSONAJE", font, 36, Accent, new Vector2(0, 1), new Vector2(30, -25), new Vector2(800, 50), TextAlignmentOptions.MidlineLeft);
        int count = Mathf.Min(8, catalog.VariantCount);
        for (int i = 0; i < count; i++)
        {
            CharacterCatalog.Variant v = catalog.GetVariant(i);
            if (v == null) continue;
            int index = i;
            string label = catalog.GetCharacterNameForVariant(i);
            if (string.IsNullOrWhiteSpace(label)) label = v.type.ToString();
            Button item = MakeButton(window.transform, "Variante_" + i, label.ToUpperInvariant(), font,
                new Vector2(.5f, 1), new Vector2(0, -92 - i * 66), new Vector2(760, 58), false);
            UnityEventTools.AddIntPersistentListener(item.onClick, presenter.SelectCharacter, index);
        }
        Button close = MakeButton(window.transform, "Cerrar", "VOLVER", font, new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(280, 58), false);
        UnityEventTools.AddBoolPersistentListener(close.onClick, overlay.SetActive, false);
        overlay.SetActive(false);
        return overlay;
    }

    // Replica de medidas, fuentes y colores de LobbyUIBuilder.BuildGameSettingsUI,
    // usada SOLO si NetworkLobby no tiene el ConfigurarPartidaPopup guardado.
    private static void CreateSettingsPopup(Transform canvas, Transform sourcePanel, TMP_FontAsset defaultFont,
        out GameObject popup, out TMP_Text mode, out TMP_Text map, out TMP_Text difficulty)
    {
        TMP_FontAsset titleFont = FontByGuid("0d82eaf50c142fa4ba34beb1fc9a12e4", defaultFont);
        TMP_FontAsset monoFont = FontByGuid("102734b590693d240886df79eaaa686c", defaultFont);
        Color panelInk = new Color(0.04f, 0.07f, 0.11f, 0.97f);
        Color roleColor = new Color(0.384f, 0.464f, 0.394f, 1f);
        Color green = new Color(0f, 1f, 0.4f, 1f);

        popup = NewUI("ConfigurarPartidaPopup", canvas);
        Stretch((RectTransform)popup.transform);
        Image fade = popup.AddComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0.78f);
        fade.raycastTarget = true;

        Image window = Image(popup.transform, "Ventana", panelInk, true);
        PlaceMiddle(window.rectTransform, new Vector2(1000f, 640f));
        Image top = Image(window.transform, "LineaSuperior", green, false);
        RectTransform line = top.rectTransform;
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0f, 4f);

        Text(window.transform, "Titulo", "CONFIGURAR PARTIDA", titleFont, 44f,
            new Color(0.70f, 0.79f, 0.71f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -26f), new Vector2(920f, 56f), TextAlignmentOptions.Left);
        Text(window.transform, "Subtitulo", "HACE CLICK EN CADA OPCION PARA CAMBIARLA",
            monoFont, 18f, roleColor, new Vector2(0f, 1f),
            new Vector2(40f, -86f), new Vector2(920f, 24f), TextAlignmentOptions.Left);
        mode = RealSettingsRow(window.transform, monoFont, "Seccion_Modo", "MODO DE JUEGO", -130f);
        map = RealSettingsRow(window.transform, monoFont, "Seccion_Mapa", "MAPA", -268f);
        difficulty = RealSettingsRow(window.transform, monoFont, "Seccion_Dificultad", "DIFICULTAD", -406f);

        Button close = CloneButtonExact(sourcePanel, window.transform, "PLAY_MULTIPLAYER", "Btn_Cerrar", "CERRAR");
        PlaceOriginalRect(close.transform as RectTransform, new Vector2(.5f, 0f),
            new Vector2(0f, 24f), new Vector2(300f, 64f));
        foreach (TMP_Text label in close.GetComponentsInChildren<TMP_Text>(true))
        {
            label.fontSize = 24f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 24f;
        }
        UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);
        popup.SetActive(false);
    }

    private static TMP_FontAsset FontByGuid(string guid, TMP_FontAsset backup)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        TMP_FontAsset result = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        return result != null ? result : backup;
    }

    private static void PlaceMiddle(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static TMP_Text RealSettingsRow(Transform parent, TMP_FontAsset font, string name, string caption, float y)
    {
        GameObject section = NewUI(name, parent);
        PlaceOriginalRect((RectTransform)section.transform, new Vector2(0f, 1f),
            new Vector2(40f, y), new Vector2(920f, 124f));
        Text(section.transform, "Titulo", caption, font, 24f,
            new Color(0f, 1f, 0.4f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(920f, 30f), TextAlignmentOptions.Left);
        Image content = Image(section.transform, "Contenido", new Color(1f, 1f, 1f, .05f), false);
        PlaceOriginalRect(content.rectTransform, new Vector2(0f, 1f),
            new Vector2(0f, -36f), new Vector2(920f, 84f));
        TMP_Text label = Text(content.transform, "Valor", "-  [ CLICK PARA ELEGIR ]", font, 20f,
            new Color(.384f, .464f, .394f, 1f), new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(920f, 84f), TextAlignmentOptions.Center);
        return label;
    }

    private static void CreateMessagePopup(Transform canvas, Transform sourcePanel, TMP_FontAsset defaultFont,
        out GameObject popup, out TMP_Text title, out TMP_Text message)
    {
        TMP_FontAsset titleFont = FontByGuid("0d82eaf50c142fa4ba34beb1fc9a12e4", defaultFont);
        TMP_FontAsset monoFont = FontByGuid("102734b590693d240886df79eaaa686c", defaultFont);
        popup = NewUI("MensajePopup", canvas);
        Stretch((RectTransform)popup.transform);
        Image shade = popup.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.82f);
        shade.raycastTarget = true;

        Image window = Image(popup.transform, "Ventana", new Color(.04f, .07f, .11f, .98f), true);
        PlaceMiddle(window.rectTransform, new Vector2(760f, 380f));
        Image border = Image(window.transform, "LineaSuperior", new Color(1f, 0f, .038f, 1f), false);
        RectTransform line = border.rectTransform;
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(.5f, 1f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0f, 4f);

        title = Text(window.transform, "Titulo", "AVISO", titleFont, 48f,
            new Color(.70f, .79f, .71f, 1f), new Vector2(0f, 1f),
            new Vector2(30f, -40f), new Vector2(700f, 64f), TextAlignmentOptions.Center);
        message = Text(window.transform, "Texto", "", monoFont, 24f, Color.white,
            new Vector2(0f, 1f), new Vector2(40f, -130f), new Vector2(680f, 130f),
            TextAlignmentOptions.Top);
        message.enableWordWrapping = true;
        Button close = CloneButtonExact(sourcePanel, window.transform, "PLAY_MULTIPLAYER", "Btn_Aceptar", "ACEPTAR");
        PlaceOriginalRect(close.transform as RectTransform, new Vector2(.5f, 0f),
            new Vector2(0f, 30f), new Vector2(300f, 64f));
        UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);
        popup.SetActive(false);
    }

    // El lobby tiene su propio Canvas. No depende de la jerarquía del menú principal ni
    // de que una versión anterior del builder haya creado correctamente el panel.
    private static Canvas EnsureSingleplayerCanvas(Scene dest)
    {
        GameObject root = FindRoot(dest, "SinglePlayerLobbyCanvas");
        Canvas canvas;
        if (root == null)
        {
            root = new GameObject("SinglePlayerLobbyCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // La escena destino ya es la escena activa; el Canvas nace allí.
            Undo.RegisterCreatedObjectUndo(root, "Canvas singleplayer independiente");
            canvas = root.GetComponent<Canvas>();
        }
        else
        {
            // Si quedó un Canvas de una construcción interrumpida, completarlo.
            if (root.GetComponent<RectTransform>() == null)
                throw new InvalidOperationException(
                    "SinglePlayerLobbyCanvas ya existe pero no tiene RectTransform. " +
                    "Renombrá ese objeto y volvé a ejecutar el builder.");
            canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = Undo.AddComponent<Canvas>(root);
            if (root.GetComponent<CanvasScaler>() == null)
                Undo.AddComponent<CanvasScaler>(root);
            if (root.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(root);
        }

        Undo.RecordObject(root, "Configurar Canvas del lobby singleplayer");
        Undo.RecordObject(canvas, "Configurar Canvas del lobby singleplayer");
        root.layer = LayerMask.NameToLayer("UI");
        root.SetActive(true);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        Undo.RecordObject(scaler, "Ajustar escala del lobby singleplayer");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        return canvas;
    }

    private static void RemoveLegacySingleplayer(RectTransform panel, Canvas canvas)
    {
        // Solo nombres que generó el builder antiguo del singleplayer.
        foreach (string name in new[] { "Lobby3D", "Partida", "Btn_Comenzar", "Btn_Personaje", "Btn_Volver", "Titulo", "TextxSuperior" })
            RemoveNamedChild(panel, name);
        foreach (string name in new[] { "ConfigurarPartidaPopup", "MensajePopup", "DQSingleLobbySelector" })
            RemoveNamedChild(canvas.transform, name);
        // No borrar el stage antiguo: la selección de personaje podría tener referencias a él.
        Transform oldStage = FindDeepInScene(SceneManager.GetActiveScene(), "LobbyCharacterStage");
        if (oldStage != null && oldStage.GetComponent<CharacterPreviewStage>() != null)
        {
            Camera oldCam = oldStage.GetComponentInChildren<Camera>(true);
            if (oldCam != null)
            {
                Undo.RecordObject(oldCam, "Desactivar cámara de preview antigua");
                oldCam.enabled = false;
            }
        }
    }

    private static void RemoveNamedInScene(Scene scene, string name)
    {
        GameObject root = FindRoot(scene, name);
        if (root != null) Undo.DestroyObjectImmediate(root);
    }
    private static void RemoveNamedChild(Transform parent, string name)
    {
        if (parent == null) return;
        Transform target = parent.Find(name);
        if (target != null) Undo.DestroyObjectImmediate(target.gameObject);
    }
    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
    private static Transform FindDeepInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] ts = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in ts) if (t.name == name) return t;
        }
        return null;
    }
    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private static void EnsureEventSystem(Scene dest)
    {
        if (FindComponentInScene<EventSystem>(dest) != null) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        Undo.RegisterCreatedObjectUndo(go, "EventSystem lobby singleplayer");
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(go);
#else
        Undo.AddComponent<StandaloneInputModule>(go);
#endif
    }
    private static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "UI lobby singleplayer");
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }
    private static Image Image(Transform parent, string name, Color color, bool raycast)
    {
        GameObject go = NewUI(name, parent);
        Image img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = raycast;
        return img;
    }
    private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font,
        float size, Color color, Vector2 anchor, Vector2 pos, Vector2 dimensions, TextAlignmentOptions alignment)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
        text.raycastTarget = false; text.enableWordWrapping = false;
        Place(text.rectTransform, anchor, pos, dimensions);
        return text;
    }
    private static Button MakeButton(Transform parent, string name, string title, TMP_FontAsset font,
        Vector2 anchor, Vector2 pos, Vector2 dimensions, bool primary)
    {
        Image img = Image(parent, name, primary ? new Color(.055f, .20f, .115f, .96f) : new Color(.035f, .07f, .085f, .92f), true);
        Place(img.rectTransform, anchor, pos, dimensions);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.12f, .38f, .23f, 1f);
        colors.pressedColor = new Color(.08f, .50f, .24f, 1f);
        button.colors = colors;
        TMP_Text label = Text(img.transform, "Texto", title, font, primary ? 31 : 25,
            primary ? Accent : Main, new Vector2(.5f, .5f), Vector2.zero, dimensions - new Vector2(8, 8), TextAlignmentOptions.Center);
        return button;
    }
    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    private static void Bind(SerializedObject obj, string field, UnityEngine.Object value)
    {
        SerializedProperty p = obj.FindProperty(field);
        if (p == null) throw new InvalidOperationException("Campo no encontrado en SinglePlayerLobbyController: " + field);
        p.objectReferenceValue = value;
    }
    private static void Error(string msg)
    {
        EditorUtility.DisplayDialog("Lobby Singleplayer", msg, "OK");
        Debug.LogError("[SinglePlayerLobby] " + msg);
    }
}
#endif
