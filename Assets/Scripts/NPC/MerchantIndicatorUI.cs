using UnityEngine;
using TMPro;
using Unity.Netcode;

// HUD (2D, no afecta al modelo 3D del jugador) que muestra a cuántos metros
// está el mercader, una flecha que apunta hacia dónde caminar para encontrarlo
// y el tiempo restante de la fase de compras.
// Solo se muestra durante la fase de compras entre rondas.
public class MerchantIndicatorUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject indicadorRoot;
    [SerializeField] private RectTransform flecha;
    [SerializeField] private TextMeshProUGUI distanciaText;

    [Header("Tiempo de la fase de compras")]
    [Tooltip("Texto debajo de la distancia. Se ve toda la fase, aunque estés al lado del mercader.")]
    [SerializeField] private TextMeshProUGUI tiempoText;
    [SerializeField] private string prefijoTiempo = "Tiempo para comprar: ";

    [Header("Comportamiento")]
    [SerializeField] private float distanciaParaOcultar = 3f;

    private Transform jugador;
    private Transform mercader;
    private NPCShopUI shopUI;

    private void Start()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        jugador = networkObject != null ? networkObject.transform : transform;

        shopUI = transform.root.GetComponentInChildren<NPCShopUI>(true);

        BuscarMercader();
    }

    private void Update()
    {
        // La flecha y el tiempo solo se muestran durante la fase de compras.
        if (RoundManager.Instance == null || !RoundManager.Instance.IsShopPhaseActive)
        {
            MostrarIndicador(false, false);
            return;
        }

        // Con la tienda abierta el tiempo ya se ve dentro del panel.
        if (shopUI != null && shopUI.EstaAbierta)
        {
            MostrarIndicador(false, false);
            return;
        }

        if (tiempoText != null)
            tiempoText.text = prefijoTiempo + FormatearTiempo(RoundManager.Instance.ShopPhaseRemaining);

        if (mercader == null)
            BuscarMercader();

        if (mercader == null || jugador == null)
        {
            MostrarIndicador(false, true);
            return;
        }

        Vector3 haciaMercader = mercader.position - jugador.position;
        float distancia = haciaMercader.magnitude;

        if (distancia <= distanciaParaOcultar)
        {
            MostrarIndicador(false, true);
            return;
        }

        MostrarIndicador(true, true);

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

    // indicadorVisible: flecha + distancia. tiempoVisible: texto del tiempo restante.
    private void MostrarIndicador(bool indicadorVisible, bool tiempoVisible)
    {
        if (tiempoText != null)
            SetActivo(tiempoText.gameObject, tiempoVisible);

        if (indicadorRoot == null)
            return;

        // Si el root es el mismo objeto que tiene este script, desactivarlo
        // apagaría también el Update y no se volvería a mostrar nunca:
        // en ese caso se prenden/apagan sus hijos (menos el del tiempo).
        if (indicadorRoot == gameObject)
        {
            foreach (Transform hijo in transform)
            {
                if (tiempoText != null && hijo == tiempoText.transform)
                    continue;

                SetActivo(hijo.gameObject, indicadorVisible);
            }

            return;
        }

        SetActivo(indicadorRoot, indicadorVisible);
    }

    private static void SetActivo(GameObject objeto, bool activo)
    {
        if (objeto.activeSelf != activo)
            objeto.SetActive(activo);
    }

    private static string FormatearTiempo(int segundos)
    {
        segundos = Mathf.Max(0, segundos);
        return (segundos / 60).ToString("00") + ":" + (segundos % 60).ToString("00");
    }

    private void BuscarMercader()
    {
        NPCWeaponVendor vendedor = FindFirstObjectByType<NPCWeaponVendor>();

        if (vendedor != null)
            mercader = vendedor.transform;
    }
}
