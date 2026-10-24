using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de selección de personaje (sirve para singleplayer y multiplayer).
/// Mantiene el modelo 3D del preview igual al personaje guardado en CharacterSelection.
/// Los botones CONFIRMAR y ATRÁS llaman a Confirm() / Back(); lo que pasa después se define en el Inspector
/// (eventos) o cargando una escena, así luego se puede enganchar a cualquier menú sin tocar el código.
/// </summary>
public class CharacterSelectScreen : MonoBehaviour
{
    [SerializeField] private CharacterPreviewStage stage;

    [Tooltip("Para la ventana emergente: la cámara del escenario solo renderiza mientras la ventana está abierta.")]
    [SerializeField] private bool toggleStageRendering;

    [Header("Al confirmar / volver")]
    [Tooltip("Opcional: escena a cargar al confirmar. Vacío = solo se disparan los eventos.")]
    [SerializeField] private string sceneOnConfirm;
    [SerializeField] private UnityEvent onConfirm;
    [SerializeField] private UnityEvent onBack;

    private void OnEnable()
    {
        if (toggleStageRendering && stage != null)
            stage.SetRendering(true);

        ShowCurrent();
    }

    private void OnDisable()
    {
        if (toggleStageRendering && stage != null)
            stage.SetRendering(false);
    }

    private void Update()
    {
        // Show() no hace nada si el modelo ya es el correcto, así que es barato.
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (stage != null)
            stage.Show(0, CharacterSelection.SelectedIndex);
    }

    // La elección ya está guardada (CharacterSelectMenu la guarda al instante); confirmar solo avanza.
    public void Confirm()
    {
        onConfirm.Invoke();

        if (!string.IsNullOrEmpty(sceneOnConfirm))
            SceneManager.LoadScene(sceneOnConfirm);
    }

    public void Back()
    {
        onBack.Invoke();
    }
}