using UnityEngine;
using TMPro;
using Unity.Netcode;

// HUD (2D, no afecta al modelo 3D del jugador) que muestra a cuántos metros
// está el mercader y una flecha que apunta hacia dónde caminar para encontrarlo.
public class MerchantIndicatorUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject indicadorRoot;
    [SerializeField] private RectTransform flecha;
    [SerializeField] private TextMeshProUGUI distanciaText;

    [Header("Comportamiento")]
    [SerializeField] private float distanciaParaOcultar = 3f;

    private Transform jugador;
    private Transform mercader;

    private void Start()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        jugador = networkObject != null ? networkObject.transform : transform;

        BuscarMercader();
    }

    private void Update()
    {
        if (mercader == null)
            BuscarMercader();

        if (mercader == null || jugador == null)
        {
            MostrarIndicador(false);
            return;
        }

        Vector3 haciaMercader = mercader.position - jugador.position;
        float distancia = haciaMercader.magnitude;

        if (distancia <= distanciaParaOcultar)
        {
            MostrarIndicador(false);
            return;
        }

        MostrarIndicador(true);

        if (distanciaText != null)
            distanciaText.text = "Mercader a " + Mathf.RoundToInt(distancia) + " m";

        if (flecha != null)
        {
            Vector3 direccionPlana = haciaMercader;
            direccionPlana.y = 0f;

            Vector3 adelanteJugador = jugador.forward;
            adelanteJugador.y = 0f;

            if (direccionPlana.sqrMagnitude > 0.0001f && adelanteJugador.sqrMagnitude > 0.0001f)
            {
                float angulo = Vector3.SignedAngle(adelanteJugador, direccionPlana, Vector3.up);
                flecha.localRotation = Quaternion.Euler(0f, 0f, -angulo);
            }
        }
    }

    private void MostrarIndicador(bool visible)
    {
        if (indicadorRoot != null && indicadorRoot.activeSelf != visible)
            indicadorRoot.SetActive(visible);
    }

    private void BuscarMercader()
    {
        NPCWeaponVendor vendedor = FindFirstObjectByType<NPCWeaponVendor>();

        if (vendedor != null)
            mercader = vendedor.transform;
    }
}
