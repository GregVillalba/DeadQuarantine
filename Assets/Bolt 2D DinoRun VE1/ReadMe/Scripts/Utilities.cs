using UnityEngine;

public class Utilities : MonoBehaviour
{
    /// <summary>
    /// Devuelve el ancho horizontal exacto del objeto en unidades del mundo (o píxeles si es UI).
    /// </summary>
    public static float ObtenerAncho(GameObject obj)
    {
        if (obj == null) return 0f;

        // 1. Si es un Sprite 2D en la escena
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            return sr.bounds.size.x;
        }

        // 2. Si tiene un Collider2D configurado
        Collider2D col = obj.GetComponent<Collider2D>();
        if (col != null)
        {
            return col.bounds.size.x;
        }

        // 3. Si es un elemento de UI dentro de un Canvas (RawImage / Image / Panel)
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt != null)
        {
            return rt.rect.width * rt.lossyScale.x;
        }

        // 4. Si es un modelo 3D con MeshRenderer
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            return rend.bounds.size.x;
        }

        Debug.LogWarning($"No se encontró un componente de renderizado o collider en {obj.name} para medir su ancho.");
        return 0f;
    }
}