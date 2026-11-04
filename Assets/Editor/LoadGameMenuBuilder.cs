
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEngine.EventSystems;

public static class LoadGameMenuBuilder
{
    [MenuItem("Tools/DeadQuarantineII/Construir menú de carga")]
    public static void ConstruirMenu()
    {
        if (GameObject.Find("LoadGameCanvas") != null)
        {
            EditorUtility.DisplayDialog(
                "Menú existente",
                "Ya existe LoadGameCanvas en esta escena.",
                "Aceptar"
            );
            return;
        }

        // EVENT SYSTEM
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            Undo.RegisterCreatedObjectUndo(eventSystem, "Crear EventSystem");
        }

        // CANVAS
        GameObject canvasObject = new GameObject("LoadGameCanvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObject.AddComponent<GraphicRaycaster>();

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Construir menú de carga"
        );

        // PANEL COMPLETO
        GameObject panel = CrearObjeto(
            canvasObject.transform,
            "PanelCarga"
        );

        Image fondo = panel.AddComponent<Image>();
        fondo.color = new Color(0.025f, 0.035f, 0.05f, 0.98f);

        Estirar(panel.GetComponent<RectTransform>());

        // PANEL CENTRAL
        GameObject contenido = CrearObjeto(
            panel.transform,
            "PanelPrincipal"
        );

        Image imagen = contenido.AddComponent<Image>();
        imagen.color = new Color(0.07f, 0.09f, 0.12f, 0.98f);

        Posicionar(
            contenido.GetComponent<RectTransform>(),
            new Vector2(0, 0),
            new Vector2(700, 650)
        );

        // TITULO
        TMP_Text titulo = CrearTexto(
            contenido.transform,
            "Titulo",
            "CARGAR PARTIDA",
            42,
            FontStyles.Bold
        );

        Posicionar(titulo.rectTransform, new Vector2(0, 245),
            new Vector2(600, 65));

        TMP_Text subtitulo = CrearTexto(
            contenido.transform,
            "Subtitulo",
            "Seleccioná el modo de juego",
            22,
            FontStyles.Normal
        );

        Posicionar(subtitulo.rectTransform, new Vector2(0, 190),
            new Vector2(600, 40));

        // BOTONES
        Button historia = CrearBoton(
            contenido.transform,
            "BotonHistoria",
            "MODO HISTORIA",
            new Vector2(0, 95),
            new Color(0.16f, 0.21f, 0.27f)
        );

        Button supervivencia = CrearBoton(
            contenido.transform,
            "BotonSupervivencia",
            "SUPERVIVENCIA - NO DISPONIBLE",
            new Vector2(0, 10),
            new Color(0.12f, 0.12f, 0.12f)
        );

        supervivencia.interactable = false;

        TMP_Text informacion = CrearTexto(
            contenido.transform,
            "InformacionCheckpoint",
            "Seleccioná Historia para consultar tu partida.",
            20,
            FontStyles.Normal
        );

        Posicionar(informacion.rectTransform, new Vector2(0, -100),
            new Vector2(600, 100));

        Button cargar = CrearBoton(
            contenido.transform,
            "BotonCargar",
            "CONTINUAR PARTIDA",
            new Vector2(0, -210),
            new Color(0.55f, 0.22f, 0.12f)
        );

        cargar.interactable = false;

        Button volver = CrearBoton(
            contenido.transform,
            "BotonVolver",
            "VOLVER",
            new Vector2(0, -290),
            new Color(0.16f, 0.21f, 0.27f)
        );

        // CONTROLADOR EN EL CANVAS ACTIVO
        LoadGameMenuController controller =
            canvasObject.AddComponent<LoadGameMenuController>();

        SerializedObject serializedController =
            new SerializedObject(controller);

        serializedController.FindProperty("panelCarga")
            .objectReferenceValue = panel;

        serializedController.FindProperty("botonHistoria")
            .objectReferenceValue = historia;

        serializedController.FindProperty("botonCargar")
            .objectReferenceValue = cargar;

        serializedController.FindProperty("botonVolver")
            .objectReferenceValue = volver;

        serializedController.FindProperty("informacion")
            .objectReferenceValue = informacion;

        serializedController.FindProperty("subtitulo")
            .objectReferenceValue = subtitulo;

        serializedController.ApplyModifiedProperties();

        // EL PANEL COMIENZA OCULTO
        panel.SetActive(false);

        EditorUtility.SetDirty(controller);

        Selection.activeGameObject = canvasObject;

        Debug.Log(
            "[LoadGameMenuBuilder] Menú de carga creado y oculto correctamente."
        );
    }

    // =====================================================
    // UTILIDADES
    // =====================================================

    private static GameObject CrearObjeto(
        Transform padre,
        string nombre)
    {
        GameObject objeto = new GameObject(
            nombre,
            typeof(RectTransform)
        );

        objeto.transform.SetParent(padre, false);

        return objeto;
    }

    private static TMP_Text CrearTexto(
        Transform padre,
        string nombre,
        string contenido,
        float tamaño,
        FontStyles estilo)
    {
        GameObject objeto = CrearObjeto(padre, nombre);

        TMP_Text texto = objeto.AddComponent<TextMeshProUGUI>();

        texto.text = contenido;
        texto.fontSize = tamaño;
        texto.fontStyle = estilo;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.Center;
        texto.enableWordWrapping = true;

        return texto;
    }

    private static Button CrearBoton(
        Transform padre,
        string nombre,
        string contenido,
        Vector2 posicion,
        Color color)
    {
        GameObject objeto = CrearObjeto(padre, nombre);

        Image imagen = objeto.AddComponent<Image>();
        imagen.color = color;

        Button boton = objeto.AddComponent<Button>();
        boton.targetGraphic = imagen;

        Posicionar(
            objeto.GetComponent<RectTransform>(),
            posicion,
            new Vector2(540, 65)
        );

        TMP_Text texto = CrearTexto(
            objeto.transform,
            "Texto",
            contenido,
            22,
            FontStyles.Bold
        );

        Estirar(texto.rectTransform);

        return boton;
    }

    private static void Posicionar(
        RectTransform rect,
        Vector2 posicion,
        Vector2 tamaño)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamaño;
    }

    private static void Estirar(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
