#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
            "Abrí PantallasUI. Este builder copia el mundo y los elementos visuales REALES del lobby multiplayer " +
            "(tarjeta con retrato, selección actual, botones y título). Usa una sola fila 1/1. " +
            "Al elegir personaje oculta el HUD y lo restaura al cerrar el selector. NetworkLobby no se modifica.", MessageType.Info);
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
            BuildUi(ui.transform, lobbyPanel, canvas, world, cam, actor, sourcePanel, sourceCinematicUi);

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
        Transform multiplayerPanel, Transform multiplayerUi)
    {
        TMP_FontAsset font = null;
        TMP_Text originalFont = multiplayerUi.GetComponentInChildren<TMP_Text>(true);
        if (originalFont != null) font = originalFont.font;
        if (font == null)
        {
            TMP_Text oldFont = lobbyPanel.GetComponentInChildren<TMP_Text>(true);
            if (oldFont != null) font = oldFont.font;
        }

        // Copias VISUALES desde la escena NetworkLobby: tamaños, tipografía,
        // espaciados, colores, retrato y jerarquía iguales al multiplayer.
        Transform originalBackdrop = FindDeep(multiplayerPanel, "LobbyCinematicBackdrop");
        if (originalBackdrop != null)
        {
            GameObject backdrop = CloneVisual(originalBackdrop, ui, "DQSingleLobbyBackdrop");
            backdrop.transform.SetAsFirstSibling();
        }
        else
        {
            AddFade(ui, true, 280f, .58f);
            AddFade(ui, false, 390f, .68f);
        }

        TMP_Text title = CloneTitle(multiplayerPanel, ui, "Titulo", "UN JUGADOR // SALA");
        if (title == null)
            title = Text(ui, "Titulo", "UN JUGADOR // SALA", font, 42, Main,
                new Vector2(0, 1), new Vector2(250, -30), new Vector2(950, 62), TextAlignmentOptions.MidlineLeft);

        Button back = CloneButton(multiplayerPanel, ui, "Home", "Atras", "ATRAS");
        if (back == null)
            back = MakeButton(ui, "Atras", "ATRAS", font, new Vector2(0, 1),
                new Vector2(40, -30), new Vector2(190, 56), false);

        Button character = CloneButton(multiplayerPanel, ui, "Btn_Personaje", "Personaje", "PERSONAJE");
        if (character == null)
            character = MakeButton(ui, "Personaje", "PERSONAJE", font, new Vector2(0, 0),
                new Vector2(40, 40), new Vector2(330, 84), true);

        Button start = CloneButton(multiplayerPanel, ui, "PLAY_MULTIPLAYER", "Comenzar", "COMENZAR");
        if (start == null)
            start = MakeButton(ui, "Comenzar", "COMENZAR", font, new Vector2(1, 0),
                new Vector2(-40, 40), new Vector2(460, 92), true);

        Button config = CloneButton(multiplayerPanel, ui, "Btn_ConfigurarPartida", "Configurar", "CONFIGURAR PARTIDA");
        if (config == null)
            config = MakeButton(ui, "Configurar", "CONFIGURAR PARTIDA", font, new Vector2(1, 0),
                new Vector2(-40, 302), new Vector2(460, 50), false);

        // Tarjeta EXACTA del multiplayer, de 4 filas a una única fila.
        GameObject partyClone = CloneVisual(multiplayerUi.Find("PartyCard"), ui, "TarjetaJugador");
        RectTransform partyRect = partyClone.GetComponent<RectTransform>();
        // En el multiplayer la tarjeta tiene 46 de encabezado, filas de 52 y pie de 8.
        partyRect.sizeDelta = new Vector2(partyRect.sizeDelta.x, 46f + 52f + 8f);
        TMP_Text partyHeader = FindDeep(partyClone.transform, "Header")?.GetComponent<TMP_Text>();
        if (partyHeader != null) partyHeader.text = "JUGADORES 1/1";
        for (int i = 1; i <= 4; i++)
        {
            Transform row = partyClone.transform.Find("Row_" + i);
            if (row != null) row.gameObject.SetActive(i == 1);
        }
        Transform row1 = partyClone.transform.Find("Row_1");
        Image icon = row1 != null ? row1.Find("Icon")?.GetComponent<Image>() : null;
        TMP_Text partyRowText = row1 != null ? row1.Find("Text")?.GetComponent<TMP_Text>() : null;
        if (icon == null || partyRowText == null)
            throw new InvalidOperationException("PartyCard del multiplayer no tiene Row_1/Icon y Row_1/Text. No se cambió nada de la escena.");
        icon.preserveAspect = true;
        icon.color = Color.white;
        partyRowText.text = "JUGADOR";

        // Tarjeta de partida real del multiplayer: no superponer una segunda tarjeta.
        GameObject selectionClone = CloneVisual(multiplayerUi.Find("SelectionCard"), ui, "SeleccionActual");
        TMP_Text selectedMode = selectionClone.transform.Find("ModeDifficulty")?.GetComponent<TMP_Text>();
        TMP_Text selectedMap = selectionClone.transform.Find("Map")?.GetComponent<TMP_Text>();
        if (selectedMode == null || selectedMap == null)
            throw new InvalidOperationException("SelectionCard del multiplayer no tiene ModeDifficulty / Map.");

        // Placa sobre la cabeza: misma que el multiplayer, sin otra placa flotando.
        RectTransform plate;
        TMP_Text plateText;
        Transform sourcePlate = multiplayerUi.Find("Nameplate_1");
        if (sourcePlate != null)
        {
            GameObject plateGo = CloneVisual(sourcePlate, ui, "NombreSobreCabeza");
            plate = plateGo.GetComponent<RectTransform>();
            plateText = plateGo.transform.Find("Name")?.GetComponent<TMP_Text>();
            Transform readyBadge = plateGo.transform.Find("ReadyBadge");
            if (readyBadge != null) readyBadge.gameObject.SetActive(false);
        }
        else
        {
            Image plateImage = Image(ui, "NombreSobreCabeza", new Color(.01f, .02f, .02f, .45f), false);
            plate = plateImage.rectTransform;
            plate.anchorMin = plate.anchorMax = new Vector2(.5f, .5f);
            plate.pivot = new Vector2(.5f, 0f);
            plate.sizeDelta = new Vector2(180f, 36f);
            plateText = Text(plate, "Name", "JUGADOR", font, 22, Main,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(180, 36), TextAlignmentOptions.Center);
        }
        plate.gameObject.SetActive(false);

        GameObject configPopup;
        TMP_Text cfgMode, cfgMap, cfgDifficulty;
        CreateSettingsPopup(canvas.transform, font, out configPopup, out cfgMode, out cfgMap, out cfgDifficulty);
        GameObject messagePopup;
        TMP_Text messageTitle, messageText;
        CreateMessagePopup(canvas.transform, font, out messagePopup, out messageTitle, out messageText);

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
        {
            // El selector nunca puede quedar dentro del grupo que ocultaremos al abrirlo.
            Undo.SetTransformParent(selector.transform, canvas.transform, "Separar selector del HUD singleplayer");
        }
        presenter.ConfigureSelector(selector);
        Canvas selectorCanvas = selector.GetComponent<Canvas>();
        if (selectorCanvas != null)
        {
            Undo.RecordObject(selectorCanvas, "Prioridad del selector de personaje");
            selectorCanvas.overrideSorting = true;
            selectorCanvas.sortingOrder = 100; // Sobre el Canvas singleplayer (orden 50).
        }
        // Se oculta TODO el HUD del lobby al seleccionar, sin apagar el mundo
        // y sin depender de eventos de botones que otros builders podrían cambiar.
        CanvasGroup group = lobbyPanel.GetComponent<CanvasGroup>();
        if (group == null) group = Undo.AddComponent<CanvasGroup>(lobbyPanel.gameObject);
        presenter.ConfigureHudGroup(group);
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        // El movimiento de arrastre afecta únicamente al actor 3D, no a las tarjetas.
        Image dragZone = Image(ui, "GirarPersonaje", new Color(0, 0, 0, 0), true);
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

    // Trae una copia visual de la escena fuente, SIN controladores de red ni eventos.
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

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static TMP_Text CloneTitle(Transform sourceRoot, Transform parent, string sourceName, string value)
    {
        Transform source = FindDeep(sourceRoot, sourceName);
        if (source == null) return null;
        GameObject clone = CloneVisual(source, parent, "Titulo");
        TMP_Text text = clone.GetComponent<TMP_Text>();
        if (text == null) text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = value;
        return text;
    }

    private static Button CloneButton(Transform sourceRoot, Transform parent,
        string sourceName, string ownName, string value)
    {
        Transform source = FindDeep(sourceRoot, sourceName);
        if (source == null || source.GetComponent<Button>() == null) return null;
        GameObject clone = CloneVisual(source, parent, ownName);
        Button button = clone.GetComponent<Button>();
        // No copiar eventos vinculados a MultiplayerLobbyController ni al código de sala.
        button.onClick = new Button.ButtonClickedEvent();
        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
        return button;
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

    private static void CreateSettingsPopup(Transform canvas, TMP_FontAsset font,
        out GameObject popup, out TMP_Text mode, out TMP_Text map, out TMP_Text difficulty)
    {
        popup = NewUI("ConfigurarPartidaPopup", canvas);
        Stretch((RectTransform)popup.transform);
        popup.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
        Image window = Image(popup.transform, "Ventana", new Color(.04f, .07f, .09f, .98f), true);
        window.rectTransform.anchorMin = window.rectTransform.anchorMax = new Vector2(.5f, .5f);
        window.rectTransform.pivot = new Vector2(.5f, .5f);
        window.rectTransform.sizeDelta = new Vector2(900, 620);
        window.rectTransform.anchoredPosition = Vector2.zero;
        Text(window.transform, "Titulo", "CONFIGURAR PARTIDA", font, 38, Main, new Vector2(0, 1), new Vector2(30, -22), new Vector2(840, 55), TextAlignmentOptions.MidlineLeft);
        Text(window.transform, "Subtitulo", "HACE CLICK EN CADA OPCION PARA CAMBIARLA", font, 18, Dim, new Vector2(0, 1), new Vector2(30, -78), new Vector2(840, 35), TextAlignmentOptions.MidlineLeft);
        mode = SettingsRow(window.transform, font, "Seccion_Modo", "MODO DE JUEGO", -130);
        map = SettingsRow(window.transform, font, "Seccion_Mapa", "MAPA", -265);
        difficulty = SettingsRow(window.transform, font, "Seccion_Dificultad", "DIFICULTAD", -400);
        Button close = MakeButton(window.transform, "Cerrar", "CERRAR", font, new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(280, 62), false);
        UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);
        popup.SetActive(false);
    }

    private static TMP_Text SettingsRow(Transform parent, TMP_FontAsset font, string name, string caption, float y)
    {
        GameObject section = NewUI(name, parent);
        Place((RectTransform)section.transform, new Vector2(0, 1), new Vector2(30, y), new Vector2(840, 120));
        Text(section.transform, "Titulo", caption, font, 23, Accent, new Vector2(0, 1), Vector2.zero, new Vector2(820, 38), TextAlignmentOptions.MidlineLeft);
        Image content = Image(section.transform, "Contenido", new Color(1, 1, 1, .08f), true);
        Place(content.rectTransform, new Vector2(0, 1), new Vector2(0, -38), new Vector2(840, 77));
        return Text(content.transform, "Valor", "-", font, 27, Main, new Vector2(.5f, .5f), Vector2.zero, new Vector2(820, 68), TextAlignmentOptions.Center);
    }

    private static void CreateMessagePopup(Transform canvas, TMP_FontAsset font,
        out GameObject popup, out TMP_Text title, out TMP_Text message)
    {
        popup = NewUI("MensajePopup", canvas);
        Stretch((RectTransform)popup.transform);
        popup.AddComponent<Image>().color = new Color(0, 0, 0, .8f);
        Image window = Image(popup.transform, "Ventana", new Color(.04f, .07f, .09f, .98f), true);
        window.rectTransform.anchorMin = window.rectTransform.anchorMax = new Vector2(.5f, .5f);
        window.rectTransform.pivot = new Vector2(.5f, .5f);
        window.rectTransform.sizeDelta = new Vector2(800, 370);
        window.rectTransform.anchoredPosition = Vector2.zero;
        title = Text(window.transform, "Titulo", "AVISO", font, 40, Accent, new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(730, 70), TextAlignmentOptions.Center);
        message = Text(window.transform, "Texto", "", font, 24, Main, new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(725, 130), TextAlignmentOptions.Center);
        Button close = MakeButton(window.transform, "Cerrar", "ACEPTAR", font, new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(280, 60), false);
        UnityEventTools.AddBoolPersistentListener(close.onClick, popup.SetActive, false);
        popup.SetActive(false);
    }

    private static void AddFade(Transform ui, bool top, float height, float opacity)
    {
        for (int i = 0; i < 12; i++)
        {
            Image strip = Image(ui, (top ? "FadeSuperior_" : "FadeInferior_") + i, new Color(.01f, .02f, .025f, opacity / 12f), false);
            RectTransform rt = strip.rectTransform;
            float anchor = top ? 1 : 0;
            rt.anchorMin = new Vector2(0, anchor); rt.anchorMax = new Vector2(1, anchor);
            rt.pivot = new Vector2(.5f, anchor); rt.sizeDelta = new Vector2(0, height * (12 - i) / 12f);
            rt.anchoredPosition = Vector2.zero;
        }
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
