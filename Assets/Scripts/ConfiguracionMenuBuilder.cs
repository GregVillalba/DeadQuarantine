#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye el menú de Configuración V3 usando como fondo la misma imagen
/// y tint del menuPrincipalPanel.
///
/// Menú:
/// PANTALLA / GRÁFICOS / AVANZADO / RENDIMIENTO / AUDIO / SENSIBILIDAD / CONTROLES
///
/// Los cambios no se aplican hasta pulsar APLICAR.
/// </summary>
public static class ConfiguracionMenuBuilder
{
    private const string TitleFontGuid =
        "0d82eaf50c142fa4ba34beb1fc9a12e4";

    private const string MonoFontGuid =
        "102734b590693d240886df79eaaa686c";

    private static readonly Color Accent =
        new Color(0.38f, 0.78f, 0.48f, 1f);

    private static readonly Color AccentSoft =
        new Color(0.38f, 0.78f, 0.48f, 0.50f);

    private static readonly Color Warm =
        new Color(0.91f, 0.84f, 0.81f, 1f);

    private static readonly Color Muted =
        new Color(0.47f, 0.57f, 0.65f, 1f);

    // Los valores reales de estos fondos se recalculan durante Build()
    // usando el mismo tint de menuPrincipalPanel.
    private static Color Panel =
        new Color(0.105f, 0.095f, 0.105f, 0.76f);

    private static Color PanelStrong =
        new Color(0.080f, 0.072f, 0.082f, 0.88f);

    private static Color Row =
        new Color(0.145f, 0.130f, 0.140f, 0.58f);

    private static readonly Color Amber =
        new Color(0.91f, 0.69f, 0.37f, 1f);

    [MenuItem("Game/Construir UI/Configuraciones")]
    public static void Build()
    {
        Canvas canvas = FindCanvas();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Configuraciones",
                "No encontré un Canvas en la escena.",
                "OK"
            );
            return;
        }

        GameObject menuPrincipal =
            FindRecursive(
                canvas.transform,
                "menuPrincipalPanel"
            );

        if (menuPrincipal == null)
        {
            EditorUtility.DisplayDialog(
                "Configuraciones",
                "No encontré menuPrincipalPanel.",
                "OK"
            );
            return;
        }

        TMP_FontAsset titleFont =
            LoadFont(TitleFontGuid) ??
            TMP_Settings.defaultFontAsset;

        TMP_FontAsset monoFont =
            LoadFont(MonoFontGuid) ??
            titleFont;

        DestroyIfExists(canvas.transform, "ConfiguracionesPanel");
        DestroyIfExists(canvas.transform, "ConfiguracionesPanel");
        DestroyIfExists(canvas.transform, "ConfiguracionesPanel_V2");

        GameObject oldConfig =
            FindCurrentConfigPanel();

        GameObject root =
            NewUI(
                "ConfiguracionesPanel",
                canvas.transform
            );

        Stretch(root.GetComponent<RectTransform>());

        // MISMO FONDO DEL MENÚ PRINCIPAL.
        Image mainImage =
            menuPrincipal.GetComponent<Image>();

        // Mantener la misma familia cromática del menú principal.
        // El panel principal de PantallasUI usa un tint rosado/grisáceo;
        // derivamos de él los fondos translúcidos, pero oscurecidos para
        // conservar contraste y legibilidad.
        Color menuTint =
            mainImage != null
                ? mainImage.color
                : new Color(0.819f, 0.734f, 0.774f, 1f);

        Color panelBase =
            Color.Lerp(
                Color.black,
                menuTint,
                0.19f
            );

        Color panelStrongBase =
            Color.Lerp(
                Color.black,
                menuTint,
                0.145f
            );

        Color rowBase =
            Color.Lerp(
                Color.black,
                menuTint,
                0.255f
            );

        Panel =
            new Color(
                panelBase.r,
                panelBase.g,
                panelBase.b,
                0.76f
            );

        PanelStrong =
            new Color(
                panelStrongBase.r,
                panelStrongBase.g,
                panelStrongBase.b,
                0.88f
            );

        Row =
            new Color(
                rowBase.r,
                rowBase.g,
                rowBase.b,
                0.58f
            );

        Image background =
            root.AddComponent<Image>();

        if (mainImage != null)
        {
            background.sprite = mainImage.sprite;
            background.color = mainImage.color;
            background.type = mainImage.type;
            background.preserveAspect = mainImage.preserveAspect;
        }
        else
        {
            background.color =
                new Color(0.08f, 0.08f, 0.09f, 1f);
        }

        // Oscurece la foto sin reemplazarla.
        GameObject shade =
            MakeImage(
                root.transform,
                "OscurecerFondo",
                new Color(0.015f, 0.012f, 0.015f, 0.36f)
            );

        Stretch(shade.GetComponent<RectTransform>());

        // Viñeta lateral similar a un menú de juego.
        GameObject leftShade =
            MakeImage(
                root.transform,
                "SombraIzquierda",
                new Color(0.020f, 0.016f, 0.020f, 0.30f)
            );

        SetRect(
            leftShade.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(0.55f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        ConfiguracionMenuController controller =
            root.AddComponent<ConfiguracionMenuController>();

        // HEADER
        GameObject header =
            MakeImage(
                root.transform,
                "Header",
                new Color(0.075f, 0.065f, 0.072f, 0.76f)
            );

        SetRect(
            header.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -132f),
            Vector2.zero
        );

        MakeImage(
            header.transform,
            "AccentLine",
            AccentSoft,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 3f)
        );

        TMP_Text title =
            MakeText(
                header.transform,
                "Titulo",
                "CONFIGURACIÓN",
                titleFont,
                66f,
                Warm,
                FontStyles.Bold
            );

        SetRect(
            title.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.55f, 1f),
            new Vector2(68f, 10f),
            new Vector2(0f, -4f)
        );

        title.alignment =
            TextAlignmentOptions.MidlineLeft;

        TMP_Text sub =
            MakeText(
                header.transform,
                "Subtitulo",
                "DEAD QUARANTINE // SISTEMA Y JUGABILIDAD",
                monoFont,
                23f,
                Muted,
                FontStyles.Bold
            );

        SetRect(
            sub.rectTransform,
            new Vector2(0.55f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 8f),
            new Vector2(-68f, -8f)
        );

        sub.alignment =
            TextAlignmentOptions.MidlineRight;

        // NAV
        GameObject nav =
            MakeImage(
                root.transform,
                "Navegacion",
                PanelStrong
            );

        SetRect(
            nav.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(52f, 78f),
            new Vector2(390f, -160f)
        );

        TMP_Text navHeader =
            MakeText(
                nav.transform,
                "Categorias",
                "CATEGORÍAS",
                monoFont,
                21f,
                Accent,
                FontStyles.Bold
            );

        SetRect(
            navHeader.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(24f, -62f),
            new Vector2(-20f, -18f)
        );

        navHeader.alignment =
            TextAlignmentOptions.MidlineLeft;

        string[] navNames =
        {
            "PANTALLA",
            "GRÁFICOS",
            "AVANZADO",
            "RENDIMIENTO",
            "AUDIO",
            "SENSIBILIDAD",
            "CONTROLES"
        };

        Button[] navButtons =
            new Button[navNames.Length];

        TMP_Text[] navTexts =
            new TMP_Text[navNames.Length];

        float navY = -82f;

        for (int i = 0; i < navNames.Length; i++)
        {
            Button button =
                MakeNavButton(
                    nav.transform,
                    navNames[i],
                    titleFont,
                    navY
                );

            navButtons[i] = button;
            navTexts[i] =
                button.GetComponentInChildren<TMP_Text>(true);

            navY -= 82f;
        }

        TMP_Text navBottom =
            MakeText(
                nav.transform,
                "Nota",
                "LOS CAMBIOS SE ACTIVAN\nAL PULSAR APLICAR",
                monoFont,
                17f,
                Muted,
                FontStyles.Bold
            );

        SetRect(
            navBottom.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(24f, 20f),
            new Vector2(-20f, 86f)
        );

        navBottom.alignment =
            TextAlignmentOptions.BottomLeft;

        // CONTENT
        GameObject content =
            MakeImage(
                root.transform,
                "Contenido",
                Panel
            );

        SetRect(
            content.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(418f, 78f),
            new Vector2(-52f, -160f)
        );

        GameObject[] sections =
            new GameObject[7];

        // PANTALLA
        sections[0] =
            MakeSection(
                content.transform,
                "Seccion_Pantalla",
                "PANTALLA",
                "RESOLUCIÓN Y MODO DE VISUALIZACIÓN",
                titleFont,
                monoFont
            );

        TMP_Dropdown resolution =
            AddDropdownRow(
                sections[0].transform,
                0,
                "RESOLUCIÓN",
                "Resolución final enviada al monitor.",
                titleFont,
                monoFont
            );

        TMP_Dropdown screenMode =
            AddDropdownRow(
                sections[0].transform,
                1,
                "MODO DE PANTALLA",
                "Pantalla completa, sin bordes o ventana.",
                titleFont,
                monoFont
            );

        AddInfo(
            sections[0].transform,
            -350f,
            "PANTALLA",
            "Los cambios de resolución y modo se ejecutan únicamente al pulsar APLICAR.",
            titleFont,
            monoFont
        );

        // GRÁFICOS
        sections[1] =
            MakeSection(
                content.transform,
                "Seccion_Graficos",
                "GRÁFICOS",
                "CALIDAD GENERAL",
                titleFont,
                monoFont
            );

        TMP_Dropdown preset =
            AddDropdownRow(
                sections[1].transform,
                0,
                "PRESET GRÁFICO",
                "Muy Bajo, Bajo, Normal, Alto o Ultra.",
                titleFont,
                monoFont
            );

        TMP_Dropdown renderScale =
            AddDropdownRow(
                sections[1].transform,
                1,
                "ESCALA DE RENDER",
                "Resolución interna de URP. Tiene gran impacto en FPS.",
                titleFont,
                monoFont
            );

        TMP_Text presetState =
            MakeText(
                sections[1].transform,
                "PresetEstado",
                "PERFIL  //  NORMAL",
                monoFont,
                20f,
                Accent,
                FontStyles.Bold
            );

        SetRect(
            presetState.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(34f, -340f),
            new Vector2(-34f, -292f)
        );

        AddInfo(
            sections[1].transform,
            -405f,
            "PERSONALIZADO",
            "Al modificar un ajuste individual, el preset pasa a PERSONALIZADO sin cambiar el resto.",
            titleFont,
            monoFont
        );

        // AVANZADO
        sections[2] =
            MakeSection(
                content.transform,
                "Seccion_Avanzado",
                "AVANZADO",
                "AJUSTES INDIVIDUALES",
                titleFont,
                monoFont
            );

        TMP_Dropdown textures =
            AddDropdownRow(sections[2].transform, 0, "TEXTURAS", "Calidad de mipmaps y uso de VRAM.", titleFont, monoFont, 78f);

        TMP_Dropdown shadows =
            AddDropdownRow(sections[2].transform, 1, "SOMBRAS", "Resolución y suavidad de sombras.", titleFont, monoFont, 78f);

        TMP_Dropdown shadowDistance =
            AddDropdownRow(sections[2].transform, 2, "DISTANCIA DE SOMBRAS", "Hasta dónde se dibujan las sombras.", titleFont, monoFont, 78f);

        TMP_Dropdown aa =
            AddDropdownRow(sections[2].transform, 3, "ANTI-ALIASING", "Suavizado de bordes.", titleFont, monoFont, 78f);

        TMP_Dropdown aniso =
            AddDropdownRow(sections[2].transform, 4, "FILTRADO ANISOTRÓPICO", "Nitidez de superficies en ángulo.", titleFont, monoFont, 78f);

        TMP_Dropdown lod =
            AddDropdownRow(sections[2].transform, 5, "DISTANCIA DE DETALLE", "Distancia de cambio entre LODs.", titleFont, monoFont, 78f);

        Toggle reflections =
            AddToggleRow(sections[2].transform, 6, "REFLEJOS EN TIEMPO REAL", "Reflection Probes dinámicos.", titleFont, monoFont, 78f);

        Toggle particles =
            AddToggleRow(sections[2].transform, 7, "PARTÍCULAS SUAVES", "Intersección suave de humo y partículas.", titleFont, monoFont, 78f);

        Toggle post =
            AddToggleRow(sections[2].transform, 8, "POST-PROCESADO", "Efectos URP de las cámaras de juego.", titleFont, monoFont, 78f);

        // RENDIMIENTO
        sections[3] =
            MakeSection(
                content.transform,
                "Seccion_Rendimiento",
                "RENDIMIENTO",
                "FPS Y DIAGNÓSTICO",
                titleFont,
                monoFont
            );

        TMP_Dropdown fpsLimit =
            AddDropdownRow(
                sections[3].transform,
                0,
                "LÍMITE DE FPS",
                "Cantidad máxima de cuadros por segundo.",
                titleFont,
                monoFont
            );

        Toggle vsync =
            AddToggleRow(
                sections[3].transform,
                1,
                "V-SYNC",
                "Sincroniza el juego con la frecuencia del monitor.",
                titleFont,
                monoFont
            );

        Toggle showFps =
            AddToggleRow(
                sections[3].transform,
                2,
                "MOSTRAR FPS",
                "Contador de rendimiento en la esquina superior.",
                titleFont,
                monoFont
            );

        TMP_Text vsyncInfo =
            MakeText(
                sections[3].transform,
                "VSyncAviso",
                "",
                monoFont,
                16f,
                Muted,
                FontStyles.Normal
            );

        SetRect(
            vsyncInfo.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(34f, -405f),
            new Vector2(-34f, -365f)
        );

        GameObject hwBox =
            MakeImage(
                sections[3].transform,
                "HardwareBox",
                new Color(0.090f, 0.078f, 0.087f, 0.78f)
            );

        SetRect(
            hwBox.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(34f, 28f),
            new Vector2(-34f, 285f)
        );

        MakeImage(
            hwBox.transform,
            "Linea",
            AccentSoft,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(4f, 0f)
        );

        TMP_Text hardware =
            MakeText(
                hwBox.transform,
                "HardwareText",
                "",
                monoFont,
                17f,
                Muted,
                FontStyles.Normal
            );

        Stretch(hardware.rectTransform, 24f, 18f, 24f, 18f);
        hardware.alignment = TextAlignmentOptions.TopLeft;

        // AUDIO
        sections[4] =
            MakeSection(
                content.transform,
                "Seccion_Audio",
                "AUDIO",
                "VOLUMEN GENERAL",
                titleFont,
                monoFont
            );

        TMP_Text volumeValue;
        Slider volume =
            AddSliderRow(
                sections[4].transform,
                0,
                "VOLUMEN GENERAL",
                "Volumen maestro de sonidos, música y ambiente.",
                titleFont,
                monoFont,
                out volumeValue
            );

        AddInfo(
            sections[4].transform,
            -305f,
            "AUDIO",
            "El nuevo volumen se escucha después de pulsar APLICAR.",
            titleFont,
            monoFont
        );

        // SENSIBILIDAD
        sections[5] =
            MakeSection(
                content.transform,
                "Seccion_Sensibilidad",
                "SENSIBILIDAD",
                "CONTROL DEL MOUSE",
                titleFont,
                monoFont
            );

        TMP_Text sensValue;
        Slider sensitivity =
            AddSliderRow(
                sections[5].transform,
                0,
                "SENSIBILIDAD DEL MOUSE",
                "Velocidad de giro horizontal y vertical del jugador.",
                titleFont,
                monoFont,
                out sensValue
            );

        AddInfo(
            sections[5].transform,
            -305f,
            "SENSIBILIDAD",
            "El PlayerLook que ya esté spawneado recibe el nuevo valor al pulsar APLICAR.",
            titleFont,
            monoFont
        );

        // CONTROLES
        sections[6] =
            MakeSection(
                content.transform,
                "Seccion_Controles",
                "CONTROLES",
                "ASIGNACIÓN DE TECLAS Y MOUSE",
                titleFont,
                monoFont
            );

        GameObject controlsScroll =
            MakeControlsScroll(
                sections[6].transform,
                titleFont,
                monoFont
            );

        ControlesRebind rebind =
            sections[6].AddComponent<ControlesRebind>();

        // FOOTER
        GameObject footer =
            MakeImage(
                root.transform,
                "Footer",
                new Color(0.070f, 0.060f, 0.068f, 0.82f)
            );

        SetRect(
            footer.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            Vector2.zero,
            new Vector2(0f, 60f)
        );

        TMP_Text status =
            MakeText(
                footer.transform,
                "EstadoCambios",
                "SIN CAMBIOS PENDIENTES",
                monoFont,
                18f,
                Muted,
                FontStyles.Bold
            );

        SetRect(
            status.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.55f, 1f),
            new Vector2(68f, 0f),
            Vector2.zero
        );

        status.alignment =
            TextAlignmentOptions.MidlineLeft;

        Button reset =
            MakeFooterButton(
                footer.transform,
                "RESTABLECER",
                titleFont,
                -550f,
                190f,
                false
            );

        Button apply =
            MakeFooterButton(
                footer.transform,
                "APLICAR",
                titleFont,
                -340f,
                170f,
                true
            );

        Button back =
            MakeFooterButton(
                footer.transform,
                "VOLVER",
                titleFont,
                -150f,
                130f,
                false
            );

        // CONECTAR
        SerializedObject so =
            new SerializedObject(controller);

        SetObj(so, "menuPrincipalPanel", menuPrincipal);
        SetArray(so, "secciones", sections);
        SetArray(so, "botonesSeccion", navButtons);
        SetArray(so, "textosBotonesSeccion", navTexts);

        SetObj(so, "resolucionDropdown", resolution);
        SetObj(so, "modoPantallaDropdown", screenMode);

        SetObj(so, "presetDropdown", preset);
        SetObj(so, "presetEstadoText", presetState);
        SetObj(so, "renderScaleDropdown", renderScale);

        SetObj(so, "texturasDropdown", textures);
        SetObj(so, "sombrasDropdown", shadows);
        SetObj(so, "distanciaSombrasDropdown", shadowDistance);
        SetObj(so, "antialiasingDropdown", aa);
        SetObj(so, "anisotropicoDropdown", aniso);
        SetObj(so, "lodDropdown", lod);
        SetObj(so, "reflejosToggle", reflections);
        SetObj(so, "particulasToggle", particles);
        SetObj(so, "postProcessingToggle", post);

        SetObj(so, "limiteFpsDropdown", fpsLimit);
        SetObj(so, "vSyncToggle", vsync);
        SetObj(so, "mostrarFpsToggle", showFps);
        SetObj(so, "vSyncAdvertencia", vsyncInfo);
        SetObj(so, "hardwareText", hardware);

        SetObj(so, "volumenSlider", volume);
        SetObj(so, "volumenValor", volumeValue);

        SetObj(so, "sensibilidadSlider", sensitivity);
        SetObj(so, "sensibilidadValor", sensValue);

        SetObj(so, "controles", rebind);

        SetObj(so, "aplicarButton", apply);
        SetObj(so, "restaurarButton", reset);
        SetObj(so, "volverButton", back);
        SetObj(so, "estadoCambiosText", status);

        so.ApplyModifiedPropertiesWithoutUndo();

        ReplaceConfigPanelReferences(root);

        if (oldConfig != null &&
            oldConfig != root)
        {
            oldConfig.SetActive(false);
        }

        for (int i = 0; i < sections.Length; i++)
            sections[i].SetActive(i == 0);

        root.SetActive(false);

        Undo.RegisterCreatedObjectUndo(
            root,
            "Configuraciones"
        );

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog(
            "Configuraciones",
            "Menú de configuración creado.\n\n" +
            "Usa el mismo fondo de menuPrincipalPanel, integra Audio/Sensibilidad/Controles y agrega APLICAR.\n\n" +
            "Guardá la escena.",
            "OK"
        );
    }

    // =========================================================
    // SECCIONES / FILAS
    // =========================================================

    private static GameObject MakeSection(
        Transform parent,
        string name,
        string title,
        string subtitle,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont)
    {
        GameObject section =
            NewUI(name, parent);

        Stretch(section.GetComponent<RectTransform>());

        TMP_Text t =
            MakeText(
                section.transform,
                "Titulo",
                title,
                titleFont,
                48f,
                Warm,
                FontStyles.Bold
            );

        SetRect(
            t.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0.55f, 1f),
            new Vector2(34f, -76f),
            new Vector2(0f, -16f)
        );

        t.alignment =
            TextAlignmentOptions.MidlineLeft;

        TMP_Text s =
            MakeText(
                section.transform,
                "Subtitulo",
                subtitle,
                monoFont,
                16f,
                Muted,
                FontStyles.Bold
            );

        SetRect(
            s.rectTransform,
            new Vector2(0.55f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -68f),
            new Vector2(-34f, -22f)
        );

        s.alignment =
            TextAlignmentOptions.MidlineRight;

        MakeImage(
            section.transform,
            "Separador",
            new Color(0.55f, 0.60f, 0.62f, 0.25f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(34f, -92f),
            new Vector2(-34f, -90f)
        );

        return section;
    }

    private static RectTransform MakeRow(
        Transform parent,
        int index,
        string label,
        string description,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont,
        float rowHeight = 108f)
    {
        float top =
            -112f -
            index * rowHeight;

        GameObject row =
            MakeImage(
                parent,
                "Fila_" + label,
                index % 2 == 0
                    ? Row
                    : new Color(Row.r, Row.g, Row.b, Row.a * 0.78f)
            );

        RectTransform rt =
            row.GetComponent<RectTransform>();

        SetRect(
            rt,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(34f, top - rowHeight + 7f),
            new Vector2(-34f, top)
        );

        MakeImage(
            row.transform,
            "Marca",
            new Color(0.68f, 0.73f, 0.76f, 0.32f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(3f, 0f)
        );

        bool compact =
            rowHeight <= 82f;

        TMP_Text l =
            MakeText(
                row.transform,
                "Label",
                label,
                titleFont,
                compact ? 26f : 33f,
                Warm,
                FontStyles.Bold
            );

        // El nombre ocupa exclusivamente la mitad superior.
        // De esta forma nunca puede superponerse con la descripción.
        SetRect(
            l.rectTransform,
            new Vector2(0f, 0.48f),
            new Vector2(0.58f, 1f),
            new Vector2(24f, 2f),
            new Vector2(0f, -6f)
        );

        l.alignment =
            TextAlignmentOptions.MidlineLeft;

        TMP_Text d =
            MakeText(
                row.transform,
                "Descripcion",
                description,
                monoFont,
                compact ? 14.5f : 18f,
                Muted,
                FontStyles.Normal
            );

        d.enableWordWrapping = true;
        d.overflowMode = TextOverflowModes.Ellipsis;

        // La descripción ocupa únicamente la mitad inferior.
        SetRect(
            d.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0.60f, 0.48f),
            new Vector2(25f, 5f),
            new Vector2(0f, -1f)
        );

        d.alignment =
            TextAlignmentOptions.MidlineLeft;

        return rt;
    }

    private static TMP_Dropdown AddDropdownRow(
        Transform parent,
        int index,
        string label,
        string description,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont,
        float rowHeight = 108f)
    {
        RectTransform row =
            MakeRow(
                parent,
                index,
                label,
                description,
                titleFont,
                monoFont,
                rowHeight
            );

        return MakeDropdown(
            row,
            titleFont
        );
    }

    private static Toggle AddToggleRow(
        Transform parent,
        int index,
        string label,
        string description,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont,
        float rowHeight = 108f)
    {
        RectTransform row =
            MakeRow(
                parent,
                index,
                label,
                description,
                titleFont,
                monoFont,
                rowHeight
            );

        return MakeToggle(
            row,
            titleFont
        );
    }

    private static Slider AddSliderRow(
        Transform parent,
        int index,
        string label,
        string description,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont,
        out TMP_Text valueText)
    {
        RectTransform row =
            MakeRow(
                parent,
                index,
                label,
                description,
                titleFont,
                monoFont
            );

        GameObject sliderRoot =
            NewUI("Slider", row);

        RectTransform rt =
            sliderRoot.GetComponent<RectTransform>();

        SetRect(
            rt,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-520f, -16f),
            new Vector2(-110f, 16f)
        );

        Slider slider =
            sliderRoot.AddComponent<Slider>();

        GameObject bg =
            MakeImage(
                sliderRoot.transform,
                "Background",
                new Color(0.12f, 0.15f, 0.17f, 0.95f)
            );

        Stretch(
            bg.GetComponent<RectTransform>(),
            0f,
            10f,
            0f,
            10f
        );

        GameObject fillArea =
            NewUI(
                "Fill Area",
                sliderRoot.transform
            );

        Stretch(
            fillArea.GetComponent<RectTransform>(),
            0f,
            10f,
            0f,
            10f
        );

        GameObject fill =
            MakeImage(
                fillArea.transform,
                "Fill",
                Accent
            );

        Stretch(
            fill.GetComponent<RectTransform>()
        );

        GameObject handleArea =
            NewUI(
                "Handle Slide Area",
                sliderRoot.transform
            );

        Stretch(
            handleArea.GetComponent<RectTransform>(),
            8f,
            0f,
            8f,
            0f
        );

        GameObject handle =
            MakeImage(
                handleArea.transform,
                "Handle",
                Warm
            );

        RectTransform handleRT =
            handle.GetComponent<RectTransform>();

        handleRT.sizeDelta =
            new Vector2(20f, 36f);

        slider.fillRect =
            fill.GetComponent<RectTransform>();

        slider.handleRect =
            handleRT;

        slider.targetGraphic =
            handle.GetComponent<Image>();

        slider.direction =
            Slider.Direction.LeftToRight;

        valueText =
            MakeText(
                row,
                "Valor",
                "100%",
                titleFont,
                29f,
                Warm,
                FontStyles.Bold
            );

        SetRect(
            valueText.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-96f, -28f),
            new Vector2(-18f, 28f)
        );

        valueText.alignment =
            TextAlignmentOptions.Center;

        return slider;
    }

    // =========================================================
    // CONTROLES
    // =========================================================

    private static GameObject MakeControlsScroll(
        Transform parent,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont)
    {
        GameObject viewport =
            MakeImage(
                parent,
                "Viewport",
                new Color(0f, 0f, 0f, 0.10f)
            );

        SetRect(
            viewport.GetComponent<RectTransform>(),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(34f, 68f),
            new Vector2(-34f, -112f)
        );

        Mask mask =
            viewport.AddComponent<Mask>();

        mask.showMaskGraphic = false;

        GameObject content =
            NewUI(
                "Content",
                viewport.transform
            );

        RectTransform contentRT =
            content.GetComponent<RectTransform>();

        contentRT.anchorMin =
            new Vector2(0f, 1f);

        contentRT.anchorMax =
            new Vector2(1f, 1f);

        contentRT.pivot =
            new Vector2(0.5f, 1f);

        contentRT.anchoredPosition =
            Vector2.zero;

        string[] objectNames =
        {
            "RebindMoveUp",
            "RebindMoveDown",
            "RebindMoveLeft",
            "RebindMoveRight",
            "RebindJump",
            "RebindSprint",
            "RebindInteractuar",
            "RebindAgacharse",
            "RebindDisparar",
            "RebindApuntar",
            "RebindRecargar",
            "RebindArmaPrimaria",
            "RebindArmaSecundaria",
            "RebindGolpe",
            "RebindSlot1",
            "RebindGranada",
            "RebindSaltarCompras"
        };

        string[] labels =
        {
            "MOVER ADELANTE",
            "MOVER ATRÁS",
            "MOVER IZQUIERDA",
            "MOVER DERECHA",
            "SALTAR",
            "CORRER",
            "INTERACTUAR",
            "AGACHARSE",
            "DISPARAR",
            "APUNTAR",
            "RECARGAR",
            "ARMA PRIMARIA",
            "ARMA SECUNDARIA",
            "GOLPE",
            "USAR SLOT 1",
            "GRANADA",
            "SALTAR COMPRAS"
        };

        const float rowH = 70f;
        float contentHeight =
            objectNames.Length * rowH + 82f;

        contentRT.sizeDelta =
            new Vector2(0f, contentHeight);

        for (int i = 0; i < objectNames.Length; i++)
        {
            GameObject row =
                MakeImage(
                    content.transform,
                    objectNames[i],
                    i % 2 == 0
                        ? Row
                        : new Color(Row.r, Row.g, Row.b, Row.a * 0.76f)
                );

            RectTransform rt =
                row.GetComponent<RectTransform>();

            rt.anchorMin =
                new Vector2(0f, 1f);

            rt.anchorMax =
                new Vector2(1f, 1f);

            rt.pivot =
                new Vector2(0.5f, 1f);

            rt.anchoredPosition =
                new Vector2(
                    0f,
                    -i * rowH
                );

            rt.sizeDelta =
                new Vector2(
                    0f,
                    rowH - 5f
                );

            Button button =
                row.AddComponent<Button>();

            ColorBlock colors =
                button.colors;

            colors.normalColor =
                Color.white;

            colors.highlightedColor =
                new Color(0.82f, 0.86f, 0.88f, 1f);

            colors.pressedColor =
                new Color(0.68f, 0.74f, 0.76f, 1f);

            button.colors = colors;

            TMP_Text label =
                MakeText(
                    row.transform,
                    "Accion",
                    labels[i],
                    titleFont,
                    26f,
                    Warm,
                    FontStyles.Bold
                );

            SetRect(
                label.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0.65f, 1f),
                new Vector2(22f, 0f),
                Vector2.zero
            );

            label.alignment =
                TextAlignmentOptions.MidlineLeft;

            TMP_Text value =
                MakeText(
                    row.transform,
                    "Valor",
                    "-",
                    monoFont,
                    23f,
                    Accent,
                    FontStyles.Bold
                );

            SetRect(
                value.rectTransform,
                new Vector2(0.65f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                new Vector2(-22f, 0f)
            );

            value.alignment =
                TextAlignmentOptions.MidlineRight;
        }

        TMP_Text aviso =
            MakeText(
                content.transform,
                "Aviso",
                "",
                monoFont,
                16f,
                Amber,
                FontStyles.Bold
            );

        RectTransform avisoRT =
            aviso.rectTransform;

        avisoRT.anchorMin =
            new Vector2(0f, 1f);

        avisoRT.anchorMax =
            new Vector2(1f, 1f);

        avisoRT.pivot =
            new Vector2(0.5f, 1f);

        avisoRT.anchoredPosition =
            new Vector2(
                0f,
                -objectNames.Length * rowH - 4f
            );

        avisoRT.sizeDelta =
            new Vector2(
                0f,
                46f
            );

        aviso.alignment =
            TextAlignmentOptions.MidlineLeft;

        GameObject scrollGO =
            NewUI(
                "Scroll",
                parent
            );

        RectTransform scrollRT =
            scrollGO.GetComponent<RectTransform>();

        SetRect(
            scrollRT,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(34f, 68f),
            new Vector2(-34f, -112f)
        );

        // Mover viewport dentro del Scroll.
        viewport.transform.SetParent(
            scrollGO.transform,
            false
        );

        Stretch(
            viewport.GetComponent<RectTransform>()
        );

        ScrollRect scroll =
            scrollGO.AddComponent<ScrollRect>();

        scroll.viewport =
            viewport.GetComponent<RectTransform>();

        scroll.content =
            contentRT;

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType =
            ScrollRect.MovementType.Clamped;

        scroll.scrollSensitivity = 35f;

        return scrollGO;
    }

    // =========================================================
    // UI BÁSICA
    // =========================================================

    private static Button MakeNavButton(
        Transform parent,
        string text,
        TMP_FontAsset font,
        float y)
    {
        GameObject go =
            MakeImage(
                parent,
                "Btn_" + text,
                new Color(0.045f, 0.060f, 0.070f, 0.46f)
            );

        SetRect(
            go.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(18f, y - 66f),
            new Vector2(-18f, y)
        );

        Button button =
            go.AddComponent<Button>();

        ColorBlock colors =
            button.colors;

        colors.normalColor = Color.white;
        colors.highlightedColor =
            new Color(0.80f, 0.84f, 0.86f, 1f);

        colors.pressedColor =
            new Color(0.67f, 0.72f, 0.75f, 1f);

        button.colors = colors;

        MakeImage(
            go.transform,
            "Barra",
            AccentSoft,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(3f, 0f)
        );

        TMP_Text label =
            MakeText(
                go.transform,
                "Text",
                text,
                font,
                29f,
                Muted,
                FontStyles.Bold
            );

        Stretch(
            label.rectTransform,
            20f,
            0f,
            8f,
            0f
        );

        label.alignment =
            TextAlignmentOptions.MidlineLeft;

        return button;
    }

    private static TMP_Dropdown MakeDropdown(
        Transform parent,
        TMP_FontAsset font)
    {
#if UNITY_2023_1_OR_NEWER
        TMP_Dropdown template =
            UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            ).FirstOrDefault();
#else
        TMP_Dropdown template =
            UnityEngine.Object.FindObjectOfType<TMP_Dropdown>(true);
#endif

        if (template == null)
        {
            throw new Exception(
                "No encontré ningún TMP_Dropdown en la escena para usar como plantilla."
            );
        }

        GameObject clone =
            UnityEngine.Object.Instantiate(
                template.gameObject,
                parent
            );

        clone.name = "Dropdown";

        RectTransform rt =
            clone.GetComponent<RectTransform>();

        SetRect(
            rt,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-510f, -35f),
            new Vector2(-24f, 35f)
        );

        TMP_Dropdown dropdown =
            clone.GetComponent<TMP_Dropdown>();

        ClearPersistentEvent(
            dropdown,
            "m_OnValueChanged"
        );

        dropdown.ClearOptions();

        Image bg =
            clone.GetComponent<Image>();

        if (bg != null)
        {
            bg.color =
                new Color(0.095f, 0.082f, 0.092f, 0.94f);
        }

        if (dropdown.captionText != null)
        {
            dropdown.captionText.font = font;
            dropdown.captionText.fontSize = 28f;
            dropdown.captionText.fontStyle = FontStyles.Bold;
            dropdown.captionText.color = Warm;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.font = font;
            dropdown.itemText.fontSize = 26f;
            dropdown.itemText.color = Warm;
        }

        return dropdown;
    }

    private static Toggle MakeToggle(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject go =
            MakeImage(
                parent,
                "Toggle",
                new Color(0.095f, 0.082f, 0.092f, 0.92f)
            );

        SetRect(
            go.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-260f, -30f),
            new Vector2(-24f, 30f)
        );

        Toggle toggle =
            go.AddComponent<Toggle>();

        GameObject box =
            MakeImage(
                go.transform,
                "Box",
                new Color(0.16f, 0.14f, 0.15f, 1f)
            );

        SetRect(
            box.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-58f, -19f),
            new Vector2(-20f, 19f)
        );

        GameObject check =
            MakeImage(
                box.transform,
                "Checkmark",
                Accent
            );

        SetRect(
            check.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-11f, -11f),
            new Vector2(11f, 11f)
        );

        TMP_Text text =
            MakeText(
                go.transform,
                "Estado",
                "ACTIVAR",
                font,
                24f,
                Warm,
                FontStyles.Bold
            );

        SetRect(
            text.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(16f, 0f),
            new Vector2(-72f, 0f)
        );

        text.alignment =
            TextAlignmentOptions.MidlineLeft;

        toggle.targetGraphic =
            box.GetComponent<Image>();

        toggle.graphic =
            check.GetComponent<Image>();

        return toggle;
    }

    private static void AddInfo(
        Transform parent,
        float top,
        string title,
        string body,
        TMP_FontAsset titleFont,
        TMP_FontAsset monoFont)
    {
        GameObject box =
            MakeImage(
                parent,
                "Info_" + title,
                new Color(0.105f, 0.090f, 0.100f, 0.68f)
            );

        SetRect(
            box.GetComponent<RectTransform>(),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(34f, top - 120f),
            new Vector2(-34f, top)
        );

        MakeImage(
            box.transform,
            "Linea",
            new Color(0.72f, 0.60f, 0.44f, 0.75f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(4f, 0f)
        );

        TMP_Text t =
            MakeText(
                box.transform,
                "Titulo",
                title,
                titleFont,
                25f,
                Amber,
                FontStyles.Bold
            );

        SetRect(
            t.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(22f, 0f),
            new Vector2(-20f, -10f)
        );

        t.alignment =
            TextAlignmentOptions.MidlineLeft;

        TMP_Text b =
            MakeText(
                box.transform,
                "Texto",
                body,
                monoFont,
                17f,
                Muted,
                FontStyles.Normal
            );

        SetRect(
            b.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(22f, 8f),
            new Vector2(-20f, 0f)
        );

        b.enableWordWrapping = true;
        b.alignment =
            TextAlignmentOptions.TopLeft;
    }

    private static Button MakeFooterButton(
        Transform parent,
        string text,
        TMP_FontAsset font,
        float right,
        float width,
        bool primary)
    {
        GameObject go =
            MakeImage(
                parent,
                "Btn_" + text,
                primary
                    ? new Color(0.10f, 0.17f, 0.12f, 0.96f)
                    : new Color(0.055f, 0.065f, 0.075f, 0.94f)
            );

        SetRect(
            go.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(right, -22f),
            new Vector2(right + width, 22f)
        );

        Button button =
            go.AddComponent<Button>();

        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor =
            new Color(0.82f, 0.86f, 0.84f, 1f);
        cb.pressedColor =
            new Color(0.68f, 0.73f, 0.70f, 1f);
        button.colors = cb;

        if (primary)
        {
            MakeImage(
                go.transform,
                "Linea",
                Accent,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, 3f)
            );
        }

        TMP_Text label =
            MakeText(
                go.transform,
                "Text",
                text,
                font,
                24f,
                primary ? Accent : Warm,
                FontStyles.Bold
            );

        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;

        return button;
    }

    // =========================================================
    // REFERENCIAS
    // =========================================================

    private static void ReplaceConfigPanelReferences(
        GameObject newPanel)
    {
#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] all =
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
#else
        MonoBehaviour[] all =
            UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif

        int count = 0;

        foreach (MonoBehaviour behaviour in all)
        {
            if (behaviour == null ||
                behaviour.gameObject == newPanel)
            {
                continue;
            }

            SerializedObject so =
                new SerializedObject(behaviour);

            SerializedProperty prop =
                so.FindProperty("configuracionesPanel");

            if (prop == null ||
                prop.propertyType !=
                    SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            prop.objectReferenceValue = newPanel;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }

        Debug.Log(
            "[Configuraciones] Referencias configuracionesPanel actualizadas: " +
            count
        );
    }

    private static GameObject FindCurrentConfigPanel()
    {
#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] all =
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
#else
        MonoBehaviour[] all =
            UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif

        foreach (MonoBehaviour behaviour in all)
        {
            if (behaviour == null)
                continue;

            SerializedObject so =
                new SerializedObject(behaviour);

            SerializedProperty prop =
                so.FindProperty("configuracionesPanel");

            if (prop != null &&
                prop.objectReferenceValue is GameObject go)
            {
                return go;
            }
        }

        return null;
    }

    private static Canvas FindCanvas()
    {
        GameObject go =
            GameObject.Find("Canvas");

        if (go != null &&
            go.TryGetComponent(out Canvas canvas))
        {
            return canvas;
        }

#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<Canvas>(
            FindObjectsInactive.Include
        );
#else
        return UnityEngine.Object.FindObjectOfType<Canvas>(true);
#endif
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static GameObject NewUI(
        string name,
        Transform parent)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform)
            );

        go.transform.SetParent(
            parent,
            false
        );

        return go;
    }

    private static GameObject MakeImage(
        Transform parent,
        string name,
        Color color)
    {
        GameObject go =
            NewUI(name, parent);

        Image image =
            go.AddComponent<Image>();

        image.color = color;
        return go;
    }

    private static GameObject MakeImage(
        Transform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go =
            MakeImage(parent, name, color);

        SetRect(
            go.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax,
            offsetMin,
            offsetMax
        );

        return go;
    }

    private static TMP_Text MakeText(
        Transform parent,
        string name,
        string text,
        TMP_FontAsset font,
        float size,
        Color color,
        FontStyles style)
    {
        GameObject go =
            NewUI(name, parent);

        TextMeshProUGUI tmp =
            go.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;

        return tmp;
    }

    private static TMP_FontAsset LoadFont(
        string guid)
    {
        string path =
            AssetDatabase.GUIDToAssetPath(guid);

        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static void Stretch(
        RectTransform rt,
        float left = 0f,
        float bottom = 0f,
        float right = 0f,
        float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        rt.localScale = Vector3.one;
    }

    private static void SetRect(
        RectTransform rt,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.localScale = Vector3.one;
    }

    private static GameObject FindRecursive(
        Transform root,
        string name)
    {
        if (root.name == name)
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found =
                FindRecursive(
                    root.GetChild(i),
                    name
                );

            if (found != null)
                return found;
        }

        return null;
    }

    private static void DestroyIfExists(
        Transform root,
        string name)
    {
        GameObject found =
            FindRecursive(root, name);

        if (found != null)
            UnityEngine.Object.DestroyImmediate(found);
    }

    private static void SetObj(
        SerializedObject so,
        string name,
        UnityEngine.Object value)
    {
        SerializedProperty prop =
            so.FindProperty(name);

        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static void SetArray<T>(
        SerializedObject so,
        string name,
        T[] values)
        where T : UnityEngine.Object
    {
        SerializedProperty prop =
            so.FindProperty(name);

        if (prop == null)
            return;

        prop.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i)
                .objectReferenceValue =
                    values[i];
        }
    }

    private static void ClearPersistentEvent(
        UnityEngine.Object target,
        string eventProperty)
    {
        SerializedObject so =
            new SerializedObject(target);

        SerializedProperty calls =
            so.FindProperty(
                eventProperty +
                ".m_PersistentCalls.m_Calls"
            );

        if (calls != null)
        {
            calls.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
