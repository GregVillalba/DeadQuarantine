using UnityEngine;

// Hace que el objeto (ej: un cartel/nombre flotante en un Canvas World Space)
// siempre mire hacia la cámara del jugador local, como una vida flotante.
public class Billboard : MonoBehaviour
{
    [SerializeField] private bool bloquearEjeVertical = true;

    private Camera camaraObjetivo;

    private void LateUpdate()
    {
        if (camaraObjetivo == null || !camaraObjetivo.isActiveAndEnabled)
        {
            camaraObjetivo = BuscarCamaraActiva();

            if (camaraObjetivo == null)
                return;
        }

        Vector3 direccion = transform.position - camaraObjetivo.transform.position;

        if (bloquearEjeVertical)
            direccion.y = 0f;

        if (direccion.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direccion);
    }

    private Camera BuscarCamaraActiva()
    {
        Camera[] camaras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

        foreach (Camera camara in camaras)
        {
            if (camara.isActiveAndEnabled)
                return camara;
        }

        return null;
    }
}
