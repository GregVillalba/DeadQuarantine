using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class HUDController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI playerLabelText;

    private RoundManager roundManager;

    [Header("Vida")]
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI healthPercentText;
    [SerializeField] private Color healthColorFull = Color.white;
    [SerializeField] private Color healthColorHalf = new Color(1f, 0.65f, 0.3f);
    [SerializeField] private Color healthColorLow = Color.red;
    [SerializeField] private GameObject vidas1;
    [SerializeField] private GameObject vidas0;

    [Header("Estamina")]
    [SerializeField] private Image staminaFill;
    [Tooltip("Número que baja junto con la barra (0-100).")]
    [SerializeField] private TextMeshProUGUI staminaValueText;

    [Header("Chaleco")]
    [SerializeField] private Image armorFill;
    [Tooltip("Número que acompaña a la barra de chaleco (0-50).")]
    [SerializeField] private TextMeshProUGUI armorValueText;

    [Header("Munición")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI ammoMaxText;
    [SerializeField] private TextMeshProUGUI weaponNameText;

    [Header("Rondas")]
    [SerializeField] private TextMeshProUGUI roundsText;
    [SerializeField] private TextMeshProUGUI zombiesText;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshairRoot;
    [SerializeField] private RectTransform dashTop;
    [SerializeField] private RectTransform dashBottom;
    [SerializeField] private RectTransform dashLeft;
    [SerializeField] private RectTransform dashRight;

    [SerializeField] private float crosshairMinGap = 40f;
    [SerializeField] private float crosshairMaxGap = 140f;

    [Header("Hit Marker")]
    [SerializeField] private GameObject hitMarker;
    [SerializeField] private float hitMarkerDuration = 0.10f;

    private Image[] hitMarkerImages;

    private Coroutine hitMarkerCoroutine;

    private bool etiquetaActualizada;

    private void Start()
    {
        BuscarRoundManager();

       /* NetworkObject networkObject =
            GetComponentInParent<NetworkObject>();

        if (
            networkObject != null &&
            networkObject.IsOwner &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient
        )
        {
            if (NetworkManager.Singleton.LocalClientId == 1)
            {
                if (playerLabelText != null)
                    playerLabelText.text = "Jugador 2 (TÚ)";
            }
        }*/

        if (hitMarker != null)
        {
            // Obtiene las Images de:
            //
            // HitMarker
            // ├── Dash_Top
            // ├── Dash_Bottom
            // ├── Dash_Left
            // └── Dash_Right
            //
            hitMarkerImages =
                hitMarker.GetComponentsInChildren<Image>(
                    true
                );

            hitMarker.SetActive(false);
        }

        ActualizarTodo();
    }

    private void ActualizarEtiquetaJugador()
    {
        if (etiquetaActualizada || playerLabelText == null)
            return;

        // El jugador al que pertenece este HUD.
        NetworkObject networkObject =
            playerHealth != null
                ? playerHealth.GetComponentInParent<NetworkObject>()
                : GetComponentInParent<NetworkObject>();

        // Todavia no esta listo: se reintenta en el proximo cuadro.
        if (networkObject == null || !networkObject.IsSpawned)
            return;

        // Este HUD no es del jugador local.
        if (!networkObject.IsOwner)
            return;

        MultiplayerPlayerSpawnAssigner assigner =
            networkObject.GetComponent<MultiplayerPlayerSpawnAssigner>();

        // Singleplayer: no hay asignador, es siempre el jugador 1.
        if (assigner == null)
        {
            playerLabelText.text = "Jugador 1 (TÚ)";
            etiquetaActualizada = true;
            return;
        }

        // Multijugador: espera a que el servidor asigne el numero de jugador.
        if (assigner.AssignedSpawnIndex.Value < 0)
            return;

        playerLabelText.text =
            "Jugador " + (assigner.AssignedSpawnIndex.Value + 1) + " (TÚ)";

        etiquetaActualizada = true;
    }

    private void Update()
    {
        ActualizarEtiquetaJugador();
        
        if (roundManager == null)
        {
            BuscarRoundManager();
        }

        ActualizarTodo();
    }

    private void UpdateLivesUI()
    {
        if (playerHealth == null)
            return;

        bool mostrarVidaLlena =
            playerHealth.Lives.Value > 0 &&
            !playerHealth.IsDowned;

        if (vidas1 != null)
            vidas1.SetActive(mostrarVidaLlena);

        if (vidas0 != null)
            vidas0.SetActive(!mostrarVidaLlena);
    }

    public void SetWeapon(Weapon newWeapon, Sprite newIcon)
    {
        weapon = newWeapon;

        // Si la imagen del icono pertenece a un objeto con ImageWeapon (arma + accesorios), ese script ya la
        // maneja solo: no se le pisa el sprite (antes se le ponía null y el icono desaparecía).
        // Solo se usa el sprite suelto cuando es una imagen común y hay un sprite para poner.
        if (weaponIcon != null && newIcon != null && weaponIcon.GetComponentInParent<ImageWeapon>(true) == null)
            weaponIcon.sprite = newIcon;
    }
    private void BuscarRoundManager()
    {
        roundManager =
            FindFirstObjectByType<RoundManager>();

        if (roundManager == null)
            return;

        Debug.Log(
            "[HUDController] RoundManager encontrado en " +
            gameObject.name +
            " | Round=" +
            roundManager.CurrentRound +
            " | Zombies=" +
            roundManager.AliveZombies +
            "/" +
            roundManager.ZombiesThisRound
        );
    }

    private void ActualizarTodo()
    {
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateArmorBar();
        UpdateAmmoText();
        UpdateRounds();
        UpdateCrosshair();
        UpdateLivesUI();
    }

    // =========================================================
    // VIDA
    // =========================================================

    private void UpdateHealthBar()
    {
        if (playerHealth == null)
            return;

        float percentage =
            (float)playerHealth.CurrentHealth.Value /
            playerHealth.MaxHealth;

        if (healthFill != null)
        {
            healthFill.fillAmount =
                percentage;
        }

        if (healthPercentText != null)
        {
            healthPercentText.text =
                Mathf.RoundToInt(
                    percentage * 100f
                ) + "%";
        }

        if (healthFill != null)
        {
            healthFill.color =
                percentage > 0.5f
                    ? Color.Lerp(
                        healthColorHalf,
                        healthColorFull,
                        (percentage - 0.5f) / 0.5f
                    )
                    : Color.Lerp(
                        healthColorLow,
                        healthColorHalf,
                        percentage / 0.5f
                    );
        }
    }

    // =========================================================
    // ESTAMINA
    // =========================================================

    private void UpdateStaminaBar()
    {
        if (playerMovement == null)
            return;

        float fraction =
            playerMovement.MaxStamina > 0f
                ? Mathf.Clamp01(playerMovement.CurrentStamina / playerMovement.MaxStamina)
                : 0f;

        if (staminaFill != null)
            staminaFill.fillAmount = fraction;

        // El número baja a medida que baja la barra.
        if (staminaValueText != null)
            staminaValueText.text = Mathf.CeilToInt(fraction * 100f).ToString();
    }

    // =========================================================
    // CHALECO
    // =========================================================

    private void UpdateArmorBar()
    {
        if (playerHealth == null)
            return;

        int current = playerHealth.Armor.Value;
        int max = Mathf.Max(playerHealth.MaxArmor, 1);

        if (armorFill != null)
            armorFill.fillAmount = Mathf.Clamp01((float)current / max);

        if (armorValueText != null)
            armorValueText.text = current.ToString();
    }

    // =========================================================
    // MUNICIÓN
    // =========================================================

    private void UpdateAmmoText()
    {
        if (weapon == null)
            return;

        if (ammoText != null)
        {
            ammoText.text =
                weapon.CurrentAmmo.ToString();
        }

        if (ammoMaxText != null)
        {
            // Reserva limitada (Difícil): "12  / 108" = cargador / reserva.
            // Con recargas ilimitadas queda como antes.
            ammoMaxText.text = weapon.UsesLimitedReserve
                ? "/ " + weapon.ReserveAmmo
                : "/ " + weapon.EffectiveMaxAmmo;
        }

        if (weaponNameText != null)
        {
            weaponNameText.text =
                weapon.WeaponName.ToUpper();
        }
    }

    // =========================================================
    // RONDAS
    // =========================================================

    private void UpdateRounds()
    {
        if (roundManager == null)
            return;

       /* if (roundsText != null)
        {
            roundsText.text =
                "RONDAS   " +
                roundManager.CurrentRound +
                "/" +
                roundManager.MaxRounds;
        }*/

        if (roundsText != null)
        {
            // MaxRounds = 0 significa rondas infinitas (Supervivencia).
           /* roundsText.text =
                roundManager.IsInfiniteMode
                    ? "RONDA   " + roundManager.CurrentRound
                    : "RONDAS   " +
                      roundManager.CurrentRound +
                      "/" +
                      roundManager.MaxRounds;*/

            roundsText.text =
                roundManager.IsInfiniteMode
                    ? roundManager.CurrentRound.ToString()
                    : roundManager.CurrentRound + "/" + roundManager.MaxRounds;
        }

        if (zombiesText != null)
        {
            zombiesText.text =
                roundManager.AliveZombies +
                "/" +
                roundManager.ZombiesThisRound;
        }
    }

    // =========================================================
    // CROSSHAIR
    // =========================================================

    private void UpdateCrosshair()
    {
        if (weapon == null ||
            crosshairRoot == null)
            return;

        if (weapon.IsAiming)
        {
            crosshairRoot.SetActive(false);
            return;
        }

        crosshairRoot.SetActive(true);

        float gap =
            Mathf.Lerp(
                crosshairMinGap,
                crosshairMaxGap,
                weapon.CurrentSpreadNormalized
            );

        if (dashTop != null)
        {
            dashTop.anchoredPosition =
                new Vector2(
                    0f,
                    gap
                );
        }

        if (dashBottom != null)
        {
            dashBottom.anchoredPosition =
                new Vector2(
                    0f,
                    -gap
                );
        }

        if (dashLeft != null)
        {
            dashLeft.anchoredPosition =
                new Vector2(
                    -gap,
                    0f
                );
        }

        if (dashRight != null)
        {
            dashRight.anchoredPosition =
                new Vector2(
                    gap,
                    0f
                );
        }
    }

    // =========================================================
    // HIT MARKER
    // =========================================================

    public void ShowHitMarker(bool killedZombie)
    {
        if (hitMarker == null)
            return;

        // Por si todavía no se obtuvieron las imágenes.
        if (hitMarkerImages == null ||
            hitMarkerImages.Length == 0)
        {
            hitMarkerImages =
                hitMarker.GetComponentsInChildren<Image>(
                    true
                );
        }

        Color markerColor =
            killedZombie
                ? Color.red
                : Color.white;

        // Cambiar el color de las 4 partes
        // del HitMarker.
        foreach (Image image in hitMarkerImages)
        {
            if (image != null)
            {
                image.color =
                    markerColor;
            }
        }

        if (hitMarkerCoroutine != null)
        {
            StopCoroutine(
                hitMarkerCoroutine
            );
        }

        hitMarkerCoroutine =
            StartCoroutine(
                HitMarkerRoutine()
            );
    }

    private IEnumerator HitMarkerRoutine()
    {
        hitMarker.SetActive(true);

        yield return new WaitForSecondsRealtime(
            hitMarkerDuration
        );

        hitMarker.SetActive(false);

        hitMarkerCoroutine = null;
    }
}