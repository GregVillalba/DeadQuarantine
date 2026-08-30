using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon;

    // No hace falta asignarlo desde el Inspector.
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
    [SerializeField] private TextMeshProUGUI rondaTexto;

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
        roundManager = RoundManager.Instance;
    }

    private void Update()
    {
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateAmmoText();
        UpdateRounds();
        UpdateCrosshair();
    }

    private void UpdateHealthBar()
    {
        if (playerHealth == null)
            return;

        float percentage =
            (float)playerHealth.CurrentHealth.Value /
            playerHealth.MaxHealth;

        healthFill.fillAmount = percentage;

        healthPercentText.text =
            Mathf.RoundToInt(percentage * 100f) + "%";

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

    private void UpdateStaminaBar()
    {
        if (playerMovement == null)
            return;

        staminaFill.fillAmount =
            playerMovement.CurrentStamina /
            playerMovement.MaxStamina;
    }

    private void UpdateAmmoText()
    {
        if (weapon == null)
            return;

        ammoText.text =
            weapon.CurrentAmmo.ToString();

        ammoMaxText.text =
            "/ " + weapon.MaxAmmo;

        weaponNameText.text =
            weapon.WeaponName.ToUpper();
    }

    private void UpdateRounds()
    {
        // Por si RoundManager todavía no estaba disponible al iniciar.
        if (roundManager == null)
        {
            roundManager = RoundManager.Instance;

            if (roundManager == null)
                return;
        }

        roundsText.text =
            "RONDAS   " +
            roundManager.CurrentRound +
            "/" +
            roundManager.MaxRounds;

        zombiesText.text =
            roundManager.AliveZombies +
            "/" +
            roundManager.ZombiesThisRound;

        if(roundManager.CurrentRound != 5)
        {
            rondaTexto.text = "Ronda " + roundManager.CurrentRound;
        }
        else
        {
            rondaTexto.text = "Ronda Final";
        }
    }

    private void UpdateCrosshair()
    {
        if (weapon == null)
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

        dashTop.anchoredPosition =
            new Vector2(0f, gap);

        dashBottom.anchoredPosition =
            new Vector2(0f, -gap);

        dashLeft.anchoredPosition =
            new Vector2(-gap, 0f);

        dashRight.anchoredPosition =
            new Vector2(gap, 0f);
    }
}