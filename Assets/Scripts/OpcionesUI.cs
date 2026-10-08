using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de opciones: re-mapeado de teclas/mouse de las acciones del jugador.
/// El volumen y la sensibilidad del mouse NO se manejan acá: ya están
/// resueltos por ControlVolumen y ControlSensibilidadMouse (que persisten
/// vía ConfiguracionesJuego); este panel puede convivir con esos controles
/// en la misma pantalla de configuraciones sin duplicar nada.
/// La UI se arma en el editor (botones/textos con nombres fijos); este
/// script los resuelve por nombre en Awake y cablea el comportamiento. Se
/// muestra/oculta con CanvasGroup y persiste los rebinds vía
/// ConfiguracionesJuego. Se cablea solo con un botón cuyo nombre contenga
/// "controles" (ej. un botón "Controles" dentro del panel de configuraciones).
/// </summary>
[DisallowMultipleComponent]
public class OpcionesUI : MonoBehaviour
{
    [SerializeField] private InputActionAsset acciones;

    // (id del GameObject del botón, acción, partición del composite o null).
    private static readonly (string, string, string)[] Controles =
    {
        ("RebindMoveUp", "Move", "up"),
        ("RebindMoveDown", "Move", "down"),
        ("RebindMoveLeft", "Move", "left"),
        ("RebindMoveRight", "Move", "right"),
        ("RebindJump", "Jump", null),
        ("RebindSprint", "Sprint", null),
        ("RebindInteractuar", "Interact", null),
        ("RebindAgacharse", "Crouch", null),
        ("RebindDisparar", "Fire", null),
        ("RebindApuntar", "Aim", null),
        ("RebindRecargar", "Reload", null),
        ("RebindArmaPrimaria", "SelectPrimary", null),
        ("RebindArmaSecundaria", "SelectSecondary", null),
        ("RebindGolpe", "Melee", null),
        ("RebindSlot1", "UseSlot1", null),
        ("RebindGranada", "Grenade", null),
    };

    // Nombres fijos de los controles a resolver en el editor.
    private const string NombreAviso = "Aviso";
    private const string NombreRestablecer = "RestablecerTeclas";
    private const string NombreGuardar = "Guardar";
    private const string NombreVolver = "Volver";
    private const string FragmentoNombreBotonConfiguracion = "controles";

    private readonly List<(Button boton, TextMeshProUGUI texto, string accion, string particion)> _filas =
        new List<(Button, TextMeshProUGUI, string, string)>();

    private readonly List<GameObject> _hermanosOcultos = new List<GameObject>();

    private CanvasGroup _grupo;
    private TextMeshProUGUI _aviso;
    private InputActionRebindingExtensions.RebindingOperation _operacion;
    private InputAction _accionSiendoReasignada;
    private TextMeshProUGUI _textoSiendoReasignado;
    private float _tamanoOriginalTexto;
    private bool _mapaEstabaActivo;
    private bool _resuelto;

    public bool EstaAbierto => _grupo != null && _grupo.alpha > 0.01f;

    public void Abrir()
    {
        ResolverControles();
        ConfiguracionesJuego.CargarRebinds(acciones);
        OcultarMenuAnterior();
        _grupo.alpha = 1f;
        _grupo.blocksRaycasts = true;
        ActualizarFilas();
    }

    public void Cerrar()
    {
        CancelarRebindActivo();
        if (_grupo == null) return;
        _grupo.alpha = 0f;
        _grupo.blocksRaycasts = false;
        RestaurarMenuAnterior();
    }

    private void OcultarMenuAnterior()
    {
        if (_hermanosOcultos.Count > 0 || transform.parent == null) return;
        foreach (Transform hermano in transform.parent)
        {
            if (hermano == transform || !hermano.gameObject.activeSelf) continue;
            hermano.gameObject.SetActive(false);
            _hermanosOcultos.Add(hermano.gameObject);
        }
    }

    private void RestaurarMenuAnterior()
    {
        foreach (var hermano in _hermanosOcultos)
        {
            if (hermano != null) hermano.SetActive(true);
        }
        _hermanosOcultos.Clear();
    }

    private void Awake()
    {
        ResolverControles();
        CablearControles();
        CablearBotonConfiguracion();
        Cerrar();
    }

    /// <summary>Resuelve por nombre los controles armados en el editor.</summary>
    private void ResolverControles()
    {
        if (_resuelto) return;
        _resuelto = true;

        _grupo = GetComponent<CanvasGroup>();
        if (_grupo == null) _grupo = gameObject.AddComponent<CanvasGroup>();

        // Fondo oscuro si el panel no trae uno en el editor.
        var fondo = Buscar<Image>("Fondo") ?? GetComponent<Image>();
        if (fondo == null)
        {
            fondo = gameObject.AddComponent<Image>();
            fondo.color = new Color(0.10f, 0.10f, 0.12f, 0.96f);
        }
        fondo.raycastTarget = true;

        _aviso = Buscar<TextMeshProUGUI>(NombreAviso);

        foreach (var control in Controles)
        {
            var boton = Buscar<Button>(control.Item1);
            if (boton == null) continue;
            var texto = boton.GetComponentInChildren<TextMeshProUGUI>(true);
            _filas.Add((boton, texto, control.Item2, control.Item3));
        }
    }

    /// <summary>Cablea el comportamiento de los controles resueltos.</summary>
    private void CablearControles()
    {
        foreach (var fila in _filas)
        {
            var f = fila;
            f.boton.onClick.AddListener(() => ComenzarRebind(f.boton, f.texto, f.accion, f.particion));
        }

        var restablecer = Buscar<Button>(NombreRestablecer);
        if (restablecer != null) restablecer.onClick.AddListener(RestablecerTeclas);

        var guardar = Buscar<Button>(NombreGuardar);
        if (guardar != null) guardar.onClick.AddListener(Guardar);

        var volver = Buscar<Button>(NombreVolver);
        if (volver != null) volver.onClick.AddListener(Cerrar);
    }

    private T Buscar<T>(string nombre) where T : Component
    {
        foreach (var componente in GetComponentsInChildren<T>(true))
        {
            if (componente.gameObject.name == nombre) return componente;
        }
        return null;
    }

    /// <summary>Busca un botón de "Controles" en la escena y lo engancha a Abrir().</summary>
    private void CablearBotonConfiguracion()
    {
        var botones = transform.root.GetComponentsInChildren<Button>(true);
        foreach (var boton in botones)
        {
            if (boton.gameObject == gameObject) continue;
            if (!boton.gameObject.name.Contains(FragmentoNombreBotonConfiguracion, StringComparison.OrdinalIgnoreCase)) continue;

            boton.onClick.RemoveListener(Abrir);
            boton.onClick.AddListener(Abrir);
            break;
        }
    }

    private void ActualizarFilas()
    {
        foreach (var fila in _filas)
        {
            if (fila.texto != null) fila.texto.text = TeclaActual(fila.accion, fila.particion);
        }
    }

    private void MostrarAviso(string mensaje)
    {
        if (_aviso != null) _aviso.text = mensaje;
    }

    private string TeclaActual(string nombreAccion, string particion)
    {
        if (acciones == null) return "?";
        var accion = acciones.FindAction(nombreAccion, false);
        if (accion == null) return "?";

        int indice = BuscarIndiceBinding(accion, particion);
        if (indice < 0) return "-";
        return accion.GetBindingDisplayString(indice);
    }

    private void ComenzarRebind(Button boton, TextMeshProUGUI texto, string nombreAccion, string particion)
    {
        if (acciones == null)
        {
            MostrarAviso("Falta asignar el asset de acciones en OpcionesUI.");
            return;
        }

        var accion = acciones.FindAction(nombreAccion, false);
        if (accion == null) return;

        int indice = BuscarIndiceBinding(accion, particion);
        if (indice < 0) return;

        CancelarRebindActivo();

        string pathActual = accion.bindings[indice].effectivePath;

        // El Input System no permite reasignar una acción mientras está
        // habilitada (el PlayerInput del jugador la tiene activa incluso en
        // pausa). Deshabilitamos el mapa y lo restauramos al terminar.
        var mapa = accion.actionMap;
        _mapaEstabaActivo = mapa != null && mapa.enabled;
        _accionSiendoReasignada = accion;
        if (_mapaEstabaActivo) mapa.Disable();

        var operacion = accion.PerformInteractiveRebinding(indice)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithCancelingThrough("<Keyboard>/escape")
            .WithTimeout(8f);
        if (!string.IsNullOrEmpty(pathActual)) operacion.WithControlsExcluding(pathActual);

        operacion
            .OnCancel(op => FinalizarRebind(op))
            .OnComplete(op =>
            {
                if (op.selectedControl != null && indice >= 0 && indice < accion.bindings.Count)
                {
                    accion.ApplyBindingOverride(indice, op.selectedControl.path);
                }
                FinalizarRebind(op);
            });

        _operacion = operacion;

        foreach (var fila in _filas) fila.boton.interactable = false;
        if (texto != null)
        {
            _textoSiendoReasignado = texto;
            _tamanoOriginalTexto = texto.fontSize;
            texto.enableAutoSizing = true;
            texto.fontSizeMin = 10f;
            texto.fontSizeMax = _tamanoOriginalTexto;
            texto.text = "... (Esc cancela)";
        }

        operacion.Start();
    }

    private void FinalizarRebind(InputActionRebindingExtensions.RebindingOperation op)
    {
        if (!ReferenceEquals(op, _operacion)) return;
        _operacion = null;
        RestaurarMapaDeAccion();
        op.Dispose();
        RestaurarTamanoTexto();
        foreach (var fila in _filas) fila.boton.interactable = true;
        ActualizarFilas();
    }

    private void CancelarRebindActivo()
    {
        if (_operacion == null) return;
        var op = _operacion;
        _operacion = null;
        RestaurarMapaDeAccion();
        op.Dispose();
        RestaurarTamanoTexto();
        foreach (var fila in _filas) fila.boton.interactable = true;
        ActualizarFilas();
    }

    /// <summary>Restaura el mapa de acciones al estado previo al re-mapeado.</summary>
    private void RestaurarMapaDeAccion()
    {
        if (_accionSiendoReasignada == null) return;
        if (_mapaEstabaActivo) _accionSiendoReasignada.actionMap.Enable();
        _accionSiendoReasignada = null;
        _mapaEstabaActivo = false;
    }

    /// <summary>Devuelve el tamaño original al texto mientras se re-mapeaba.</summary>
    private void RestaurarTamanoTexto()
    {
        if (_textoSiendoReasignado == null) return;
        _textoSiendoReasignado.enableAutoSizing = false;
        _textoSiendoReasignado.fontSize = _tamanoOriginalTexto;
        _textoSiendoReasignado = null;
    }

    /// <summary>
    /// Devuelve el índice del binding editable (teclado o mouse):
    /// la parte del composite (ej. "up") o la primera binding simple.
    /// </summary>
    private static int BuscarIndiceBinding(InputAction accion, string particion)
    {
        var bindings = accion.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];

            if (!string.IsNullOrEmpty(particion))
            {
                if (!binding.isPartOfComposite) continue;
                if (!string.Equals(binding.name, particion, StringComparison.OrdinalIgnoreCase)) continue;
            }
            else
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;
            }

            if (binding.path == null) continue;
            bool esTeclado = binding.path.StartsWith("<Keyboard>", StringComparison.Ordinal);
            bool esMouse = binding.path.StartsWith("<Mouse>", StringComparison.Ordinal);
            if (!esTeclado && !esMouse) continue;

            return i;
        }

        return -1;
    }

    public void Guardar()
    {
        ConfiguracionesJuego.GuardarRebinds(acciones);
        MostrarAviso("Configuración guardada.");
    }

    public void RestablecerTeclas()
    {
        ConfiguracionesJuego.RestablecerRebinds(acciones);
        ActualizarFilas();
        MostrarAviso("Teclas restablecidas.");
    }
}
