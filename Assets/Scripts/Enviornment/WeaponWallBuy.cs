using UnityEngine;
using Unity.Netcode;

public class WeaponWallBuy : MonoBehaviour
{
    [Header("Arma")]
    [SerializeField] private string weaponId = "Rifle";
    [SerializeField] private int cost = 1500;

    [Header("Detección")]
    [SerializeField] private Transform lookTarget; // collider sobre SK_AR_01
    [SerializeField] private float interactRange = 3f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSuccessSound;
    [SerializeField] private AudioClip purchaseDeniedSound; // opcional: sonido de "no alcanza"

    private PlayerControls controls;
    private Camera localPlayerCamera;

    private WeaponSwitcher pendingSwitcher;
    private PlayerScore pendingScore;

    public string WeaponId => weaponId;
    public int Cost => cost;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    private void Update()
    {
        BuscarCamaraJugadorLocal();

        if (localPlayerCamera == null)
        {
            // Si ves esto en loop, el problema es que nunca
            // encuentra la cámara del jugador local.
            return;
        }

        if (controls.Player.Interact.triggered)
        {
            Debug.Log("[WeaponWallBuy] Se detectó input de Interact.");

            bool mirando = EstaMirandoElArma();
            Debug.Log("[WeaponWallBuy] ¿Mirando el arma? " + mirando);

            if (mirando)
                TryPurchase();
        }
    }

    // Mismo patrón que Door.cs.
    private void BuscarCamaraJugadorLocal()
    {
        if (localPlayerCamera != null &&
            localPlayerCamera.isActiveAndEnabled)
            return;

        localPlayerCamera = null;

        Camera[] cameras =
            FindObjectsByType<Camera>(FindObjectsSortMode.None);

        foreach (Camera cam in cameras)
        {
            if (!cam.isActiveAndEnabled)
                continue;

            NetworkObject networkObject =
                cam.GetComponentInParent<NetworkObject>();

            if (networkObject != null &&
                networkObject.IsSpawned &&
                networkObject.IsOwner)
            {
                localPlayerCamera = cam;
                return;
            }

            if (networkObject == null &&
                cam == Camera.main)
            {
                localPlayerCamera = cam;
                return;
            }
        }

        // Si esto se imprime seguido, ese es el problema real.
        Debug.LogWarning("[WeaponWallBuy] No se encontró cámara del jugador local.");
    }

    private bool EstaMirandoElArma()
    {
        if (localPlayerCamera == null || lookTarget == null)
        {
            Debug.LogWarning("[WeaponWallBuy] Falta localPlayerCamera o lookTarget (¿asignaste Look Target en el Inspector?).");
            return false;
        }

        Ray ray = new Ray(
            localPlayerCamera.transform.position,
            localPlayerCamera.transform.forward
        );

        if (!Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            Debug.Log("[WeaponWallBuy] Raycast no pegó contra nada.");
            return false;
        }

        Debug.Log("[WeaponWallBuy] Raycast pegó en: " + hit.transform.name);

        return hit.transform == lookTarget ||
               hit.transform.IsChildOf(lookTarget);
    }

    private void TryPurchase()
    {
        WeaponSwitcher switcher =
            localPlayerCamera.transform.root.GetComponentInChildren<WeaponSwitcher>();

        PlayerScore playerScore =
            localPlayerCamera.transform.root.GetComponentInChildren<PlayerScore>();

        if (switcher == null)
        {
            Debug.LogWarning("[WeaponWallBuy] No se encontró WeaponSwitcher en el jugador.");
            return;
        }

        if (playerScore == null)
        {
            Debug.LogWarning("[WeaponWallBuy] No se encontró PlayerScore en el jugador.");
            return;
        }

        if (switcher.IsUnlocked(weaponId))
        {
            Debug.Log("[WeaponWallBuy] " + weaponId + " ya estaba desbloqueada.");
            return;
        }

        Debug.Log("[WeaponWallBuy] Pidiendo compra de " + weaponId + " por " + cost + " puntos.");

        pendingSwitcher = switcher;
        pendingScore = playerScore;

        playerScore.OnPurchaseResult += OnPurchaseResult;
        playerScore.ComprarArmaServerRpc(weaponId, cost);
    }

    private void OnPurchaseResult(string resultWeaponId, bool exito)
    {
        if (resultWeaponId != weaponId)
            return;

        Debug.Log("[WeaponWallBuy] Resultado de compra de " + weaponId + ": " + (exito ? "ÉXITO" : "SIN PUNTOS"));

        if (pendingScore != null)
            pendingScore.OnPurchaseResult -= OnPurchaseResult;

        PlaySound(exito ? purchaseSuccessSound : purchaseDeniedSound);

        if (exito && pendingSwitcher != null)
            pendingSwitcher.UnlockWeapon(weaponId, equipAfterUnlock: true);

        pendingSwitcher = null;
        pendingScore = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}