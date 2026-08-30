using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon;

    private RoundManager roundManager;

    [Header("Vida")]
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI healthPercentText;
    [SerializeField] private Color healthColorFull = Color.white;
    [SerializeField] private Color healthColorHalf = new Color(1f, 0.65f, 0.3f);
    [SerializeField] private Color healthColorLow = Color.red;

    [Header("Estamina")]
    [SerializeField] private Image staminaFill;

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
    [SerializeField] private float crosshairMinGap = 6f;
    [SerializeField] private float crosshairMaxGap = 22f;

    private void Start()
    {
        BuscarRoundManager();
        ActualizarTodo();
    }

    private void Update()
    {
        if (roundManager == null)
        {
            BuscarRoundManager();
        }

        ActualizarTodo();
    }

    private void BuscarRoundManager()
    {
        roundManager = FindFirstObjectByType<RoundManager>();

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
        UpdateAmmoText();
        UpdateRounds();
        UpdateCrosshair();
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
            healthFill.fillAmount = percentage;

        if (healthPercentText != null)
        {
            healthPercentText.text =
                Mathf.RoundToInt(percentage * 100f) + "%";
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
        if (playerMovement == null ||
            staminaFill == null)
            return;

        staminaFill.fillAmount =
            playerMovement.CurrentStamina /
            playerMovement.MaxStamina;
    }

    // =========================================================
    // MUNICIÓN
    // =========================================================

    private void UpdateAmmoText()
    {
        if (weapon == null)
            return;

        if (ammoText != null)
            ammoText.text =
                weapon.CurrentAmmo.ToString();

        if (ammoMaxText != null)
            ammoMaxText.text =
                "/ " + weapon.MaxAmmo;

        if (weaponNameText != null)
            weaponNameText.text =
                weapon.WeaponName.ToUpper();
    }

    // =========================================================
    // RONDAS
    // =========================================================

    private void UpdateRounds()
    {
        if (roundManager == null)
            return;

        if (roundsText != null)
        {
            roundsText.text =
                "RONDAS   " +
                roundManager.CurrentRound +
                "/" +
                roundManager.MaxRounds;
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
            dashTop.anchoredPosition =
                new Vector2(0f, gap);

        if (dashBottom != null)
            dashBottom.anchoredPosition =
                new Vector2(0f, -gap);

        if (dashLeft != null)
            dashLeft.anchoredPosition =
                new Vector2(-gap, 0f);

        if (dashRight != null)
            dashRight.anchoredPosition =
                new Vector2(gap, 0f);
    }
}