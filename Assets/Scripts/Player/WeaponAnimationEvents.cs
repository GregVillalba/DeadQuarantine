using UnityEngine;

public class WeaponAnimationEvents : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Weapon weapon;

    [Header("Vaina")]
    [SerializeField] private GameObject casingPrefab;
    [SerializeField] private Transform casingEjectPoint;
    [SerializeField] private float casingForce = 2f;

    [Header("Weapon Switcher")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    private Weapon integratedEjectWeapon;
    private uint integratedEjectSequence;
    public void OnIntegratedEjectCasing(AnimationEvent animationEvent)
    {
        if (weapon == null || weaponSwitcher == null ||
            !weapon.gameObject.name.StartsWith("DQ_Weapon_", System.StringComparison.Ordinal) ||
            animationEvent.intParameter != weaponSwitcher.CurrentWeaponIndex)
            return;
        uint sequence = weapon.PresentationShotSequence;
        if (sequence == 0 || (integratedEjectWeapon == weapon && integratedEjectSequence == sequence))
            return;
        integratedEjectWeapon = weapon;
        integratedEjectSequence = sequence;
        OnEjectCasing();
    }

    public void OnEjectCasing()
    {
        if (casingPrefab == null || casingEjectPoint == null)
            return;

        Vector3 position = casingEjectPoint.position;
        Quaternion rotation = casingEjectPoint.rotation;
        bool isIntegrated = weapon != null && weapon.gameObject.name.StartsWith("DQ_Weapon_", System.StringComparison.Ordinal);

        if (weapon != null)
        {
            // Para armas originales se conserva exactamente su camino previo.
            weapon.PresentationPose(casingEjectPoint, out position, out rotation);

            // Las armas nuevas usan una cámara Overlay para las manos. La posición
            // de ese espacio no equivale a la profundidad física del mundo.
            // Proyectamos su punto de expulsión a un rayo de la cámara jugable.
            if (isIntegrated)
            {
                Camera overlay = weapon.GetComponentInParent<Camera>();
                Camera world = null;
                Camera[] cameras = weapon.transform.root.GetComponentsInChildren<Camera>(true);
                foreach (Camera c in cameras)
                {
                    if (c == null || c == overlay) continue;
                    if (c.CompareTag("MainCamera")) { world = c; break; }
                    if (world == null) world = c;
                }
                if (overlay != null && world != null)
                {
                    Vector3 v = overlay.WorldToViewportPoint(casingEjectPoint.position);
                    if (v.z > 0.01f)
                    {
                        Ray ray = world.ViewportPointToRay(new Vector3(Mathf.Clamp(v.x, 0.04f, 0.96f),
                            Mathf.Clamp(v.y, 0.04f, 0.96f), 0f));
                        position = ray.GetPoint(Mathf.Clamp(v.z, 0.45f, 0.95f));
                        rotation = world.transform.rotation *
                                   Quaternion.Inverse(overlay.transform.rotation) * casingEjectPoint.rotation;
                    }
                }
            }
        }

        GameObject casing = Instantiate(casingPrefab, position, rotation);
        if (isIntegrated && casing != null)
        {
            Rigidbody body = casing.GetComponent<Rigidbody>();
            if (body != null)
            {
                Transform origin = weapon.transform.root;
                Camera[] cameras = origin.GetComponentsInChildren<Camera>(true);
                Transform direction = cameras.Length > 0 && cameras[0] != null
                    ? cameras[0].transform : origin;
                body.AddForce((direction.right * 0.85f + Vector3.up * 0.45f) *
                              Mathf.Max(0.5f, casingForce), ForceMode.VelocityChange);
            }
        }
        if (weapon != null) weapon.PresentationEvent(NetworkWeaponPresentation.WeaponEvent.Eject);
    }
    public void OnSlideBack()
    {
        // El retroceso de la corredera ya está contenido
        // en la propia animación A_FP_PCH_Handgun_Reload_Empty.
        // Este evento existe para ser compatible con el clip.
    }

    public void OnAmmunitionFill()
    {
        if (weapon != null)
            weapon.AnimationAmmunitionFill();
    }

    // NUEVO: se llama una vez por cada vuelta del clip Reload_Insert
    // (Shotgun_01, Sniper_01), para cargar una bala por vez.
    public void OnInsertShell()
    {
        if (weapon != null)
            weapon.AnimationInsertOneShell();
    }

    // Some existing shotgun close clips use the direct Weapon method name.
    public void AnimationReloadFinished() => OnAnimationEndedReload();

    public void OnAnimationEndedReload()
    {
        if (weapon != null)
            weapon.AnimationReloadFinished();
    }

    public void AnimationInsertOneShell()
    {
        if (weapon != null)
            weapon.AnimationInsertOneShell();
    }

    public void SetWeapon(Weapon newWeapon)
    {
        weapon = newWeapon;
    }

    public void OnAnimationEndedHolster()
    {
        // Se dispara al terminar A_FP_PCH_Handgun_Unholster:
        // el arma ya terminó de salir y está lista para disparar.
        if (weaponSwitcher != null)
            weaponSwitcher.NotifyUnholsterFinished();
    }

    public void SetCasingData(GameObject newCasingPrefab, Transform newCasingEjectPoint)
    {
        casingPrefab = newCasingPrefab;
        casingEjectPoint = newCasingEjectPoint;
    }

    // =========================================================
    // RECARGA POR PARTES — SONIDOS (Escopeta / Sniper)
    // =========================================================

    public void AnimationReloadOpen()
    {
        if (weapon != null)
            weapon.AnimationReloadOpen();
    }

    public void AnimationReloadClose()
    {
        if (weapon != null)
            weapon.AnimationReloadClose();
    }

    public void AnimationBoltOpen()
    {
        if (weapon != null)
            weapon.AnimationBoltOpen();
    }

    public void AnimationBoltClose()
    {
        if (weapon != null)
            weapon.AnimationBoltClose();
    }

}