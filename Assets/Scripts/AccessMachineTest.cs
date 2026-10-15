
using UnityEngine;
using UnityEngine.InputSystem;

public class AccessMachineTest : MonoBehaviour
{
    [SerializeField] private Renderer indicador;

    private Material materialInstancia;
    private bool accesoAutorizado = false;

    private void Start()
    {
        if (indicador == null)
        {
            Debug.LogError(
                "AccessMachineTest: Asigná el Renderer del indicador."
            );
            return;
        }

        materialInstancia = indicador.material;
        materialInstancia.EnableKeyword("_EMISSION");

        CambiarColor();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            accesoAutorizado = !accesoAutorizado;
            CambiarColor();
        }
    }

    private void CambiarColor()
    {
        if (materialInstancia == null)
            return;

        Color color = accesoAutorizado
            ? Color.green
            : Color.yellow;

        materialInstancia.SetColor("_EmissionColor", color * 2f);

        Debug.Log(accesoAutorizado
            ? "Acceso autorizado"
            : "Acceso bloqueado");
    }

    private void OnDestroy()
    {
        if (materialInstancia != null)
            Destroy(materialInstancia);
    }
}
