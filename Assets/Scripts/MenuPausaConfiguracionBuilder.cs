#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuPausaConfiguracionBuilder
{
    private const string PanelPrincipal = "ConfiguracionesPanel";
    private const string PanelPausa = "ConfiguracionesPanelPausa";

    [MenuItem("Game/Construir UI/Configuración en menú de pausa")]
    public static void Construir()
    {
        GameObject seleccionado = Selection.activeObject as GameObject;

        string playerPath = seleccionado != null
            ? AssetDatabase.GetAssetPath(seleccionado)
            : string.Empty;

        if (
            seleccionado == null ||
            string.IsNullOrEmpty(playerPath) ||
            Path.GetExtension(playerPath).ToLowerInvariant() != ".prefab"
        )
        {
            EditorUtility.DisplayDialog(
                "Configuración en menú de pausa",
                "Seleccioná en la ventana Project el Player.prefab que usa realmente el juego y volvé a ejecutar este menú.",
                "OK"
            );

            return;
        }

        Scene escenaPantallas = default;
        bool cerrarPantallas = false;

        try
        {
            string pantallasPath = BuscarPantallasUIScene();

            if (string.IsNullOrEmpty(pantallasPath))
                throw new Exception("No encontré PantallasUI.unity.");

            escenaPantallas = SceneManager.GetSceneByPath(pantallasPath);

            if (
                !escenaPantallas.IsValid() ||
                !escenaPantallas.isLoaded
            )
            {
                escenaPantallas =
                    EditorSceneManager.OpenScene(
                        pantallasPath,
                        OpenSceneMode.Additive
                    );

                cerrarPantallas = true;
            }

            GameObject panelFuente =
                BuscarEnEscena(
                    escenaPantallas,
                    PanelPrincipal
                );

            if (panelFuente == null)
            {
                throw new Exception(
                    "No encontré ConfiguracionesPanel dentro de PantallasUI. " +
                    "Primero construí y guardá el menú de configuración principal."
                );
            }

            GameObject playerRoot =
                PrefabUtility.LoadPrefabContents(playerPath);

            try
            {
                PauseController pause =
                    playerRoot.GetComponentInChildren<PauseController>(true);

                if (pause == null)
                {
                    throw new Exception(
                        "El prefab seleccionado no contiene PauseController."
                    );
                }

                SerializedObject pauseSO =
                    new SerializedObject(pause);

                SerializedProperty popupProp =
                    pauseSO.FindProperty("popupMenuHome");

                SerializedProperty menuProp =
                    pauseSO.FindProperty("menuConfiguracion");

                if (popupProp == null)
                {
                    throw new Exception(
                        "PauseController no contiene popupMenuHome."
                    );
                }

                if (menuProp == null)
                {
                    throw new Exception(
                        "PauseController no contiene el campo menuConfiguracion. " +
                        "Asegurate de estar usando el PauseController actualizado y de que Unity haya terminado de compilar."
                    );
                }

                GameObject popupPausa =
                    popupProp.objectReferenceValue as GameObject;

                if (popupPausa == null)
                {
                    throw new Exception(
                        "popupMenuHome está vacío en PauseController."
                    );
                }

                Transform uiParent =
                    popupPausa.transform.parent;

                if (uiParent == null)
                {
                    throw new Exception(
                        "El popup de pausa no tiene un padre UI válido."
                    );
                }

                // =====================================================
                // ELIMINAR INTEGRACIÓN ANTERIOR
                // =====================================================

                Transform anterior =
                    playerRoot
                        .GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(
                            t => t.name == PanelPausa
                        );

                if (anterior != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        anterior.gameObject
                    );
                }

                // =====================================================
                // COPIAR EL MENÚ DE CONFIGURACIÓN
                // =====================================================

                GameObject menu =
                    UnityEngine.Object.Instantiate(
                        panelFuente,
                        uiParent,
                        false
                    );

                menu.name = PanelPausa;

                PrepararPanelParaPartida(
                    menu,
                    popupPausa
                );

                menu.SetActive(false);

                // =====================================================
                // ASIGNAR REFERENCIA EN PAUSE CONTROLLER
                // =====================================================

                pauseSO.Update();

                menuProp =
                    pauseSO.FindProperty("menuConfiguracion");

                menuProp.objectReferenceValue = menu;

                pauseSO.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(pause);

                // =====================================================
                // BUSCAR BOTÓN CONFIGURACIÓN
                // =====================================================

                Button botonConfiguracion =
                    EncontrarBotonConfiguracion(
                        popupPausa
                    );

                if (botonConfiguracion == null)
                {
                    throw new Exception(
                        "No pude identificar el botón de Configuración del menú de pausa."
                    );
                }

                Button referencia =
                    EncontrarBotonPorMetodo(
                        popupPausa,
                        "Salir"
                    )
                    ??
                    EncontrarBotonPorMetodo(
                        popupPausa,
                        "ReanudarJuego"
                    );

                // =====================================================
                // TRANSFORMAR TUERCA EN BOTÓN
                // =====================================================

                ConvertirEnBotonConfiguracion(
                    botonConfiguracion,
                    referencia
                );

                // =====================================================
                // CONECTAR BOTÓN
                // =====================================================

                ConectarBoton(
                    botonConfiguracion,
                    pause
                );

                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    botonConfiguracion
                );

                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    pause
                );

                EditorUtility.SetDirty(
                    botonConfiguracion
                );

                EditorUtility.SetDirty(pause);
                EditorUtility.SetDirty(playerRoot);

                // =====================================================
                // GUARDAR PREFAB
                // =====================================================

                PrefabUtility.SaveAsPrefabAsset(
                    playerRoot,
                    playerPath
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    playerRoot
                );
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // =========================================================
            // VERIFICACIÓN
            // =========================================================

            VerificarResultado(playerPath);

            EditorUtility.DisplayDialog(
                "Configuración en menú de pausa",
                "Integración terminada y verificada.\n\n" +
                "Se modificó exactamente:\n" +
                playerPath +
                "\n\n" +
                "• Existe ConfiguracionesPanelPausa.\n" +
                "• PauseController tiene Menu Configuracion asignado.\n" +
                "• El botón llama a AbrirConfiguracion().\n" +
                "• La foto estática del menú principal no se usa durante la partida.",
                "OK"
            );
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);

            EditorUtility.DisplayDialog(
                "Error al integrar Configuración",
                ex.Message,
                "OK"
            );
        }
        finally
        {
            if (
                cerrarPantallas &&
                escenaPantallas.IsValid() &&
                escenaPantallas.isLoaded
            )
            {
                EditorSceneManager.CloseScene(
                    escenaPantallas,
                    true
                );
            }
        }
    }

    // =========================================================
    // PREPARAR MENÚ PARA LA PARTIDA
    // =========================================================

    private static void PrepararPanelParaPartida(
        GameObject menu,
        GameObject popupPausa
    )
    {
        RectTransform rt =
            menu.GetComponent<RectTransform>();

        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = Vector2.zero;
        }

        // =====================================================
        // QUITAR FOTO DEL MENÚ PRINCIPAL
        // =====================================================

        Image rootImage =
            menu.GetComponent<Image>();

        if (rootImage != null)
        {
            rootImage.sprite = null;
            rootImage.color = Color.clear;
            rootImage.raycastTarget = false;
        }

        // =====================================================
        // OSCURECER FONDO
        // =====================================================

        Transform oscurecer =
            BuscarHijo(
                menu.transform,
                "OscurecerFondo"
            );

        if (
            oscurecer != null &&
            oscurecer.TryGetComponent(
                out Image oscurecerImage
            )
        )
        {
            oscurecerImage.sprite = null;

            oscurecerImage.color =
                new Color(
                    0.025f,
                    0.022f,
                    0.025f,
                    0.48f
                );
        }

        // =====================================================
        // SOMBRA LATERAL
        // =====================================================

        Transform sombra =
            BuscarHijo(
                menu.transform,
                "SombraIzquierda"
            );

        if (
            sombra != null &&
            sombra.TryGetComponent(
                out Image sombraImage
            )
        )
        {
            sombraImage.sprite = null;

            sombraImage.color =
                new Color(
                    0.03f,
                    0.027f,
                    0.03f,
                    0.30f
                );
        }

        // =====================================================
        // HACER QUE "VOLVER" REGRESE A PAUSA
        // =====================================================

        MonoBehaviour[] componentes =
            menu.GetComponentsInChildren<MonoBehaviour>(
                true
            );

        foreach (MonoBehaviour componente in componentes)
        {
            if (componente == null)
                continue;

            SerializedObject so =
                new SerializedObject(componente);

            SerializedProperty prop =
                so.FindProperty("menuPrincipalPanel");

            if (
                prop == null ||
                prop.propertyType !=
                    SerializedPropertyType.ObjectReference
            )
            {
                continue;
            }

            prop.objectReferenceValue =
                popupPausa;

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                componente
            );
        }
    }

    // =========================================================
    // BUSCAR BOTÓN CONFIGURACIÓN
    // =========================================================

    private static Button EncontrarBotonConfiguracion(
        GameObject popup
    )
    {
        Button[] botones =
            popup.GetComponentsInChildren<Button>(
                true
            );

        // -----------------------------------------------------
        // 1. Buscar por evento relacionado con configuración.
        // -----------------------------------------------------

        foreach (Button boton in botones)
        {
            int cantidad =
                boton.onClick.GetPersistentEventCount();

            for (int i = 0; i < cantidad; i++)
            {
                string metodo =
                    boton.onClick.GetPersistentMethodName(
                        i
                    );

                if (
                    !string.IsNullOrEmpty(metodo) &&
                    metodo.IndexOf(
                        "config",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {
                    return boton;
                }
            }
        }

        // -----------------------------------------------------
        // 2. Buscar por nombre.
        // -----------------------------------------------------

        foreach (Button boton in botones)
        {
            string nombre =
                boton.name.ToLowerInvariant();

            if (
                nombre.Contains("config") ||
                nombre.Contains("setting") ||
                nombre.Contains("gear") ||
                nombre.Contains("tuerca")
            )
            {
                return boton;
            }
        }

        // -----------------------------------------------------
        // 3. Buscar por texto.
        // -----------------------------------------------------

        foreach (Button boton in botones)
        {
            TMP_Text texto =
                boton.GetComponentInChildren<TMP_Text>(
                    true
                );

            if (texto == null)
                continue;

            string valor =
                (texto.text ?? string.Empty)
                    .Trim()
                    .ToLowerInvariant();

            if (valor.Contains("config"))
                return boton;
        }

        // -----------------------------------------------------
        // 4. Buscar el botón sobrante.
        // -----------------------------------------------------

        Button[] candidatos =
            botones
                .Where(
                    b =>
                        !TieneMetodo(
                            b,
                            "ReanudarJuego"
                        )
                        &&
                        !TieneMetodo(
                            b,
                            "ReiniciarJuego"
                        )
                        &&
                        !TieneMetodo(
                            b,
                            "Salir"
                        )
                )
                .ToArray();

        if (candidatos.Length == 1)
            return candidatos[0];

        return null;
    }

    // =========================================================
    // BUSCAR BOTÓN POR MÉTODO
    // =========================================================

    private static Button EncontrarBotonPorMetodo(
        GameObject root,
        string metodo
    )
    {
        return root
            .GetComponentsInChildren<Button>(
                true
            )
            .FirstOrDefault(
                b => TieneMetodo(
                    b,
                    metodo
                )
            );
    }

    private static bool TieneMetodo(
        Button boton,
        string metodo
    )
    {
        int cantidad =
            boton.onClick.GetPersistentEventCount();

        for (int i = 0; i < cantidad; i++)
        {
            if (
                boton.onClick
                    .GetPersistentMethodName(i)
                ==
                metodo
            )
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // CONECTAR BOTÓN
    // =========================================================

    private static void ConectarBoton(
        Button boton,
        PauseController pause
    )
    {
        // Eliminar todos los eventos persistentes anteriores.
        for (
            int i =
                boton.onClick
                    .GetPersistentEventCount() - 1;
            i >= 0;
            i--
        )
        {
            UnityEventTools.RemovePersistentListener(
                boton.onClick,
                i
            );
        }

        UnityAction accion =
            pause.AbrirConfiguracion;

        UnityEventTools.AddPersistentListener(
            boton.onClick,
            accion
        );

        EditorUtility.SetDirty(
            boton
        );
    }

    // =========================================================
    // CONVERTIR TUERCA EN BOTÓN CONFIGURACIÓN
    // =========================================================

    private static void ConvertirEnBotonConfiguracion(
        Button boton,
        Button referencia
    )
    {
        boton.gameObject.name =
            "BotonConfiguracion";

        RectTransform rt =
            boton.GetComponent<RectTransform>();

        RectTransform refRT =
            referencia != null
                ? referencia.GetComponent<RectTransform>()
                : null;

        if (
            rt != null &&
            refRT != null
        )
        {
            float y =
                rt.anchoredPosition.y;

            rt.anchorMin =
                refRT.anchorMin;

            rt.anchorMax =
                refRT.anchorMax;

            rt.pivot =
                refRT.pivot;

            rt.sizeDelta =
                refRT.sizeDelta;

            rt.anchoredPosition =
                new Vector2(
                    refRT.anchoredPosition.x,
                    y
                );
        }

        // =====================================================
        // FONDO
        // =====================================================

        Image fondo =
            boton.GetComponent<Image>();

        if (fondo == null)
        {
            fondo =
                boton.gameObject.AddComponent<Image>();
        }

        Image fondoReferencia =
            referencia != null
                ? referencia.targetGraphic as Image
                : null;

        if (fondoReferencia != null)
        {
            fondo.sprite =
                fondoReferencia.sprite;

            fondo.material =
                fondoReferencia.material;

            fondo.type =
                fondoReferencia.type;
        }

        fondo.color =
            new Color(
                0.095f,
                0.085f,
                0.09f,
                0.94f
            );

        fondo.raycastTarget = true;

        boton.targetGraphic =
            fondo;

        // =====================================================
        // COLORES
        // =====================================================

        ColorBlock colores =
            boton.colors;

        colores.normalColor =
            Color.white;

        colores.highlightedColor =
            new Color(
                0.82f,
                0.92f,
                0.84f,
                1f
            );

        colores.pressedColor =
            new Color(
                0.62f,
                0.78f,
                0.65f,
                1f
            );

        colores.selectedColor =
            colores.highlightedColor;

        boton.colors =
            colores;

        // =====================================================
        // OCULTAR LA TUERCA
        // =====================================================

        Image[] imagenes =
            boton.GetComponentsInChildren<Image>(
                true
            );

        foreach (Image image in imagenes)
        {
            if (
                image != null &&
                image != fondo
            )
            {
                image.gameObject.SetActive(
                    false
                );
            }
        }

        // =====================================================
        // TEXTO
        // =====================================================

        TMP_Text label =
            boton.GetComponentInChildren<TMP_Text>(
                true
            );

        TMP_Text refLabel =
            referencia != null
                ? referencia.GetComponentInChildren<TMP_Text>(
                    true
                )
                : null;

        if (label == null)
        {
            GameObject textoGO =
                new GameObject(
                    "TextoConfiguracion",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)
                );

            textoGO.transform.SetParent(
                boton.transform,
                false
            );

            label =
                textoGO.GetComponent<TextMeshProUGUI>();
        }

        label.gameObject.SetActive(true);

        label.text =
            "CONFIGURACIÓN";

        label.alignment =
            TextAlignmentOptions.Center;

        label.fontStyle =
            FontStyles.Bold;

        label.enableWordWrapping =
            false;

        label.raycastTarget =
            false;

        if (refLabel != null)
        {
            label.font =
                refLabel.font;

            label.fontSharedMaterial =
                refLabel.fontSharedMaterial;

            label.fontSize =
                Mathf.Max(
                    28f,
                    refLabel.fontSize
                );

            label.color =
                refLabel.color;
        }
        else
        {
            label.fontSize = 30f;

            label.color =
                new Color(
                    0.93f,
                    0.88f,
                    0.84f,
                    1f
                );
        }

        RectTransform labelRT =
            label.rectTransform;

        labelRT.anchorMin =
            Vector2.zero;

        labelRT.anchorMax =
            Vector2.one;

        labelRT.offsetMin =
            new Vector2(
                12f,
                4f
            );

        labelRT.offsetMax =
            new Vector2(
                -12f,
                -4f
            );

        EditorUtility.SetDirty(
            boton
        );

        EditorUtility.SetDirty(
            label
        );
    }

    // =========================================================
    // VERIFICACIÓN FINAL
    // =========================================================

    private static void VerificarResultado(
        string playerPath
    )
    {
        GameObject root =
            PrefabUtility.LoadPrefabContents(
                playerPath
            );

        try
        {
            PauseController pause =
                root.GetComponentInChildren<PauseController>(
                    true
                );

            if (pause == null)
            {
                throw new Exception(
                    "Verificación falló: falta PauseController."
                );
            }

            Transform panel =
                root
                    .GetComponentsInChildren<Transform>(
                        true
                    )
                    .FirstOrDefault(
                        t =>
                            t.name ==
                            PanelPausa
                    );

            if (panel == null)
            {
                throw new Exception(
                    "Verificación falló: ConfiguracionesPanelPausa no quedó guardado dentro del prefab seleccionado."
                );
            }

            SerializedObject so =
                new SerializedObject(
                    pause
                );

            SerializedProperty menuProp =
                so.FindProperty(
                    "menuConfiguracion"
                );

            if (
                menuProp == null ||
                menuProp.objectReferenceValue == null
            )
            {
                throw new Exception(
                    "Verificación falló: Menu Configuracion quedó vacío en PauseController."
                );
            }

            SerializedProperty popupProp =
                so.FindProperty(
                    "popupMenuHome"
                );

            GameObject popup =
                popupProp != null
                    ? popupProp.objectReferenceValue
                        as GameObject
                    : null;

            if (popup == null)
            {
                throw new Exception(
                    "Verificación falló: popupMenuHome quedó vacío."
                );
            }

            Button boton =
                popup
                    .GetComponentsInChildren<Button>(
                        true
                    )
                    .FirstOrDefault(
                        b =>
                            b.name ==
                                "BotonConfiguracion"
                            &&
                            TieneMetodo(
                                b,
                                "AbrirConfiguracion"
                            )
                    );

            if (boton == null)
            {
                throw new Exception(
                    "Verificación falló: el botón CONFIGURACIÓN no quedó conectado a AbrirConfiguracion()."
                );
            }

            Debug.Log(
                "<color=green>[MenuPausaConfiguracionBuilder] VERIFICACIÓN OK: panel, referencia y botón guardados correctamente.</color>"
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(
                root
            );
        }
    }

    // =========================================================
    // BUSCAR PANTALLAS UI
    // =========================================================

    private static string BuscarPantallasUIScene()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "PantallasUI t:Scene"
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid
                );

            if (
                string.Equals(
                    Path.GetFileNameWithoutExtension(
                        path
                    ),
                    "PantallasUI",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return path;
            }
        }

        return null;
    }

    // =========================================================
    // BUSCAR OBJETO EN ESCENA
    // =========================================================

    private static GameObject BuscarEnEscena(
        Scene scene,
        string nombre
    )
    {
        if (
            !scene.IsValid() ||
            !scene.isLoaded
        )
        {
            return null;
        }

        foreach (
            GameObject root
            in scene.GetRootGameObjects()
        )
        {
            Transform encontrado =
                BuscarHijo(
                    root.transform,
                    nombre
                );

            if (encontrado != null)
                return encontrado.gameObject;
        }

        return null;
    }

    // =========================================================
    // BUSCAR HIJO RECURSIVAMENTE
    // =========================================================

    private static Transform BuscarHijo(
        Transform root,
        string nombre
    )
    {
        if (root == null)
            return null;

        if (root.name == nombre)
            return root;

        for (
            int i = 0;
            i < root.childCount;
            i++
        )
        {
            Transform resultado =
                BuscarHijo(
                    root.GetChild(i),
                    nombre
                );

            if (resultado != null)
                return resultado;
        }

        return null;
    }
}
#endif