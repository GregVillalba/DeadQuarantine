#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuCinematicBuilder : EditorWindow
{
    private const string RootName = "MainMenuBackground_Cinematic";

    // Transform exacto que definiste manualmente en la escena.
    private static readonly Vector3 ExactCharacterPosition =
        new Vector3(-2.83f, -1.26f, -12.95f);

    private static readonly Vector3 ExactCharacterEuler =
        new Vector3(0f, 1.89f, 0f);

    private static readonly Vector3 ExactCameraPosition =
        new Vector3(-2.76f, -0.015f, -7.786f);

    private static readonly Vector3 ExactCameraEuler =
        new Vector3(-4.683f, -180.967f, -0.389f);

    [Header("Assets")]
    [SerializeField] private GameObject mansionPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject rainPrefab;
    [SerializeField] private GameObject fogFlatPrefab;
    [SerializeField] private GameObject fogSmallPrefab;
    [SerializeField] private GameObject zombiePrefab;
    [SerializeField] private GameObject postProcessPrefab;

    [Header("Composición")]
    [SerializeField] private float cameraDistance = 11.5f;
    [SerializeField] private float cameraHeight = 2.25f;
    [SerializeField] private float cameraLateralOffset = -0.55f;
    [SerializeField] private float characterRightOffset = 3.15f;
    [SerializeField] private float characterOutsideOffset = 2.75f;
    [SerializeField] private int decorativeZombieCount = 12;
    [SerializeField] private float fountainClearRadius = 2.75f;

    [Header("Opciones")]
    [SerializeField] private bool includeDecorativePlayer = false;
    [SerializeField] private bool useHandgunForMenu = true;
    [SerializeField] private bool hideStaticMenuBackground = true;
    [SerializeField] private bool disablePreviousMainCamera = true;
    [SerializeField] private bool addRain = true;
    [SerializeField] private bool addFog = true;
    [SerializeField] private bool addLightning = true;
    [SerializeField] private bool addCharacterLights = true;
    [SerializeField] private bool addPostProcessPrefab = true;

    private Vector2 scroll;

    [MenuItem("Tools/Dead Quarantine/Menu Principal/Construir fondo cinematografico")]
    public static void Open()
    {
        GetWindow<MainMenuCinematicBuilder>("Menu Cinematico");
    }

    private void OnEnable()
    {
        AutoFindAssets();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Esta versión usa el FRENTE REAL de Escenario_3. La cámara queda afuera mirando la fachada, " +
            "usa el Player real para obtener el modelo/pose/rifle de NetworkWeaponPresentation, agrega lluvia + niebla " +
            "y coloca más zombies decorativos separados en el jardín frontal, respetando una zona libre alrededor de la fuente.",
            MessageType.Info
        );

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

        mansionPrefab = (GameObject)EditorGUILayout.ObjectField("Mansión", mansionPrefab, typeof(GameObject), false);
        playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player real", playerPrefab, typeof(GameObject), false);
        rainPrefab = (GameObject)EditorGUILayout.ObjectField("Lluvia", rainPrefab, typeof(GameObject), false);
        fogFlatPrefab = (GameObject)EditorGUILayout.ObjectField("Niebla plana", fogFlatPrefab, typeof(GameObject), false);
        fogSmallPrefab = (GameObject)EditorGUILayout.ObjectField("Niebla pequeña", fogSmallPrefab, typeof(GameObject), false);
        zombiePrefab = (GameObject)EditorGUILayout.ObjectField("Zombie decorativo", zombiePrefab, typeof(GameObject), false);
        postProcessPrefab = (GameObject)EditorGUILayout.ObjectField("Post Process", postProcessPrefab, typeof(GameObject), false);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Encuadre", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Player y cámara usan ahora los Transform EXACTOS que definiste manualmente. " +
            "El builder ya no recalcula ni corrige esas dos posiciones.",
            MessageType.None
        );

        EditorGUILayout.LabelField(
            "Player",
            "Pos (-2.83, -1.26, -12.95)  Rot (0, 1.89, 0)"
        );

        EditorGUILayout.LabelField(
            "Cámara",
            "Pos (-2.76, -0.015, -7.786)  Rot (-4.683, -180.967, -0.389)"
        );

        decorativeZombieCount = EditorGUILayout.IntSlider("Cantidad zombies", decorativeZombieCount, 0, 16);
        fountainClearRadius = EditorGUILayout.Slider("Radio libre alrededor de la fuente", fountainClearRadius, 2.0f, 4.5f);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Atmósfera", EditorStyles.boldLabel);

        includeDecorativePlayer = EditorGUILayout.ToggleLeft("Mostrar jugador decorativo", includeDecorativePlayer);
        using (new EditorGUI.DisabledScope(!includeDecorativePlayer))
        {
            useHandgunForMenu = EditorGUILayout.ToggleLeft("Usar pistola del Player en vez del rifle (recomendado si la mano del rifle queda rara)", useHandgunForMenu);
        }
        hideStaticMenuBackground = EditorGUILayout.ToggleLeft("Ocultar imagen 2D actual de menuPrincipalPanel", hideStaticMenuBackground);
        disablePreviousMainCamera = EditorGUILayout.ToggleLeft("Desactivar Main Camera anterior", disablePreviousMainCamera);
        addRain = EditorGUILayout.ToggleLeft("Agregar lluvia", addRain);
        addFog = EditorGUILayout.ToggleLeft("Agregar niebla FX", addFog);
        addLightning = EditorGUILayout.ToggleLeft("Agregar relámpagos sutiles", addLightning);
        addCharacterLights = EditorGUILayout.ToggleLeft("Agregar luces cinematográficas al personaje", addCharacterLights);
        addPostProcessPrefab = EditorGUILayout.ToggleLeft("Instanciar prefab de post process", addPostProcessPrefab);

        EditorGUILayout.Space(12f);

        if (GUILayout.Button("BUSCAR ASSETS AUTOMATICAMENTE", GUILayout.Height(30f)))
            AutoFindAssets();

        using (new EditorGUI.DisabledScope(mansionPrefab == null || playerPrefab == null))
        {
            if (GUILayout.Button("CONSTRUIR / RECONSTRUIR FONDO", GUILayout.Height(42f)))
                Build();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "IMPORTANTE: no busca un rifle por nombre. Lee del Player -> NetworkWeaponPresentation -> Weapons -> Rifle " +
            "y usa SU Model, SU Controller de tercera persona y SUS GripPosition/GripRotation.",
            MessageType.None
        );

        EditorGUILayout.EndScrollView();
    }

    private void AutoFindAssets()
    {
        if (mansionPrefab == null)
            mansionPrefab = FindBestPrefab(new[] { "Escenario_3", "Escenario 3" });

        if (playerPrefab == null)
            playerPrefab = FindPlayerPrefab();

        if (rainPrefab == null)
            rainPrefab = FindBestPrefab(new[] { "FX_Rain_01", "Rain" });

        if (fogFlatPrefab == null)
            fogFlatPrefab = FindBestPrefab(new[] { "FX_Fog_Flat_01", "Fog Flat" });

        if (fogSmallPrefab == null)
            fogSmallPrefab = FindBestPrefab(new[] { "FX_Fog_Small_01", "Fog Small" });

        if (zombiePrefab == null)
            zombiePrefab = FindZombiePrefab();

        if (postProcessPrefab == null)
            postProcessPrefab = FindBestPrefab(new[] { "Post_Process_Mansion", "Post Process Mansion" });

        Repaint();
    }

    private void Build()
    {
        if (mansionPrefab == null || playerPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Faltan assets",
                "Necesito como mínimo Escenario_3 y el prefab Player.",
                "OK"
            );
            return;
        }

        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
            Undo.DestroyObjectImmediate(oldRoot);

        Camera previousMainCamera = Camera.main;

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Construir fondo cinematografico");

        // =========================================================
        // MANSIÓN
        // =========================================================
        GameObject mansion = InstantiatePrefab(mansionPrefab, root.transform, "Mansion_Menu");
        mansion.transform.localPosition = Vector3.zero;
        mansion.transform.localRotation = Quaternion.identity;
        mansion.transform.localScale = Vector3.one;

        // La FUENTE es la referencia correcta del frente de la mansión.
        // Así evitamos que Player/Zombies/FX terminen adentro del edificio.
        Transform fountain = FindDeep(mansion.transform, "SM_Env_Fountain_01");

        Vector3 frontAnchor =
            fountain != null
                ? fountain.position
                : FindFrontAnchor(mansion.transform);

        Transform building = FindDeep(mansion.transform, "Building");

        Vector3 buildingCenter =
            building != null
                ? CalculateBounds(building.gameObject).center
                : CalculateBounds(mansion).center;

        // OUTSIDE apunta desde la casa hacia la fuente/cámara.
        Vector3 outsideDirection = frontAnchor - buildingCenter;
        outsideDirection.y = 0f;

        if (outsideDirection.sqrMagnitude < 0.25f)
            outsideDirection = Vector3.back;

        outsideDirection.Normalize();

        Vector3 insideDirection = -outsideDirection;

        // Derecha tal como la ve la cámara cuando mira hacia la mansión.
        Vector3 right =
            Vector3.Cross(Vector3.up, insideDirection).normalized;

        GameObject compositionAnchorGO =
            new GameObject("Fountain_CompositionAnchor");

        compositionAnchorGO.transform.SetParent(root.transform);
        compositionAnchorGO.transform.position = frontAnchor;

        // =========================================================
        // PERSONAJE - PLAYER REAL + RIFLE REAL
        // =========================================================
        GameObject characterAnchorGO = new GameObject("CharacterAnchor");
        characterAnchorGO.transform.SetParent(root.transform);

        // =========================================================
        // TRANSFORM EXACTO DEL PLAYER
        // =========================================================
        // No se calcula desde fuente/spawn ni se ajusta al piso.
        Vector3 characterPosition = ExactCharacterPosition;

        characterAnchorGO.transform.position = ExactCharacterPosition;
        characterAnchorGO.transform.rotation =
            Quaternion.Euler(ExactCharacterEuler);

        // =========================================================
        // TRANSFORM EXACTO DE CÁMARA
        // =========================================================
        Vector3 cameraBase = ExactCameraPosition;
        Quaternion exactCameraRotation =
            Quaternion.Euler(ExactCameraEuler);

        GameObject character = null;

        if (includeDecorativePlayer)
        {
            string preferredWeaponId =
                useHandgunForMenu
                    ? "Handgun"
                    : "Rifle";

            character =
                BuildPlayerDecorCharacter(
                    characterAnchorGO.transform,
                    preferredWeaponId
                );

            if (character == null)
            {
                Debug.LogError(
                    "[MainMenuCinematicBuilder] No pude crear el personaje decorativo desde Player. " +
                    "Revisá Console: tiene que existir NetworkWeaponPresentation y un elemento Weapons con Id = Handgun o Rifle."
                );
            }
            else
            {
                // Se respeta el Transform exacto que definiste.
                character.transform.localPosition = Vector3.zero;
                character.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            characterAnchorGO.SetActive(false);
        }

        // =========================================================
        // ZOMBIES DECORATIVOS EN EL JARDÍN FRONTAL
        // =========================================================
        GameObject zombiesRoot = new GameObject("DecorativeZombies");
        zombiesRoot.transform.SetParent(root.transform);

        if (zombiePrefab != null && decorativeZombieCount > 0)
        {
            CreateDecorativeZombies(
                zombiesRoot.transform,
                mansion.transform,
                frontAnchor,
                outsideDirection,
                right,
                decorativeZombieCount
            );
        }
        else if (decorativeZombieCount > 0)
        {
            Debug.LogWarning(
                "[MainMenuCinematicBuilder] No encontré un prefab de zombie automáticamente. " +
                "Asignalo en el Builder y reconstruí si querés zombies en el fondo."
            );
        }

        // Ya usamos los colliders del escenario para apoyar Player/Zombies en el piso.
        // Recién ahora desactivamos gameplay/físicas de la copia del menú.
        PrepareEnvironment(mansion);

        // =========================================================
        // NIEBLA
        // =========================================================
        if (addFog)
        {
            GameObject fogRoot = new GameObject("MenuFog");
            fogRoot.transform.SetParent(root.transform);

            if (fogFlatPrefab != null)
            {
                CreateFog(
                    fogFlatPrefab,
                    fogRoot.transform,
                    "Fog_Flat_Center",
                    frontAnchor + outsideDirection * 0.55f + Vector3.up * 0.18f,
                    Quaternion.LookRotation(insideDirection, Vector3.up),
                    2.05f
                );

                CreateFog(
                    fogFlatPrefab,
                    fogRoot.transform,
                    "Fog_Flat_Left",
                    frontAnchor + outsideDirection * 1.15f - right * 5.2f + Vector3.up * 0.18f,
                    Quaternion.LookRotation(insideDirection, Vector3.up),
                    1.55f
                );

                CreateFog(
                    fogFlatPrefab,
                    fogRoot.transform,
                    "Fog_Flat_Right",
                    frontAnchor + outsideDirection * 1.05f + right * 5.0f + Vector3.up * 0.18f,
                    Quaternion.LookRotation(insideDirection, Vector3.up),
                    1.55f
                );
            }

            if (fogSmallPrefab != null)
            {
                CreateFog(
                    fogSmallPrefab,
                    fogRoot.transform,
                    "Fog_Small_01",
                    frontAnchor + outsideDirection * 1.6f - right * 2.2f + Vector3.up * 0.42f,
                    Quaternion.identity,
                    1.25f
                );

                CreateFog(
                    fogSmallPrefab,
                    fogRoot.transform,
                    "Fog_Small_02",
                    frontAnchor + outsideDirection * 2.1f + right * 2.4f + Vector3.up * 0.42f,
                    Quaternion.identity,
                    1.35f
                );

                CreateFog(
                    fogSmallPrefab,
                    fogRoot.transform,
                    "Fog_Small_03",
                    frontAnchor + outsideDirection * 0.7f + right * 0.25f + Vector3.up * 0.48f,
                    Quaternion.identity,
                    1.4f
                );
            }
        }

        // =========================================================
        // CÁMARA: EXTERIOR -> FACHADA
        // =========================================================
        GameObject lookTargetGO = new GameObject("LookTarget");
        lookTargetGO.transform.SetParent(root.transform);

        // El punto de mirada se deriva de la ROTACIÓN exacta de tu cámara.
        // Por eso la primera toma queda igual a la segunda captura.
        lookTargetGO.transform.position =
            ExactCameraPosition +
            exactCameraRotation * Vector3.forward * 20f;

        GameObject pathGO = new GameObject("CameraPath");
        pathGO.transform.SetParent(root.transform);

        Transform[] cameraPoints = CreateExactCameraPath(
            pathGO.transform,
            ExactCameraPosition,
            exactCameraRotation
        );

        GameObject cameraGO = new GameObject("MainMenuCamera");
        cameraGO.transform.SetParent(root.transform);

        Camera menuCamera = cameraGO.AddComponent<Camera>();
        menuCamera.fieldOfView = 46f;
        menuCamera.nearClipPlane = 0.08f;
        menuCamera.farClipPlane = 500f;
        menuCamera.clearFlags = CameraClearFlags.Skybox;
        menuCamera.depth = -10f;
        menuCamera.cullingMask = ~0;

        cameraGO.tag = "MainCamera";
        cameraGO.transform.position = ExactCameraPosition;
        cameraGO.transform.rotation = exactCameraRotation;

        EnableUrpPostProcessingIfAvailable(cameraGO);

        if (addPostProcessPrefab && postProcessPrefab != null)
        {
            GameObject postFX =
                InstantiatePrefab(
                    postProcessPrefab,
                    root.transform,
                    "Menu_PostProcess"
                );

            postFX.transform.localPosition = Vector3.zero;
            postFX.transform.localRotation = Quaternion.identity;
            postFX.transform.localScale = Vector3.one;
        }

        MainMenuCinematicBackground cinematic =
            cameraGO.AddComponent<MainMenuCinematicBackground>();

        cinematic.Configure(menuCamera, lookTargetGO.transform, cameraPoints);

        // Si el Canvas estaba en Screen Space Camera, que no pierda la UI al apagar la cámara anterior.
        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                (canvas.worldCamera == null || canvas.worldCamera == previousMainCamera))
            {
                Undo.RecordObject(canvas, "Asignar cámara del menú");
                canvas.worldCamera = menuCamera;
            }
        }

        if (disablePreviousMainCamera && previousMainCamera != null && previousMainCamera != menuCamera)
        {
            Undo.RecordObject(previousMainCamera, "Desactivar Main Camera anterior");
            previousMainCamera.enabled = false;

            if (previousMainCamera.CompareTag("MainCamera"))
                previousMainCamera.tag = "Untagged";
        }

        // =========================================================
        // LLUVIA
        // =========================================================
        if (addRain && rainPrefab != null)
        {
            GameObject rain = InstantiatePrefab(rainPrefab, root.transform, "MenuRain");
            rain.transform.position = menuCamera.transform.position + Vector3.up * 8f;

            MainMenuRainFollower follower = rain.GetComponent<MainMenuRainFollower>();
            if (follower == null)
                follower = rain.AddComponent<MainMenuRainFollower>();

            follower.Configure(menuCamera.transform);
        }

        // Fuerza a que lluvia/niebla sigan animándose incluso si el menú pausa Time.timeScale.
        MainMenuAtmosphere atmosphere = root.AddComponent<MainMenuAtmosphere>();

        // =========================================================
        // LUCES DEL PERSONAJE
        // =========================================================
        if (addCharacterLights && character != null)
        {
            CreatePointLight(
                root.transform,
                "Character_Key_Warm",
                characterPosition + outsideDirection * 2.4f - right * 1.3f + Vector3.up * 2.1f,
                new Color(1f, 0.50f, 0.24f),
                3.1f,
                9f
            );

            CreatePointLight(
                root.transform,
                "Character_Rim_Cold",
                characterPosition + insideDirection * 2.1f + right * 1.7f + Vector3.up * 2.35f,
                new Color(0.27f, 0.52f, 1f),
                2.0f,
                10f
            );
        }

        if (addLightning)
        {
            GameObject flashGO = new GameObject("Storm_Lightning");
            flashGO.transform.SetParent(root.transform);

            Light flash = flashGO.AddComponent<Light>();
            flash.type = LightType.Directional;
            flash.color = new Color(0.67f, 0.78f, 1f);
            flash.intensity = 0f;
            flashGO.transform.rotation = Quaternion.Euler(48f, -25f, 0f);

            MainMenuLightning lightning = flashGO.AddComponent<MainMenuLightning>();
            lightning.Configure(flash);
        }

        // =========================================================
        // QUITAR FONDO 2D ACTUAL, PERO NO LOS BOTONES/LOGO
        // =========================================================
        if (hideStaticMenuBackground)
            HideStaticMenuImage();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(
            "[MainMenuCinematicBuilder] Fondo reconstruido.\n" +
            "Fuente / frente detectado: " + frontAnchor +
            "\nCámara exacta: " + ExactCameraPosition + " / " + ExactCameraEuler +
            "\nPersonaje exacto: " + ExactCharacterPosition + " / " + ExactCharacterEuler +
            "\nEl arma se toma directamente de NetworkWeaponPresentation.Weapons[Handgun o Rifle]. Si la mano del rifle queda rara, activá 'Usar pistola del Player'."
        );

        EditorUtility.DisplayDialog(
            "Menú cinematográfico listo",
            "Se reconstruyó el fondo usando:\n\n" +
            "- Frente real de la mansión\n" +
            "- Player decorativo opcional\n" +
            "- Arma real del Player (pistola o rifle)\n" +
            "- Lluvia\n" +
            "- Niebla FX\n" +
            "- Zombies decorativos en el jardín frontal, más numerosos, más separados y con modelos variados\n\n" +
            "Probalo en Play. Para retocar encuadre mové CameraPath/Point_01...04.",
            "OK"
        );
    }

    // =====================================================================
    // PLAYER / RIFLE REAL
    // =====================================================================

    private GameObject BuildPlayerDecorCharacter(Transform parent, string preferredWeaponId)
    {
        GameObject tempPlayer = null;

        try
        {
            tempPlayer = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            if (tempPlayer == null)
                return null;

            tempPlayer.name = "__TEMP_PLAYER_MENU_SOURCE__";
            tempPlayer.SetActive(false);

            MonoBehaviour presentation =
                FindBehaviourByTypeName(tempPlayer, "NetworkWeaponPresentation");

            if (presentation == null)
            {
                Debug.LogError(
                    "[MainMenuCinematicBuilder] El Player no tiene NetworkWeaponPresentation."
                );
                return null;
            }

            SerializedObject so = new SerializedObject(presentation);
            so.Update();

            SerializedProperty bodyProp = so.FindProperty("Body");
            SerializedProperty animatorProp = so.FindProperty("BodyAnimator");
            SerializedProperty weaponsProp = so.FindProperty("Weapons");

            Transform bodySource = ObjectToTransform(
                bodyProp != null ? bodyProp.objectReferenceValue : null
            );

            Animator sourceAnimator =
                animatorProp != null
                ? animatorProp.objectReferenceValue as Animator
                : null;

            if (bodySource == null && sourceAnimator != null)
                bodySource = sourceAnimator.transform;

            if (bodySource == null)
            {
                Debug.LogError(
                    "[MainMenuCinematicBuilder] NetworkWeaponPresentation.Body no apunta a un Transform válido."
                );
                return null;
            }

            WeaponPresentation selectedWeapon = ReadWeaponPresentation(weaponsProp, preferredWeaponId);

            // Fallback al rifle si el arma pedida no existe.
            if ((selectedWeapon.model == null || selectedWeapon.controller == null) && !string.Equals(preferredWeaponId, "Rifle", StringComparison.OrdinalIgnoreCase))
                selectedWeapon = ReadWeaponPresentation(weaponsProp, "Rifle");

            if (selectedWeapon.model == null || selectedWeapon.controller == null)
            {
                Debug.LogError(
                    "[MainMenuCinematicBuilder] No pude leer el elemento Id = Rifle de NetworkWeaponPresentation.Weapons."
                );
                return null;
            }

            GameObject character =
                Instantiate(bodySource.gameObject, parent);

            character.name = "MenuPlayer_Rifle";
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale = bodySource.localScale;
            character.SetActive(true);

            PrepareVisualOnly(character);

            Animator animator =
                character.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogError(
                    "[MainMenuCinematicBuilder] El Body del Player no contiene Animator."
                );
                DestroyImmediate(character);
                return null;
            }

            animator.gameObject.SetActive(true);
            animator.runtimeAnimatorController = selectedWeapon.controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            // EXACTAMENTE como NetworkWeaponPresentation.OnNetworkSpawn():
            // hand = Body.GetComponentsInChildren<Transform>(true) donde name == Hand_R
            Transform handR =
                FindDeep(character.transform, "Hand_R");

            if (handR == null)
            {
                Debug.LogError(
                    "[MainMenuCinematicBuilder] No encontré Hand_R dentro del Body de tercera persona."
                );
                DestroyImmediate(character);
                return null;
            }

            GameObject rifleGO =
                InstantiatePrefab(
                    selectedWeapon.model,
                    handR,
                    "SM_Wep_FP_Rifle"
                );

            rifleGO.SetActive(true);
            rifleGO.transform.localPosition = selectedWeapon.gripPosition;
            rifleGO.transform.localRotation =
                Quaternion.Euler(selectedWeapon.gripRotation);
            rifleGO.transform.localScale = Vector3.one;

            PrepareVisualOnly(rifleGO);

            SetLayerRecursive(character, 0);
            SetLayerRecursive(rifleGO, 0);

            animator.Rebind();
            animator.Update(0f);

            MainMenuCharacterPose pose =
                character.AddComponent<MainMenuCharacterPose>();

            pose.Configure(
                animator,
                selectedWeapon.animatorWeaponId,
                rifleGO.transform
            );

            ForceVisualRenderers(character);
            ForceVisualRenderers(rifleGO);

            Debug.Log(
                "[MainMenuCinematicBuilder] Arma decorativa usada: " +
                selectedWeapon.id
            );

            return character;
        }
        finally
        {
            if (tempPlayer != null)
                DestroyImmediate(tempPlayer);
        }
    }

    private WeaponPresentation ReadWeaponPresentation(
        SerializedProperty weapons,
        string wantedId)
    {
        WeaponPresentation result = new WeaponPresentation
        {
            id = wantedId
        };

        if (weapons == null || !weapons.isArray)
            return result;

        for (int i = 0; i < weapons.arraySize; i++)
        {
            SerializedProperty element =
                weapons.GetArrayElementAtIndex(i);

            SerializedProperty id =
                element.FindPropertyRelative("Id");

            if (id == null ||
                !string.Equals(
                    id.stringValue,
                    wantedId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SerializedProperty model =
                element.FindPropertyRelative("Model");

            SerializedProperty controller =
                element.FindPropertyRelative("Controller");

            SerializedProperty gripPos =
                element.FindPropertyRelative("GripPosition");

            SerializedProperty gripRot =
                element.FindPropertyRelative("GripRotation");

            result.id =
                id != null ? id.stringValue : wantedId;

            result.model =
                model != null
                    ? model.objectReferenceValue as GameObject
                    : null;

            result.controller =
                controller != null
                    ? controller.objectReferenceValue as RuntimeAnimatorController
                    : null;

            result.gripPosition =
                gripPos != null ? gripPos.vector3Value : Vector3.zero;

            result.gripRotation =
                gripRot != null ? gripRot.vector3Value : Vector3.zero;

            // ThirdPersonAnimatorSync usa CurrentWeaponIndex + 1.
            result.animatorWeaponId = i + 1;
            break;
        }

        return result;
    }

    private struct WeaponPresentation
    {
        public string id;
        public GameObject model;
        public RuntimeAnimatorController controller;
        public Vector3 gripPosition;
        public Vector3 gripRotation;
        public int animatorWeaponId;
    }

    // =====================================================================
    // ZOMBIES
    // =====================================================================

    private void CreateDecorativeZombies(
        Transform parent,
        Transform mansionRoot,
        Vector3 fountain,
        Vector3 outside,
        Vector3 right,
        int count)
    {
        // Más zombies, distribuidos en DOS grupos claros:
        // izquierda y derecha de la fuente.
        //
        // Ningún punto inicial queda dentro del radio libre de la fuente y
        // ningún recorrido cruza de un lado al otro.
        float[] lateralOffsets =
        {
            -10.2f, -8.7f, -7.1f, -5.6f, -4.2f, -3.25f,
              3.25f,  4.2f,  5.6f,  7.1f,  8.7f, 10.2f,
             -6.3f, -4.8f,  4.8f,  6.3f
        };

        float[] outsideOffsets =
        {
            1.45f, 2.15f, 0.75f, 1.70f, 0.55f, 2.55f,
            2.45f, 0.65f, 1.85f, 0.85f, 2.20f, 1.55f,
            3.20f, 3.55f, 3.45f, 3.05f
        };

        int total =
            Mathf.Min(
                count,
                Mathf.Min(
                    lateralOffsets.Length,
                    outsideOffsets.Length
                )
            );

        for (int i = 0; i < total; i++)
        {
            float lateral = lateralOffsets[i];
            float side = Mathf.Sign(lateral);

            Vector3 desiredStart =
                fountain +
                outside * outsideOffsets[i] +
                right * lateral;

            desiredStart =
                PushOutsideFountainRadius(
                    desiredStart,
                    fountain,
                    right,
                    fountainClearRadius
                );

            Vector3 grounded =
                GroundOnMansion(
                    mansionRoot,
                    desiredStart,
                    fountain.y
                );

            GameObject zombie =
                InstantiatePrefab(
                    zombiePrefab,
                    parent,
                    "MenuZombie_" + (i + 1).ToString("00")
                );

            zombie.transform.position = grounded;

            // Miran aproximadamente hacia la cámara, con pequeñas variaciones.
            Vector3 lookDirection =
                outside +
                right * side * 0.10f;

            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.01f)
            {
                zombie.transform.rotation =
                    Quaternion.LookRotation(
                        lookDirection.normalized,
                        Vector3.up
                    );
            }

            TrySelectZombieAppearance(zombie, i);
            PrepareZombieVisual(zombie);

            Animator animator =
                zombie.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogWarning(
                    "[MainMenuCinematicBuilder] " +
                    zombie.name +
                    " no tiene Animator."
                );
                continue;
            }

            animator.applyRootMotion = false;
            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode =
                AnimatorUpdateMode.UnscaledTime;

            MainMenuZombieDecor decor =
                zombie.AddComponent<MainMenuZombieDecor>();

            // 5 quedan en idle. El resto camina, pero SIEMPRE
            // alejándose lateralmente de la fuente desde su propio lado.
            bool walking =
                i >= 5;

            float walkDistance =
                1.7f + (i % 3) * 0.55f;

            Vector3 desiredEnd = grounded;

            if (walking)
            {
                desiredEnd +=
                    right *
                    side *
                    walkDistance;

                // Pequeña variación hacia afuera/atrás para que no todos
                // caminen en líneas paralelas idénticas.
                desiredEnd +=
                    outside *
                    ((i % 2 == 0) ? 0.55f : -0.25f);

                desiredEnd =
                    PushOutsideFountainRadius(
                        desiredEnd,
                        fountain,
                        right,
                        fountainClearRadius
                    );

                desiredEnd =
                    GroundOnMansion(
                        mansionRoot,
                        desiredEnd,
                        grounded.y
                    );
            }

            decor.Configure(
                animator,
                walking,
                grounded,
                desiredEnd,
                0.31f + (i % 5) * 0.035f,
                i % 4,
                i % 5
            );
        }
    }

    private static Vector3 PushOutsideFountainRadius(
        Vector3 position,
        Vector3 fountain,
        Vector3 right,
        float clearRadius)
    {
        Vector3 flatDelta =
            position - fountain;

        flatDelta.y = 0f;

        float radius =
            Mathf.Max(0.5f, clearRadius);

        if (flatDelta.magnitude >= radius)
            return position;

        // Conserva el lado original. Si por algún motivo está exactamente
        // en el centro, lo empuja hacia la derecha.
        float side =
            Mathf.Sign(
                Vector3.Dot(
                    flatDelta,
                    right
                )
            );

        if (Mathf.Approximately(side, 0f))
            side = 1f;

        Vector3 safe =
            fountain +
            right * side * radius;

        safe.y = position.y;
        return safe;
    }

    private void TrySelectZombieAppearance(
        GameObject zombie,
        int uniqueSeed)
    {
        MonoBehaviour appearance =
            FindBehaviourByTypeName(zombie, "ZombieAppearance");

        if (appearance == null)
            return;

        // Priorizamos selección ÚNICA por índice para que no salgan todos iguales.
        try
        {
            SerializedObject so =
                new SerializedObject(appearance);

            SerializedProperty modelsProp =
                so.FindProperty("models");

            Transform modelsRoot =
                ObjectToTransform(
                    modelsProp != null
                        ? modelsProp.objectReferenceValue
                        : null
                );

            if (modelsRoot != null && modelsRoot.childCount > 0)
            {
                int chosenIndex =
                    Mathf.Abs(uniqueSeed) % modelsRoot.childCount;

                for (int i = 0; i < modelsRoot.childCount; i++)
                    modelsRoot.GetChild(i).gameObject.SetActive(i == chosenIndex);

                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[MainMenuCinematicBuilder] No pude forzar modelo único en ZombieAppearance: " +
                ex.Message
            );
        }

        // Fallback si la estructura del prefab cambia.
        MethodInfo method =
            appearance.GetType().GetMethod(
                "SelectRandomModel",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

        if (method == null)
            return;

        try
        {
            UnityEngine.Random.InitState(
                System.Environment.TickCount + uniqueSeed * 997
            );

            method.Invoke(appearance, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[MainMenuCinematicBuilder] No pude ejecutar ZombieAppearance.SelectRandomModel: " +
                ex.Message
            );
        }
    }

    private void PrepareZombieVisual(GameObject go)
    {
        if (go == null)
            return;

        // Primero deshabilitamos/eliminamos gameplay, red e IA.
        // El MainMenuZombieDecor se agrega DESPUÉS de esta limpieza.
        MonoBehaviour[] scripts =
            go.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour script in scripts)
        {
            if (script == null)
                continue;

            DestroyImmediate(script);
        }

        // Sin colisiones ni físicas en el menú.
        foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Evitar cámaras/audio propios del prefab.
        foreach (Camera cam in go.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (AudioListener listener in go.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        // Asegurar que todos los modelos seleccionados puedan renderizarse.
        ForceVisualRenderers(go);

        SetLayerRecursive(go, 0);
        go.SetActive(true);
    }

    // =====================================================================
    // ENCUADRE / FRENTE
    // =====================================================================

    private Vector3 FindFrontAnchor(Transform mansion)
    {
        List<Transform> spawns = new List<Transform>();
        CollectByNamePrefix(mansion, "PlayerSpawn1", spawns);

        if (spawns.Count == 0)
        {
            Transform spawnRoot = FindDeep(mansion, "SpawnPlayer");
            if (spawnRoot != null)
            {
                foreach (Transform child in spawnRoot)
                    spawns.Add(child);
            }
        }

        if (spawns.Count == 0)
        {
            Bounds bounds = CalculateBounds(mansion.gameObject);
            Debug.LogWarning(
                "[MainMenuCinematicBuilder] No encontré PlayerSpawn1. Uso el borde frontal de los bounds."
            );
            return new Vector3(bounds.center.x, bounds.min.y, bounds.min.z);
        }

        Vector3 average = Vector3.zero;

        foreach (Transform spawn in spawns)
            average += spawn.position;

        average /= spawns.Count;

        return average;
    }

    private Transform[] CreateExactCameraPath(
        Transform parent,
        Vector3 basePosition,
        Quaternion baseRotation)
    {
        // Point_01 ES exactamente la cámara de tu captura.
        // Los demás puntos solo agregan unos centímetros de desplazamiento,
        // manteniendo el mismo encuadre general.
        Vector3 cameraRight = baseRotation * Vector3.right;
        Vector3 cameraUp = baseRotation * Vector3.up;
        Vector3 cameraForward = baseRotation * Vector3.forward;

        Vector3[] offsets =
        {
            Vector3.zero,
            cameraRight * 0.12f + cameraUp * 0.015f,
            cameraRight * -0.10f + cameraForward * 0.035f,
            cameraRight * 0.045f - cameraUp * 0.012f
        };

        Transform[] result = new Transform[offsets.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject point =
                new GameObject(
                    "Point_" + (i + 1).ToString("00")
                );

            point.transform.SetParent(parent);
            point.transform.position = basePosition + offsets[i];
            point.transform.rotation = baseRotation;

            result[i] = point.transform;
        }

        return result;
    }

    private Transform[] CreateFrontCameraPath(
        Transform parent,
        Vector3 basePosition,
        Vector3 inside,
        Vector3 right)
    {
        Vector3[] offsets =
        {
            -right * 0.75f,
            right * 0.15f + inside * 0.12f + Vector3.up * 0.04f,
            right * 0.85f + inside * 0.22f,
            -right * 0.05f - inside * 0.08f - Vector3.up * 0.03f
        };

        Transform[] result = new Transform[offsets.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject point = new GameObject("Point_" + (i + 1).ToString("00"));
            point.transform.SetParent(parent);
            point.transform.position = basePosition + offsets[i];
            result[i] = point.transform;
        }

        return result;
    }

    // =====================================================================
    // ATMÓSFERA
    // =====================================================================

    private void CreateFog(
        GameObject prefab,
        Transform parent,
        string name,
        Vector3 position,
        Quaternion rotation,
        float scaleMultiplier)
    {
        if (prefab == null)
            return;

        GameObject fog = InstantiatePrefab(prefab, parent, name);
        fog.transform.position = position;
        fog.transform.rotation = rotation;
        fog.transform.localScale *= scaleMultiplier;
    }

    private void CreatePointLight(
        Transform parent,
        string name,
        Vector3 position,
        Color color,
        float intensity,
        float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    // =====================================================================
    // PREPARACIÓN VISUAL
    // =====================================================================

    private void PrepareEnvironment(GameObject root)
    {
        foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null)
                continue;

            string type = script.GetType().Name;

            if (IsEnvironmentalVisualScript(type))
                continue;

            script.enabled = false;
        }

        // NavMeshAgent deriva de Behaviour, no necesariamente de MonoBehaviour.
        foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name == "NavMeshAgent")
                behaviour.enabled = false;
        }
    }

    private bool IsEnvironmentalVisualScript(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;

        string n = typeName.ToLowerInvariant();

        return
            n.Contains("fog") ||
            n.Contains("niebla") ||
            n.Contains("ambiente") ||
            n.Contains("environment") ||
            n.Contains("volume") ||
            n.Contains("weather") ||
            n.Contains("rain") ||
            n.Contains("fx");
    }

    private void PrepareVisualOnly(GameObject root)
    {
        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script != null)
                DestroyImmediate(script);
        }

        foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        ForceVisualRenderers(root);

        root.SetActive(true);
    }

    private static void ForceVisualRenderers(GameObject root)
    {
        if (root == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;

            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
    }

    private static void AlignVisualFeetToGround(
        GameObject visual,
        float groundY)
    {
        if (visual == null)
            return;

        Renderer[] renderers =
            visual.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
            return;

        bool initialized = false;
        Bounds bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!initialized)
            return;

        float delta = groundY - bounds.min.y;

        // Evita correcciones absurdas si un FX/mesh externo tiene bounds enormes.
        if (Mathf.Abs(delta) <= 3f)
        {
            visual.transform.position += Vector3.up * delta;
        }
    }

    private static Vector3 GroundOnMansion(
        Transform mansionRoot,
        Vector3 position,
        float fallbackY)
    {
        Vector3 origin =
            new Vector3(position.x, fallbackY + 35f, position.z);

        RaycastHit[] hits =
            Physics.RaycastAll(
                origin,
                Vector3.down,
                80f,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(
                hits,
                (a, b) => a.distance.CompareTo(b.distance)
            );

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;

                if (hitTransform != mansionRoot &&
                    !hitTransform.IsChildOf(mansionRoot))
                {
                    continue;
                }

                // Piso, vereda, tierra, escalones bajos; evita paredes/troncos.
                if (hit.normal.y < 0.45f)
                    continue;

                return hit.point + Vector3.up * 0.02f;
            }
        }

        position.y = fallbackY;
        return position;
    }

    private void HideStaticMenuImage()
    {
        GameObject panel = GameObject.Find("menuPrincipalPanel");

        if (panel == null)
        {
            Debug.LogWarning(
                "[MainMenuCinematicBuilder] No encontré menuPrincipalPanel para ocultar la imagen 2D."
            );
            return;
        }

        Image image = panel.GetComponent<Image>();

        if (image != null)
        {
            Undo.RecordObject(image, "Ocultar fondo 2D del menú");
            image.enabled = false;
        }
        else
        {
            Debug.LogWarning(
                "[MainMenuCinematicBuilder] menuPrincipalPanel no tiene Image directo."
            );
        }
    }

    // =====================================================================
    // AUTO FIND
    // =====================================================================

    private GameObject FindPlayerPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("Player t:Prefab");

        GameObject best = null;
        int bestScore = int.MinValue;

        foreach (string guid in guids)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );

            if (prefab == null)
                continue;

            int score = 0;

            if (string.Equals(prefab.name, "Player", StringComparison.OrdinalIgnoreCase))
                score += 100;

            if (FindBehaviourByTypeName(prefab, "NetworkWeaponPresentation") != null)
                score += 300;

            if (score > bestScore)
            {
                bestScore = score;
                best = prefab;
            }
        }

        return best;
    }

    private GameObject FindZombiePrefab()
    {
        string[] guids = AssetDatabase.FindAssets("Zombie t:Prefab");

        GameObject best = null;
        int bestScore = int.MinValue;

        foreach (string guid in guids)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid)
                );

            if (prefab == null)
                continue;

            string lower = prefab.name.ToLowerInvariant();

            if (lower.Contains("dog") ||
                lower.Contains("perro") ||
                lower.Contains("boss") ||
                lower.Contains("jefe") ||
                lower.Contains("spawn"))
            {
                continue;
            }

            int score = 0;

            if (lower == "zombie")
                score += 150;

            if (prefab.GetComponentInChildren<Animator>(true) != null)
                score += 50;

            if (FindBehaviourByTypeName(prefab, "ZombieAI") != null)
                score += 200;

            if (FindBehaviourByTypeName(prefab, "ZombieHealth") != null)
                score += 100;

            if (score > bestScore)
            {
                bestScore = score;
                best = prefab;
            }
        }

        return best;
    }

    private GameObject FindBestPrefab(string[] possibleNames)
    {
        GameObject best = null;
        int bestScore = int.MinValue;

        foreach (string query in possibleNames)
        {
            string[] guids = AssetDatabase.FindAssets(query + " t:Prefab");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    continue;

                int score = 0;

                foreach (string wanted in possibleNames)
                {
                    if (string.Equals(prefab.name, wanted, StringComparison.OrdinalIgnoreCase))
                        score += 100;
                    else if (prefab.name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 20;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = prefab;
                }
            }
        }

        return best;
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
    {
        GameObject instance =
            PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

        if (instance == null)
            instance = Instantiate(prefab, parent);

        instance.name = name;
        return instance;
    }

    private static MonoBehaviour FindBehaviourByTypeName(GameObject root, string typeName)
    {
        if (root == null)
            return null;

        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (string.Equals(
                behaviour.GetType().Name,
                typeName,
                StringComparison.OrdinalIgnoreCase))
            {
                return behaviour;
            }
        }

        return null;
    }

    private static Transform ObjectToTransform(UnityEngine.Object value)
    {
        if (value is Transform t)
            return t;

        if (value is GameObject go)
            return go.transform;

        if (value is Component component)
            return component.transform;

        return null;
    }

    private static Transform FindDeep(Transform root, string exactName)
    {
        if (root == null)
            return null;

        if (root.name == exactName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, exactName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void CollectByNamePrefix(
        Transform root,
        string prefix,
        List<Transform> results)
    {
        if (root == null)
            return;

        if (root.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            results.Add(root);

        foreach (Transform child in root)
            CollectByNamePrefix(child, prefix, results);
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;

        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static void EnableUrpPostProcessingIfAvailable(GameObject cameraGO)
    {
        Type type = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");

        if (type == null)
            return;

        Component component = cameraGO.GetComponent(type);

        if (component == null)
            component = cameraGO.AddComponent(type);

        SerializedObject so = new SerializedObject(component);
        SerializedProperty post = so.FindProperty("m_RenderPostProcessing");

        if (post != null)
        {
            post.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
                return type;
        }

        return null;
    }
}
#endif
