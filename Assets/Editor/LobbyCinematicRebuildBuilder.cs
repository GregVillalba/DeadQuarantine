#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reconstrucción visual cinematográfica del lobby MULTIPLAYER (NetworkLobby).
///
/// - Se puede ejecutar todas las veces que quieras: antes de construir borra TODO lo generado
///   anteriormente (cualquier objeto cuyo nombre empiece con "LobbyCinematic"), así que no quedan
///   cámaras, mundos, personajes, FX ni UI duplicados.
/// - NO crea botones ni textos de código: reubica los reales (LISTO, PERSONAJE, CONFIGURAR PARTIDA,
///   ATRÁS, LOBBY_ID, código, COPIAR, CountDown). Sus listeners no se tocan.
/// - La UI vieja (tarjetas de slot, caja Partida, modelos 3D viejos) se oculta SOLO visualmente.
///   Los scripts del lobby siguen actualizándola.
/// - Los datos (jugadores, variantes, modo, mapa, dificultad) los lee LobbyCinematicPartyPresenter
///   desde LobbyPlayerSlot y LobbyGameSettingsRuntime. Acá no se inventa ningún dato.
/// </summary>
public class LobbyCinematicRebuildBuilder : EditorWindow
{
    // ---------------------------------------------------------
    // Prefabs
    // ---------------------------------------------------------
    [SerializeField] private GameObject mansionPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject postProcessPrefab;
    [SerializeField] private GameObject rainPrefab;
    [SerializeField] private GameObject rainSplashPrefab;
    [SerializeField] private GameObject fogFlatPrefab;
    [SerializeField] private GameObject fogSmallPrefab;

    // ---------------------------------------------------------
    // Cámara exacta
    // ---------------------------------------------------------
    [SerializeField] private Vector3 cameraPosition = new Vector3(0.034f, -0.011f, -72.831f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(0.445f, 359.889f, 0.46f);
    [SerializeField, Range(35f, 70f)] private float cameraFov = 50f;

    // ---------------------------------------------------------
    // Jugadores en el escenario (la cámara NO se mueve: se mueven los jugadores)
    // ---------------------------------------------------------
    [SerializeField] private float playerGroundY = -1.26f;
    [SerializeField] private float playerDepthZ = -65.6f;
    [SerializeField] private float groupCenterX = -1.2f;
    [SerializeField] private string weaponId = "Handgun";

    // ---------------------------------------------------------
    // Atmósfera
    // ---------------------------------------------------------
    [SerializeField] private bool addPostProcess = true;
    [SerializeField] private bool addRain = true;
    [SerializeField] private bool addFog = true;
    [SerializeField] private bool addPartyLights = true;

    private const string GeneratedPrefix = "LobbyCinematic";

    // Colores de la UI (en línea con el verde del lobby).
    private static readonly Color Accent = new Color(0.10f, 0.95f, 0.50f, 1f);
    private static readonly Color PanelDark = new Color(0.015f, 0.025f, 0.035f, 0.58f);
    private static readonly Color TextMain = new Color(0.93f, 0.96f, 0.98f, 1f);
    private static readonly Color TextDim = new Color(0.62f, 0.72f, 0.78f, 1f);
    private static readonly Color MapGold = new Color(1f, 0.82f, 0.18f, 1f);

    // Resultado de construir el mundo.
    private class WorldRefs
    {
        public Camera camera;
        public LobbyCinematicPlayerDisplay[] actors;
        public LobbyCinematicPartyPresenter presenter;
    }

    [MenuItem("Game/Dead Quarantine/Lobby/Reconstructor Cinematografico")]
    private static void Open()
    {
        GetWindow<LobbyCinematicRebuildBuilder>("Lobby Cinematico");
    }

    private void OnEnable()
    {
        AutoFindAssets();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Reconstruye SOLO el lobby MULTIPLAYER (escena NetworkLobby). " +
            "Se puede ejecutar varias veces: limpia lo generado antes de volver a construir.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Escenario", EditorStyles.boldLabel);

        mansionPrefab = (GameObject)EditorGUILayout.ObjectField("Escenario_3", mansionPrefab, typeof(GameObject), false);
        playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player.prefab", playerPrefab, typeof(GameObject), false);
        postProcessPrefab = (GameObject)EditorGUILayout.ObjectField("Post_Process_Mansion", postProcessPrefab, typeof(GameObject), false);
        rainPrefab = (GameObject)EditorGUILayout.ObjectField("FX_Rain_01", rainPrefab, typeof(GameObject), false);
        rainSplashPrefab = (GameObject)EditorGUILayout.ObjectField("FX_Rain_SplashLayer_01", rainSplashPrefab, typeof(GameObject), false);
        fogFlatPrefab = (GameObject)EditorGUILayout.ObjectField("FX_Fog_Flat_01", fogFlatPrefab, typeof(GameObject), false);
        fogSmallPrefab = (GameObject)EditorGUILayout.ObjectField("FX_Fog_Small_01", fogSmallPrefab, typeof(GameObject), false);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Camara exacta", EditorStyles.boldLabel);
        cameraPosition = EditorGUILayout.Vector3Field("Position", cameraPosition);
        cameraRotation = EditorGUILayout.Vector3Field("Rotation", cameraRotation);
        cameraFov = EditorGUILayout.Slider("Field of View", cameraFov, 35f, 70f);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Jugadores en el escenario", EditorStyles.boldLabel);
        playerGroundY = EditorGUILayout.FloatField("Altura del piso (Y)", playerGroundY);
        playerDepthZ = EditorGUILayout.FloatField("Profundidad base (Z)", playerDepthZ);
        groupCenterX = EditorGUILayout.FloatField("Centro del grupo (X)", groupCenterX);
        weaponId = EditorGUILayout.TextField("Arma idle", weaponId);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Atmosfera", EditorStyles.boldLabel);
        addPostProcess = EditorGUILayout.ToggleLeft("Post_Process_Mansion", addPostProcess);
        addRain = EditorGUILayout.ToggleLeft("Lluvia", addRain);
        addFog = EditorGUILayout.ToggleLeft("Niebla", addFog);
        addPartyLights = EditorGUILayout.ToggleLeft("Luces cinematograficas del grupo", addPartyLights);

        EditorGUILayout.Space(10f);

        if (GUILayout.Button("RECONSTRUIR LOBBY MULTIPLAYER", GUILayout.Height(38f)))
            BuildCurrentScene();

        EditorGUILayout.Space(4f);

        if (GUILayout.Button("Buscar assets automaticamente", GUILayout.Height(25f)))
            AutoFindAssets();
    }

    // =========================================================
    // PROCESO PRINCIPAL
    // =========================================================

    private void BuildCurrentScene()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Salí del Play Mode",
                "El reconstructor se ejecuta con el juego detenido.",
                "OK");
            return;
        }

        Transform panel = FindInScene("salaEsperaPanel");

        if (panel == null || panel.gameObject.scene != SceneManager.GetActiveScene())
        {
            EditorUtility.DisplayDialog(
                "No encuentro el lobby",
                "Abrí la escena NetworkLobby (tiene que existir salaEsperaPanel).",
                "OK");
            return;
        }

        if (mansionPrefab == null || playerPrefab == null)
        {
            EditorUtility.DisplayDialog("Faltan assets", "Necesito Escenario_3 y Player.prefab.", "OK");
            return;
        }

        LobbyPlayerSlot[] slots = panel.GetComponentsInChildren<LobbyPlayerSlot>(true);

        if (slots.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No encuentro los slots",
                "No hay LobbyPlayerSlot dentro de salaEsperaPanel. No puedo mostrar jugadores reales.",
                "OK");
            return;
        }

        Array.Sort(slots, (a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Reconstruir lobby cinematografico");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) Limpieza TOTAL de lo generado antes (cámaras, mundos, personajes, FX, UI).
        CleanupPreviousBuild();

        // 2) Mundo 3D.
        WorldRefs world = BuildWorld(panel);

        // 3) UI.
        BuildUI(panel, slots, world);

        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = panel.gameObject;

        Debug.Log(
            "[LobbyCinematic] Multiplayer reconstruido. Slots reales: " + slots.Length +
            ". Probá en Play con 1 a 4 jugadores.");

        EditorUtility.DisplayDialog("Lobby reconstruido", "Multiplayer reconstruido con Mansion + Player real.", "OK");
    }

    /// <summary>Borra todo objeto generado por esta herramienta (o por versiones anteriores).</summary>
    private static void CleanupPreviousBuild()
    {
        List<GameObject> found = new List<GameObject>();
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
            CollectGenerated(roots[i].transform, found);

        for (int i = 0; i < found.Count; i++)
        {
            if (found[i] != null)
                Undo.DestroyObjectImmediate(found[i]);
        }
    }

    private static void CollectGenerated(Transform t, List<GameObject> list)
    {
        if (t.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
        {
            list.Add(t.gameObject);
            return;
        }

        foreach (Transform child in t)
            CollectGenerated(child, list);
    }

    // =========================================================
    // MUNDO 3D
    // =========================================================

    private WorldRefs BuildWorld(Transform panel)
    {
        GameObject controllerGO = new GameObject(GeneratedPrefix + "Controller_Multiplayer");
        Undo.RegisterCreatedObjectUndo(controllerGO, "Crear Lobby Cinematico");

        GameObject content = new GameObject(GeneratedPrefix + "World_Multiplayer");
        Undo.RegisterCreatedObjectUndo(content, "Crear Lobby Cinematico");

        // --- Mansión ---
        GameObject mansion = InstantiatePrefab(mansionPrefab, content.transform, "Mansion_Lobby");
        mansion.transform.position = Vector3.zero;
        mansion.transform.rotation = Quaternion.identity;
        PrepareEnvironment(mansion);

        // --- Cámara exacta ---
        GameObject cameraGO = new GameObject(GeneratedPrefix + "Camera", typeof(Camera));
        Undo.RegisterCreatedObjectUndo(cameraGO, "Crear camara lobby");
        cameraGO.transform.SetParent(content.transform, false);
        cameraGO.transform.position = cameraPosition;
        cameraGO.transform.rotation = Quaternion.Euler(cameraRotation);

        Camera cam = cameraGO.GetComponent<Camera>();
        cam.fieldOfView = cameraFov;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.depth = 50f;

        UniversalAdditionalCameraData urp = cameraGO.GetComponent<UniversalAdditionalCameraData>();

        if (urp == null)
            urp = Undo.AddComponent<UniversalAdditionalCameraData>(cameraGO);

        urp.renderPostProcessing = true;

        // --- 4 jugadores visuales (uno por slot). Solo se muestran los conectados. ---
        LobbyCinematicPartyPresenter.PartyLayout[] layouts =
            LobbyCinematicPartyPresenter.CreateDefaultLayouts(playerGroundY, playerDepthZ, groupCenterX);

        LobbyCinematicPlayerDisplay[] actors = new LobbyCinematicPlayerDisplay[4];

        for (int i = 0; i < actors.Length; i++)
        {
            GameObject anchorGO = new GameObject("LobbyPlayerAnchor_" + (i + 1));
            Undo.RegisterCreatedObjectUndo(anchorGO, "Crear jugador lobby " + (i + 1));
            anchorGO.transform.SetParent(content.transform, false);

            LobbyCinematicPartyPresenter.PartyLayout fullLayout = layouts[3];
            anchorGO.transform.position = fullLayout.positions[i];
            anchorGO.transform.rotation = Quaternion.Euler(0f, fullLayout.yaws[i], 0f);

            LobbyCinematicPlayerDisplay actor = content.AddComponent<LobbyCinematicPlayerDisplay>();
            actor.Configure(playerPrefab, anchorGO.transform, weaponId);
            actor.SetPose(fullLayout.positions[i], fullLayout.yaws[i]);

            // Se activa en runtime solo si ese slot está conectado.
            anchorGO.SetActive(false);
            actors[i] = actor;
        }

        LobbyCinematicPartyPresenter presenter = content.AddComponent<LobbyCinematicPartyPresenter>();
        presenter.ConfigureWorld(cam, actors, layouts);

        // --- Post process ---
        if (addPostProcess && postProcessPrefab != null)
        {
            GameObject post = InstantiatePrefab(postProcessPrefab, content.transform, "Post_Process_Mansion");
            post.transform.localPosition = Vector3.zero;
            post.transform.localRotation = Quaternion.identity;
            post.transform.localScale = Vector3.one;
        }

        // --- Lluvia ---
        if (addRain)
        {
            if (rainPrefab != null)
            {
                GameObject rain = InstantiatePrefab(rainPrefab, content.transform, "Lobby_Rain");
                LobbyCinematicRainFollower follower = rain.AddComponent<LobbyCinematicRainFollower>();
                follower.Configure(cameraGO.transform);
            }

            // Salpicaduras de las gotas contra el piso. El FX original de lluvia se centra
            // camera + (0, 8, 7), por lo que usamos exactamente el mismo centro X/Z y lo
            // apoyamos en playerGroundY. La cámara del lobby es fija, así que no necesita follower.
            if (rainSplashPrefab != null)
            {
                GameObject splash = InstantiatePrefab(rainSplashPrefab, content.transform, "Lobby_Rain_SplashLayer");
                splash.transform.position = new Vector3(
                    cameraGO.transform.position.x,
                    playerGroundY + 0.02f,
                    cameraGO.transform.position.z + 7f);
                splash.transform.rotation = Quaternion.identity;
                splash.transform.localScale = Vector3.one;
            }
        }

        // --- Niebla (usa la fuente como centro si existe) ---
        if (addFog)
        {
            Transform fountain = FindDeep(mansion.transform, "SM_Env_Fountain_01");
            Vector3 center = fountain != null ? fountain.position : new Vector3(0f, -1f, -35f);

            if (fogFlatPrefab != null)
            {
                CreateFog(fogFlatPrefab, content.transform, "LobbyFog_Flat_01", center + new Vector3(-6f, 0.18f, -3f), 1.6f);
                CreateFog(fogFlatPrefab, content.transform, "LobbyFog_Flat_02", center + new Vector3(6f, 0.18f, -2f), 1.6f);
            }

            if (fogSmallPrefab != null)
            {
                CreateFog(fogSmallPrefab, content.transform, "LobbyFog_Small_01", center + new Vector3(-2.5f, 0.35f, -5f), 1.25f);
                CreateFog(fogSmallPrefab, content.transform, "LobbyFog_Small_02", center + new Vector3(3f, 0.40f, -4f), 1.25f);
            }
        }

        // --- Luces del grupo (cubren hasta 4 personajes) ---
        if (addPartyLights)
        {
            CreatePointLight("Lobby_Party_Key", content.transform,
                new Vector3(groupCenterX - 2.2f, 2.4f, playerDepthZ - 4.0f), 18f, 16f,
                new Color(0.92f, 0.94f, 1f), true);

            CreatePointLight("Lobby_Party_Fill", content.transform,
                new Vector3(groupCenterX + 3.2f, 2.0f, playerDepthZ - 3.5f), 11f, 14f,
                new Color(0.80f, 0.86f, 1f), false);

            CreatePointLight("Lobby_Party_Rim", content.transform,
                new Vector3(groupCenterX + 1.0f, 2.2f, playerDepthZ + 2.2f), 12f, 12f,
                new Color(0.30f, 0.50f, 0.85f), false);
        }

        // --- El mundo solo existe mientras el panel del lobby está abierto ---
        LobbyCinematicWorldToggle toggle = controllerGO.AddComponent<LobbyCinematicWorldToggle>();
        toggle.Configure(panel.gameObject, content);

        content.SetActive(panel.gameObject.activeInHierarchy);

        EditorUtility.SetDirty(presenter);

        WorldRefs refs = new WorldRefs();
        refs.camera = cam;
        refs.actors = actors;
        refs.presenter = presenter;
        return refs;
    }

    // =========================================================
    // UI
    // =========================================================

    private void BuildUI(Transform panel, LobbyPlayerSlot[] slots, WorldRefs world)
    {
        RectTransform panelRect = panel as RectTransform;

        // Fondo 2D anterior apagado: la mansión queda como protagonista.
        Image panelBg = panel.GetComponent<Image>();

        if (panelBg != null)
        {
            Undo.RecordObject(panelBg, "Fondo lobby transparente");
            panelBg.enabled = false;
        }

        // Objetos REALES del lobby (hijos directos de salaEsperaPanel).
        Transform title = Require(panel, "Titulo");
        Transform subtitle = Require(panel, "TextxSuperior");
        Transform lobbyIdLabel = Require(panel, "TextxSuperior (1)");
        Transform codeBox = Require(panel, "Cuadrado");
        Transform code = Require(panel, "placeholder_codigo");
        Transform copied = Require(panel, "Copiado");
        Transform copyButton = Require(panel, "copiar_codigo");
        Transform ready = Require(panel, "PLAY_MULTIPLAYER");
        Transform countdown = Require(panel, "CountDown");
        Transform home = Require(panel, "Home");
        Transform characterButton = Require(panel, "Btn_Personaje");
        Transform partida = Require(panel, "Partida");
        Transform lobby3D = Require(panel, "Lobby3D");
        Transform configButton = FindDeep(panel, "Btn_ConfigurarPartida");

        if (configButton == null)
            Debug.LogWarning("[LobbyCinematic] No encontré Btn_ConfigurarPartida.");

        // Fuente de datos del lobby (la de ModoValor), si existe.
        TMP_FontAsset dataFont = null;
        Transform modoValor = FindDeep(panel, "ModoValor");

        if (modoValor != null)
        {
            TMP_Text sample = modoValor.GetComponent<TMP_Text>();
            if (sample != null) dataFont = sample.font;
        }

        if (dataFont == null)
        {
            TMP_Text any = panel.GetComponentInChildren<TMP_Text>(true);
            if (any != null) dataFont = any.font;
        }

        // ------------------------------------------------
        // Degradados suaves (en lugar de rectángulos negros).
        // ------------------------------------------------
        GameObject backdrop = NewUIObject(GeneratedPrefix + "Backdrop", panel);
        Stretch((RectTransform)backdrop.transform, 0f);
        backdrop.transform.SetSiblingIndex(0);

        // Sin sprites ni archivos generados: capas oscuras apiladas (nunca pueden quedar blancas).
        CreateFade("TopFade", backdrop.transform, true, 260f, 0.55f);
        CreateFade("BottomFade", backdrop.transform, false, 360f, 0.65f);

        // ------------------------------------------------
        // ARRIBA A LA IZQUIERDA: ATRÁS, título y LOBBY_ID horizontal.
        // ------------------------------------------------
        Vector2 topLeft = new Vector2(0f, 1f);

        PlaceTextRect(home, topLeft, new Vector2(40f, -30f), new Vector2(190f, 56f), 30f, TextAlignmentOptions.Center);
        PlaceTextRect(title, topLeft, new Vector2(250f, -30f), new Vector2(1000f, 56f), 44f, TextAlignmentOptions.MidlineLeft);

        SetText(title, "MULTIJUGADOR // SALA");

        // El texto "CONEXIÓN ESTABLE..." queda oculto solo visualmente.
        HideVisually(subtitle);

        PlaceTextRect(lobbyIdLabel, topLeft, new Vector2(40f, -106f), new Vector2(260f, 52f), 30f, TextAlignmentOptions.MidlineLeft);
        PlaceTextRect(code, topLeft, new Vector2(306f, -106f), new Vector2(250f, 52f), 34f, TextAlignmentOptions.Center);
        PlaceTextRect(copyButton, topLeft, new Vector2(572f, -106f), new Vector2(190f, 52f), 28f, TextAlignmentOptions.Center);
        PlaceTextRect(copied, topLeft, new Vector2(780f, -106f), new Vector2(340f, 52f), 24f, TextAlignmentOptions.MidlineLeft);

        // El recuadro verde original queda DETRÁS del código, con el MISMO rectángulo que el texto.
        Place(codeBox as RectTransform, topLeft, topLeft, new Vector2(306f, -106f), new Vector2(250f, 52f));
        Place(code as RectTransform, topLeft, topLeft, new Vector2(306f, -106f), new Vector2(250f, 52f));
        if (codeBox != null && code != null)
            codeBox.SetSiblingIndex(code.GetSiblingIndex());

        // ------------------------------------------------
        // ABAJO A LA IZQUIERDA: PERSONAJE.
        // ------------------------------------------------
        Vector2 bottomLeft = new Vector2(0f, 0f);
        PlaceTextRect(characterButton, bottomLeft, new Vector2(40f, 40f), new Vector2(330f, 84f), 34f, TextAlignmentOptions.Center);

        // ------------------------------------------------
        // ABAJO A LA DERECHA: LISTO, SELECCIÓN ACTUAL y CONFIGURAR PARTIDA.
        // ------------------------------------------------
        Vector2 bottomRight = new Vector2(1f, 0f);

        const float rightMargin = 40f;
        const float rightWidth = 460f;
        const float readyHeight = 92f;
        const float selectionHeight = 150f;
        const float gap = 10f;

        PlaceTextRect(ready, bottomRight, new Vector2(-rightMargin, 40f), new Vector2(rightWidth, readyHeight), 40f, TextAlignmentOptions.Center);

        float selectionY = 40f + readyHeight + gap;
        float configY = selectionY + selectionHeight + gap;

        if (configButton != null)
        {
            // Se saca de "Partida" (que se oculta) y se deja integrado sobre la selección actual.
            if (configButton.parent != panel)
                Undo.SetTransformParent(configButton, panel, "Mover boton configurar");

            PlaceTextRect(configButton, bottomRight, new Vector2(-rightMargin, configY), new Vector2(rightWidth, 50f), 24f, TextAlignmentOptions.Center);
            configButton.SetAsLastSibling();
        }

        // La caja vieja de modo/mapa/dificultad se oculta; sus textos se siguen actualizando.
        if (partida != null && partida.gameObject.activeSelf)
        {
            Undo.RecordObject(partida.gameObject, "Ocultar Partida");
            partida.gameObject.SetActive(false);
        }

        // CountDown arriba al centro (antes pisaba el panel de jugadores).
        if (countdown != null)
        {
            Place(countdown as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(760f, 56f));
            TMP_Text[] countdownTexts = countdown.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < countdownTexts.Length; i++)
            {
                Undo.RecordObject(countdownTexts[i], "Countdown");
                countdownTexts[i].enableWordWrapping = false;
                countdownTexts[i].overflowMode = TextOverflowModes.Overflow;
                countdownTexts[i].alignment = TextAlignmentOptions.Center;

                RectTransform childRect = countdownTexts[i].rectTransform;

                if (childRect.gameObject != countdown.gameObject)
                    Stretch(childRect, 0f);
            }
        }

        // ------------------------------------------------
        // UI vieja del slot: oculta solo visualmente.
        // ------------------------------------------------
        HideOldSlotUI(lobby3D);

        // Cámara del preview viejo apagada.
        Transform oldStage = FindInScene("LobbyCharacterStage");

        if (oldStage != null)
        {
            Camera previewCam = oldStage.GetComponentInChildren<Camera>(true);

            if (previewCam != null)
            {
                Undo.RecordObject(previewCam, "Apagar preview viejo");
                previewCam.enabled = false;
            }
        }

        // ------------------------------------------------
        // UI NUEVA (todo cuelga de un solo root: LobbyCinematicUI).
        // ------------------------------------------------
        GameObject uiRoot = NewUIObject(GeneratedPrefix + "UI", panel);
        Stretch((RectTransform)uiRoot.transform, 0f);
        uiRoot.transform.SetAsLastSibling();

        // Nombres sobre la cabeza + indicador de LISTO (estilo party/lobby).
        LobbyCinematicPartyPresenter.NameplateView[] plates = new LobbyCinematicPartyPresenter.NameplateView[4];
        GameObject[] readyBadges = new GameObject[4];

        for (int i = 0; i < plates.Length; i++)
            plates[i] = CreateNameplate(uiRoot.transform, i, dataFont, out readyBadges[i]);

        // Panel compacto de jugadores (arriba a la derecha).
        const float partyWidth = 420f;
        const float headerHeight = 46f;
        const float rowHeight = 52f;

        Image partyBg = NewImage("PartyCard", uiRoot.transform, PanelDark, false);
        RectTransform partyRect = partyBg.rectTransform;
        Place(partyRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-rightMargin, -30f),
            new Vector2(partyWidth, headerHeight + 4 * rowHeight + 8f));

        TMP_Text partyHeader = NewTMP("Header", partyRect, "JUGADORES 0/4", dataFont, 22f, TextMain,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Place(partyHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, 0f), new Vector2(partyWidth - 28f, headerHeight));

        Image headerLine = NewImage("HeaderLine", partyRect, Accent, false);
        Place(headerLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -headerHeight + 2f), new Vector2(partyWidth, 2f));

        LobbyCinematicPartyPresenter.PartyRowView[] rows = new LobbyCinematicPartyPresenter.PartyRowView[4];

        for (int i = 0; i < rows.Length; i++)
        {
            GameObject rowGO = NewUIObject("Row_" + (i + 1), partyRect);
            RectTransform rowRect = (RectTransform)rowGO.transform;
            Place(rowRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -headerHeight - i * rowHeight), new Vector2(partyWidth, rowHeight));

            Image icon = NewImage("Icon", rowRect, Color.white, false);
            icon.preserveAspect = true;
            Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 44f));

            TMP_Text text = NewTMP("Text", rowRect, "", dataFont, 24f, TextMain, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 24f;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(partyWidth - 80f, rowHeight - 6f));

            rowGO.SetActive(false);

            LobbyCinematicPartyPresenter.PartyRowView view = new LobbyCinematicPartyPresenter.PartyRowView();
            view.root = rowRect;
            view.icon = icon;
            view.text = text;
            rows[i] = view;
        }

        // Selección actual (justo arriba de LISTO).
        Image selectionBg = NewImage("SelectionCard", uiRoot.transform, PanelDark, false);
        RectTransform selectionRect = selectionBg.rectTransform;
        Place(selectionRect, bottomRight, bottomRight, new Vector2(-rightMargin, selectionY), new Vector2(rightWidth, selectionHeight));

        Image selectionLine = NewImage("TopLine", selectionRect, Accent, false);
        Place(selectionLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(rightWidth, 3f));

        TMP_Text selectionTitle = NewTMP("Title", selectionRect, "SELECCIÓN ACTUAL", dataFont, 18f, TextDim,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Place(selectionTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(rightWidth - 32f, 24f));

        TMP_Text modeDifficulty = NewTMP("ModeDifficulty", selectionRect, "", dataFont, 28f, TextMain,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        modeDifficulty.enableAutoSizing = true;
        modeDifficulty.fontSizeMin = 16f;
        modeDifficulty.fontSizeMax = 28f;
        modeDifficulty.overflowMode = TextOverflowModes.Ellipsis;
        Place(modeDifficulty.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -42f), new Vector2(rightWidth - 32f, 42f));

        TMP_Text map = NewTMP("Map", selectionRect, "", dataFont, 40f, MapGold, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        map.enableAutoSizing = true;
        map.fontSizeMin = 20f;
        map.fontSizeMax = 40f;
        map.overflowMode = TextOverflowModes.Ellipsis;
        Place(map.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -88f), new Vector2(rightWidth - 32f, 54f));

        // ------------------------------------------------
        // Conectar el presentador con los datos reales.
        // ------------------------------------------------
        world.presenter.ConfigureUI(panelRect, slots, plates, partyRect, partyHeader, rows, modeDifficulty, map);

        // LISTO y visibilidad de CONFIGURAR PARTIDA se refrescan desde
        // LobbyCinematicPartyPresenter. No se agregan responsabilidades al
        // LobbyCinematicPlayerDisplay original.

        EditorUtility.SetDirty(world.presenter);
    }

    /// <summary>Tarjetas, zonas de giro, modelos y columnas viejas: invisibles, pero siguen existiendo.</summary>
    private static void HideOldSlotUI(Transform lobby3D)
    {
        if (lobby3D == null)
            return;

        // Columnas y modelos 3D del preview viejo.
        string[] hiddenObjects = { "Columnas", "Modelos" };

        for (int i = 0; i < hiddenObjects.Length; i++)
        {
            Transform t = FindDirectChild(lobby3D, hiddenObjects[i]);

            if (t != null && t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Ocultar lobby viejo");
                t.gameObject.SetActive(false);
            }
        }

        foreach (Transform slot in lobby3D)
        {
            if (slot.GetComponent<LobbyPlayerSlot>() == null)
                continue;

            Transform card = FindDirectChild(slot, "Tarjeta");

            if (card != null)
            {
                CanvasGroup group = card.GetComponent<CanvasGroup>();

                if (group == null)
                    group = Undo.AddComponent<CanvasGroup>(card.gameObject);

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            // LobbyPlayerSlot re-activa ZonaGiro al azar: se vuelve invisible y sin raycast.
            Transform drag = FindDeep(slot, "ZonaGiro");

            if (drag != null)
            {
                Graphic[] graphics = drag.GetComponentsInChildren<Graphic>(true);

                for (int g = 0; g < graphics.Length; g++)
                {
                    Undo.RecordObject(graphics[g], "Ocultar zona de giro");
                    graphics[g].raycastTarget = false;
                    graphics[g].enabled = false;
                }
            }
        }
    }

    private static LobbyCinematicPartyPresenter.NameplateView CreateNameplate(
        Transform parent,
        int index,
        TMP_FontAsset font,
        out GameObject readyBadge)
    {
        Image bg = NewImage("Nameplate_" + (index + 1), parent, new Color(0.01f, 0.015f, 0.02f, 0.45f), false);
        RectTransform root = bg.rectTransform;

        // Anclas al centro, pivot abajo-centro: el nombre se apoya sobre la cabeza.
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(180f, 36f);

        TMP_Text text = NewTMP("Name", root, "", font, 22f, TextMain, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f);
        text.overflowMode = TextOverflowModes.Overflow;

        // Etiqueta compacta tipo Fortnite: aparece solamente cuando ESTE jugador está listo.
        Image readyBg = NewImage("ReadyBadge", root, new Color(0.05f, 0.58f, 0.27f, 0.94f), false);
        RectTransform readyRect = readyBg.rectTransform;
        Place(readyRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(112f, 26f));

        TMP_Text readyText = NewTMP("ReadyText", readyRect, "LISTO", font, 17f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(readyText.rectTransform, 0f);
        readyText.overflowMode = TextOverflowModes.Overflow;

        readyBadge = readyBg.gameObject;
        readyBadge.SetActive(false);
        bg.gameObject.SetActive(false);

        LobbyCinematicPartyPresenter.NameplateView view = new LobbyCinematicPartyPresenter.NameplateView();
        view.root = root;
        view.nameText = text;
        return view;
    }

    /// <summary>
    /// Degradado oscuro hacia el borde, hecho con capas apiladas de color liso: cada capa cubre un poco menos
    /// que la anterior, asi la oscuridad crece hacia el borde. No usa sprites ni archivos: no puede quedar blanco.
    /// </summary>
    private static void CreateFade(string name, Transform parent, bool top, float height, float edgeAlpha)
    {
        const int layers = 16;

        // Alpha por capa para que, apiladas en el borde, sumen exactamente edgeAlpha.
        float layerAlpha = 1f - Mathf.Pow(1f - Mathf.Clamp01(edgeAlpha), 1f / layers);

        GameObject group = NewUIObject(name, parent);
        Stretch((RectTransform)group.transform, 0f);

        for (int i = 0; i < layers; i++)
        {
            float layerHeight = height * (layers - i) / layers;

            Image image = NewImage(name + "_" + i, group.transform, new Color(0.01f, 0.015f, 0.02f, layerAlpha), false);

            RectTransform rt = image.rectTransform;
            float y = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, y);
            rt.anchorMax = new Vector2(1f, y);
            rt.pivot = new Vector2(0.5f, y);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, layerHeight);
        }
    }

    // =========================================================
    // BÚSQUEDA DE ASSETS
    // =========================================================

    private void AutoFindAssets()
    {
        if (mansionPrefab == null)
            mansionPrefab = FindPrefab(new[] { "Escenario_3", "Escenario 3" }, null);

        if (playerPrefab == null)
            playerPrefab = FindPrefab(new[] { "Player" }, typeof(NetworkWeaponPresentation));

        if (postProcessPrefab == null)
            postProcessPrefab = FindPrefab(new[] { "Post_Process_Mansion", "Post Process Mansion" }, null);

        if (rainPrefab == null)
            rainPrefab = FindPrefab(new[] { "FX_Rain_01" }, null);

        if (rainSplashPrefab == null)
            rainSplashPrefab = FindPrefab(new[] { "FX_Rain_SplashLayer_01" }, null);

        if (fogFlatPrefab == null)
            fogFlatPrefab = FindPrefab(new[] { "FX_Fog_Flat_01" }, null);

        if (fogSmallPrefab == null)
            fogSmallPrefab = FindPrefab(new[] { "FX_Fog_Small_01" }, null);

        Repaint();
    }

    private static GameObject FindPrefab(string[] names, Type requiredComponent)
    {
        foreach (string name in names)
        {
            string[] guids = AssetDatabase.FindAssets(name + " t:Prefab");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (go == null)
                    continue;

                if (requiredComponent != null && go.GetComponentInChildren(requiredComponent, true) == null)
                    continue;

                return go;
            }
        }

        return null;
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

        if (instance == null)
            instance = Instantiate(prefab, parent);

        Undo.RegisterCreatedObjectUndo(instance, "Instanciar " + name);
        instance.name = name;
        return instance;
    }

    private static void PrepareEnvironment(GameObject root)
    {
        foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            string n = behaviour.GetType().Name;

            bool visual =
                n.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Flicker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!visual)
                behaviour.enabled = false;
        }
    }

    private static void CreateFog(GameObject prefab, Transform parent, string name, Vector3 position, float scale)
    {
        GameObject fog = InstantiatePrefab(prefab, parent, name);
        fog.transform.position = position;
        fog.transform.rotation = Quaternion.identity;
        fog.transform.localScale = Vector3.one * scale;
    }

    private static void CreatePointLight(string name, Transform parent, Vector3 position, float intensity, float range, Color color, bool shadows)
    {
        GameObject go = new GameObject(name, typeof(Light));
        Undo.RegisterCreatedObjectUndo(go, "Crear luz lobby");
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        Light light = go.GetComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.range = range;
        light.color = color;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }

    // =========================================================
    // UTILIDADES DE ESCENA Y UI
    // =========================================================

    private static Transform Require(Transform parent, string name)
    {
        Transform found = FindDirectChild(parent, name);

        if (found == null)
            Debug.LogWarning("[LobbyCinematic] No encontré '" + name + "' dentro de " + parent.name + ". Esa parte no se reubica.");

        return found;
    }

    private static Transform FindInScene(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            Transform found = FindDeep(root.transform, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
        }

        return null;
    }

    private static GameObject NewUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear UI lobby");
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
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

    private static TMP_Text NewTMP(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject go = NewUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        if (font != null)
            text.font = font;

        return text;
    }

    private static void Stretch(RectTransform rt, float margin)
    {
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.one * margin;
        rt.offsetMax = Vector2.one * -margin;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (rt == null)
            return;

        Undo.RecordObject(rt, "Reubicar UI");
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>Coloca un objeto real del lobby (texto o botón) y fuerza el texto en una línea, sin apilar letras.</summary>
    private static void PlaceTextRect(Transform target, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        if (target == null)
            return;

        Place(target as RectTransform, anchor, anchor, pos, size);

        TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            Undo.RecordObject(texts[i], "Texto lobby");
            texts[i].enableWordWrapping = false;
            texts[i].overflowMode = TextOverflowModes.Overflow;
            texts[i].enableAutoSizing = false;
            texts[i].fontSize = fontSize;
            texts[i].alignment = alignment;

            // La escena trae margenes viejos (p. ej. placeholder_codigo tiene margen derecho 854): sin esto el
            // texto se corre y el recuadro verde queda desalineado.
            texts[i].margin = Vector4.zero;
        }
    }

    private static void SetText(Transform target, string value)
    {
        if (target == null)
            return;

        TMP_Text text = target.GetComponent<TMP_Text>();

        if (text == null)
            return;

        Undo.RecordObject(text, "Texto lobby");
        text.text = value;
    }

    /// <summary>Oculta un objeto sin desactivarlo, para no romper scripts que lo sigan usando.</summary>
    private static void HideVisually(Transform target)
    {
        if (target == null)
            return;

        CanvasGroup group = target.GetComponent<CanvasGroup>();

        if (group == null)
            group = Undo.AddComponent<CanvasGroup>(target.gameObject);

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
#endif
